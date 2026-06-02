# Caché HTTP

## Índice

* [Visión general](#visi%C3%B3n-general)
* [Arquitectura y ubicación en capas](#arquitectura-y-ubicaci%C3%B3n-en-capas)
* [Componentes](#componentes)
  * [ConfigureCache (registro y política base)](#configurecache-registro-y-pol%C3%ADtica-base)
  * [UseCacheMiddleware (pipeline)](#usecachemiddleware-pipeline)
  * [\[OutputCache\]](#outputcache)
  * [\[OutputCacheInvalidate\]](#outputcacheinvalidate)
* [Configuración](#configuraci%C3%B3n)
* [Cómo habilitar caché en un endpoint](#c%C3%B3mo-habilitar-cach%C3%A9-en-un-endpoint)
* [Cómo invalidar el caché](#c%C3%B3mo-invalidar-el-cach%C3%A9)
* [Variación por tenant y locale](#variaci%C3%B3n-por-tenant-y-locale)
* [Pruebas](#pruebas)
* [Cambiar el backend (Redis, SQL, etc.)](#cambiar-el-backend-redis-sql-etc)


---

## Visión general

El caché HTTP se apoya en **ASP.NET Core Output Caching** (`Microsoft.AspNetCore.OutputCaching`). El store se elige en tiempo de ejecución: si `Cache:ConnectionString` está definido, se usa **Redis** (`Microsoft.AspNetCore.OutputCaching.StackExchangeRedis`); si está vacío, se usa el store **en memoria** del framework.

Principios:

* **Opt-in por endpoint** con `[OutputCache]` en el método del controlador.
* **Aislamiento por tenant y locale** vía `SetVaryByHeader("X-Tenant-Id", "Accept-Language")` en la política base.
* **Invalidación por etiquetas (tags)**: los `GET` se etiquetan con `Tags = [...]` y las mutaciones usan el filtro `[OutputCacheInvalidate("tag")]` que llama a `IOutputCacheStore.EvictByTagAsync` tras una respuesta exitosa para invalidar esas llaves de caché.


---

## Arquitectura y ubicación en capas

```
src/
├── Api/
│   ├── Program.cs                               ← .ConfigureCache(..) + .UseCacheMiddleware()
│   ├── DependencyInjection/
│   │   └── OutputCacheExtensions.cs             ← ConfigureCache + UseCacheMiddleware
│   ├── Filters/
│   │   └── OutputCacheInvalidateAttribute.cs    ← IActionFilter que llama EvictByTagAsync
│   └── Controllers/
│       └── *Controller.cs                       ← [OutputCache] y [OutputCacheInvalidate]
│
└── Infrastructure/
    └── Settings/
        └── CacheSettings.cs                     ← Configuración tipada (Enabled, DefaultTtlSeconds, ConnectionString)
```


---

## Componentes

### ConfigureCache (registro y política base)

**Ruta:** `src/Api/DependencyInjection/OutputCacheExtensions.cs`

Método de extensión que lee `CacheSettings`, lo registra en `IOptions<CacheSettings>`, elige el backend según `ConnectionString` (Redis vs. memoria), y llama a `AddOutputCache`.

```csharp
services.ConfigureCache(builder.Configuration);
```

Cuando `Enabled = false`, no se registra el servicio y los atributos `[OutputCache]` se ignoran silenciosamente. `DefaultExpirationTimeSpan` se usa cuando un endpoint no declara `Duration` en su atributo.

### UseCacheMiddleware (pipeline)

En `Program.cs` el middleware se habilita con:

```csharp
app.UseCacheMiddleware();
```

Así el toggle `Cache:Enabled` controla **tanto el registro del servicio como el middleware** desde un único punto.


---

### `[OutputCache]`

Atributo estándar de ASP.NET Core. Se coloca sobre el método del controlador.

```csharp
[HttpGet]
[OutputCache(Duration = 60, Tags = ["weather-forecasts"])]
public Task<IActionResult> GetAll(...) { ... }
```

| Propiedad | Tipo | Descripción |
|-----------|------|-------------|
| `Duration` | `int` (s) | TTL en segundos. |
| `Tags`    | `string[]` | Etiquetas para invalidación con `EvictByTagAsync`. |
| `VaryByHeaderNames` | `string[]` | Añade headers a la clave (complementa la política base). |
| `VaryByQueryKeys` | `string[]` | Restringe qué claves de query string varían la clave (`["*"]` = todas). |
| `VaryByRouteValueNames` | `string[]` | Varía por valores de ruta. |
| `PolicyName` | `string` | Selecciona una política nombrada en lugar de la base. |
| `NoStore` | `bool` | Desactiva el caché para esta acción. |

Comportamiento por defecto del middleware:

* Solo hace caché en respuestas `GET` y `HEAD`.
* Solo guarda en caché respuestas con status 200.


---

### `[OutputCacheInvalidate]`

**Ruta:** `src/Api/Filters/OutputCacheInvalidateAttribute.cs`

El framework no trae un atributo de invalidación; solo expone la API `IOutputCacheStore.EvictByTagAsync(tag, ct)`. Este filtro la envuelve.

**Solo invalida si:**

* el handler no lanzó excepción, y
* el status code final es `< 400`.

`**AllowMultiple = true**` permite invalidar varios tags desde un solo endpoint (ver [múltiples recursos](#m%C3%BAltiples-recursos)).


---

## Configuración

### CacheSettings

**Ruta:** `src/Infrastructure/Settings/CacheSettings.cs`

```csharp
public sealed class CacheSettings
{
    public const string SectionName = "Cache";

    public bool Enabled { get; init; }

    [Range(1, int.MaxValue)]
    public int DefaultTtlSeconds { get; init; } = 300;

    public string ConnectionString { get; init; } = string.Empty;
}
```

| Propiedad | Tipo | Valor por defecto | Descripción |
|-----------|------|-------------------|-------------|
| `Enabled` | `bool` | `false`           | Activa o desactiva OutputCaching. Si es `false`, el middleware no se registra. |
| `DefaultTtlSeconds` | `int` | `300`             | TTL global cuando el endpoint no especifica `Duration`. Mínimo: 1 (validado). |
| `ConnectionString` | `string` | `""`              | Cadena de conexión a Redis (StackExchange.Redis). Vacío = store en memoria. |

### appsettings

`**appsettings.json**`:

```json
{
    "Cache": {
        "Enabled": true,
        "DefaultTtlSeconds": 300,
        "ConnectionString": ""
    }
}
```

### Variables de entorno

```bash
Cache__Enabled=true
Cache__DefaultTtlSeconds=120
Cache__ConnectionString=localhost:6379
```


---

## Cómo habilitar caché en un endpoint

### Caso básico

```csharp
[HttpGet]
[OutputCache(Duration = 60, Tags = ["weather-forecasts"])]
public async Task<IActionResult> GetAll(
    IGetWeatherForecastPort useCase,
    CancellationToken cancellationToken)
{
    var result = await useCase.ExecuteAsync(cancellationToken);

    if (result.IsFailure)
        return BadRequest(new { error = result.Error.Description });

    return Ok(result.Value);
}
```

### Por ruta (resource por id)

```csharp
[HttpGet("{id}")]
[OutputCache(
    Duration = 120,
    Tags = ["weather-forecasts"],
    VaryByRouteValueNames = ["id"])]
public Task<IActionResult> GetById(Guid id, ...) { ... }
```

### Ignorando tenant/locale (datos globales)

Crear una política nombrada en `Program.cs`:

```csharp
[HttpGet("config")]
[OutputCache(PolicyName = "Global", Duration = 3600, Tags = ["config"])]
public Task<IActionResult> GetConfig(...) { ... }
```


---

## Cómo invalidar el caché

El argumento de `[OutputCacheInvalidate("...")]` debe coincidir con uno de los `Tags` declarados en el `[OutputCache]` correspondiente.

### Caso básico

```csharp
[HttpPost]
[OutputCacheInvalidate("weather-forecasts")]
public async Task<IActionResult> Create(
    [FromBody] CreateWeatherForecastInputDto input,
    ICreateWeatherForecastPort useCase,
    CancellationToken cancellationToken)
{
    var result = await useCase.ExecuteAsync(input, cancellationToken);

    if (result.IsFailure)
        return BadRequest(new { error = result.Error.Description });

    return Created(string.Empty, result.Value);
}

[HttpPut("{id}")]
[OutputCacheInvalidate("orders")]
public Task<IActionResult> Update(Guid id, ...) { ... }

[HttpDelete("{id}")]
[OutputCacheInvalidate("orders")]
public Task<IActionResult> Delete(Guid id, ...) { ... }
```

### Múltiples recursos

```csharp
[HttpPost("{orderId}/items")]
[OutputCacheInvalidate("orders")]
[OutputCacheInvalidate("inventory")]
public Task<IActionResult> AddItem(Guid orderId, ...) { ... }
```


---

## Variación por tenant y locale

La política base varía la clave por dos headers:

* `X-Tenant-Id` — header identificador de tenant.
* `Accept-Language` — locale del cliente.

Si ambos headers están ausentes, la respuesta se guarda como "sin tenant / sin locale" y se comparte entre todas las peticiones.


---

## Pruebas

```
tests/ServiceTemplate.Tests/Api/
├── Doubles/
│   ├── CountingGetWeatherForecastUseCase.cs     ← decorator singleton que cuenta ejecuciones
│   └── FakeOutputCacheStore.cs                  ← IOutputCacheStore espía para tests unitarios
├── TestWebApplicationFactory.cs                 ← factory con cache habilitado y use case decorado
├── OutputCacheTests.cs                          ← tests de integración: hit, vary, round-trip
└── OutputCacheInvalidateAttributeTests.cs       ← tests unitarios del filtro de invalidación
```

* **Integración** (`OutputCacheTests`): usa `TestWebApplicationFactory` con un decorator que cuenta ejecuciones del caso de uso. Prueba hit/miss, variación por tenant/locale, y round-trip GET→POST→GET.
* **Unitarias** (`OutputCacheInvalidateAttributeTests`): ejecuta el filtro directamente con un `FakeOutputCacheStore`, sin levantar el pipeline HTTP. Prueba evicción por tag, skip on error, múltiples tags.

### Ejecutar

```bash
dotnet test
```


---

## Cambiar el backend (Redis, SQL, etc.)

### Redis (integrado)

Redis ya viene implementado en `OutputCacheExtensions.ConfigureCache`. Para activarlo basta con definir la cadena de conexión:

```json
{
    "Cache": {
        "Enabled": true,
        "ConnectionString": "localhost:6379"
    }
}
```

o vía variable de entorno `Cache__ConnectionString`. Cuando `ConnectionString` está vacío, el framework usa por defecto el store de memoria.

El `InstanceName` usado como prefijo de las llaves de Redis se deriva de `AppInfo:ServiceName` (`"{ServiceName}:"`), lo que aísla de forma natural los keys entre servicios que compartan una misma instancia de Redis.

El paquete ya está declarado en `src/Api/Api.csproj`:

```xml
<PackageReference Include="Microsoft.AspNetCore.OutputCaching.StackExchangeRedis" Version="8.0.*" />
```

El registro de `AddStackExchangeRedisOutputCache` reemplaza `IOutputCacheStore`. El resto del código (atributos, filtros, políticas) no cambia.

### Otros backends

Se debe realizar una implementación de `IOutputCacheStore`. La interfaz tiene 3 métodos (`GetAsync`, `SetAsync`, `EvictByTagAsync`). Luego se registra con `builder.Services.AddSingleton<IOutputCacheStore, YourCustomStore>();` preferiblemente en `OutputCacheExtensions`


---
