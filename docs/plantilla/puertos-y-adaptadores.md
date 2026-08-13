# Puertos y adaptadores

## 1. Qué son puertos y adaptadores

La plantilla implementa **Arquitectura Hexagonal** (Ports & Adapters): Domain y Application definen **qué necesitan** del mundo exterior mediante interfaces, sin saber cómo se implementan. Infrastructure y Api proveen esa implementación concreta — los **adaptadores** — sin que el dominio las conozca.

```
Domain / Application  →  define el contrato (interfaz)
Infrastructure / Api   →  implementa el contrato (adaptador / caso de uso concreto)
```

El propósito no es la nomenclatura en sí, sino lo que habilita: el dominio se puede testear con un doble de prueba en lugar de una base de datos o un servicio externo real, y la tecnología concreta (SQL Server, Serilog, una API externa) se puede cambiar sin tocar la lógica de negocio. Es la misma regla de dependencias de [arquitectura.md](arquitectura.md) expresada a nivel de interfaz: nunca es Domain o Application quien importa un tipo de Infrastructure — siempre es al revés.

Dentro de esta idea general, la plantilla distingue **tres contratos distintos**, no dos — y solo uno de ellos se llama realmente "Port":

| Contrato | Para qué existe | Sufijo | Ubicación |
|---|---|---|---|
| **Caso de uso** (puerto de entrada / *driving*) | Punto de entrada a una operación de negocio | `UseCase` | `Application/UseCases/{CasoDeUso}/` (coubicado con su implementación) |
| **Repositorio** | Persistir y recuperar los Aggregates de un contexto | `Repository` | `Domain/Repositories/` |
| **Reader** (puerto de salida de solo lectura) | Leer datos que no son el Aggregate del contexto: catálogos, tablas foráneas, vistas | `Reader` | `Application/Ports/` |
| **Port** (puerto de salida / *driven*, no persistencia) | Cualquier otra capacidad externa que la Application necesita y que no es guardar/leer un Aggregate | `Port` | `Application/Ports/` |

El **Reader** es un puerto de salida como cualquier otro y por eso su interfaz vive en `Application/Ports/`, pero se nombra `Reader` en lugar de `Port` porque el nombre ya describe con precisión qué hace — la misma razón por la que el repositorio se llama `Repository`. Cuándo es un Reader, cuándo un Provider y cuándo un Repository está en [conceptos-reader-provider-repository.md](conceptos-reader-provider-repository.md).

---

## 2. Por qué el Repositorio no es un "Port"

En la literatura clásica de Arquitectura Hexagonal, un repositorio *sí* se presenta como un ejemplo de puerto de salida (driven port). Pero DDD (Eric Evans) ya trata **Repository** como un patrón de primera clase con nombre y semántica propios — dar la ilusión de una colección en memoria de Aggregates —, independiente del vocabulario de Cockburn. Esta plantilla sigue esa idea: si el contrato persiste o recupera Aggregates, se llama `Repository`, no `Port`. `Port` queda reservado para lo que **no** tiene ya un nombre DDD propio.

Esto no es una preferencia arbitraria: en este mismo servicio, `IAnnouncementRepositoryPort` existió en algún momento en `Domain/Ports/` y fue eliminado a propósito, consolidando todo en `IAnnouncementRepository` bajo `Domain/Repositories/` (`git log` — *"Merges IAnnouncementRepository (minimal) and IAnnouncementRepositoryPort (full contract)... Removes the Ports folder for announcements"*). La carpeta `Ports/` para repositorios ya no existe en el código real.

### Cuándo usar `Port` (no persistencia)

Un `Port` representa una capacidad externa que un Use Case necesita **para completar su lógica**, sin que esa capacidad sea guardar o leer el Aggregate del contexto. Ejemplos:

- Convertir el precio de un producto a otra moneda usando una tasa de cambio externa.
- Enviar una notificación o un correo.
- Leer información de configuración o de otro sistema para enriquecer un DTO de salida.

Ejemplo real de este último caso en el propio servicio — `IServiceInfoPort`, consumido por `GetServiceInfoUseCase` para completar su respuesta con datos que vienen de configuración, no de un Aggregate:

```csharp
// Contexts/ServiceInfo/Application/Ports/IServiceInfoPort.cs
public interface IServiceInfoPort
{
    string Name { get; }
    string Version { get; }
    string TemplateVersion { get; }
}
```

