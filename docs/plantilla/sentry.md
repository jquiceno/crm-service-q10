# Sentry

## Introducción

Sentry cumple dos roles en el proyecto: **error tracking** (captura automática de excepciones no manejadas con contexto de stack trace, request y entorno) y **performance monitoring** (trazas distribuidas de requests HTTP con `TracesSampleRate`).

La pregunta clave al integrar cualquier herramienta de observabilidad en una arquitectura hexagonal es: **¿en qué capa vive y qué capas la conocen?** Este documento explica las decisiones tomadas.


---

## Cómo opera Sentry en el proyecto

Sentry se inicializa a nivel de **ASP.NET Core WebHost**, no como un servicio inyectado en los casos de uso:

```csharp
// src/Api/Program.cs
builder.WebHost.UseSentry(options => { ... });
```

Esto significa que el SDK de Sentry actúa como middleware del framework, capturando automáticamente:

* Excepciones no manejadas que llegan al pipeline HTTP
* Trazas de requests (cuando `TracesSampleRate > 0`)
* Logs de Serilog marcados como `Error` o superior (via el sink `WriteTo.Sentry`)

**Ningún caso de uso, entidad de dominio ni servicio de aplicación llama a Sentry directamente.** No hay `SentrySdk.CaptureException(...)` ni `SentrySdk.AddBreadcrumb(...)` en capas internas.


---

## Regla de dependencias

La regla fundamental de Arquitectura Hexagonal es que las dependencias solo apuntan hacia adentro:

```
┌────────────────────────────────────────────────────────┐
│  Infrastructure (Adaptadores)                          │
│  ┌──────────────────────────────────────────────────┐  │
│  │  Application (Casos de uso, Puertos)             │  │
│  │  ┌────────────────────────────────────────────┐  │  │
│  │  │  Domain (Entidades, Agregados, Errores)    │  │  │
│  │  └────────────────────────────────────────────┘  │  │
│  └──────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────┘

Sentry SDK → solo en Infrastructure
```

Verificación en el proyecto:

| Capa | Referencia a Sentry SDK | Estado |
|------|-------------------------|--------|
| `Domain` | Ninguna                 | ✅      |
| `Application` | Ninguna                 | ✅      |
| `Infrastructure.Extensions` | `SentryExtensions.cs`, `SerilogExtensions.cs` | ✅ Correcto |
| `Infrastructure.Settings` | `SentrySettings.cs` (POCO de configuración) | ✅ Correcto |

Los tipos concretos del SDK (`SentryRequest`, `Breadcrumb`, `SentryOptions`) solo aparecen en `src/Infrastructure/Extensions/SentryExtensions.cs`. Las capas internas no los conocen.


---

## El composition root y `Program.cs`

`Program.cs` llama directamente a `builder.AddSentry()`, que es una extensión definida en `Infrastructure.Extensions`:

```csharp
// src/Api/Program.cs
using Infrastructure.Extensions;

builder.AddSentry();
```

Esto **no viola** la regla de dependencias porque `Program.cs` es el **composition root**: el único lugar del sistema que tiene permiso de conocer todas las capas concretas para cablearlas. Su única responsabilidad es construir el grafo de objetos y arrancar el host; no contiene lógica de negocio.

El mismo principio aplica a `services.AddDbContext<ApplicationDbContext>()` o `services.AddScoped<IWeatherForecastRepository, WeatherForecastRepository>()`: son registros de DI en el composition root, no dependencias de negocio.


---

## ¿Por qué no existe `IErrorTracker`?

En el proyecto existe `ILoggerService<T>` como puerto de logging (Application → implementado por `SerilogLogger<T>` en Infrastructure). Podría preguntarse si debería existir un `IErrorTrackingService` o `IErrorTracker` como puerto para Sentry.

**La respuesta es no, en el estado actual**, por esta razón: un puerto se justifica cuando una capa interna necesita **invocar activamente** un comportamiento externo. En este proyecto:

* Los casos de uso solo llaman a `ILoggerService<T>.Error(exception, message)`.
* El fan-out hacia Sentry ocurre automáticamente a través del sink de Serilog — sin que Application lo sepa.
* No existe código en Application o Domain que diga "envía esto a Sentry".

```csharp
// src/Contexts/WeatherForecast/Application/UseCases/GetWeatherForecast/GetWeatherForecastUseCase.cs
// Solo usa ILoggerService<T>. No conoce Sentry.
public sealed class GetWeatherForecastUseCase(
    IWeatherForecastRepository repository,
    ILoggerService<GetWeatherForecastUseCase> logger)
```

**Cuándo sí se justificaría** `**IErrorTracker**`**:** si un caso de uso necesitara enriquecer eventos con contexto de dominio (tags, breadcrumbs de negocio, captura selectiva de excepciones manejadas con datos adicionales). En ese caso se introduciría la interfaz en `src/Shared/Application/Ports/` con un adaptador en `src/Infrastructure/Adapters/Sentry/`.


---

## Integración con Serilog

Serilog es el adaptador del puerto `ILoggerService<T>`. El sink `WriteTo.Sentry(...)` conecta Serilog con Sentry, convirtiendo los logs de nivel `Error` o superior en eventos de Sentry:

