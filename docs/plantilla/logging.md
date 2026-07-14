# Logging

**Ver también:** [sentry.md](sentry.md) — error tracking y performance monitoring.


---

## Tabla de contenido


 1. [Visión general](#1-visi%C3%B3n-general)
 2. [Cómo usar el logger](#2-c%C3%B3mo-usar-el-logger)
 3. [Formato de salida](#3-formato-de-salida)
 4. [Campos del log](#4-campos-del-log)
 5. [Bloque `http`](#5-bloque-http)
 6. [Bloque `properties`](#6-bloque-properties)
 7. [Pipeline de Serilog](#7-pipeline-de-serilog)
 8. [Niveles y filtros](#8-niveles-y-filtros)
 9. [Terminología](#9-terminolog%C3%ADa)
10. [Archivos clave](#10-archivos-clave)


---

## 1. Visión general

El sistema de logging está construido sobre **Serilog** como implementación concreta, expuesto a las capas internas únicamente a través del puerto `ILoggerPort<T>`. Ninguna capa de dominio o aplicación depende de Serilog directamente.

```
Domain / Application  →  ILoggerPort<T>  ←  SerilogLoggerAdapter<T>  →  Serilog pipeline
```

Serilog se usa por dos razones principales:

* **Logging estructurado nativo**: cada propiedad del mensaje (`{PageIndex}`, `{StatusCode}`) se almacena como campo independiente, no como texto plano. Esto permite filtrar y agregar en herramientas como Seq, Datadog o ELK sin parseo.
* **Pipeline extensible**: el sistema de sinks y enrichers permite enrutar logs a múltiples destinos y enriquecerlos automáticamente con contexto (traceId, ambiente, versión) sin tocar el código de negocio.


---

## 2. Cómo usar el logger

### Puerto — `ILoggerPort<T>`

Definido en `src/Shared/Application/Ports/ILoggerPort.cs`. Es la única abstracción de logging que deben conocer Application y Domain:

```csharp
public interface ILoggerPort<out T>
{
    void Debug(string message, params object[] args);
    void Info(string message, params object[] args);
    void Warning(string message, params object[] args);
    void Warning(Exception? exception, string message, params object[] args);
    void Error(Exception? exception, string message, params object[] args);
}
```

### Adaptador — `SerilogLoggerAdapter<T>`

`src/Infrastructure/Adapters/Logging/SerilogLoggerAdapter.cs` implementa `ILoggerPort<T>` usando `ILogger` de Serilog con `ForContext<T>()`, de modo que cada log incluye el tipo que lo emitió como `sourceContext`.

### Registro en DI

```csharp
// src/Api/DependencyInjection/SharedServiceExtensions.cs
services.AddSingleton(typeof(ILoggerPort<>), typeof(SerilogLoggerAdapter<>));
```

Una sola línea registra el adaptador para todos los tipos (open generic registration).

### Uso en use cases e infraestructura

Inyectar por constructor y usar los métodos del puerto:

```csharp
public sealed class GetAllProductsUseCase(
    IProductRepository repository,
    ILoggerPort<GetAllProductsUseCase> logger)
{
    public async Task<...> ExecuteAsync(...)
    {
        logger.Info("Retrieving products (page {PageIndex}, size {PageSize})", page.PageIndex, page.PageSize);

        // ...

        logger.Error(exception, "Failed to retrieve products");
    }
}
```


---

## 3. Formato de salida

| Ambiente | Formato | Destino |
|----------|---------|---------|
| Development | Texto plano coloreado | Consola |
| Staging / Production | JSON plano, camelCase, un objeto por línea | Consola (para ingesta por colectores) |

Ejemplo de salida JSON en producción (`FlatJsonFormatter`):

```json
{
  "message": "Retrieving products (page 1, size 20)",
  "timestamp": "2026-05-21T10:30:00.000Z",
  "level": "information",
  "sourceContext": "GetAllProductsUseCase",
  "service": "ProductService",
  "environment": "production",
  "version": "1.0.0",
  "traceId": "819875943ff06821d25dcc54c02144cc",
  "spanId": "402b952b1e6a896d"
}
```


---

## 4. Campos del log

### Siempre presentes

| Campo | Origen | Descripción |
|-------|--------|-------------|
| `message` | Serilog | Mensaje renderizado |
| `timestamp` | Serilog | Fecha y hora UTC en ISO 8601 |
| `level` | Serilog | Nivel: `debug`, `information`, `warning`, `error` |
| `sourceContext` | Serilog | Clase que emitió el log |
| `service` | `AppInfo.ServiceName` | Nombre del servicio |
| `environment` | `ASPNETCORE_ENVIRONMENT` | Entorno en minúsculas |
| `version` | `AppInfo.Version` | Versión del servicio |

### Presentes durante un request HTTP

| Campo | Origen | Descripción |
|-------|--------|-------------|
| `traceId` | `ActivityEnricher` (W3C) | ID de traza distribuida — atraviesa microservicios |
| `spanId` | `ActivityEnricher` (W3C) | ID del span actual |
| `requestId` | ASP.NET Core | ID único del request HTTP |
| `requestPath` | ASP.NET Core | Ruta del request |
| `connectionId` | ASP.NET Core | ID de la conexión TCP |
| `http` | `RequestLoggingMiddleware` | Contexto HTTP — siempre presente, automático |

### Presentes dentro de una acción MVC

| Campo | Origen | Descripción |
|-------|--------|-------------|
| `actionId` | ASP.NET Core MVC | ID único de la acción del controller |
| `actionName` | ASP.NET Core MVC | Nombre completo de la acción del controller |

### Opcionales

| Campo | Origen | Descripción |
|-------|--------|-------------|
| `properties` | `PushLogProperties()` | Contexto de negocio — solo si el dev lo usa explícitamente |
| `exception` | Serilog | Stack trace completo, solo en logs de nivel `error` |


---

## 5. Bloque `http`

Inyectado automáticamente por `RequestLoggingMiddleware` en todos los logs del pipeline. No requiere ninguna acción del desarrollador.

### Funcionamiento en dos fases

El middleware opera en dos fases para que **todos los logs del request hereden el contexto HTTP**:


1. **Fase temprana**: enriquece el `LogContext` con los datos del request (método, ruta, IP, user-agent) antes de invocar el siguiente middleware. Cualquier log emitido desde un use case o repositorio ya incluye estos campos.
2. **Fase final** (`finally`): agrega `statusCode` y `latencyMs` y emite el evento de cierre `http.request.completed`.

### Durante el request — campos disponibles

```json
"http": {
  "userAgent": "Mozilla/5.0 ...",
  "remoteAddress": "::1",
  "method": "GET",
  "route": "/api/v1/products"
}
```

### Al final del request — evento `http.request.completed`

```json
{
  "message": "http.request.completed",
  "http": {
    "userAgent": "Mozilla/5.0 ...",
    "remoteAddress": "::1",
    "method": "GET",
    "route": "/api/v1/products",
    "statusCode": 200,
    "latencyMs": 351
  },
  "traceId": "819875943ff06821d25dcc54c02144cc",
  "spanId": "402b952b1e6a896d",
  "service": "ServiceTemplate",
  "environment": "staging",
  "version": "1.0.0"
}
```

> Este evento reemplaza al `Request finished` nativo de ASP.NET Core, que está silenciado.

### Extender `http` con campos adicionales


1. Agregar la propiedad al record `HttpRequestLogProperties` en `src/Infrastructure/Logging/HttpRequestLogProperties.cs`.
2. Actualizar el método `BuildRequestLogProperties` en `src/Api/Middleware/RequestLoggingMiddleware.cs`.


---

## 6. Bloque `properties`

Permite adjuntar contexto de negocio arbitrario a los logs de un scope. Es **opcional** — solo aparece cuando el desarrollador lo invoca explícitamente con `PushLogProperties`.

### Comportamiento

* Aparece en **todos los logs** generados dentro del bloque `using` (use cases, validadores, repositorios, etc.).
* Aparece también en el evento `http.request.completed` del mismo request.
* Se descarta automáticamente al salir del bloque `using`.

### Uso desde un controller

```csharp
using Api.Middleware;

public async Task<IActionResult> Create([FromBody] CreateProductInputDto input, ...)
{
    using (HttpContext.PushLogProperties(new Dictionary<string, object?>
    {
        ["userId"]      = currentUser.Id,
        ["tenantId"]    = currentUser.TenantId,
        ["productName"] = input.Name
    }))
    {
        var result = await useCase.ExecuteAsync(input, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error.Description });

        return Created(string.Empty, result.Value);
    }
}
```

### Log resultante (dentro del scope)

```json
{
  "message": "Product created",
  "properties": {
    "userId": "usr-123",
    "tenantId": "tenant-456",
    "productName": "Keyboard"
  },
  "http": {
    "method": "POST",
    "route": "/api/v1/products"
  }
}
```

### Tipos de valores soportados

`string`, `int`, `long`, `bool`, `decimal`, `Guid`, `DateTime` y `null`.

```csharp
new Dictionary<string, object?>
{
    ["productId"]       = Guid.NewGuid(),
    ["price"]           = 99.99m,
    ["isFeatured"]      = true,
    ["discontinuedAt"]  = (DateTime?)null   // los valores null se omiten del JSON
}
```

### Helpers disponibles en `LogContextExtensions`

`src/Infrastructure/Logging/LogContextExtensions.cs` expone métodos adicionales para el enriquecimiento interno del pipeline:

| Método | Uso |
|--------|-----|
| `PushLogProperties(dictionary)` | Contexto de negocio desde un controller |
| `PushHttpProperties(httpProperties)` | Datos HTTP estructurados (uso interno del middleware) |
| `PushFlatProperties(iLogProperties)` | Implementaciones de `ILogProperties` (uso interno) |


---

## 7. Pipeline de Serilog

### Inicialización

```csharp
// src/Api/Program.cs
builder.Host.AddSerilog(builder.Configuration);
```

`AddSerilog` es una extensión en `src/Infrastructure/Extensions/SerilogExtensions.cs`. Allí se configura el pipeline completo: niveles, enrichers y sinks.

### Enrichers automáticos

Cada log event recibe estas propiedades sin intervención del código de negocio:

| Enricher | Propiedades que agrega | Fuente |
|----------|------------------------|--------|
| Configuración | `service`, `environment`, `version` | `appsettings.json` |
| `ActivityEnricher` | `traceId`, `spanId`    | `Activity.Current` (OpenTelemetry / W3C) |
| `LogContext` | Cualquier propiedad empujada con `PushLogProperties` | `LogContext.PushProperty` |

`ActivityEnricher` (`src/Infrastructure/Logging/ActivityEnricher.cs`) implementa `ILogEventEnricher` y lee `Activity.Current` del sistema de diagnóstico de .NET, compatible con OpenTelemetry. Permite correlacionar logs de un mismo request a través de múltiples servicios.

### Sinks

| Sink | Ambiente | Formato |
|------|----------|---------|
| Console | Development | Texto plano coloreado |
| Console | Staging / Production | `FlatJsonFormatter` — JSON plano para ingesta por colectores |
| Sentry | Ambos (si habilitado) | Errores → issues; Warnings → breadcrumbs |

`FlatJsonFormatter` (`src/Infrastructure/Logging/FlatJsonFormatter.cs`) implementa `ITextFormatter` y genera JSON plano sin anidamiento, con propiedades en camelCase.


---

## 8. Niveles y filtros

Usar `ILoggerPort<T>` inyectado por DI:

```csharp
logger.Debug("Validating input: {Input}", input);
logger.Info("Product created with id {Id}", product.Id);
logger.Warning("Product {Id} not found", id);
logger.Error(exception, "Failed to process order {Id}", id);
```

### Filtros por entorno y namespace

Configurados en `appsettings.json` y `appsettings.Development.json` bajo `Serilog.MinimumLevel.Override`:

| Namespace | Development | Staging / Production |
|-----------|-------------|----------------------|
| Servicio (default) | `Debug+`    | `Information+`       |
| `Microsoft.*` | `Warning+`  | `Warning+`           |
| `Microsoft.Hosting.Lifetime` | `Information+` | `Information+`       |
| `System.*` | `Warning+`  | `Warning+`           |

Los namespaces `Microsoft.*` y `System.*` se elevan a `Warning` en producción porque generan un gran volumen de logs de nivel `Information` sobre el funcionamiento interno del framework (negociación de contenido, ciclo de vida de conexiones) que en la mayoría de los casos no aportan valor operacional.


---

## 9. Terminología

**Sink** Destino al que Serilog envía los logs. Un pipeline puede tener varios sinks activos simultáneamente. Agregar un nuevo destino (un archivo, Seq, Datadog) no requiere cambiar el código de negocio, solo registrar el sink en `SerilogExtensions`.

**Enricher** Componente que agrega propiedades adicionales a todos los logs de forma automática, sin que el código que emite el log tenga que incluirlas. `ActivityEnricher` es el ejemplo más claro: agrega `traceId` y `spanId` a cada evento sin que ningún use case los mencione.

**LogContext** Mecanismo de Serilog que adjunta propiedades al hilo de ejecución actual usando un patrón de pila. Cualquier log emitido mientras una propiedad está en el contexto la hereda automáticamente. Es lo que usa `RequestLoggingMiddleware` para que todos los logs del ciclo de vida de un request incluyan método, ruta e IP.

**Logging estructurado** Enfoque en el que cada log no es una cadena de texto plano sino un objeto con campos tipados. En lugar de `"Página 1 de 20"` se emite `{ pageIndex: 1, pageSize: 20 }`. Esto permite filtrar, agrupar y consultar en herramientas como Datadog o ELK sin parseo de texto.

**Nivel de log** Clasificación de la importancia de un evento. De menor a mayor: `Debug` → `Information` → `Warning` → `Error` → `Fatal`. Configurar un nivel mínimo descarta todos los eventos de niveles inferiores.

**Traza distribuida** Mecanismo para correlacionar los logs de un mismo request a través de múltiples servicios. Cada request recibe un `traceId` único; si el servicio A llama al servicio B, ambos comparten el mismo `traceId`, lo que permite reconstruir el flujo completo en una herramienta de observabilidad.


---

## 10. Archivos clave

| Archivo | Responsabilidad |
|---------|-----------------|
| `src/Shared/Application/Ports/ILoggerPort.cs` | Puerto de logging — abstracción visible desde Application |
| `src/Infrastructure/Adapters/Logging/SerilogLoggerAdapter.cs` | Implementación Serilog del puerto |
| `src/Infrastructure/Extensions/SerilogExtensions.cs` | Pipeline de Serilog: enrichers, sinks y niveles |
| `src/Infrastructure/Logging/ActivityEnricher.cs` | Enricher para `traceId` / `spanId` (OpenTelemetry) |
| `src/Infrastructure/Logging/FlatJsonFormatter.cs` | Formatter JSON plano para producción |
| `src/Infrastructure/Logging/HttpRequestLogProperties.cs` | Record con los campos del bloque `http` |
| `src/Infrastructure/Logging/LogContextExtensions.cs` | Helpers para enriquecimiento de contexto |
| `src/Api/Middleware/RequestLoggingMiddleware.cs` | Logging automático de requests HTTP (dos fases) |
| `src/Api/DependencyInjection/SharedServiceExtensions.cs` | Registro de `ILoggerPort<>` en DI |
| `src/Api/Program.cs` | Punto de arranque: `builder.Host.AddSerilog(...)` |
