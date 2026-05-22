# Sistema de Logging — Plantilla de Servicios .NET

**Relacionado:** [Sentry en Clean Architecture] (error tracking y performance monitoring)

---

## Visión general

El sistema de logging está construido sobre **Serilog** como implementación concreta, expuesto a las capas internas únicamente a través de un puerto `ILoggerPort<T>`. Esto garantiza que ninguna capa de dominio o aplicación dependa de Serilog directamente.

```
Domain / Application  →  ILoggerPort<T>  ←  SerilogLoggerAdapter<T>  →  Serilog pipeline
```

---

## Serilog: por qué y cómo

Serilog reemplaza al `ILogger<T>` de Microsoft en las capas internas por dos razones principales:

- **Logging estructurado nativo**: cada propiedad del mensaje (`{PageIndex}`, `{StatusCode}`) se almacena como campo independiente, no como texto plano. Esto permite filtrar y agregar logs en herramientas como Seq, Datadog o ELK sin parseo.
- **Pipeline extensible**: el sistema de *sinks* y *enrichers* permite enrutar logs a múltiples destinos y enriquecerlos automáticamente con contexto (traceId, ambiente, versión) sin tocar el código de negocio.

### Inicialización

```csharp
// src/Api/Program.cs
builder.Host.AddSerilog(builder.Configuration);
```

`AddSerilog` es una extensión definida en `src/Infrastructure/Extensions/SerilogExtensions.cs`. Allí se configura el pipeline completo: niveles, enrichers y sinks.

### Niveles por ambiente

| Ambiente | Fuente de configuración | Nivel mínimo |
|---|---|---|
| Development | `appsettings.Development.json` | Debug — se registran todos los logs, incluidos los más detallados |
| Production / Staging | `appsettings.json` | Information — se omiten los logs de Debug |

**Excepción en Production / Staging:** los componentes internos de ASP.NET Core y Entity Framework Core (namespaces `Microsoft.*` y `System.*`) producen por defecto una gran cantidad de logs de nivel Information sobre su funcionamiento interno (negociación de contenido, ciclo de vida de conexiones, etc.) que en la mayoría de los casos no aportan valor operacional. Para esos namespaces el nivel mínimo se eleva a Warning, de modo que solo se registran advertencias y errores provenientes del propio framework. El código del servicio sigue usando Information normalmente.

### Enrichers automáticos

Cada log event recibe estas propiedades sin intervención del código de negocio:

| Propiedad | Fuente | Descripción |
|---|---|---|
| `service` | `appsettings.json` | Nombre del servicio |
| `environment` | `appsettings.json` | Ambiente de ejecución |
| `version` | `appsettings.json` | Versión del servicio |
| `traceId` | `ActivityEnricher` | ID de traza distribuida (OpenTelemetry) |
| `spanId` | `ActivityEnricher` | ID del span actual |
| Cualquier propiedad de `LogContext.PushProperty` | `LogContext` | Contexto de request (ver más abajo) |

#### ActivityEnricher

`src/Infrastructure/Logging/ActivityEnricher.cs` implementa `ILogEventEnricher`. Lee `Activity.Current` del sistema de diagnóstico de .NET (compatible con OpenTelemetry) y agrega `traceId` y `spanId` a todos los eventos. Esto permite correlacionar logs de un mismo request a través de múltiples servicios.

### Sinks

| Sink | Ambiente | Formato | Descripción |
|---|---|---|---|
| Console | Development | Texto plano | Lectura humana durante desarrollo |
| Console | Production | FlatJson | JSON plano para ingesta por colectores |
| Sentry | Ambos (si habilitado) | — | Errores → issues; Warnings → breadcrumbs |

La integración con Sentry está documentada en detalle en [sentry-clean-architecture.md](sentry-clean-architecture.md).

#### FlatJsonFormatter

`src/Infrastructure/Logging/FlatJsonFormatter.cs` implementa `ITextFormatter`. Genera JSON plano (sin anidamiento) con las propiedades en camelCase. El esquema de salida es:

```json
{
  "message": "Retrieving weather forecasts (page 1, size 20)",
  "timestamp": "2026-05-21T10:30:00.000Z",
  "level": "Information",
  "service": "WeatherService",
  "environment": "production",
  "traceId": "abc123",
  "spanId": "def456"
}
```

---

## Puerto y adaptador

### Puerto — `ILoggerPort<T>`

`src/Shared/Application/Ports/ILoggerPort.cs` define el contrato de logging visible desde Application:

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

Las capas de Domain y Application solo conocen esta interfaz. No hay referencias a `Serilog` ni a `Microsoft.Extensions.Logging` en esas capas.

### Adaptador — `SerilogLoggerAdapter<T>`

`src/Infrastructure/Adapters/Logging/SerilogLoggerAdapter<T>` implementa `ILoggerPort<T>` usando `ILogger` de Serilog con `ForContext<T>()` para que cada log incluya el tipo que lo emitió.

### Registro en DI

```csharp
// src/Api/DependencyInjection/SharedServiceExtensions.cs
services.AddSingleton(typeof(ILoggerPort<>), typeof(SerilogLoggerAdapter<>));
```

Open generic registration: una sola línea registra el adaptador para todos los tipos.

### Uso en casos de uso

