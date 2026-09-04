# Arquitectura de la plantilla

## Capas

```
┌─────────────────────────────────────────────────────┐
│  API (Presentación)                                 │
│  Controllers · Filters · Results · Middleware       │
├─────────────────────────────────────────────────────┤
│  Application                                        │
│  Use Cases · Ports · DTOs · Mappings · Providers    │
├─────────────────────────────────────────────────────┤
│  Domain                                             │
│  Aggregates · Entities · Value Objects · Errors     │
├─────────────────────────────────────────────────────┤
│  Infrastructure                                     │
│  EF Core · Repositorios · UoW · Logging · Validación│
└─────────────────────────────────────────────────────┘
```

**Regla fundamental:** las dependencias apuntan siempre hacia adentro. Domain no importa nada de Infrastructure ni de Application. Application no importa nada de Infrastructure.


---

## Estructura de carpetas

```
src/
├── Api/                              # Presentación (ASP.NET Core)
│   ├── Controllers/                  # Endpoints HTTP
│   ├── DependencyInjection/          # Registro de servicios por contexto
│   ├── Filters/                      # ValidateRequestFilter
│   ├── HostedServices/               # TenantResolverStartupProbe
│   ├── Middleware/                   # RequestLoggingMiddleware, TenantMiddleware
│   └── Session/                      # TenantContext, ITenantConnectionInitializer
│
├── Shared/
│   ├── Domain/                       # Primitivos reutilizables entre contextos
│   │   ├── Aggregates/               # AggregateRoot<TId> con CreatedAt / UpdatedAt
│   │   ├── Entities/                 # Entity<TId> (Id + igualdad)
│   │   ├── ValueObjects/             # ValueObject (base abstracta), DateRange
│   │   ├── Interfaces/               # IRootRepository<TAggregate, TId>, IAggregateRoot
│   │   ├── Errors/                   # DateRangeErrors
│   │   └── Pagination/               # PageQuery
│   ├── Results/                      # Result<T>, PagedResult<T>
│   │   └── Errors/                   # DomainError, ValidationError, ErrorType, SharedErrors
│   ├── Application/
│   │   ├── Ports/                    # IUnitOfWorkPort, ILoggerPort<T>, IRequestValidatorPort<T>, ICacheStore
│   │   ├── Caching/                  # CacheKey
│   │   ├── Interfaces/               # ILogProperties
│   │   └── Dtos/                     # PageQueryInputDto
│   └── Infrastructure/
│       ├── Presentation/
│       │   ├── Attributes/           # [ValidateRequest]
│       │   ├── Filters/              # ModelStateValidationAdapter, OutputCacheInvalidateAttribute
│       │   ├── Mapping/              # ErrorType → HTTP status code
│       │   ├── Middleware/           # GlobalExceptionHandler
│       │   ├── Responses/            # Estructura uniforme de respuesta API
│       │   ├── Results/              # HttpOkResult, HttpCreatedResult, HttpNoContentResult, HttpOkPagedResult
│       │   └── Routing/              # GlobalRoutePrefixConvention, RoutePrefixConfig, KebabCaseParameterTransformer
│       └── MasterAccess/             # TenantResolverServiceClient, AesConnectionStringDecryptor
│
├── Contexts/                         # Bounded Contexts — uno por dominio de negocio
│   └── {Contexto}/
│       ├── Domain/
│       │   ├── Aggregates/           # {Contexto}Aggregate + sus records de argumentos ({Contexto}Args)
│       │   ├── Entities/             # entidades hijas dentro del agregado (opcional)
│       │   ├── ValueObjects/         # VOs exclusivos del contexto
│       │   ├── Enums/                # enums del dominio
│       │   ├── Queries/              # objetos de filtro y modelos de consulta ({Contexto}Filter)
│       │   ├── Models/               # modelos de lectura que no son agregados (opcional)
│       │   ├── Repositories/         # I{Contexto}Repository (persistencia del Aggregate)
│       │   └── Errors/               # {Contexto}Errors
│       └── Application/
│           ├── Ports/                # I{Capacidad}Port e I{Concepto}Reader (opcional)
│           ├── Providers/            # Application services de resolución auxiliar (opcional)
│           └── UseCases/
│               └── {NombreUseCase}/  # I{NombreUseCase}UseCase + UseCase + InputDto + OutputDto + Mapping
│
└── Infrastructure/
    ├── Adapters/
    │   ├── Persistence/
    │   │   ├── UnitOfWorkAdapter.cs
    │   │   └── SqlServer/            # SqlServerErrorClassifier
    │   ├── Logging/                  # SerilogLoggerAdapter<T>
    │   ├── Validation/               # FluentRequestValidationAdapter<T>
    │   └── {Contexto}/               # {Contexto}Adapter — implementa un Port del contexto
    ├── Persistence/
    │   └── EntityFramework/
    │       ├── ApplicationDbContext.cs
    │       ├── Common/               # RepositoryBaseEF<TAggregate, TId>, PersistenceErrors
    │       └── {Contexto}/           # {Aggregate}Repository.cs, {Concepto}Reader.cs
    │           ├── Entities/         # entidad de persistencia (fila de la tabla)
    │           ├── Configurations/   # IEntityTypeConfiguration<T>
    │           └── Mappers/          # {Aggregate}RepositoryMapper (Aggregate ↔ entidad)
    ├── Caching/                      # RedisCacheStore, NoOpCacheStore, DistributedCacheExtensions
    ├── Extensions/                   # SerilogExtensions, SentryExtensions, EfCorePersistenceExtensions
    ├── Logging/                      # FlatJsonFormatter, ActivityEnricher, LogContextExtensions
    ├── Observability/                # TraceHeaders
    ├── Settings/                     # POCOs de configuración tipada
    └── Validation/FluentValidation/  # IStructuralValidator<T> y los validadores
```

