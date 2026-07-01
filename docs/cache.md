# Caché

El servicio dispone de dos niveles de caché complementarios:

* **Nivel 1 (HTTP / OutputCache):** cachea respuestas HTTP completas en el borde de la capa API. Usa `IOutputCacheStore` (Redis o memoria).
* **Nivel 2 (aplicación / cache-aside):** cachea resultados de llamadas costosas por debajo de HTTP (consultas de BD, adaptadores externos). Usa `ICacheStore` (Redis o NoOp).

## Índice

* [Nivel 1: Caché HTTP (OutputCache)](#nivel-1-cach%C3%A9-http-outputcache)
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
* [Nivel 2: Caché de aplicación (cache-aside)](#nivel-2-cach%C3%A9-de-aplicaci%C3%B3n-cache-aside)
  * [Visión general](#visi%C3%B3n-general-1)
  * [Ubicación en capas](#ubicaci%C3%B3n-en-capas)
  * [Contrato `ICacheStore`](#contrato-icachestore)
  * [Llaves (`CacheKey`)](#llaves-cachekey)
  * [TTL por llamada](#ttl-por-llamada)
  * [Solo éxitos y degradación transparente](#solo-%C3%A9xitos-y-degradaci%C3%B3n-transparente)
  * [Invalidación post-commit](#invalidaci%C3%B3n-post-commit)
  * [Partición por tenant](#partici%C3%B3n-por-tenant)
  * [Configuración](#configuraci%C3%B3n-1)
  * [Advertencia sobre agregados](#advertencia-sobre-agregados)
  * [Ejemplo real](#ejemplo-real)


---

## Nivel 1: Caché HTTP (OutputCache)

### Visión general

El caché HTTP se apoya en **ASP.NET Core Output Caching** (`Microsoft.AspNetCore.OutputCaching`). El store se elige en tiempo de ejecución: si `Cache:ConnectionString` está definido, se usa **Redis** (`Microsoft.AspNetCore.OutputCaching.StackExchangeRedis`); si está vacío, se usa el store **en memoria** del framework.

Principios:

* **Opt-in por endpoint** con `[OutputCache]` en el método del controlador.
* **Aislamiento por tenant y locale** vía `SetVaryByHeader("X-Tenant-Id", "Accept-Language")` en la política base.
* **Invalidación por etiquetas (tags)**: los `GET` se etiquetan con `Tags = [...]` y las mutaciones usan el filtro `[OutputCacheInvalidate("tag")]` que llama a `IOutputCacheStore.EvictByTagAsync` tras una respuesta exitosa para invalidar esas llaves de caché.


---

### Arquitectura y ubicación en capas

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
        └── CacheSettings.cs                     ← Configuración tipada (Enabled, L2Enabled, DefaultTtlSeconds, ConnectionString)
```


---

### Componentes

#### ConfigureCache (registro y política base)

**Ruta:** `src/Api/DependencyInjection/OutputCacheExtensions.cs`

Método de extensión que lee `CacheSettings`, lo registra en `IOptions<CacheSettings>`, elige el backend según `ConnectionString` (Redis vs. memoria), y llama a `AddOutputCache`.

```csharp
services.ConfigureCache(builder.Configuration);
```

Cuando `Enabled = false`, no se registra el servicio y los atributos `[OutputCache]` se ignoran silenciosamente. `DefaultExpirationTimeSpan` se usa cuando un endpoint no declara `Duration` en su atributo.

#### UseCacheMiddleware (pipeline)

En `Program.cs` el middleware se habilita con:

```csharp
app.UseCacheMiddleware();
```

Así el toggle `Cache:Enabled` controla **tanto el registro del servicio como el middleware** desde un único punto.


---

#### `[OutputCache]`

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

#### `[OutputCacheInvalidate]`

**Ruta:** `src/Api/Filters/OutputCacheInvalidateAttribute.cs`

El framework no trae un atributo de invalidación; solo expone la API `IOutputCacheStore.EvictByTagAsync(tag, ct)`. Este filtro la envuelve.

**Solo invalida si:**

* el handler no lanzó excepción, y
* el status code final es `< 400`.

`**AllowMultiple = true**` permite invalidar varios tags desde un solo endpoint (ver [múltiples recursos](#m%C3%BAltiples-recursos)).


---

### Configuración

#### CacheSettings

**Ruta:** `src/Infrastructure/Settings/CacheSettings.cs`

```csharp
public sealed class CacheSettings
{
    public const string SectionName = "Cache";

    public bool Enabled { get; init; }

    public bool L2Enabled { get; init; }

    [Range(1, int.MaxValue)]
    public int DefaultTtlSeconds { get; init; } = 300;

    public string ConnectionString { get; init; } = string.Empty;
}
```

| Propiedad | Tipo | Valor por defecto | Descripción |
|-----------|------|-------------------|-------------|
| `Enabled` | `bool` | `false`           | Activa o desactiva OutputCaching (L1). Si es `false`, el middleware no se registra. |
| `L2Enabled` | `bool` | `false`         | Activa la caché L2 de aplicación (cache-aside). Requiere `ConnectionString`. |
| `DefaultTtlSeconds` | `int` | `300`             | TTL global del L1 cuando el endpoint no especifica `Duration`. Mínimo: 1 (validado). |
| `ConnectionString` | `string` | `""`              | Cadena de conexión a Redis (StackExchange.Redis). Compartida por L1 y L2. Vacío = store en memoria para L1, NoOp para L2. |

#### appsettings

**`appsettings.json`**:

```json
{
    "Cache": {
        "Enabled": true,
        "L2Enabled": true,
        "DefaultTtlSeconds": 300,
        "ConnectionString": ""
    }
}
```

#### Variables de entorno

```bash
Cache__Enabled=true
Cache__L2Enabled=true
Cache__DefaultTtlSeconds=120
Cache__ConnectionString=localhost:6379
```


---

### Cómo habilitar caché en un endpoint

#### Caso básico

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

#### Por ruta (resource por id)

```csharp
[HttpGet("{id}")]
[OutputCache(
    Duration = 120,
    Tags = ["weather-forecasts"],
    VaryByRouteValueNames = ["id"])]
public Task<IActionResult> GetById(Guid id, ...) { ... }
```

#### Ignorando tenant/locale (datos globales)

Crear una política nombrada en `Program.cs`:

```csharp
[HttpGet("config")]
[OutputCache(PolicyName = "Global", Duration = 3600, Tags = ["config"])]
public Task<IActionResult> GetConfig(...) { ... }
```


---

### Cómo invalidar el caché

El argumento de `[OutputCacheInvalidate("...")]` debe coincidir con uno de los `Tags` declarados en el `[OutputCache]` correspondiente.

#### Caso básico

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

#### Múltiples recursos

```csharp
[HttpPost("{orderId}/items")]
[OutputCacheInvalidate("orders")]
[OutputCacheInvalidate("inventory")]
public Task<IActionResult> AddItem(Guid orderId, ...) { ... }
```


---

### Variación por tenant y locale

La política base varía la clave por dos headers:

* `X-Tenant-Id` — header identificador de tenant.
* `Accept-Language` — locale del cliente.

Si ambos headers están ausentes, la respuesta se guarda como "sin tenant / sin locale" y se comparte entre todas las peticiones.


---

### Pruebas

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

#### Ejecutar

```bash
dotnet test
```


---

### Cambiar el backend (Redis, SQL, etc.)

#### Redis (integrado)

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

#### Otros backends

Se debe realizar una implementación de `IOutputCacheStore`. La interfaz tiene 3 métodos (`GetAsync`, `SetAsync`, `EvictByTagAsync`). Luego se registra con `builder.Services.AddSingleton<IOutputCacheStore, YourCustomStore>();` preferiblemente en `OutputCacheExtensions`


---

## Nivel 2: Caché de aplicación (cache-aside)

### Visión general

El caché de aplicación (L2) cachea llamadas costosas **por debajo de HTTP**: consultas de base de datos y adaptadores externos (e.g. llamadas a APIs de terceros). Complementa el L1 (Output Caching), que actúa sobre la respuesta HTTP completa.

Un caso de uso puede beneficiarse de ambos niveles simultáneamente: L2 evita consultas redundantes a la BD; L1 evita ejecuciones del caso de uso. L2 actúa primero, antes de que el resultado llegue a la capa API.

Principios:

* **Cache-aside explícito:** la lógica de caché se escribe en el adaptador de persistencia o en el puerto de salida adecuado, nunca en el dominio.
* **Solo se cachean éxitos:** los resultados de error no contaminan el caché.
* **Degradación transparente:** un fallo de Redis se registra y se ignora; el servicio continúa sin caché.
* **Invalidación post-commit:** la invalidación ocurre en el caso de uso, después de que la transacción haya tenido éxito.


---

### Ubicación en capas

```
src/
├── Shared/
│   └── Application/
│       ├── Ports/
│       │   └── ICacheStore.cs              ← contrato (puerto de salida)
│       └── Caching/
│           └── CacheKey.cs                 ← constructor de llaves
│
└── Infrastructure/
    └── Caching/
        ├── RedisCacheStore.cs              ← implementación Redis (StackExchange.Redis + System.Text.Json)
        ├── NoOpCacheStore.cs               ← implementación no-op (siempre ejecuta la factory)
        └── DistributedCacheExtensions.cs   ← registro DI (AddDistributedCache)
```

Los repositorios e implementaciones de adaptadores en `Infrastructure` reciben `ICacheStore` por inyección de dependencias. El dominio y los casos de uso en `Application` no conocen la implementación concreta.


---

### Contrato `ICacheStore`

**Ruta:** `src/Shared/Application/Ports/ICacheStore.cs`

```csharp
public interface ICacheStore
{
    Task<Result<T>> GetOrSetAsync<T>(
        string key,
        TimeSpan ttl,
        Func<Task<Result<T>>> factory,
        CancellationToken cancellationToken = default);

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}
```

| Método | Propósito |
|--------|-----------|
| `GetOrSetAsync<T>` | Devuelve el valor cacheado para `key`; en miss ejecuta `factory` y cachea su resultado solo si es exitoso (`Result.IsSuccess`). |
| `RemoveAsync` | Elimina una sola llave (invalidación precisa tras una mutación puntual). |
| `RemoveByPrefixAsync` | Elimina todas las llaves que empiecen con `prefix` (invalidación de colección). `prefix` debe ser no vacío y empezar con `"ctx:"`. |


---

### Llaves (`CacheKey`)

**Ruta:** `src/Shared/Application/Caching/CacheKey.cs`

Builder fluido que garantiza el formato canónico y previene colisiones entre contextos, recursos y tenants.

```csharp
// Llave simple (sin tenant)
string key = CacheKey.For("masteraccess").Resource("tenant", code);
// → ctx:masteraccess:v1:tenant:{code}

// Con partición por tenant
string key = CacheKey.For("orders").Tenant("acme-corp").Resource("order", orderId);
// → ctx:orders:v1:t:acme-corp:order:{orderId}

// Prefijo para invalidación de colección
string prefix = CacheKey.For("masteraccess").Prefix("tenant");
// → ctx:masteraccess:v1:tenant
```

Formato canónico sin tenant: `ctx:{context}:v1:{resource}:{id}`
Formato canónico con tenant: `ctx:{context}:v1:t:{tenantId}:{resource}:{id}`

Restricciones:
* `context`, `resource` y `tenantId` no pueden contener `":"` ni estar vacíos (se lanza `ArgumentException`).
* `v1` es la versión de esquema (la constante privada `SchemaVersion` en `CacheKey.cs`). Si el contrato de serialización de un tipo cacheado cambia, se debe incrementar esta constante para invalidar entradas antiguas automáticamente.
* `RemoveByPrefixAsync` requiere que el prefijo empiece con `"ctx:"` (validado en `RedisCacheStore`).


---

### TTL por llamada

El TTL se pasa en cada llamada a `GetOrSetAsync`. No existe un TTL global de L2 — cada sitio de caché elige el valor adecuado al dominio.

```csharp
// 10 minutos
await cache.GetOrSetAsync(key, TimeSpan.FromMinutes(10), factory, ct);

// 1 hora
await cache.GetOrSetAsync(key, TimeSpan.FromHours(1), factory, ct);
```

> **Nota:** `Cache:DefaultTtlSeconds` en `CacheSettings` corresponde exclusivamente al L1 (Output Cache). No aplica al L2.


---

### Solo éxitos y degradación transparente

**Solo se cachean éxitos**

Si `factory` devuelve `Result.Failure(...)`, el resultado se devuelve al llamador pero no se escribe en Redis. Los errores de dominio (entidad no encontrada, validación fallida, etc.) no contaminan el caché.

**Degradación transparente**

Si Redis no está disponible durante una lectura, `RedisCacheStore` registra un `Warning` y ejecuta `factory` directamente, como si hubiera un miss. Si falla durante una invalidación (`RemoveAsync` / `RemoveByPrefixAsync`), también registra el `Warning` y continúa sin lanzar excepción. El servicio opera correctamente sin caché ante cualquier fallo del backend.

**NoOpCacheStore**

Cuando `L2Enabled = false` o `ConnectionString` está vacío, se registra `NoOpCacheStore`, que siempre ejecuta la factory y hace no-op en las invalidaciones. No hay overhead de red ni serialización.


---

### Invalidación post-commit

La invalidación de L2 se realiza **en el caso de uso, después de que `IUnitOfWorkPort.CommitAsync()` haya tenido éxito**. Nunca se llama a `RemoveAsync` / `RemoveByPrefixAsync` desde dentro de un repositorio (métodos `Update`, `Remove`).

**Por qué:** si la transacción falla, la invalidación sería prematura — el caché habría sido vaciado pero el cambio no habría sido persistido. Llamar después del commit garantiza que el caché solo se invalida cuando hay un cambio real en la BD.

```csharp
// En el caso de uso
var commit = await _unitOfWork.CommitAsync(cancellationToken);
if (commit.IsFailure)
    return commit.Error;

// Invalidación post-commit: llave puntual
await _cache.RemoveAsync(
    CacheKey.For("masteraccess").Resource("tenant", tenant.Code),
    cancellationToken);

// Invalidación de colección (si existe caché de listados del mismo recurso)
await _cache.RemoveByPrefixAsync(
    CacheKey.For("masteraccess").Prefix("tenant"),
    cancellationToken);
```


---

### Partición por tenant

Cuando una entrada de caché es tenant-específica (los datos difieren entre tenants), se usa `.Tenant(tenantId)` para incluir el tenant en la llave:

```csharp
string key = CacheKey
    .For("billing")
    .Tenant(tenantId)
    .Resource("invoice", invoiceId);
// → ctx:billing:v1:t:{tenantId}:invoice:{invoiceId}
```

Esto garantiza que dos tenants con el mismo `invoiceId` no compartan entrada de caché.

> **Nota:** si los datos son globales (independientes de tenant), no incluir `.Tenant(...)` — la llave es más corta y la invalidación más simple.


---

### Configuración

El flag `Cache:L2Enabled` es **independiente** de `Cache:Enabled` (que controla el L1). Ambos niveles comparten la misma `ConnectionString`.

#### CacheSettings

**Ruta:** `src/Infrastructure/Settings/CacheSettings.cs`

| Propiedad | Tipo | Valor por defecto | Descripción |
|-----------|------|-------------------|-------------|
| `L2Enabled` | `bool` | `false` | Activa la caché L2 de aplicación (cache-aside). Requiere `ConnectionString`. |
| `ConnectionString` | `string` | `""` | Compartida con L1. Vacío ⇒ `NoOpCacheStore` aunque `L2Enabled` sea `true`. |

#### Registro DI

**Ruta:** `src/Infrastructure/Caching/DistributedCacheExtensions.cs`

```csharp
services.AddDistributedCache(configuration);
```

`AddDistributedCache` registra `ICacheStore` como singleton:
* `RedisCacheStore` cuando `L2Enabled = true` y `ConnectionString` está definido. El `IConnectionMultiplexer` se crea con `AbortOnConnectFail = false`.
* `NoOpCacheStore` en cualquier otro caso.

#### appsettings.json

```json
{
    "Cache": {
        "Enabled": true,
        "L2Enabled": true,
        "DefaultTtlSeconds": 300,
        "ConnectionString": "localhost:6379"
    }
}
```

#### Variables de entorno

```bash
Cache__L2Enabled=true
Cache__ConnectionString=localhost:6379
```


---

### Advertencia sobre agregados

> **Atención:** cachear agregados de dominio directamente puede causar problemas.
>
> * Los agregados cacheados vuelven como objetos **detached** (sin tracking de EF Core). Si se pasan a `repository.Update(...)`, pueden generar inconsistencias o excepciones de tracking.
> * Muchos agregados tienen constructores privados y no pueden ser deserializados por `System.Text.Json`. Cachearlos directamente provoca que cada cache hit lanze una excepción que se traga como `Warning`, degradando silenciosamente a un 0 % de hit rate efectivo.
>
> **Preferencia:** cachear **snapshots serializables** (records o DTOs simples) en lugar de agregados. Reconstruir el agregado desde el snapshot al leer del caché (ver ejemplo real más abajo).


---

### Ejemplo real

`TenantRepository.GetByCodeAsync` (en `src/Shared/Infrastructure/MasterAccess/Persistence/EntityFramework/Tenants/TenantRepository.cs`) ilustra el patrón completo aplicando la advertencia anterior: `TenantAggregate` tiene un constructor privado y no puede ser deserializado por `System.Text.Json`, por lo que el repositorio cachea un snapshot serializable (`TenantCacheModel`) y reconstruye el agregado mediante `TenantAggregate.Reconstruct` en cada cache hit.

```csharp
public async Task<Result<TenantAggregate>> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
{
    var cached = await cache.GetOrSetAsync(
        CacheKey.For("masteraccess").Resource("tenant", code),
        TimeSpan.FromMinutes(10),
        () => QueryByCodeAsync(code, cancellationToken),
        cancellationToken).ConfigureAwait(false);

    return cached.IsSuccess
        ? Result<TenantAggregate>.Success(
            TenantAggregate.Reconstruct(cached.Value.Code, cached.Value.Database, cached.Value.ServerDatabase))
        : Result<TenantAggregate>.Failure(cached.Error);
}
```

El método privado `QueryByCodeAsync` devuelve `Result<TenantCacheModel>` — un record con solo propiedades primitivas que `System.Text.Json` puede serializar y deserializar sin configuración adicional:

```csharp
return document is null
    ? TenantErrors.NotFound(code)
    : new TenantCacheModel(document.Code, document.Database, document.ServerDatabase);
```

* La llave resultante es `ctx:masteraccess:v1:tenant:{code}`.
* TTL de 10 minutos, adecuado para datos de tenant que raramente cambian.
* En miss o fallo de Redis, se ejecuta `QueryByCodeAsync` directamente (con `AsNoTracking()`).
* Si `QueryByCodeAsync` devuelve un `Result.Failure` (tenant no encontrado, error de BD), el resultado no se cachea.
* El snapshot `TenantCacheModel` se define en `src/Shared/Infrastructure/MasterAccess/Persistence/EntityFramework/Tenants/TenantCacheModel.cs` como `internal sealed record` — es un detalle de implementación de la infraestructura, no parte del dominio.


---
