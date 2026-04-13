# Caché HTTP

## Índice

- [Visión general](#visión-general)
- [Arquitectura y ubicación en capas](#arquitectura-y-ubicación-en-capas)
- [Componentes](#componentes)
    - [ICacheService](#icacheservice)
    - [CacheSettings](#cachesettings)
    - [CacheKeyBuilder](#cachekeybuilder)
    - [NullCacheService](#nullcacheservice)
    - [HttpCacheAttribute](#httpcacheattribute)
    - [InvalidateCacheAttribute](#invalidatecacheattribute)
- [Formato de la llave de caché](#formato-de-la-llave-de-caché)
- [Configuración](#configuración)
- [Cómo cachear un endpoint](#cómo-cachear-un-endpoint)
- [Cómo invalidar el caché](#cómo-invalidar-el-caché)
- [Graceful degradation](#graceful-degradation)
- [Registro de dependencias](#registro-de-dependencias)
- [Pruebas](#pruebas)
- [Agregar una nueva implementación de caché](#agregar-una-nueva-implementación-de-caché)
- [Limitaciones y consideraciones](#limitaciones-y-consideraciones)

---

## Visión general

El sistema de caché HTTP almacena respuestas de endpoints idempotentes (principalmente `GET`) para reducir carga en base de datos y mejorar latencia. Está diseñado con los siguientes principios:

- **Opt-in por endpoint**: cada acción del controlador decide explícitamente si participa en el caché.
- **Aislamiento por tenant y locale**: las llaves incluyen identificadores de tenant y locale para evitar que un usuario vea datos de otro.
- **Invalidación por mutaciones**: los endpoints que modifican datos (`POST`, `PUT`, `DELETE`) invalidan el caché del recurso afectado.
- **Graceful degradation**: si el backend de cache no responde, el sistema continúa funcionando como si el caché no existiera, sin lanzar errores al cliente.
- **Implementación intercambiable**: la abstracción `ICacheService` permite utilizar cualquier backend sin tocar el código de aplicación.

---

## Arquitectura y ubicación en capas

```
src/
├── Shared/Application/Interfaces/
│   └── ICacheService.cs              ← Contrato (disponible para todas las capas)
│
├── Infrastructure/
│   ├── Settings/
│   │   └── CacheSettings.cs          ← Configuración tipada
│   ├── Cache/
│   │   ├── CacheKeyBuilder.cs        ← Construcción determinista de llaves
│   │   └── NullCacheService.cs       ← No-op cuando el caché está deshabilitado
│   └── Extensions/
│       └── CacheExtensions.cs        ← Registro en DI
│
└── Api/
    └── Filters/
        ├── HttpCacheAttribute.cs     ← Intercepta GETs y aplica caché
        └── InvalidateCacheAttribute.cs ← Invalida caché tras mutaciones exitosas
```

**Regla de dependencias:**

```
Api  →  Infrastructure  →  Shared.Application
```

Los filtros (`Api/Filters`) usan `ICacheService` y `CacheKeyBuilder` en tiempo de ejecución a través del contenedor de DI y referencias de proyecto directas. Las capas de dominio y aplicación de negocio no conocen la existencia del caché.

---

## Componentes

### ICacheService

**Ruta:** `src/Shared/Application/Interfaces/ICacheService.cs`

Contrato genérico del servicio de caché. Al vivir en `Shared.Application`, está disponible para casos de uso de negocio que requieran control fino del caché.

```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);
}
```

| Método                | Descripción                                                                                   |
| --------------------- | --------------------------------------------------------------------------------------------- |
| `GetAsync<T>`         | Obtiene un valor deserializado. Retorna `default` si la llave no existe.                      |
| `SetAsync<T>`         | Serializa y almacena un valor con TTL.                                                        |
| `RemoveAsync`         | Elimina una llave específica.                                                                 |
| `RemoveByPrefixAsync` | Elimina todas las llaves que comienzan con el prefijo dado (útil para invalidación por ruta). |

---

### CacheSettings

**Ruta:** `src/Infrastructure/Settings/CacheSettings.cs`

Clase de configuración tipada enlazada a la sección `Cache` del `appsettings.json`.

```csharp
public sealed class CacheSettings
{
    public const string SectionName = "Cache";

    public bool Enabled { get; init; }
    public string ConnectionString { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int DefaultTtlSeconds { get; init; } = 300;

    public string KeyPrefix { get; init; } = "api:v1";
}
```

| Propiedad           | Tipo     | Valor por defecto | Descripción                                                                   |
| ------------------- | -------- | ----------------- | ----------------------------------------------------------------------------- |
| `Enabled`           | `bool`   | `false`           | Activa o desactiva el caché. Si es `false`, se registra `NullCacheService`.   |
| `ConnectionString`  | `string` | `""`              | Cadena de conexión al servicio de caché.                                      |
| `DefaultTtlSeconds` | `int`    | `300`             | TTL en segundos usado cuando el endpoint no especifica uno propio. Mínimo: 1. |
| `KeyPrefix`         | `string` | `"api:v1"`        | Prefijo global de todas las llaves. Permite aislar entornos o versiones.      |

**Ejemplo de configuración:**

```json
{
    "Cache": {
        "Enabled": true,
        "ConnectionString": "redis-host:6379,password=secret,ssl=true",
        "DefaultTtlSeconds": 300,
        "KeyPrefix": "api:v1"
    }
}
```

La clase está registrada con `IOptions<CacheSettings>` y se valida al inicio con `ValidateDataAnnotations()` y `ValidateOnStart()`. Si `DefaultTtlSeconds` es menor a 1, la aplicación falla en el arranque con un error descriptivo.

---

### CacheKeyBuilder

**Ruta:** `src/Infrastructure/Cache/CacheKeyBuilder.cs`

Clase estática que construye llaves determinísticas. No tiene dependencias de DI.

```csharp
// Construye: {prefix}:{route}:{queryHash}:{tenant}:{locale}
string Build(string prefix, string route, string? queryString, string? tenant, string? locale)

// Construye: {prefix}:{route}:
string BuildRoutePrefix(string prefix, string route)
```

**Algoritmo del hash de query string:**

1. Se eliminan el `?` inicial y los parámetros vacíos.
2. Todos los pares `clave=valor` se convierten a minúsculas.
3. Se ordenan alfabéticamente (para que `?a=1&b=2` y `?b=2&a=1` produzcan el mismo hash).
4. Se calcula SHA-256 del string normalizado.
5. Se toman los primeros 8 bytes → 16 caracteres hexadecimales en minúsculas.

Si el query string es nulo o vacío, el segmento de hash es `_`.

**Reglas de sanitización para tenant y locale:**

- Si el valor es nulo o solo espacios en blanco → `_`
- Se aplica `Trim()` y conversión a minúsculas

`BuildRoutePrefix` retorna el prefijo con `:` final, listo para ser usado en `RemoveByPrefixAsync`.

---

### NullCacheService

**Ruta:** `src/Infrastructure/Cache/NullCacheService.cs`

Implementación no-op de `ICacheService`. Se registra automáticamente cuando:

- `Cache:Enabled` es `false`
- `Cache:ConnectionString` está vacío o es nulo (y `Enabled` es `true`)
- La conexión al servicio de Caché falla durante el arranque de la aplicación

`NullCacheService` descarta silenciosamente todas las escrituras y siempre reporta cache miss en las lecturas. Esto garantiza que el código que usa `ICacheService` no requiere guardas de `if (enabled)`.

---

### HttpCacheAttribute

**Ruta:** `src/Api/Filters/HttpCacheAttribute.cs`

Action filter de ASP.NET Core que aplica caché a respuestas `GET` exitosas. Se coloca directamente sobre el método del controlador.

```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class HttpCacheAttribute : Attribute, IAsyncActionFilter
```

**Parámetros:**

| Parámetro      | Tipo   | Descripción                                                                   |
| -------------- | ------ | ----------------------------------------------------------------------------- |
| `ttlSeconds`   | `int`  | TTL específico para este endpoint. `0` usa `CacheSettings.DefaultTtlSeconds`. |
| `VaryByTenant` | `bool` | Incluye el tenant en la llave. Defecto: `true`.                               |
| `VaryByLocale` | `bool` | Incluye el locale en la llave. Defecto: `true`.                               |

**Flujo de ejecución:**

```
Request GET
    │
    ▼
¿Es método GET?  ──No──► ejecutar handler normalmente
    │
   Sí
    ▼
Construir llave de caché
    │
    ▼
ICacheService.GetAsync<JsonElement>(key)
    │
    ├── HIT  ──► OkObjectResult(jsonElement)  [handler NO se ejecuta]
    │
    └── MISS
          │
          ▼
       Ejecutar handler
          │
          ▼
       ¿Resultado es OkObjectResult con valor?
          │
         Sí
          ▼
       ICacheService.SetAsync(key, value, ttl)
```

**Extracción de tenant y locale:**

- **Tenant:** primero busca el header `X-Tenant-Id`, luego el claim JWT `tenant_id`. Si ninguno existe y `VaryByTenant = true`, el segmento es `_`.
- **Locale:** lee el header `Accept-Language`. Si no existe y `VaryByLocale = true`, el segmento es `_`.

**Solo cachea respuestas exitosas:** únicamente se almacena en caché si el handler retorna `OkObjectResult` (HTTP 200) con valor no nulo. Respuestas 400, 404, 500, etc. no se guardan.

---

### InvalidateCacheAttribute

**Ruta:** `src/Api/Filters/InvalidateCacheAttribute.cs`

Action filter que invalida las llaves de caché relacionadas después de una mutación exitosa.

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class InvalidateCacheAttribute : Attribute, IAsyncActionFilter
```

**Parámetros:**

| Parámetro     | Tipo     | Descripción                                                                 |
| ------------- | -------- | --------------------------------------------------------------------------- |
| `routePrefix` | `string` | Prefijo de ruta del recurso a invalidar (ej. `"api/v1/weather-forecasts"`). |

**Flujo de ejecución:**

```
Request POST/PUT/DELETE
    │
    ▼
Ejecutar handler (next())
    │
    ▼
¿Hubo excepción?  ──Sí──► salir sin invalidar
    │
   No
    ▼
¿StatusCode >= 400?  ──Sí──► salir sin invalidar
    │
   No (mutación exitosa)
    │
    ▼
Construir prefijo: {keyPrefix}:{routePrefix}:
    │
    ▼
ICacheService.RemoveByPrefixAsync(prefix)
```

**La invalidación ocurre después del handler**, lo que garantiza que si la mutación falla, el caché no se toca.

**Soporte para múltiples recursos:** como `AllowMultiple = true`, se puede invalidar más de un recurso desde el mismo endpoint:

```csharp
[HttpPost("batch")]
[InvalidateCache("api/v1/orders")]
[InvalidateCache("api/v1/inventory")]
public async Task<IActionResult> BatchUpdate(...) { ... }
```

---

## Formato de la llave de caché

```
{KeyPrefix}:{path}:{queryHash}:{tenant}:{locale}
```

| Segmento    | Origen                                         | Ejemplo                    |
| ----------- | ---------------------------------------------- | -------------------------- |
| `KeyPrefix` | `CacheSettings.KeyPrefix`                      | `api:v1`                   |
| `path`      | `HttpRequest.Path` sin `/` inicial             | `api/v1/weather-forecasts` |
| `queryHash` | SHA-256 de params ordenados (8 bytes → 16 hex) | `a3f1c82b9e047d56`         |
| `tenant`    | Header `X-Tenant-Id` o claim `tenant_id`       | `acme-corp` o `_`          |
| `locale`    | Header `Accept-Language`                       | `es-co` o `_`              |

**Ejemplos de llaves reales:**

```
# GET /api/v1/weather-forecasts (sin tenant ni locale)
api:v1:api/v1/weather-forecasts:_:_:_

# GET /api/v1/weather-forecasts?page=2&size=10 (tenant acme, locale en-US)
api:v1:api/v1/weather-forecasts:a3f1c82b9e047d56:acme:en-us

# GET /api/v1/orders/123 (tenant beta, sin locale)
api:v1:api/v1/orders/123:_:beta:_
```

**Prefijo de invalidación para `api/v1/weather-forecasts`:**

```
api:v1:api/v1/weather-forecasts:
```

Este prefijo matchea **todas** las llaves de ese recurso independientemente del query string, tenant o locale.

---

## Configuración

### Variables de entorno

Las propiedades de `CacheSettings` pueden sobreescribirse con variables de entorno usando el separador `__`:

```bash
Cache__Enabled=true
Cache__ConnectionString=redis:6379,password=secret
Cache__DefaultTtlSeconds=120
Cache__KeyPrefix=api:v1
```

### Configuración por entorno

**`appsettings.json`** (base, producción):

```json
{
    "Cache": {
        "Enabled": false,
        "ConnectionString": "",
        "DefaultTtlSeconds": 300,
        "KeyPrefix": "api:v1"
    }
}
```

**`appsettings.Development.json`** (desarrollo local):

```json
{
    "Cache": {
        "Enabled": false,
        "ConnectionString": "localhost:6379",
        "DefaultTtlSeconds": 60
    }
}
```

> **Nota:** en desarrollo el caché está deshabilitado por defecto. Para probarlo localmente, cambiar `Enabled` a `true`.

---

## Cómo cachear un endpoint

Agregar `[HttpCache]` sobre el método del controlador. Solo aplica a `GET`.

### Caso básico

```csharp
[HttpGet]
[HttpCache(ttlSeconds: 60)]
public async Task<IActionResult> GetAll(
    IGetWeatherForecastUseCase useCase,
    CancellationToken cancellationToken)
{
    var result = await useCase.ExecuteAsync(cancellationToken);

    if (result.IsFailure)
        return BadRequest(new { error = result.Error.Description });

    return Ok(result.Value);
}
```

### TTL por defecto (usa `CacheSettings.DefaultTtlSeconds`)

```csharp
[HttpGet("{id}")]
[HttpCache]  // ttlSeconds = 0 → usa DefaultTtlSeconds (300s por defecto)
public async Task<IActionResult> GetById(Guid id, ...) { ... }
```

### Datos globales (igual para todos los tenants y locales)

```csharp
[HttpGet("config")]
[HttpCache(ttlSeconds: 3600, VaryByTenant = false, VaryByLocale = false)]
public async Task<IActionResult> GetConfig(...) { ... }
```

### Datos por tenant pero sin variación por locale

```csharp
[HttpGet("profile")]
[HttpCache(ttlSeconds: 120, VaryByLocale = false)]
public async Task<IActionResult> GetProfile(...) { ... }
```

### Tabla de decisión: `VaryByTenant` y `VaryByLocale`

| Caso                                | `VaryByTenant` | `VaryByLocale` | Uso típico                              |
| ----------------------------------- | -------------- | -------------- | --------------------------------------- |
| Datos globales idénticos para todos | `false`        | `false`        | Configuración, catálogos maestros       |
| Datos por tenant, idioma único      | `true`         | `false`        | Perfiles, datos de negocio sin i18n     |
| Datos globales traducidos           | `false`        | `true`         | Traducciones, mensajes del sistema      |
| Datos por tenant y por locale       | `true`         | `true`         | **Defecto** — datos de negocio con i18n |

---

## Cómo invalidar el caché

Agregar `[InvalidateCache("ruta")]` sobre el método de mutación. El argumento debe ser la ruta del recurso cacheado tal como aparece en `HttpRequest.Path` sin la barra inicial.

### Caso básico

```csharp
[HttpPost]
[InvalidateCache("api/v1/weather-forecasts")]
public async Task<IActionResult> Create(
    [FromBody] CreateWeatherForecastInputDto input,
    ICreateWeatherForecastUseCase useCase,
    CancellationToken cancellationToken)
{
    var result = await useCase.ExecuteAsync(input, cancellationToken);

    if (result.IsFailure)
        return BadRequest(new { error = result.Error.Description });

    return Created(string.Empty, result.Value);
}
```

### PUT y DELETE

```csharp
[HttpPut("{id}")]
[InvalidateCache("api/v1/orders")]
public async Task<IActionResult> Update(Guid id, [FromBody] UpdateOrderInput input, ...) { ... }

[HttpDelete("{id}")]
[InvalidateCache("api/v1/orders")]
public async Task<IActionResult> Delete(Guid id, ...) { ... }
```

### Invalidar múltiples recursos

```csharp
[HttpPost("{orderId}/items")]
[InvalidateCache("api/v1/orders")]
[InvalidateCache("api/v1/inventory")]
public async Task<IActionResult> AddItem(Guid orderId, [FromBody] AddItemInput input, ...) { ... }
```

### Comportamiento condicional de la invalidación

| Escenario                           | ¿Se invalida? | Motivo                                       |
| ----------------------------------- | ------------- | -------------------------------------------- |
| Handler retorna 201 Created         | ✅ Sí         | Mutación exitosa                             |
| Handler retorna 200 OK              | ✅ Sí         | Mutación exitosa                             |
| Handler retorna 400 BadRequest      | ❌ No         | Error de validación — datos no modificados   |
| Handler retorna 404 NotFound        | ❌ No         | Recurso no encontrado — datos no modificados |
| Handler retorna 500 (excepción)     | ❌ No         | Fallo del servidor — estado desconocido      |
| Model binding falla (body inválido) | ❌ No         | El action ni siquiera llega a ejecutarse     |

---

## Graceful degradation

El sistema está diseñado para nunca afectar al cliente por fallas en el servicio de caché

### Durante el arranque

Si `Cache:Enabled = true` y backend configurado no es alcanzable:

1. Si la configuración en sí es inválida y lanza una excepción, se captura en `CacheExtensions.AddCacheServices` y se registra `NullCacheService` en su lugar.
2. La aplicación arranca normalmente; el caché simplemente no funciona.

### Durante operación

La implementación específica de caché debe considerar:

- El `try/catch` en cada método para capturar la excepción.
- Emitir un log `Warning` con el detalle del error.
- El flujo continúa: `Get` retorna `null` (cache miss) y el handler se ejecuta normalmente.

### Logs de degradación

Todos los mensajes de caché usan el prefijo `[Cache]`:

```
[WRN] [Cache] Get failed for key api:v1:api/v1/weather-forecasts:_:_:_. Treating as cache miss.
[WRN] [Cache] Set failed for key api:v1:api/v1/orders:abc123:acme:es-co. Skipping cache write.
[WRN] [Cache] RemoveByPrefix failed for prefix api:v1:api/v1/orders:. [excepción]
```

Los mensajes de arranque usan `Console.WriteLine` (Serilog no está configurado aún en ese momento):

```
[Cache] Cache is disabled. Using NullCacheService.
[Cache] Cache is enabled but ConnectionString is empty. Using NullCacheService.
```

---

## Registro de dependencias

El registro se hace automáticamente en `InfrastructureServiceExtensions.AddInfrastructureServices`. No se requiere ninguna configuración adicional.

**Flujo de registro:**

```csharp
// InfrastructureServiceExtensions.cs
var cacheSettings = configuration
    .GetSection(CacheSettings.SectionName)
    .Get<CacheSettings>() ?? new CacheSettings();

services.AddCacheServices(cacheSettings);
```

**Árbol de decisión en `CacheExtensions.AddCacheServices`:**

```
Cache:Enabled = false?
    └── Sí → registrar NullCacheService

Cache:ConnectionString vacío?
    └── Sí → registrar NullCacheService

ConnectionMultiplexer.Connect() lanza excepción?
    └── Sí → registrar NullCacheService

Todo OK
    └── registrar NullCacheService (Por el momento que no se tienen implementaciones)
```

**Lifetime:** `ICacheService` se registra como **Singleton** en todos los casos.

**`IOptions<CacheSettings>`** se registra en `SettingsExtensions.AddApiSettings` con validación en startup:

```csharp
services.AddOptions<CacheSettings>()
    .Bind(configuration.GetSection(CacheSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

---

## Pruebas

### Estructura del proyecto de pruebas

```
tests/ServiceTemplate.Tests/
├── Cache/
│   ├── CacheKeyBuilderTests.cs           ← Pruebas unitarias del builder de llaves
│   ├── NullCacheServiceTests.cs          ← Pruebas unitarias del no-op
└── Api/
    ├── Doubles/
    │   └── SpyCacheService.cs            ← Implementación espía para tests de filtros
    ├── TestWebApplicationFactory.cs      ← Factory con SpyCacheService inyectado
    ├── HttpCacheAttributeTests.cs        ← Pruebas del filtro de lectura
    └── InvalidateCacheAttributeTests.cs  ← Pruebas del filtro de invalidación
```

### Ejecutar pruebas

```bash
# Todas las pruebas
dotnet test
```

### SpyCacheService

`SpyCacheService` es una implementación en memoria de `ICacheService` que registra cada llamada y actúa como caché funcional durante los tests.

```csharp
// Consultas registradas
List<string> GetCalls
List<(string Key, TimeSpan Ttl)> SetCalls
List<string> RemoveCalls
List<string> RemoveByPrefixCalls

// Sembrar datos de prueba (simula un hit)
void Seed<T>(string key, T value)

// Verificar si una llave existe en el store
bool Contains(string key)
```

**Ejemplo de prueba de hit de caché:**

```csharp
[Fact]
public async Task GetAll_CacheHit_ReturnsCachedValueWithoutExecutingHandler()
{
    await using var factory = new TestWebApplicationFactory();
    var client = factory.CreateClient();

    // Sembrar datos conocidos en el caché antes del request
    var cacheKey = CacheKeyBuilder.Build("api:v1", "api/v1/weather-forecasts", null, null, null);
    factory.CacheService.Seed(cacheKey, new[] { new { summary = "Seeded forecast" } });

    var response = await client.GetAsync("/api/v1/weather-forecasts");
    var body = await response.Content.ReadAsStringAsync();

    body.Should().Contain("Seeded forecast");
    factory.CacheService.SetCalls.Should().BeEmpty("el handler no debe haberse ejecutado");
}
```

---

## Agregar una nueva implementación de caché

Para agregar un backend (Redis, Memcached, in-memory distribuido, etc.):

### 1. Crear la implementación

```csharp
// src/Infrastructure/Cache/MemcachedCacheService.cs
using Shared.Application.Interfaces;

namespace Infrastructure.Cache;

public sealed class MemcachedCacheService : ICacheService
{
    // Inyectar el cliente de Memcached via constructor
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default) { ... }
    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default) { ... }
    public Task RemoveAsync(string key, CancellationToken ct = default) { ... }
    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default) { ... }
}
```

### 2. Agregar configuración específica (si aplica)

Extender `CacheSettings` o crear una clase separada para las opciones del nuevo backend.

### 3. Registrar en `CacheExtensions`

```csharp
public static IServiceCollection AddCacheServices(this IServiceCollection services, CacheSettings settings)
{
    // ... validaciones existentes ...

    // Seleccionar implementación según configuración
    if (settings.Provider == "Memcached")
    {
        services.AddSingleton<ICacheService, MemcachedCacheService>();
    }
    else
    {
        // Redis (por defecto)
        var multiplexer = ConnectionMultiplexer.Connect(config);
        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        services.AddSingleton<ICacheService, RedisCacheService>();
    }

    return services;
}
```

### 4. Agregar pruebas de integración

Crear una fixture ej. `RedisFixture` con Testcontainers o un servidor embebido, y probar los mismos escenarios: hit, miss, TTL, invalidación por prefijo y degradación graceful.

---

## Limitaciones y consideraciones

### Solo cachea `OkObjectResult`

`HttpCacheAttribute` únicamente almacena respuestas que sean `OkObjectResult` (HTTP 200) con valor no nulo. Respuestas con otros códigos de éxito (ej. 204 No Content) no son cacheadas. Si se necesita cachear un 200 devuelto por `ActionResult<T>` directamente, el filtro funciona igual ya que ASP.NET Core convierte `ActionResult<T>` a `OkObjectResult`.

### El caché de la respuesta es el objeto de negocio, no los bytes HTTP

El caché almacena el **valor del objeto de negocio** (ej. `List<WeatherForecastDto>`) serializado como JSON, no los headers HTTP ni el status code. Esto significa:

- Los headers de respuesta (ej. `ETag`, `Cache-Control`) no se restauran en un hit de caché.
- El contenido JSON puede diferir levemente si las opciones de serialización de ASP.NET Core difieren de las implementadas en el Backend de caché.

### TTLs cortos por diseño

Los TTLs deben ser cortos (segundos a pocos minutos) para recursos que cambian frecuentemente. Para datos muy estables (catálogos, configuración), TTLs de horas son aceptables siempre que la invalidación explícita esté implementada.

### Endpoints con datos sensibles

No cachear endpoints que devuelvan datos sensibles (tokens, información financiera confidencial) a menos que el aislamiento por tenant esté estrictamente garantizado. Verificar que `VaryByTenant = true` (valor por defecto) esté activo para todos esos endpoints.