Dos puntos que no son evidentes en el árbol:

- **El repositorio y los readers de un contexto no viven en `Adapters/`** ni llevan sufijo `Adapter`: van en `Persistence/EntityFramework/{Contexto}/`, junto a su entidad, su configuración y su mapper. En `Adapters/Persistence/` solo queda lo transversal. Ver [repositorio.md](repositorio.md#ubicación-y-naming-del-repositorio).
- **El agregado no es la entidad que EF Core mapea.** Al ser proyectos Database First sobre esquemas heredados, hay una entidad de persistencia aparte y un mapper que traduce en ambos sentidos. Ver [repositorio.md](repositorio.md#el-agregado-no-es-la-entidad-de-ef-core--entidad-de-persistencia--mapper).


---

## Bounded Context de ejemplo

Todos los documentos de esta carpeta usan `Product` (`Name: string`, `Price: decimal`) como contexto de ejemplo para mostrar los patrones implementados — es el mismo contexto en `casos-de-uso.md`, `contextos.md`, `controllers.md`, `repositorio.md` y `puertos-y-adaptadores.md`. Es un ejemplo de documentación: no existe en el código de la plantilla.

El único contexto que la plantilla trae en `src/` es `ServiceInfo`, un contexto liviano de solo lectura que expone el endpoint de información del servicio. Su proyecto `Domain/` existe y está referenciado por `ServiceInfo.Application`, pero solo como andamiaje: las carpetas (`Aggregates/`, `Entities/`, `Enums/`, `Errors/`, `ValueObjects/`) están vacías con `.gitkeep`, porque los datos que expone vienen de configuración y no hay invariantes que modelar. Los ejemplos con nombres reales (`AcademicProgram`, `Audit`) provienen de servicios ya implementados sobre esta plantilla y se citan cuando ilustran una decisión concreta.


---

## Flujo de un request HTTP

```
HTTP Request
    │
    ▼
Controller.Action(InputDto)                      ← casos de uso inyectados por constructor
    │  [ValidateRequest] — FluentValidation antes de entrar al use case
    ▼
UseCase.ExecuteAsync(input, cancellationToken)
    ├── input.ToAggregate()
    │       └── Aggregate.Create(args)
    │               └── ValueObject.Create()  (por cada VO)
    ├── Lectura auxiliar (Reader / Provider), si aplica
    ├── repository.AddAsync(aggregate)  /  repository.CreateAsync(aggregate)
    │       └── Mapper.ToDocument(aggregate) → entidad de persistencia
    └── unitOfWork.CommitAsync()        (salvo que el repositorio ya haya confirmado)
    │
    ▼
HTTP Response (201 / 200 / 4xx / 5xx)
```

Cada paso retorna un `Result`; ninguno lanza excepción. El use case sella con `Context` y `Origin` únicamente los errores que él o el dominio originan, y propaga sin tocar los que ya vienen sellados por el repositorio, un Reader o el Unit of Work — ver [casos-de-uso.md](casos-de-uso.md#7-propagación-de-errores-context-y-origin).


---

## Ver también

- [puertos-y-adaptadores.md](puertos-y-adaptadores.md) — qué son, driving vs. driven, y nomenclatura de casos de uso, repositorios, ports y adaptadores
- [patron-result.md](patron-result.md) — patrón transversal de manejo de errores
- [repositorio.md](repositorio.md) — repositorio, entidad de persistencia + mapper, relaciones por navegación
- [conceptos-reader-provider-repository.md](conceptos-reader-provider-repository.md) — Reader vs. Provider vs. Repository
- [providers.md](providers.md) — cuándo y cómo extraer lógica auxiliar de un use case a un Provider
- [contextos.md](contextos.md) — qué es un bounded context y flujo completo para implementar uno nuevo
