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
│   ├── Results/                      # HttpOkResult, HttpCreatedResult, HttpOkPagedResult
│   ├── Responses/                    # Estructura uniforme de respuesta API
│   ├── Middleware/                   # GlobalExceptionMiddleware, RequestLoggingMiddleware
│   ├── Mapping/                      # ErrorType → HTTP status code
│   └── Attributes/                   # [ValidateRequest]
│
├── Shared/
│   ├── Domain/                       # Primitivos reutilizables entre contextos
│   │   ├── Aggregates/               # AggregateRoot<TEntity, TId>
│   │   ├── Entities/                 # Entity<TId> con CreatedAt / UpdatedAt
│   │   ├── ValueObjects/             # ValueObject (base abstracta), Address
│   │   ├── Interfaces/               # IRepositoryBase<T>, IAggregateRoot
│   │   ├── Result/                   # Result<T>, PagedResult<T>
│   │   ├── Errors/                   # DomainError, ValidationError, ErrorType, SharedErrors
│   │   └── Pagination/               # PageQuery
│   └── Application/
│       ├── Ports/                    # IUnitOfWorkPort, ILoggerPort<T>, IRequestValidatorPort<T>
│       └── Dtos/                     # PageQueryInputDto, AddressInputDto/OutputDto
│
├── Contexts/                         # Bounded Contexts — uno por dominio de negocio
│   └── {Contexto}/
│       ├── Domain/
│       │   ├── Aggregates/           # {Contexto}Aggregate
│       │   ├── Entities/             # {Contexto}Entity
│       │   ├── ValueObjects/         # VOs exclusivos del contexto
│       │   ├── Ports/                # I{Contexto}RepositoryPort
│       │   └── Errors/               # {Contexto}Errors
│       └── Application/
│           ├── Ports/                # I{Acción}{Contexto}Port (driving ports)
│           ├── Providers/            # Application services de resolución auxiliar
│           └── UseCases/
│               └── {NombreUseCase}/  # UseCase + InputDto + OutputDto + Mapping
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
    │       ├── Common/               # BaseAggregateRepository<TAggregate, TEntity, TId>
    │       └── {Contexto}/Configurations/
    ├── Extensions/                   # SerilogExtensions, SentryExtensions, EfCorePersistenceExtensions
    ├── Settings/                     # POCOs de configuración tipada
    └── Logging/                      # FlatJsonFormatter, ActivityEnricher, LogContextExtensions
```


---

## Bounded Context de ejemplo

La plantilla incluye `WeatherForecast` como contexto de referencia. Todos los documentos y guías usan este contexto (o `Product` como contexto secundario en ejemplos) para mostrar los patrones implementados.

Al crear un nuevo servicio, `WeatherForecast` puede eliminarse o mantenerse como referencia.


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

- [puertos-y-adaptadores.md](puertos-y-adaptadores.md) — nomenclatura de puertos, adaptadores y extensiones DI
- [patron-result.md](patron-result.md) — patrón transversal de manejo de errores
- [providers.md](providers.md) — cuándo y cómo extraer lógica auxiliar de un use case a un Provider
- [guias/nuevo-contexto.md](guias/nuevo-contexto.md) — flujo completo para implementar un nuevo contexto