```csharp
// src/Contexts/WeatherForecast/Application/UseCases/GetWeatherForecast/GetWeatherForecastUseCase.cs
public sealed class GetWeatherForecastUseCase(
    IWeatherForecastRepository repository,
    ILoggerPort<GetWeatherForecastUseCase> logger)
{
    public async Task<...> Execute(...)
    {
        logger.Info("Retrieving weather forecasts (page {PageIndex}, size {PageSize})", page, size);
        // ...
        logger.Error(ex, "Error retrieving weather forecasts");
    }
}
```

---

## Logging de requests HTTP

`src/Api/Middleware/RequestLoggingMiddleware.cs` registra cada request HTTP con contexto completo y latencia.

### Registro

```csharp
// src/Api/Program.cs
app.UseMiddleware<RequestLoggingMiddleware>();
```

### Qué registra

Al finalizar cada request se emite un log `http.request.completed` con las siguientes propiedades estructuradas:

| Propiedad | Ejemplo |
|---|---|
| `method` | `GET` |
| `route` | `/api/weather` |
| `remoteAddress` | `192.168.1.1` |
| `userAgent` | `Mozilla/5.0 ...` |
| `statusCode` | `200` |
| `latencyMs` | `42` |

### Enriquecimiento acumulativo

El middleware usa un patrón de dos fases:

1. **Fase temprana**: enriquece el `LogContext` con propiedades parciales del request (método, ruta, IP) antes de invocar el siguiente middleware. Todos los logs emitidos durante el request heredan este contexto.
2. **Fase final** (`finally`): agrega `statusCode` y `latencyMs` y emite el log de cierre.

Esto significa que si un caso de uso emite un log de error, ese log ya contiene la información del request HTTP gracias al enriquecimiento previo.

---

## LogContext: enriquecimiento de contexto

`src/Infrastructure/Logging/LogContextExtensions.cs` expone helpers para enriquecer el contexto de Serilog durante el ciclo de vida del request:

```csharp
context.PushHttpProperties(httpProperties);   // Añade datos HTTP al LogContext
context.PushProperties(dictionary);           // Añade propiedades arbitrarias
context.PushFlatProperties(iLogProperties);   // Añade implementaciones de ILogProperties
```

Las propiedades acumuladas en `context.Items` se incluyen automáticamente en el log final `http.request.completed`, lo que permite que cualquier parte del pipeline agregue contexto relevante sin acoplamiento directo al middleware de logging.

---

## Terminología

**Sink**  
Destino al que Serilog envía los logs. Un pipeline puede tener varios sinks activos simultáneamente. En este proyecto se usan: consola (para desarrollo y producción con distinto formato) y Sentry (para error tracking). Agregar un nuevo destino —por ejemplo un archivo o Seq— no requiere cambiar el código de negocio, solo registrar el sink en `SerilogExtensions`.

**Enricher**  
Componente que agrega propiedades adicionales a todos los logs de forma automática, sin que el código que emite el log tenga que incluirlas. Por ejemplo, `ActivityEnricher` agrega `traceId` y `spanId` a cada evento sin que ningún caso de uso los mencione explícitamente.

**LogContext**  
Mecanismo de Serilog que permite adjuntar propiedades al hilo de ejecución actual usando un patrón de pila. Cualquier log emitido mientras una propiedad está en el contexto la hereda automáticamente. En este proyecto se usa para que el middleware de request HTTP enriquezca todos los logs del ciclo de vida de ese request (método, ruta, IP) antes de que lleguen a los casos de uso.

**Logging estructurado**  
Enfoque en el que cada log no es una cadena de texto plano sino un objeto con campos tipados. En lugar de `"Página 1 de 20"` se emite `{ pageIndex: 1, pageSize: 20 }`. Esto permite filtrar, agrupar y consultar logs en herramientas como Datadog o ELK sin parseo de texto.

**Nivel de log**  
Clasificación de la importancia de un evento. De menor a mayor: `Debug` → `Information` → `Warning` → `Error` → `Fatal`. Configurar un nivel mínimo descarta todos los eventos de niveles inferiores; sirve para reducir el volumen de logs en producción sin perder los relevantes.

**Traza distribuida**  
Mecanismo para correlacionar los logs generados por un mismo request a través de múltiples servicios. Cada request recibe un `traceId` único; todos los logs emitidos durante ese request lo incluyen. Si el servicio A llama al servicio B, ambos comparten el mismo `traceId`, lo que permite reconstruir el flujo completo en una herramienta de observabilidad.

---

## Resumen de archivos clave

| Archivo | Responsabilidad |
|---|---|
| `src/Shared/Application/Ports/ILoggerPort.cs` | Puerto de logging (abstracción) |
| `src/Infrastructure/Adapters/Logging/SerilogLoggerAdapter.cs` | Implementación Serilog del puerto |
| `src/Infrastructure/Extensions/SerilogExtensions.cs` | Pipeline de Serilog (enrichers, sinks, niveles) |
| `src/Infrastructure/Logging/ActivityEnricher.cs` | Enricher para traceId/spanId (OpenTelemetry) |
| `src/Infrastructure/Logging/FlatJsonFormatter.cs` | Formatter JSON plano para producción |
| `src/Infrastructure/Logging/LogContextExtensions.cs` | Helpers para enriquecimiento de contexto |
| `src/Api/Middleware/RequestLoggingMiddleware.cs` | Logging automático de requests HTTP |
| `src/Api/DependencyInjection/SharedServiceExtensions.cs` | Registro de `ILoggerPort<>` en DI |
| `src/Api/Program.cs` | Punto de arranque: `builder.Host.AddSerilog(...)` |
