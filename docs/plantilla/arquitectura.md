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
│   ├── Filters/                      # ValidateRequestFilter, OutputCacheInvalidateAttribute
│   ├── Results/                      # HttpOkResult, HttpCreatedResult, HttpNoContentResult, HttpOkPagedResult
│   ├── Responses/                    # Estructura uniforme de respuesta API
│   ├── Middleware/                   # GlobalExceptionMiddleware, RequestLoggingMiddleware
│   ├── Mapping/                      # ErrorType → HTTP status code
│   └── Attributes/                   # [ValidateRequest]
│
├── Shared/
│   ├── Domain/                       # Primitivos reutilizables entre contextos
│   │   ├── Aggregates/               # AggregateRoot<TId> con CreatedAt / UpdatedAt
│   │   ├── Entities/                 # Entity<TId> (Id + igualdad)
│   │   ├── ValueObjects/             # ValueObject (base abstracta), Address
│   │   ├── Interfaces/               # IRootRepository<TAggregate, TId>, IAggregateRoot
│   │   ├── Result/                   # Result<T>, PagedResult<T>
│   │   ├── Errors/                   # DomainError, ValidationError, ErrorType, SharedErrors
│   │   └── Pagination/               # PageQuery
│   └── Application/
│       ├── Ports/                    # IUnitOfWorkPort, ILoggerPort<T>, IRequestValidatorPort<T> — compartidos
│       └── Dtos/                     # PageQueryInputDto, AddressInputDto/OutputDto
│
├── Contexts/                         # Bounded Contexts — uno por dominio de negocio
│   └── {Contexto}/
│       ├── Domain/
│       │   ├── Aggregates/           # {Contexto}Aggregate — el agregado ES la entidad
│       │   ├── ValueObjects/         # VOs exclusivos del contexto
│       │   ├── Repositories/         # I{Contexto}Repository (persistencia del Aggregate)
│       │   └── Errors/               # {Contexto}Errors
│       └── Application/
│           ├── Ports/                # I{Capacidad}Port — capacidad externa del contexto, no persistencia (opcional)
│           ├── Providers/            # Application services de resolución auxiliar
│           └── UseCases/
│               └── {NombreUseCase}/  # I{NombreUseCase}UseCase + UseCase + InputDto + OutputDto + Mapping
│
└── Infrastructure/
    ├── Adapters/
    │   ├── Persistence/
    │   │   ├── UnitOfWorkAdapter.cs
    │   │   └── {Contexto}/           # {Contexto}RepositoryAdapter
    │   ├── Logging/                  # SerilogLoggerAdapter<T>
    │   └── Validation/               # FluentRequestValidationAdapter<T>
    ├── Persistence/
    │   └── EntityFramework/
    │       ├── ApplicationDbContext.cs
    │       ├── Common/               # RepositoryBaseEF<TAggregate, TId>
    │       └── {Contexto}/Configurations/
    ├── Extensions/                   # SerilogExtensions, SentryExtensions, EfCorePersistenceExtensions
    ├── Settings/                     # POCOs de configuración tipada
    └── Logging/                      # FlatJsonFormatter, ActivityEnricher, LogContextExtensions
```


---

## Bounded Context de ejemplo

Todos los documentos de esta carpeta usan `Product` (`Name: string`, `Price: decimal`) como contexto de ejemplo para mostrar los patrones implementados — es el mismo contexto en `casos-de-uso.md`, `contextos.md`, `controllers.md`, `repositorio.md` y `puertos-y-adaptadores.md`.

La plantilla también incluye `WeatherForecast` como contexto scaffold real dentro del código (no como ejemplo de esta documentación). Al crear un nuevo servicio, `WeatherForecast` puede eliminarse o mantenerse como referencia de arranque.


---

## Flujo de un request HTTP

```
HTTP Request
    │
    ▼
Controller.Action(InputDto)
    │  [ValidateRequest] — FluentValidation antes de entrar al use case
    ▼
UseCase.ExecuteAsync(input, ct)
    ├── Precondición de negocio (consulta al repositorio)
    ├── input.ToAggregate()
    │       └── Aggregate.Create()
    │               └── ValueObject.Create()  (por cada VO)
    ├── repository.AddAsync(aggregate)
    └── unitOfWork.CommitAsync()
    │
    ▼
HTTP Response (201 / 200 / 4xx / 5xx)
```

Cada paso retorna un `Result`. Si cualquier paso falla, el use case retorna el error enriquecido con `Context` y `Origin` sin lanzar excepción.


---

## Ver también

- [puertos-y-adaptadores.md](puertos-y-adaptadores.md) — qué son, driving vs. driven, y nomenclatura de casos de uso, repositorios, ports y adaptadores
- [patron-result.md](patron-result.md) — patrón transversal de manejo de errores
- [providers.md](providers.md) — cuándo y cómo extraer lógica auxiliar de un use case a un Provider
- [contextos.md](contextos.md) — qué es un bounded context y flujo completo para implementar uno nuevo
