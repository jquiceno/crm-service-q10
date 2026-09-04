# Validación de configuración al inicio

## Objetivo

Impedir que la aplicación arranque si le falta alguna variable de entorno o valor de configuración que sea crítico para su funcionamiento. En lugar de fallar silenciosamente o en tiempo de ejecución, la app lanza una excepción descriptiva en el momento del inicio, dejando en claro exactamente qué falta y cómo corregirlo.

Este patrón se conoce como **fail-fast**: fallar pronto y con un mensaje útil, antes de que la app quede en un estado inconsistente.


---

## Terminología

| Término | Significado en este contexto |
|---------|------------------------------|
| **Fail-fast** | Principio de diseño que plantea detectar errores lo antes posible y detener la ejecución de inmediato, en lugar de continuar en un estado inválido. |
| **Options Pattern** | Mecanismo de .NET para tipar y validar secciones de configuración mediante clases POCO enlazadas a `IOptions<T>`. |
| `**ValidateOnStart()**` | Extensión de .NET que hace que las validaciones del `Options Pattern` se ejecuten al arrancar la app, en lugar de la primera vez que se usa la opción. |
| `**ValidateDataAnnotations()**` | Activa la validación de atributos como `[Required]` o `[Range]` sobre la clase de configuración. |
| `**OptionsValidationException**` | Excepción que lanza .NET cuando una validación del `Options Pattern` falla. |
| `**InvalidOperationException**` | Excepción usada en este proyecto para señalar que una dependencia crítica no está configurada. |


---

## Mecanismos de validación

La plantilla usa dos estrategias complementarias:

### 1. Validación manual con `throw` (para dependencias externas)

Se usa cuando la habilitación de un componente es opcional (`Enabled: true/false`). Si el componente está habilitado pero le falta un valor crítico, se lanza una `InvalidOperationException` con un mensaje que indica dónde configurar la variable.

> **¿Por qué no usar el Options Pattern aquí?** Los valores como el `ConnectionString` y el `Sentry DSN` son **secretos**: contienen credenciales de acceso. Registrarlos con `AddOptions` los haría inyectables en cualquier clase de la aplicación mediante `IOptions<T>`, lo que amplía innecesariamente su superficie de exposición. El patrón manual garantiza que estos valores se lean una sola vez en el startup para configurar el componente, y no queden disponibles en el contenedor de dependencias.

**Variables validadas con este mecanismo:**

| Variable | Condición de fallo |
|----------|--------------------|
| `RoutePrefix` | **siempre requerida**; vacía o solo espacios (es el prefijo de URL bajo el que se sirve todo) |
| `Sentry:Dsn` | `Sentry:Enabled = true` y el DSN está vacío |
| `TenantResolverService:Enabled` | **siempre requerida en `true`**: no hay modo single-tenant ni base en memoria, así que sin tenant-resolver no hay base de datos a la que conectarse |
| `TenantResolverService:BaseUrl` | vacío o no es una URL absoluta válida |
| `TenantResolverService:EncryptionKey` | vacío (es la clave con la que el resolver cifra los connection strings) |
| `Cache:L2Enabled` / `Cache:ConnectionString` | L2 apagada o sin connection string: la resolución de tenant quedaría llamando al resolver por HTTP en cada petición |

**Ejemplo del mensaje de error (Sentry):**

```
Critical Error: SENTRY is enabled but Dsn is missing.
Set the 'SENTRY_DSN' environment variable (platform-shared secret) or 'Sentry:Dsn' in appsettings.json.
Application startup aborted.
```

> En variables de entorno, los dos puntos (`:`) se reemplazan por doble guion bajo (`__`). Por ejemplo, `Sentry:Dsn` se convierte en `Sentry__Dsn`.

> **Variables agnósticas de plataforma:** los valores del secreto compartido `/platform/{env}/shared` usan un nombre canónico independiente del lenguaje — `TENANT_RESOLVER_SERVICE_URL`, `CONNSTRING_ENCRYPTION_KEY` y `SENTRY_DSN` — para que todos los servicios (Node, .NET, etc.) consuman la misma variable. En el arranque, `AddTenantResolverEnvironmentAliases()` (en `TenancyConfigurationExtensions`) mapea las dos primeras a `TenantResolverService:BaseUrl` y `TenantResolverService:EncryptionKey`, y `AddSentry()` (en `SentryExtensions`) mapea `SENTRY_DSN` a `Sentry:Dsn` antes de que nada lea esa sección; las claves .NET explícitas siguen funcionando para desarrollo local.


---

### 2. Options Pattern con `ValidateDataAnnotations()` + `ValidateOnStart()` (para configuración propia)

Se usa en clases de configuración que siempre deben estar presentes. Los atributos de validación (`[Required]`, `[Range]`, etc.) se evalúan al arrancar la app mediante `ValidateOnStart()`.

**Variables validadas con este mecanismo:**