```csharp
// Infrastructure/Adapters/ServiceInfo/ServiceInfoAdapter.cs
public sealed class ServiceInfoAdapter(
    IOptions<ServiceInfoSettings> serviceInfo,
    IOptions<TemplateSettings> templateInfo) : IServiceInfoPort
{
    public string Name            => serviceInfo.Value.Name;
    public string Version         => serviceInfo.Value.Version;
    public string TemplateVersion => templateInfo.Value.Version;
}
```

```csharp
// Application/UseCases/GetServiceInfo/GetServiceInfoUseCase.cs
public sealed class GetServiceInfoUseCase(IServiceInfoPort serviceInfo) : IGetServiceInfoUseCase
{
    public Task<Result<GetServiceInfoOutputDto>> ExecuteAsync(CancellationToken ct = default)
    {
        var output = new GetServiceInfoOutputDto("ok", serviceInfo.Name, serviceInfo.Version, serviceInfo.TemplateVersion);
        return Task.FromResult(Result<GetServiceInfoOutputDto>.Success(output));
    }
}
```

No hay Aggregate, no hay persistencia, no hay CRUD — solo un dato técnico externo que el caso de uso necesita para construir su resultado.

### `Port` específico de un contexto vs. compartido

Un `Port` puede vivir en dos lugares distintos según quién lo usa:

| Alcance | Ubicación | Ejemplo |
|---|---|---|
| Específico de un contexto | `Contexts/{Contexto}/Application/Ports/` | `IServiceInfoPort` |
| Compartido entre todos los contextos | `Shared/Application/Ports/` | `ILoggerPort<T>`, `IUnitOfWorkPort`, `IRequestValidatorPort<T>` |

Un `Port` nace en `Contexts/{Contexto}/Application/Ports/`; solo se promueve a `Shared/Application/Ports/` cuando un segundo contexto necesita exactamente la misma capacidad. No crear una versión por contexto de algo ya compartido (logging, validación, unit of work).

---

## 3. Propósito y cuándo se usa cada tipo

| Situación | Contrato a usar |
|---|---|
| Agregas una operación nueva sobre un Aggregate (crear, actualizar, listar, eliminar, relacionar) | Caso de uso (`I{CasoDeUso}UseCase`) — ver [casos-de-uso.md](casos-de-uso.md) |
| Un Use Case necesita persistir o consultar el Aggregate de su propio contexto | Repositorio del contexto (`I{Contexto}Repository`) — ver [repositorio.md](repositorio.md) |
| Un Use Case necesita leer un catálogo, una tabla foránea o una vista que **no** es su Aggregate | `Reader` del contexto (`Application/Ports/`) — ver [conceptos-reader-provider-repository.md](conceptos-reader-provider-repository.md) |
| Un Use Case necesita una capacidad externa que no es guardar/leer su Aggregate (config, servicio externo, cálculo con datos de otro sistema) | `Port` específico del contexto (`Application/Ports/`) |
| Esa misma capacidad la necesitan dos o más contextos | `Port` compartido (`Shared/Application/Ports/`) |

---

## 4. Cómo se usan

```
Controller
    │  conoce solo la interfaz →  IUpdateProductUseCase                 (caso de uso)
    ▼
UpdateProductUseCase : IUpdateProductUseCase
    │  conoce solo la interfaz →  IProductRepository                    (repositorio del contexto)
    │  conoce solo la interfaz →  IUnitOfWorkPort, ILoggerPort<T>       (ports compartidos)
    │  conoce solo la interfaz →  IProductPricingPort                   (port específico del contexto, si aplica)
    ▼
ProductRepository         : IProductRepository      ← vive en Infrastructure/Persistence/EntityFramework/Products/
UnitOfWorkAdapter         : IUnitOfWorkPort
SerilogLoggerAdapter<T>   : ILoggerPort<T>
ExchangeRatePricingAdapter: IProductPricingPort
```