```csharp
// src/Infrastructure/Extensions/SerilogExtensions.cs
if (sentrySettings.Enabled)
{
    loggerConfig.WriteTo.Sentry(options =>
    {
        options.InitializeSdk = false; // SDK ya inicializado por SentryExtensions
        options.MinimumEventLevel = sentrySettings.MinimumEventLevel;
        options.MinimumBreadcrumbLevel = sentrySettings.MinimumBreadcrumbLevel;
    });
}
```

Los niveles del sink (`MinimumEventLevel`, `MinimumBreadcrumbLevel`) son configurables vía
`SentrySettings` con defaults `Error` y `Warning` respectivamente (ver sección [Configuración](#configuración)).

`SerilogExtensions` lee `SentrySettings.Enabled` para decidir si registra el sink. Este es un acoplamiento **intra-capa** (ambas clases viven en `Infrastructure`) — no cruza ninguna frontera de capa. El flag `InitializeSdk = false` es crítico: documenta que `SentryExtensions.AddSentry()` es el propietario del ciclo de vida del SDK, y que el sink de Serilog solo se engancha a él.


---

## Data scrubbing: sanitización de headers sensibles

`SentryExtensions` implementa lógica de sanitización para evitar que headers sensibles (tokens de autenticación, cookies, IPs) lleguen a Sentry:

```csharp
// src/Infrastructure/Extensions/SentryExtensions.cs
options.SetBeforeSend((sentryEvent, _) =>
{
    ScrubRequest(sentryEvent.Request, deniedHeaders, shouldScrubCookies);
    return sentryEvent;
});
```

Esta lógica opera sobre tipos del SDK (`SentryRequest`, `Breadcrumb`) en los hooks `SetBeforeSend`, `SetBeforeSendTransaction` y `SetBeforeBreadcrumb`. **Está en el lugar correcto** porque:


1. Opera sobre el formato de payload de Sentry, no sobre datos de dominio.
2. Pertenece a la frontera del adaptador de Infrastructure, justo antes de que los datos salgan del sistema.
3. Moverla a Application acoplaría una capa interna a tipos concretos del SDK.

Los headers filtrados por defecto se configuran en `SentrySettings.DeniedHeaders`:

```
Authorization, Proxy-Authorization, Cookie, Set-Cookie,
X-Api-Key, X-Forwarded-For, X-Real-Ip, X-Csrf-Token, X-Xsrf-Token
```


---

## Configuración

`SentrySettings` es un POCO de configuración en `src/Infrastructure/Settings/SentrySettings.cs`:

| Propiedad | Descripción | Default |
|-----------|-------------|---------|
| `Enabled` | Activa/desactiva Sentry en el arranque | `false` |
| `Dsn`     | Data Source Name de Sentry (obligatorio si `Enabled = true`) | —       |
| `TracesSampleRate` | Porcentaje de requests muestreados para performance (0.0 a 1.0) | `0.2` (20%) |
| `MinimumEventLevel` | Nivel mínimo de log que se envía a Sentry como evento. Valores: `Verbose`, `Debug`, `Information`, `Warning`, `Error`, `Fatal` | `Error` |
| `MinimumBreadcrumbLevel` | Nivel mínimo de log capturado como breadcrumb. Mismos valores que `MinimumEventLevel` | `Warning` |
| `DeniedHeaders` | Headers a sanitizar antes de enviar eventos | Ver lista anterior |

Configuración en `appsettings.json`:

```json
{
  "Sentry": {
    "Enabled": false,
    "Dsn": "",
    "TracesSampleRate": 0.2,
    "MinimumEventLevel": "Error",
    "MinimumBreadcrumbLevel": "Warning",
    "DeniedHeaders": "Authorization,Cookie,X-Api-Key"
  }
}
```

Los niveles (`MinimumEventLevel`, `MinimumBreadcrumbLevel`) se especifican por **nombre** —no por número—, y el binding es case-insensitive (aplica igual vía variable de entorno, p.ej. `Sentry__MinimumEventLevel=Warning`). Un valor inválido produce `InvalidOperationException` en el arranque (**fail-fast**): un typo no degrada silenciosamente el nivel de logging.

Si `Enabled = true` y `Dsn` está vacío, la aplicación lanza `InvalidOperationException` en el arranque para evitar inicios silenciosos sin observabilidad. 


---

## Resumen

| Decisión | Justificación arquitectónica |
|----------|------------------------------|
| Sentry se inicializa en `WebHost`, no en DI | Cross-cutting concern de framework; no necesita ser inyectado |
| No existe `IErrorTracker` | Ningún caso de uso invoca Sentry activamente |
| `SentryExtensions` vive en Infrastructure | Los tipos del SDK son detalles de infraestructura |
| `Program.cs` llama a `builder.AddSentry()` | Composition root: permitido referenciar Infrastructure |
| `SerilogExtensions` lee `SentrySettings` | Acoplamiento intra-capa; el sink necesita saber si Sentry está activo |
| Data scrubbing en `SentryExtensions` | Frontera del adaptador: sanitización antes de que los datos salgan del sistema |
