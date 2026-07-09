# Nomenclatura de Puertos y Adaptadores

Convenciones aplicadas en el proyecto `service-template-dotnet`.


---

## Puertos

Los puertos son **interfaces** que definen contratos entre capas. Siempre llevan el sufijo `Port` y viven en carpetas `Ports/`.

### Driving ports (entrada — casos de uso)

Representan operaciones que el exterior puede invocar.

```
I{Acción}{Contexto}Port
```

| Ejemplo | Ubicación |
|---------|-----------|
| `ICreateWeatherForecastPort` | `Contexts/WeatherForecast/Application/Ports/` |
| `IGetWeatherForecastPort` | `Contexts/WeatherForecast/Application/Ports/` |

### Driven ports (salida — infraestructura)

Representan capacidades que la aplicación necesita del exterior (persistencia, logging, validación).

```
I{Contexto}RepositoryPort     → repositorios
I{Capacidad}Port<T>           → servicios genéricos
```

| Ejemplo | Ubicación |
|---------|-----------|
| `IWeatherForecastRepositoryPort` | `Contexts/WeatherForecast/Domain/Ports/` |
| `ILoggerPort<T>` | `Shared/Application/Ports/` |
| `IRequestValidatorPort<T>` | `Shared/Application/Ports/` |


---

## Adaptadores

Los adaptadores son **implementaciones concretas** de puertos usando una tecnología específica. Siempre llevan el sufijo `Adapter` y viven en `Infrastructure/Adapters/{Concern}/{Contexto}/`.

```
{Tecnología}{Contexto}Adapter
```

| Ejemplo | Puerto que implementa | Ubicación |
|---------|-----------------------|-----------|
| `SerilogLoggerAdapter<T>` | `ILoggerPort<T>`      | `Adapters/Logging/` |
| `WeatherForecastRepositoryAdapter` | `IWeatherForecastRepositoryPort` | `Adapters/Persistence/WeatherForecast/` |
| `FluentRequestValidationAdapter<T>` | `IRequestValidatorPort<T>` | `Adapters/Validation/` |


---

## Infraestructura tecnológica

Clases de soporte de una tecnología que **no implementan puertos directamente** (contextos de BD, clases base, configuraciones, validadores concretos). Se ubican en `Infrastructure/{Concern}/{Tecnología}/`.

| Ejemplo | Ubicación |
|---------|-----------|
| `ApplicationDbContext` | `Persistence/EntityFramework/` |
| `BaseAggregateRepository<,>` | `Persistence/EntityFramework/Common/` |
| `IStructuralValidator<T>` | `Validation/FluentValidation/` |
| `CreateWeatherForecastInputValidator` | `Validation/FluentValidation/WeatherForecast/` |


---

## Extensiones de registro DI

Los métodos de extensión para registrar servicios en el contenedor van en `Infrastructure/Extensions/`, junto a los demás extension methods del host.

| Ejemplo | Propósito |
|---------|-----------|
| `ValidatorRegistrationExtensions` | Registra todos los `IStructuralValidator<T>` y sus adaptadores |
| `EfCorePersistenceExtensions` | Registra el `DbContext` |
| `SerilogExtensions` | Configura Serilog |
| `SentryExtensions` | Configura Sentry SDK |


---

## Estructura resumida

```
src/
├── Contexts/{Contexto}/
│   ├── Domain/Ports/          → driven ports de dominio
│   └── Application/Ports/     → driving ports (casos de uso)
│
├── Shared/Application/Ports/  → driven ports compartidos
│
└── Infrastructure/
    ├── Adapters/
    │   ├── Logging/            → {Tecnología}LoggerAdapter
    │   ├── Persistence/{Contexto}/  → {Contexto}RepositoryAdapter
    │   └── Validation/         → {Tecnología}ValidationAdapter
    ├── {Concern}/{Tecnología}/ → infraestructura de soporte
    └── Extensions/             → extensiones de registro DI
```
