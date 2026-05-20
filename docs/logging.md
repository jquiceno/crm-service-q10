# Logging

## Formato de salida

- **Development**: texto plano coloreado en consola.
- **Staging / Production**: JSON plano con camelCase, un objeto por línea.

---

## Campos del log

### Campos siempre presentes

| Campo | Origen | Descripción |
|---|---|---|
| `message` | Serilog | Mensaje renderizado |
| `timestamp` | Serilog | Fecha y hora UTC en ISO 8601 |
| `level` | Serilog | Nivel: `debug`, `information`, `warning`, `error` |
| `sourceContext` | Serilog | Clase que emitió el log |
| `service` | `AppInfo.ServiceName` | Nombre del servicio |
| `environment` | `ASPNETCORE_ENVIRONMENT` | Entorno en minúsculas |
| `version` | `AppInfo.Version` | Versión del servicio |

### Campos presentes durante un request HTTP

| Campo | Origen | Descripción |
|---|---|---|
| `traceId` | `Activity` (W3C) | ID de traza distribuida — atraviesa microservicios |
| `spanId` | `Activity` (W3C) | ID del span actual |
| `requestId` | ASP.NET Core | ID único del request HTTP |
| `requestPath` | ASP.NET Core | Ruta del request |
| `connectionId` | ASP.NET Core | ID de la conexión TCP |
| `http` | `RequestLoggingMiddleware` | Contexto HTTP — **siempre presente, automático** |

### Campos presentes dentro de una acción MVC

| Campo | Origen | Descripción |
|---|---|---|
| `actionId` | ASP.NET Core MVC | ID único de la acción del controller |
| `actionName` | ASP.NET Core MVC | Nombre completo de la acción del controller |

### Campos opcionales

| Campo | Origen | Descripción |
|---|---|---|
| `properties` | `PushLogProperties()` | Contexto de negocio — **solo si el dev lo usa explícitamente** |
| `exception` | Serilog | Stack trace completo, solo en logs de nivel `error` |

---

## Bloque `http`

Inyectado automáticamente por `RequestLoggingMiddleware` en todos los logs del pipeline. No requiere ninguna acción del desarrollador.

### Durante el request — campos disponibles

```json
"http": {
  "userAgent": "Mozilla/5.0 ...",
  "remoteAddress": "::1",
  "method": "GET",
  "route": "/api/v1/weather-forecasts"
}
```

### Al final del request — evento `http.request.completed`

Al terminar el request se emite un evento con el bloque `http` completo, incluyendo datos de la respuesta:

```json
{
  "message": "http.request.completed",
  "http": {
    "userAgent": "Mozilla/5.0 ...",
    "remoteAddress": "::1",
    "method": "GET",
    "route": "/api/v1/weather-forecasts",
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

## Bloque `properties`

Permite adjuntar contexto de negocio arbitrario a los logs de un scope. Es **opcional** — solo aparece cuando el desarrollador lo invoca explícitamente.

### Comportamiento

- Aparece en **todos los logs** generados dentro del bloque `using` (use cases, validadores, etc.).
- Aparece también en el evento `http.request.completed` del mismo request.
- Se descarta automáticamente al salir del bloque `using`.

### Uso desde un controller

```csharp
using Api.Middleware;

public async Task<IActionResult> Create([FromBody] CreateOrderInputDto input, ...)
{
    using (HttpContext.PushLogProperties(new Dictionary<string, object?>
    {
        ["userId"]   = currentUser.Id,
        ["tenantId"] = currentUser.TenantId,
        ["orderId"]  = input.OrderId
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
  "message": "Order created",
  "properties": {
    "userId": "usr-123",
    "tenantId": "tenant-456",
    "orderId": "ord-789"
  },
  "http": {
    "method": "POST",
    "route": "/api/v1/orders"
  }
}
```

### Tipos de valores soportados

`string`, `int`, `long`, `bool`, `decimal`, `Guid`, `DateTime` y `null`.

```csharp
new Dictionary<string, object?>
{
    ["orderId"]     = Guid.NewGuid(),
    ["amount"]      = 99.99m,
    ["isPriority"]  = true,
    ["cancelledAt"] = (DateTime?)null  // los valores null se omiten del JSON
}
```

---

## Niveles de log

Usar `ILoggerPort<T>` inyectado por DI en use cases e infraestructura:

```csharp
_logger.Info("Order created with id {Id}", order.Id);
_logger.Warning("Order {Id} not found", id);
_logger.Error(exception, "Failed to process order {Id}", id);
_logger.Debug("Validating input: {Input}", input);
```

### Filtros configurados por entorno

Definidos en `appsettings.json` y `appsettings.Development.json` bajo `Serilog.MinimumLevel.Override`.

| Namespace | Development | Staging / Production |
|---|---|---|
| Servicio (default) | `Debug+` | `Information+` |
| `Microsoft.*` | `Warning+` | `Warning+` |
| `Microsoft.Hosting.Lifetime` | `Information+` | `Information+` |
| `System.*` | `Warning+` | `Warning+` |
