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
    * [Cómo se arma la clave de caché](#c%C3%B3mo-se-arma-la-clave-de-cach%C3%A9)
    * [Qué se cachea y qué no](#qu%C3%A9-se-cachea-y-qu%C3%A9-no)
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

* **Opt-in por endpoint** con `[OutputCache]` en el método del controlador: sin el atributo no se cachea (ver [Qué se cachea y qué no](#qu%C3%A9-se-cachea-y-qu%C3%A9-no)). La política base solo aporta las reglas de variación, que los endpoints anotados heredan.
* **Aislamiento por tenant y locale** vía `SetVaryByHeader("X-Entity-Code", "Accept-Language")` en la política base.
* **Invalidación por etiquetas (tags)**: los `GET` se etiquetan con `Tags = [...]` y las mutaciones usan el filtro `[OutputCacheInvalidate("tag")]` que llama a `IOutputCacheStore.EvictByTagAsync` tras una respuesta exitosa para invalidar esas llaves de caché.


---

### Arquitectura y ubicación en capas

```
src/
├── Api/
│   ├── Program.cs                               ← .ConfigureCache(..) + .UseCacheMiddleware()
│   ├── DependencyInjection/
│   │   └── OutputCacheExtensions.cs             ← ConfigureCache + UseCacheMiddleware
│   └── Controllers/
│       └── *Controller.cs                       ← [OutputCache] y [OutputCacheInvalidate]
│
├── Shared/
│   └── Infrastructure/
│       └── Presentation/
│           └── Filters/
│               └── OutputCacheInvalidateAttribute.cs  ← IAsyncActionFilter que llama EvictByTagAsync
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

Cuando `Enabled = false`, no se registra el servicio y los atributos `[OutputCache]` se ignoran silenciosamente. `DefaultExpirationTimeSpan` se aplica cuando un endpoint declara `[OutputCache]` sin la propiedad `Duration`.

#### UseCacheMiddleware (pipeline)

En `Program.cs` el middleware se habilita con:

```csharp
app.UseCacheMiddleware();
```

Así el toggle `Cache:Enabled` controla **tanto el registro del servicio como el middleware** desde un único punto.

**Orden en el pipeline.** Va después de `UseCors`, como exige la documentación de ASP.NET Core. Si el servicio incorpora autenticación, `UseCacheMiddleware()` debe quedar **después** de `UseAuthentication` y `UseAuthorization`: en caso contrario el middleware puede servir contenido cacheado para usuarios no autorizados a usuarios que sí lo están.


---

#### `[OutputCache]`

Atributo estándar de ASP.NET Core. Se coloca sobre el método del controlador.

```csharp
[HttpGet]
[OutputCache(Duration = 60, Tags = ["products"])]
public Task<IActionResult> GetAll(...) { ... }
```

| Propiedad | Tipo | Descripción |
|-----------|------|-------------|
| `Duration` | `int` | **TTL en segundos.** El framework lo convierte con `TimeSpan.FromSeconds(Duration)`: `60` ⇒ 1 minuto, `3600` ⇒ 1 hora, `86400` ⇒ 1 día. Si no se declara, aplica `Cache:DefaultTtlSeconds`. |
| `Tags`    | `string[]` | Etiquetas para invalidación con `EvictByTagAsync`. |
| `VaryByHeaderNames` | `string[]` | Añade headers a la clave (se suma a los de la política base). |
| `VaryByQueryKeys` | `string[]` | **Restringe** la clave a esas claves de query string. Sin declararlo, ya varían **todas** (ver abajo). |
| `VaryByRouteValueNames` | `string[]` | Varía por valores de ruta. **Normalmente innecesario:** la ruta ya forma parte de la clave (ver abajo). |
| `PolicyName` | `string` | Selecciona una política nombrada en lugar de la base. |
| `NoStore` | `bool` | Desactiva el caché para esta acción. |

La política base se construye sobre `DefaultPolicy`, que solo permite cachear si se cumplen **todas** estas condiciones:

* Método `GET` o `HEAD`.
* Respuesta con status 200.
* Sin header `Authorization` y sin usuario autenticado (`HttpContext.User.Identity.IsAuthenticated`).
* Respuesta sin `Set-Cookie`.


---

#### Cómo se arma la clave de caché

Desconocer esta composición lleva a declarar `VaryByRouteValueNames` de forma preventiva, cuando la ruta ya forma parte de la clave.

`OutputCacheKeyProvider` compone la clave en dos bloques:

**1. Clave base — siempre presente, no es configurable por atributo:**

```
{MÉTODO}·{ESQUEMA}·{HOST}·{PATHBASE}{PATH}
```

El `PATH` es la **URL ya resuelta**, es decir con los segmentos de ruta sustituidos: `GET /{RoutePrefix}/products/7d3f…` y `GET /{RoutePrefix}/products/91ab…` producen dos claves distintas **sin declarar nada**. El prefijo de servicio forma parte del path, porque se antepone a las rutas y no vía `UsePathBase`. Salvo que se active `UseCaseSensitivePaths`, el path se normaliza a mayúsculas, así que `/products` y `/Products` comparten entrada.

**2. Bloque de variación — lo que aportan las políticas y el atributo:**

| Marca | Origen | En esta plantilla |
|-------|--------|-------------------|
| `H` | Headers | `X-Entity-Code` y `Accept-Language` (política base) más los de `VaryByHeaderNames` |
| `Q` | Query string | **Todas** las claves, salvo que se declare `VaryByQueryKeys` |
| `R` | Valores de ruta | Solo los declarados en `VaryByRouteValueNames` |
| `V` | Valores personalizados | Solo con políticas custom (`VaryByValue`) |

**Por qué la query varía por defecto:** `DefaultPolicy` fija `QueryKeys = "*"`. La política base lo restringe a `EntityCode`, pero el atributo `[OutputCache]` aplica `DefaultPolicy` de nuevo después de ella y restaura `"*"`. Resultado: en todo endpoint anotado varían todas las claves de query string.

> **Cuidado con `VaryByQueryKeys`:** declararlo *restringe* la clave a esas claves. `[OutputCache(VaryByQueryKeys = ["pageIndex"])]` en un listado filtrado hace que dos filtros distintos compartan entrada de caché. Se declara solo cuando se quiere ignorar deliberadamente el resto de la query (p. ej. parámetros de tracking).
>
> También desplaza el `EntityCode` de la política base: al declararlo, esa clave deja de participar en la variación salvo que se incluya en la lista. El aislamiento por tenant sigue cubierto por el header `X-Entity-Code`, pero si un cliente identifica el tenant **solo** por query string, hay que declarar `EntityCode` junto a las demás.

**Cuándo aporta `VaryByRouteValueNames`:** solo cuando el valor de ruta no se refleja en el path — valores por defecto de la plantilla de ruta o inyectados por el routing. En los controllers de esta plantilla todo parámetro de ruta viene de un segmento de la URL, así que declararlo no altera la clave.


---

#### Qué se cachea y qué no

La caché es **opt-in**: solo se almacena la respuesta de un endpoint que declare `[OutputCache]`. Eso lo consigue el `excludeDefaultPolicy: true` de `ConfigureCache`, que deja a la política base aportando únicamente las reglas de variación:

```csharp
options.AddBasePolicy(policy => policy
    .SetVaryByHeader("X-Entity-Code", "Accept-Language")
    .SetVaryByQuery("EntityCode"),
    excludeDefaultPolicy: true);
```

Sin esa bandera, `DefaultPolicy` fijaría `EnableOutputCaching = true` para toda petición que llegue al middleware, y se cachearían endpoints que nunca lo pidieron. Comportamiento verificado con `Cache:Enabled = true`:

| Endpoint | Anotación | Resultado |
|----------|-----------|-----------|
| cualquier `GET` de controlador | ninguna | **No se cachea** |
| `GET /health/live`, `/health/ready` | ninguna (minimal API) | **No se cachean** |
| `GET /products/{id}` | `[OutputCache(Duration = 60)]` | Se cachea 60 s; cada `id` es una entrada, sin declarar `VaryByRouteValueNames` |
| `GET /list?filter=x` | `[OutputCache(Duration = 60)]` | Se cachea 60 s; cada valor de `filter` es una entrada, sin declarar `VaryByQueryKeys` |
| cualquiera, con header `Authorization` | cualquiera | No se cachea: ni lectura ni escritura de la entrada |

Un endpoint anotado sigue heredando las reglas de variación de la política base: su clave varía por `X-Entity-Code` y `Accept-Language` aunque no declare `VaryByHeaderNames`.

> **Por qué importa la bandera.** Output Caching es caché de servidor y no interpreta las cabeceras HTTP de caché, así que el `Cache-Control: no-store` que emiten los health checks no lo protege. Con la política base habilitando la caché, `/health/ready` seguía respondiendo `200 Healthy` durante `DefaultTtlSeconds` después de que el servicio dejara de estarlo — y el fallo era asimétrico, porque un `503` nunca se almacena y solo se cachean respuestas 200. Lo mismo aplicaba a `/info`, que reportaba `status: ok` desde una entrada guardada.

> **Toda lectura cacheada necesita tags.** Una entrada guardada sin `Tags` no se puede invalidar: `EvictByTagAsync` solo borra por tag, así que quedaría fuera del alcance de `[OutputCacheInvalidate]` hasta que expire su TTL. Al declarar `[OutputCache]`, declarar también `Tags = [...]`.

> **Respuestas autenticadas.** Si las peticiones llegan con header `Authorization`, el L1 no almacena nada, aunque el endpoint declare `[OutputCache]`: el atributo reintroduce `DefaultPolicy`, que lo impide. La comprobación es sobre el header crudo, así que aplica aunque el pipeline no tenga `UseAuthentication`. Cachear respuestas autenticadas exigiría una política propia que no pase por `DefaultPolicy`, con la clave variando por identidad, o se sirven datos de un usuario a otro.


---

#### `[OutputCacheInvalidate]`

**Ruta:** `src/Shared/Infrastructure/Presentation/Filters/OutputCacheInvalidateAttribute.cs` (namespace `Shared.Presentation.Filters`)

El framework no trae un atributo de invalidación; solo expone la API `IOutputCacheStore.EvictByTagAsync(tag, ct)`. Este filtro la envuelve.

**Solo invalida si:**

* el handler no lanzó excepción, y
* el status code final es `< 400`.

`AllowMultiple = true` permite invalidar varios tags desde un solo endpoint (ver [múltiples recursos](#m%C3%BAltiples-recursos)).


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

La columna es el valor que toma la propiedad **si la clave no está en la configuración**, no el que trae la plantilla: su `appsettings.json` distribuye `Enabled` en `true`.

| Propiedad | Tipo | Sin la clave | Descripción |
|-----------|------|-------------------|-------------|
| `Enabled` | `bool` | `false`           | Activa o desactiva OutputCaching (L1). Si es `false`, el middleware no se registra. |
| `L2Enabled` | `bool` | `false`         | Activa la caché L2 de aplicación (cache-aside). Requiere `ConnectionString`. |
| `DefaultTtlSeconds` | `int` | `300`             | TTL global del L1 cuando el endpoint no especifica `Duration`. Mínimo: 1 (validado). |
| `ConnectionString` | `string` | `""`              | Cadena de conexión a Redis (StackExchange.Redis). Compartida por L1 y L2. Vacío = store en memoria para L1, NoOp para L2. |

#### appsettings

Valores que trae `appsettings.json` en la plantilla:

```json
{
    "Cache": {
        "Enabled": true,
        "L2Enabled": false,
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

El caso de uso se inyecta por el constructor del controller y el tag se declara una vez como constante, para que la lectura y su invalidación no puedan desalinearse:

```csharp
private const string CacheTag = "products";

[HttpGet]
[OutputCache(Duration = 60, Tags = [CacheTag])]
public async Task<HttpOkPagedResult<GetProductsOutputDto>> GetProducts(
    [FromQuery] GetProductsInputDto filter,
    [FromQuery] PageQueryInputDto pagination,
    CancellationToken cancellationToken = default)
{
    return await getProductsUseCase.ExecuteAsync(
        filter,
        new PageQuery(pagination.PageIndex, pagination.PageSize),
        cancellationToken).ConfigureAwait(false);
}
```

> La clave incluye el path, todas las claves de query (`filter`, `pageIndex`, `pageSize`…) y los headers de la política base, así que cada combinación de filtro y página es una entrada distinta. Conviene dimensionarlo: un filtro de alta cardinalidad genera muchas entradas con baja tasa de acierto, y ahí suele convenir `[OutputCache(NoStore = true)]` o cachear en L2 la consulta subyacente.

#### Por ruta (resource por id)

No hace falta `VaryByRouteValueNames`: el `{id}` ya forma parte del path y, por tanto, de la clave base.

```csharp
[HttpGet("{id}")]
[OutputCache(Duration = 120, Tags = ["products"])]
public Task<IActionResult> GetById(Guid id, ...) { ... }
```

#### Datos globales (compartidos entre tenants)

`ConfigureCache` registra una política nombrada `Global`, que un endpoint selecciona con `PolicyName`:

```csharp
options.AddPolicy("Global", p => { });
```

> **Tal como está registrada, `Global` no elimina la variación por tenant ni por locale.** Las políticas base se aplican antes que la del endpoint: cuando corre la nombrada, `SetVaryByHeader` ya dejó `X-Entity-Code` y `Accept-Language` en la clave, y una política vacía no los quita. Comprobado: un endpoint con `PolicyName = "Global"` sigue generando una entrada por tenant, igual que uno sin `PolicyName`.

Para ignorar esos headers, la política nombrada debe reasignarlos con un array vacío — `VaryByHeaderPolicy` lo interpreta como "no variar por headers" y sobrescribe lo que fijó la política base:

```csharp
options.AddPolicy("Global", p => p.SetVaryByHeader([]));
```

Con esa corrección, el endpoint la selecciona así:

```csharp
[HttpGet("config")]
[OutputCache(PolicyName = "Global", Duration = 3600, Tags = ["config"])]
public Task<IActionResult> GetConfig(...) { ... }
```

Solo para datos idénticos en todos los tenants. Aplicarla a datos dependientes del tenant expone los de uno a los demás.


---

### Cómo invalidar el caché

El argumento de `[OutputCacheInvalidate("...")]` debe coincidir con uno de los `Tags` declarados en el `[OutputCache]` correspondiente.

#### Caso básico

```csharp
[HttpPost]
[OutputCacheInvalidate("products")]
public async Task<IActionResult> Create(
    [FromBody] CreateProductInputDto input,
    ICreateProductUseCase useCase,
    CancellationToken cancellationToken)
{
    var result = await useCase.ExecuteAsync(input, cancellationToken);

    if (result.IsFailure)
        return BadRequest(new { error = result.Error.Description });

    return Created(string.Empty, result.Value);
}

[HttpPut("{id}")]
[OutputCacheInvalidate("products")]
public Task<IActionResult> Update(Guid id, ...) { ... }

[HttpDelete("{id}")]
[OutputCacheInvalidate("products")]
public Task<IActionResult> Delete(Guid id, ...) { ... }
```

#### Múltiples recursos

```csharp
[HttpPost("{productId}/categories")]
[OutputCacheInvalidate("products")]
[OutputCacheInvalidate("categories")]
public Task<IActionResult> LinkCategory(Guid productId, ...) { ... }
```


---

### Variación por tenant y locale

La política base varía la clave por dos headers:

* `X-Entity-Code` — header identificador de tenant.
* `Accept-Language` — locale del cliente.

Si ambos headers están ausentes, la respuesta se guarda como "sin tenant / sin locale" y se comparte entre todas las peticiones.

Para el tenant enviado por query string, la política base declara `SetVaryByQuery("EntityCode")`. En un endpoint anotado el atributo restaura la variación por toda la query, que ya incluye `EntityCode`, así que el tenant queda en la clave por cualquiera de las dos vías.


---

### Pruebas

* **L1** — `OutputCacheInvalidateAttributeTests` (unit): ejecuta el filtro de invalidación directamente sobre un `ActionExecutingContext` construido a mano, sin levantar el pipeline HTTP. Cubre la invalidación por tag, el caso sin status code explícito (asume 200 e invalida) y la omisión cuando la acción falla.
* **L2** — `CacheKeyTests` fija el formato canónico de las llaves; `DistributedCacheExtensionsTests` verifica la elección Redis vs. NoOp; `NoOpCacheStoreTests`, `RedisCacheStoreTests` y `RedisCacheStoreEdgeCaseTests` cubren los stores y su degradación; `RedisCacheStoreIntegrationTests` corre contra Redis real (`tests/IntegrationTests/Caching/`).

La estructura de los proyectos de test, cómo correrlos y sus prerrequisitos están en [testing.md](testing.md).


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

El `InstanceName` usado como prefijo de las llaves de Redis se deriva de `ServiceInfo:Name` (`"{Name}:"`), lo que aísla de forma natural los keys entre servicios que compartan una misma instancia de Redis.

El paquete ya está declarado en `src/Api/Api.csproj`:

```xml
<PackageReference Include="Microsoft.AspNetCore.OutputCaching.StackExchangeRedis" Version="10.0.*" />
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
        ├── NoOpCacheStore.cs               ← implementación no-op (toda lectura es miss)
        └── DistributedCacheExtensions.cs   ← registro DI (AddDistributedCache)
```

Los repositorios e implementaciones de adaptadores en `Infrastructure` reciben `ICacheStore` por inyección de dependencias. El dominio y los casos de uso en `Application` no conocen la implementación concreta.


---

### Contrato `ICacheStore`

**Ruta:** `src/Shared/Application/Ports/ICacheStore.cs`

```csharp
public interface ICacheStore
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;

    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) where T : class;

    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);
}
```

El patrón cache-aside lo orquesta explícitamente quien llama: se lee con `GetAsync`; si devuelve `null` (miss), se consulta la fuente real y se pobla con `SetAsync`. Así, la política "solo cachear resultados exitosos" vive en el sitio de llamada (basta con no llamar a `SetAsync` en un miss o error).

| Método | Propósito |
|--------|-----------|
| `GetAsync<T>` | Devuelve el valor cacheado para `key`, o `null` en miss (o cuando el backend no está disponible — degradación transparente). El tipo cacheado debe ser de referencia (`where T : class`). |
| `SetAsync<T>` | Almacena `value` bajo `key` durante `ttl`. Los fallos de backend se registran y se ignoran. |
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

El TTL se pasa en cada llamada a `SetAsync`. No existe un TTL global de L2 — cada sitio de caché elige el valor adecuado al dominio.

```csharp
// 10 minutos
await cache.SetAsync(key, value, TimeSpan.FromMinutes(10), ct);

// 1 hora
await cache.SetAsync(key, value, TimeSpan.FromHours(1), ct);
```

> **Nota:** `Cache:DefaultTtlSeconds` en `CacheSettings` corresponde exclusivamente al L1 (Output Cache). No aplica al L2.


---

### Solo éxitos y degradación transparente

**Solo se cachean éxitos**

La política vive en el sitio de llamada: solo se invoca `SetAsync` cuando la fuente devuelve un valor válido. Un miss o un error (entidad no encontrada, validación fallida, error de BD) simplemente no llama a `SetAsync`, así que los errores de dominio no contaminan el caché.

**Degradación transparente**

Si Redis no está disponible durante una lectura, `GetAsync` registra un `Warning` y devuelve `null` — el llamador lo trata como un miss y consulta la fuente real. Si falla durante una escritura (`SetAsync`) o una invalidación (`RemoveAsync` / `RemoveByPrefixAsync`), también registra el `Warning` y continúa sin lanzar excepción. El servicio opera correctamente sin caché ante cualquier fallo del backend.

**NoOpCacheStore**

Cuando `L2Enabled = false` o `ConnectionString` está vacío, se registra `NoOpCacheStore`, cuyo `GetAsync` siempre devuelve `null` y cuyas escrituras e invalidaciones son no-op. No hay overhead de red ni serialización.


---

### Invalidación post-commit

La invalidación de L2 se realiza **en el caso de uso, después de que `IUnitOfWorkPort.CommitAsync()` haya tenido éxito**. Nunca se llama a `RemoveAsync` / `RemoveByPrefixAsync` desde dentro de un repositorio (métodos `Update`, `RemoveAsync`).

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
> * Muchos agregados tienen constructores privados y no pueden ser deserializados por `System.Text.Json`. Cachearlos directamente provoca que cada cache hit lance una excepción que se traga como `Warning`, degradando silenciosamente a un 0 % de hit rate efectivo.
>
> **Preferencia:** cachear un **snapshot serializable** en lugar del agregado y reconstruir el agregado al leer del caché. Un buen candidato de snapshot es un tipo **plano y sin propiedades de navegación ni proxies de lazy-loading** (una entidad de EF Core con constructor público sin parámetros, o un `record` simple): `System.Text.Json` lo (de)serializa sin configuración adicional. El ejemplo real más abajo cachea directamente un `record` porque los datos son configuración de infraestructura, no un agregado de dominio.


---

### Ejemplo real

`TenantResolverServiceClient.GetByCodeAsync` (en `src/Shared/Infrastructure/MasterAccess/Http/Tenants/TenantResolverServiceClient.cs`) ilustra el patrón completo. Se trata de configuración de infraestructura (no DDD), pero con una decisión de seguridad clave: **lo que se cachea es el payload cifrado** (`TenantInfoResponse`, tal como llega del endpoint), **no** el `TenantInfo` ya descifrado. El connection string se descifra en cada lectura, de modo que Redis nunca almacena credenciales en claro. `TenantInfoResponse` es un record plano y serializable, así que `System.Text.Json` lo (de)serializa sin configuración adicional:

```csharp
public async Task<Result<TenantInfo>> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
{
    // (validación de 'code' omitida por brevedad: no vacío y sin ':')
    var key = CacheKey.For("masteraccess").Resource("tenant", code);

    // Cache hit: se devuelve el payload CIFRADO y se descifra por request (nunca se cachea el claro).
    var cached = await cache.GetAsync<TenantInfoResponse>(key, cancellationToken).ConfigureAwait(false);
    if (cached is not null)
        return Decrypt(cached);

    try
    {
        // El cliente solo appendea el código a BaseAddress: BaseUrl debe incluir ya el segmento de recurso
        // (p. ej. https://resolver/tenants/). Ver la nota sobre BaseUrl más abajo.
        using var response = await httpClient
            .GetAsync(Uri.EscapeDataString(code), cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return new NotFoundError($"Tenant with code '{code}' was not found.") { Context = Context };

        if (!response.IsSuccessStatusCode)
            return new InternalError("The tenant info service returned an error.") { Context = Context };

        var payload = (await response.Content
            .ReadFromJsonAsync<TenantInfoEnvelope>(cancellationToken)
            .ConfigureAwait(false))?.Data;

        if (payload is null || string.IsNullOrWhiteSpace(payload.DbConnectionString))
            return new InternalError("The tenant info service returned an invalid payload.") { Context = Context };

        // (verificación del algoritmo de cifrado omitida por brevedad)

        var tenant = Decrypt(payload);
        if (tenant.IsFailure)
            return tenant;

        // Se cachea el payload CIFRADO
        await cache.SetAsync(key, payload, TimeSpan.FromMinutes(_settings.CacheTtlMinutes), cancellationToken)
            .ConfigureAwait(false);
        return tenant;
    }
    catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
    {
        logger.Error(ex, "Error calling tenant info endpoint for code {Code}", code);
        return new InternalError("A network error occurred while resolving the tenant.") { Context = Context };
    }
}
```

* La llave resultante es `ctx:masteraccess:v1:tenant:{code}`.
* TTL configurable (`TenantResolverService:CacheTtlMinutes`, por defecto 10 minutos), adecuado para datos de tenant que raramente cambian.
* **Se cachea el ciphertext, se descifra por request**: Redis nunca contiene el connection string en claro. El coste del descifrado AES por cache hit es deliberado a cambio de esa garantía de seguridad.
* En miss o fallo de Redis, `GetAsync` devuelve `null` y se consulta el endpoint directamente.
* `SetAsync` solo se llama cuando la resolución es exitosa: un tenant no encontrado (`NotFound`) o un error de red/servicio **no** se cachean.
* **Contrato de `BaseUrl`**: el cliente construye la petición appendeando **solo** el código del tenant a `HttpClient.BaseAddress`. Por tanto `TenantResolverService:BaseUrl` debe incluir ya la ruta del recurso con `/` final (p. ej. `https://resolver.interno/tenants/`). Un `BaseUrl` sin ese segmento resolvería contra el endpoint equivocado silenciosamente. En el configmap base va vacío a propósito: cada overlay lo fija por entorno.


---
