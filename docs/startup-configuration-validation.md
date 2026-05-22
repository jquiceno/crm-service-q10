# Validación de configuración al inicio

## Objetivo

Impedir que la aplicación arranque si le falta alguna variable de entorno o valor de configuración que sea crítico para su funcionamiento. En lugar de fallar silenciosamente o en tiempo de ejecución, la app lanza una excepción descriptiva en el momento del inicio, dejando en claro exactamente qué falta y cómo corregirlo.

Este patrón se conoce como **fail-fast**: fallar pronto y con un mensaje útil, antes de que la app quede en un estado inconsistente.

---

## Terminología

| Término | Significado en este contexto |
|---|---|
| **Fail-fast** | Principio de diseño que plantea detectar errores lo antes posible y detener la ejecución de inmediato, en lugar de continuar en un estado inválido. |
| **Options Pattern** | Mecanismo de .NET para tipar y validar secciones de configuración mediante clases POCO enlazadas a `IOptions<T>`. |
| **`ValidateOnStart()`** | Extensión de .NET que hace que las validaciones del `Options Pattern` se ejecuten al arrancar la app, en lugar de la primera vez que se usa la opción. |
| **`ValidateDataAnnotations()`** | Activa la validación de atributos como `[Required]` o `[Range]` sobre la clase de configuración. |
| **`OptionsValidationException`** | Excepción que lanza .NET cuando una validación del `Options Pattern` falla. |
| **`InvalidOperationException`** | Excepción usada en este proyecto para señalar que una dependencia crítica no está configurada. |

---

## Mecanismos de validación

La plantilla usa dos estrategias complementarias:

### 1. Validación manual con `throw` (para dependencias externas)

Se usa cuando la habilitación de un componente es opcional (`Enabled: true/false`). Si el componente está habilitado pero le falta un valor crítico, se lanza una `InvalidOperationException` con un mensaje que indica dónde configurar la variable.

> **¿Por qué no usar el Options Pattern aquí?**
> Los valores como el `ConnectionString` y el `Sentry DSN` son **secretos**: contienen credenciales de acceso. Registrarlos con `AddOptions` los haría inyectables en cualquier clase de la aplicación mediante `IOptions<T>`, lo que amplía innecesariamente su superficie de exposición. El patrón manual garantiza que estos valores se lean una sola vez en el startup para configurar el componente, y no queden disponibles en el contenedor de dependencias.

**Variables validadas con este mecanismo:**

| Variable | Condición de fallo |
|---|---|
| `Sentry:Dsn` | `Sentry:Enabled = true` y el DSN está vacío |
| `Persistence:ConnectionString` | `Persistence:Enabled = true` y el ConnectionString está vacío |

**Ejemplo del mensaje de error (Sentry):**

```
Critical Error: SENTRY is enabled but Dsn is missing.
Set 'Sentry:Dsn' in appsettings.json or 'Sentry__Dsn' as an environment variable.
Application startup aborted.
```

> En variables de entorno, los dos puntos (`:`) se reemplazan por doble guion bajo (`__`). Por ejemplo, `Sentry:Dsn` se convierte en `Sentry__Dsn`.

---

### 2. Options Pattern con `ValidateDataAnnotations()` + `ValidateOnStart()` (para configuración propia)

Se usa en clases de configuración que siempre deben estar presentes. Los atributos de validación (`[Required]`, `[Range]`, etc.) se evalúan al arrancar la app mediante `ValidateOnStart()`.

**Variables validadas con este mecanismo:**

| Variable | Restricción |
|---|---|
| `AppInfo:ServiceName` | `[Required]`, `[MinLength(1)]` |
| `AppInfo:Version` | `[Required]`, `[MinLength(1)]` |
| `Cache:DefaultTtlSeconds` | `[Range(1, int.MaxValue)]` |

**Ejemplo de registro:**

```csharp
services.AddOptions<AppInfoSettings>()
    .Bind(configuration.GetSection(AppInfoSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```

---

## Orden de validación al inicio

Las validaciones se ejecutan en el orden en que se registran en `Program.cs`:

1. **Sentry DSN** — al invocar `builder.AddSentry()`
2. **AppInfo (ServiceName, Version)** — al invocar `builder.Host.AddSerilog()` y luego `AddApiSettings()`
3. **Persistence ConnectionString** — al invocar `AddInfrastructureServices()`
4. **Cache Settings** — al invocar `ConfigureCache()`
5. **Validaciones de `ValidateOnStart()`** — al invocar `builder.Build()`

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
|---|---|
| `src/Api/Program.cs` | Punto de entrada; define el orden de registro y validación |
| `src/Api/DependencyInjection/SettingsExtensions.cs` | Registra `AppInfoSettings` con `ValidateOnStart()` |
| `src/Api/DependencyInjection/InfrastructureServiceExtensions.cs` | Valida `Persistence:ConnectionString` |
| `src/Infrastructure/Extensions/SentryExtensions.cs` | Valida `Sentry:Dsn` |
| `src/Infrastructure/Extensions/SerilogExtensions.cs` | Valida presencia de la sección `AppInfo` |
| `src/Infrastructure/Extensions/OutputCacheExtensions.cs` | Registra `CacheSettings` con `ValidateOnStart()` |
| `src/Infrastructure/Settings/` | Clases POCO de configuración con sus atributos de validación |