Ninguna flecha apunta hacia arriba: el Use Case nunca conoce `ProductRepository`, solo `IProductRepository`. La conexión entre interfaz e implementación concreta ocurre en un único lugar — la extensión de registro DI del contexto (ver [4.5 en casos-de-uso.md](casos-de-uso.md) y [4.5 más abajo](#45-extensión-de-registro-di)).

---

## 5. Patrones con código real

### 5.1 Caso de uso (puerto de entrada)

```
I{CasoDeUso}UseCase
```

Ubicación: coubicado con su implementación en `Application/UseCases/{CasoDeUso}/` — no vive en una carpeta `Ports/` separada. Uno por operación, nunca una interfaz con varios métodos agrupando varias operaciones.

```csharp
// Contexts/Product/Application/UseCases/UpdateProduct/IUpdateProductUseCase.cs
public interface IUpdateProductUseCase
{
    Task<Result<UpdateProductOutputDto>> ExecuteAsync(
        Guid id, UpdateProductInputDto input, CancellationToken ct = default);
}
```

Quién lo implementa y quién lo invoca está desarrollado con detalle en [casos-de-uso.md](casos-de-uso.md) (Use Case) y [controllers.md](controllers.md) (Controller).

### 5.2 Repositorio

```
I{Contexto}Repository
```

Ubicación: `Domain/Repositories/`. Extiende `IRootRepository<TAggregate, TId>` — la interfaz genérica con las operaciones CRUD comunes a todo agregado — y agrega las queries específicas del contexto:

```csharp
// Contexts/Product/Domain/Repositories/IProductRepository.cs
public interface IProductRepository : IRootRepository<ProductAggregate, Guid>
{
    Task<Result<bool>> ExistsByNameAsync(string name, CancellationToken ct = default);
}
```

El contrato genérico (`GetByIdAsync`, `ExistsAsync`, `GetAllAsync`, `AddAsync`, `Update`, `RemoveAsync`), la implementación concreta y el par entidad-de-persistencia + mapper están documentados en [repositorio.md](repositorio.md) — este documento no los repite.

### 5.3 Port (puerto de salida, sin persistencia)

```
I{Capacidad}Port           → específico de un contexto, en Contexts/{Contexto}/Application/Ports/
I{Capacidad}Port<T>        → compartido, en Shared/Application/Ports/
```

Ejemplo compartido — usado por cualquier contexto:

```csharp
// Shared/Application/Ports/ILoggerPort.cs
public interface ILoggerPort<out T>
{
    void Debug(string message, params object[] args);
    void Info(string message, params object[] args);
    void Warning(string message, params object[] args);
    void Error(Exception? exception, string message, params object[] args);
}
```

```csharp
// Shared/Application/Ports/IRequestValidatorPort.cs
public interface IRequestValidatorPort
{
    Task<Result> ValidateAsync(object input, CancellationToken ct = default);
}

public interface IRequestValidatorPort<T> : IRequestValidatorPort
{
    Task<Result> ValidateAsync(T input, CancellationToken ct = default);
}
```

`IRequestValidatorPort` (sin genérico) existe para que `[ValidateRequest]` pueda resolver el validador correcto en tiempo de ejecución a partir del tipo del DTO, sin conocerlo de antemano — ver [validaciones.md](validaciones.md). El detalle completo de `ILoggerPort<T>` vive en [logging.md](logging.md).

Ejemplo específico de un contexto — `IServiceInfoPort` (ver [sección 2](#2-por-qué-el-repositorio-no-es-un-port) para el código completo).

### 5.4 Implementaciones concretas: repositorios, readers y adaptadores

No todo lo que implementa un contrato se llama `Adapter` ni vive en `Adapters/`. Hay tres familias, y la de persistencia es la excepción:

```
{Aggregate}Repository                → implementa I{Contexto}Repository       (persistencia)
{Concepto}Reader                     → implementa I{Concepto}Reader           (lectura, no agregados)
{Tecnología}{Capacidad}Adapter<T>    → implementa un Port compartido
{Contexto}Adapter                    → implementa un Port específico del contexto
```

**Persistencia — `Persistence/EntityFramework/{Contexto}/`, sin sufijo `Adapter`.** El repositorio y los readers del contexto son piezas de EF Core inseparables de su entidad, su `IEntityTypeConfiguration<>` y su mapper, y viven junto a ellos:

```csharp
// Infrastructure/Persistence/EntityFramework/Products/ProductRepository.cs
public sealed class ProductRepository(
    ApplicationDbContext context,
    ILoggerPort<ProductRepository> logger) : IProductRepository
{
    private const string Origin = nameof(ProductRepository);

    private readonly DbSet<Entities.Product> _products = context.Set<Entities.Product>();

    public async Task<Result<bool>> ExistsByNameAsync(
        string name, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _products.AnyAsync(p => p.Name == name, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error checking product name {Name}", name);
            return PersistenceErrors.Failure(Origin);
        }
    }
}
```

En `Infrastructure/Adapters/Persistence/` solo queda lo transversal a todos los contextos: `UnitOfWorkAdapter` y `SqlServer/SqlServerErrorClassifier`.

**Resto de capacidades — `Infrastructure/Adapters/{Concern}/`, con sufijo `Adapter`.** Logging, validación y los `Port` específicos de contexto sí siguen la convención clásica (`SerilogLoggerAdapter<T>`, `FluentRequestValidationAdapter<T>`, `ServiceInfoAdapter`).

El repositorio es también el lugar donde un `Port` compartido (`ILoggerPort<T>`) y el repositorio del contexto se cruzan — consume uno para implementar el otro. Ver ejemplos completos de repositorio en [repositorio.md](repositorio.md), de Reader en [conceptos-reader-provider-repository.md](conceptos-reader-provider-repository.md), de logging en [logging.md](logging.md), y de `Port` específico de contexto en [sección 2](#2-por-qué-el-repositorio-no-es-un-port) (`ServiceInfoAdapter`).

### 5.5 Extensión de registro DI

```
Add{Contexto}Services
```

Ubicación: `Api/DependencyInjection/{Contexto}ServiceExtensions.cs`. Es el único lugar del código donde una interfaz y su implementación concreta aparecen juntas:

```csharp
// Api/DependencyInjection/ProductServiceExtensions.cs
public static class ProductServiceExtensions
{
    public static IServiceCollection AddProductServices(this IServiceCollection services)
    {
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductCategoryReader, ProductCategoryReader>();

        services.AddScoped<IUpdateProductUseCase, UpdateProductUseCase>();
        return services;
    }
}
```

Los `Port` compartidos (`ILoggerPort<T>`, `IRequestValidatorPort<T>`, `IUnitOfWorkPort`) se registran una sola vez para todo el servicio, no por contexto:

```csharp
// Api/DependencyInjection/SharedServiceExtensions.cs
services.AddSingleton(typeof(ILoggerPort<>), typeof(SerilogLoggerAdapter<>));
```

El detalle de qué lifetime (`Scoped` / `Singleton`) usa cada tipo de contrato está en la tabla de [repositorio.md](repositorio.md#registro-de-dependencias). Cómo se conecta esta extensión al resto del contexto está en [contextos.md](contextos.md#55-registro-de-dependencias).

---

## 6. Nomenclatura — referencia rápida

| Contrato | Patrón | Ubicación | Ejemplo |
|---|---|---|---|
| Caso de uso (driving) | `I{CasoDeUso}UseCase` | `Application/UseCases/{CasoDeUso}/` | `IUpdateProductUseCase` |
| Repositorio | `I{Contexto}Repository` | `Domain/Repositories/` | `IProductRepository` |
| Reader | `I{Concepto}Reader` | `Contexts/{Contexto}/Application/Ports/` | `IProgramClassificationReader` |
| Port específico de contexto | `I{Capacidad}Port` | `Contexts/{Contexto}/Application/Ports/` | `IServiceInfoPort` |
| Port compartido | `I{Capacidad}Port<T>` | `Shared/Application/Ports/` | `ILoggerPort<T>`, `IUnitOfWorkPort`, `IRequestValidatorPort<T>` |

### Implementaciones concretas

| Patrón | Ubicación | Ejemplo | Contrato que implementa |
|---|---|---|---|
| `{Aggregate}Repository` | `Infrastructure/Persistence/EntityFramework/{Contexto}/` | `ProgramRepository`, `AuditLogRepository` | `IProgramRepository` |
| `{Concepto}Reader` | `Infrastructure/Persistence/EntityFramework/{Contexto}/` | `ProgramClassificationReader`, `PersonNameReader` | `IProgramClassificationReader` |
| `{Contexto}Adapter` | `Infrastructure/Adapters/{Contexto}/` | `ServiceInfoAdapter` | `IServiceInfoPort` |
| `{Tecnología}LoggerAdapter<T>` | `Infrastructure/Adapters/Logging/` | `SerilogLoggerAdapter<T>` | `ILoggerPort<T>` |
| `{Tecnología}RequestValidationAdapter<T>` | `Infrastructure/Adapters/Validation/` | `FluentRequestValidationAdapter<T>` | `IRequestValidatorPort<T>` |

### Infraestructura de soporte (no implementa un contrato directamente)

Clases de una tecnología concreta que no son adaptadores porque no implementan un contrato — son la base sobre la que los adaptadores se construyen, o configuración pura.

| Ejemplo | Ubicación |
|---|---|
| `ApplicationDbContext` | `Persistence/EntityFramework/` |
| `RepositoryBaseEF<TAggregate, TId>` (no usado por los repositorios actuales — ver [repositorio.md](repositorio.md#repositorybaseeftaggregate-tid--solo-para-agregados-que-sí-son-la-entidad)) | `Persistence/EntityFramework/Common/` |
| `Program`, `AuditLog` (entidad de persistencia) | `Persistence/EntityFramework/{Contexto}/Entities/` |
| `ProgramConfiguration` (`IEntityTypeConfiguration<T>`) | `Persistence/EntityFramework/{Contexto}/Configurations/` |
| `ProgramRepositoryMapper` (Aggregate ↔ entidad) | `Persistence/EntityFramework/{Contexto}/Mappers/` |
| `SqlServerErrorClassifier` | `Adapters/Persistence/SqlServer/` |
| `IStructuralValidator<T>` (marcador de FluentValidation) | `Validation/FluentValidation/` |
| `CreateProductInputValidator` | `Validation/FluentValidation/Product/` |

### Extensiones de registro DI

| Ejemplo | Propósito |
|---|---|
| `Add{Contexto}Services` | Registra los casos de uso, repositorio y ports del contexto — ver [5.5](#55-extensión-de-registro-di) |
| `ValidatorRegistrationExtensions` | Registra todos los `IStructuralValidator<T>` y sus adaptadores, vía reflection |
| `EfCorePersistenceExtensions` | Registra el `DbContext` |
| `SerilogExtensions` | Configura el pipeline de Serilog |
| `SentryExtensions` | Configura el SDK de Sentry |

---

## 7. Estructura de carpetas resumida

```
src/
├── Contexts/{Contexto}/
│   ├── Domain/
│   │   ├── Repositories/          → I{Contexto}Repository (persistencia del Aggregate)
│   │   ├── Queries/               → objetos de filtro del contexto ({Contexto}Filter)
│   │   └── Models/                → modelos de lectura que no son agregados
│   └── Application/
│       ├── Ports/                 → I{Capacidad}Port y I{Concepto}Reader
│       ├── Providers/             → {Contexto}{Concepto}Provider (opcional)
│       └── UseCases/{CasoDeUso}/  → I{CasoDeUso}UseCase + implementación, coubicados
│
├── Shared/Application/Ports/      → Port compartidos entre todos los contextos (ILoggerPort<T>, IUnitOfWorkPort, IRequestValidatorPort<T>)
│
└── Infrastructure/
    ├── Adapters/
    │   ├── Logging/                  → {Tecnología}LoggerAdapter<T>
    │   ├── Persistence/              → UnitOfWorkAdapter, SqlServer/SqlServerErrorClassifier (transversales)
    │   ├── Validation/               → {Tecnología}RequestValidationAdapter<T>
    │   └── {Contexto}/               → {Contexto}Adapter (implementa un Port específico del contexto)
    ├── Persistence/EntityFramework/
    │   ├── Common/                   → RepositoryBaseEF<TAggregate, TId>, PersistenceErrors
    │   └── {Contexto}/               → {Aggregate}Repository, {Concepto}Reader
    │       ├── Entities/             → entidad de persistencia (fila de la tabla)
    │       ├── Configurations/       → IEntityTypeConfiguration<T>
    │       └── Mappers/              → {Aggregate}RepositoryMapper
    └── Extensions/                   → extensiones de registro DI

Api/
└── DependencyInjection/        → Add{Contexto}Services, ApplicationServiceExtensions
```

---

## Ver también

- [arquitectura.md](arquitectura.md) — capas, regla de dependencias, estructura de carpetas completa
- [contextos.md](contextos.md) — dónde nacen los contratos de un contexto nuevo
- [casos-de-uso.md](casos-de-uso.md) — implementación del caso de uso de cada tipo de operación
- [controllers.md](controllers.md) — cómo el controller invoca un caso de uso
- [repositorio.md](repositorio.md) — `IRootRepository`, entidad de persistencia + mapper, Unit of Work, lifetimes de registro
- [conceptos-reader-provider-repository.md](conceptos-reader-provider-repository.md) — Reader vs. Provider vs. Repository
- [logging.md](logging.md) — `ILoggerPort<T>` en detalle
- [validaciones.md](validaciones.md) — `IStructuralValidator<T>` y las cinco capas de validación