| Variable | Restricción |
|----------|-------------|
| `ServiceInfo:Name` | `[Required]`, `[MinLength(1)]` |
| `ServiceInfo:Version` | `[Required]`, `[MinLength(1)]` |
| `Cache:DefaultTtlSeconds` | `[Range(1, int.MaxValue)]` |

**Ejemplo de registro:**

```csharp
services.AddOptions<ServiceInfoSettings>()
    .Bind(configuration.GetSection(ServiceInfoSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```


---

### 3. Sonda de dependencia externa (reachability)

Además de validar **configuración**, la plantilla puede abortar el arranque si una **dependencia externa en runtime** no está disponible. Hoy aplica al tenant-resolver, que es obligatorio: `TenantResolverStartupProbe` (un `IHostedLifecycleService`) hace una petición HTTP a `{BaseUrl}/health` en `StartingAsync` —antes de que Kestrel abra el puerto—. Cualquier respuesta HTTP cuenta como "alcanzable"; solo un fallo de conexión o timeout aborta el arranque con `InvalidOperationException`.

> **Config vs dependencia:** un error de **configuración** (falta un valor) es irrecuperable y siempre debe abortar. Una **dependencia de red caída** es recuperable; este gate duro es una decisión deliberada de "no levantar si no puedo resolver tenants". El orquestador reintenta reiniciando la instancia.

La readiness (`/{RoutePrefix}/health/ready`, vía `AddUrlGroup` a `{BaseUrl}/health`) es el gate **suave y recuperable** complementario: no recibe tráfico hasta que el resolver responde 2xx, y se recupera solo sin reiniciar. La sonda de arranque es el gate **duro**; la readiness, el continuo.


---

## Orden de validación al inicio

Las validaciones se ejecutan en el orden en que se registran en `Program.cs`:


1. **Sentry DSN** — al invocar `builder.AddSentry()`
2. **RoutePrefix** — justo tras cargar la configuración (antes de `builder.Services`/`AddApiSettings`); aborta si está vacío
3. **ServiceInfo (Name, Version)** — al invocar `builder.Host.AddSerilog()` y luego `AddApiSettings()`
4. **Prerequisitos de multitenencia** (`Enabled`, `BaseUrl`, `EncryptionKey`, caché L2) — al invocar `AddInfrastructureServices()`
5. **Cache Settings** — al invocar `ConfigureCache()`
6. **Validaciones de** `**ValidateOnStart()**` — al invocar `builder.Build()`
7. **Reachability del tenant-resolver** — en `StartingAsync`, tras `builder.Build()` y antes de que Kestrel abra el puerto

Si cualquiera de estas falla, la app se detiene y el error queda registrado antes de atender cualquier solicitud.


---

## Cómo agregar una nueva validación

### Si la variable pertenece a un componente con `Enabled`:

Agrega el guard en el método de extensión correspondiente, siguiendo el patrón existente:

```csharp
if (string.IsNullOrWhiteSpace(settings.MiVariable))
{
    throw new InvalidOperationException(
        "Critical Error: MI_COMPONENTE is enabled but MiVariable is missing. "
        + "Set 'Seccion:MiVariable' in appsettings.json or "
        + "'Seccion__MiVariable' as an environment variable. "
        + "Application startup aborted.");
}
```

### Si la variable siempre es requerida:

Agrega el atributo de validación sobre la propiedad en la clase de settings y asegúrate de que el registro incluya `ValidateDataAnnotations()` y `ValidateOnStart()`:

```csharp
// En la clase de settings
[Required]
public string MiVariable { get; set; } = string.Empty;

// En el método de extensión
services.AddOptions<MisSettings>()
    .Bind(configuration.GetSection(MisSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```


---

## Archivos clave

| Archivo | Responsabilidad |
|---------|-----------------|
| `src/Api/Program.cs` | Punto de entrada; define el orden de registro y validación (incluye el fail-fast de `RoutePrefix`) |
| `src/Shared/Infrastructure/Presentation/Routing/RoutePrefixConfig.cs` | Lee y normaliza `RoutePrefix` (`GetRoutePrefix()`) |
| `src/Api/DependencyInjection/SettingsExtensions.cs` | Registra `ServiceInfoSettings` con `ValidateOnStart()` |
| `src/Api/DependencyInjection/InfrastructureServiceExtensions.cs` | Valida los prerequisitos de multitenencia y aborta si está apagada |
| `src/Api/HostedServices/TenantResolverStartupProbe.cs` | Sonda de reachability del tenant-resolver al arranque |
| `src/Infrastructure/Extensions/SentryExtensions.cs` | Valida `Sentry:Dsn` |
| `src/Infrastructure/Extensions/SerilogExtensions.cs` | Valida presencia de la sección `ServiceInfo` |
| `src/Api/DependencyInjection/OutputCacheExtensions.cs` | Registra `CacheSettings` con `ValidateOnStart()` |
| `src/Infrastructure/Settings/` | Clases POCO de configuración con sus atributos de validación |
