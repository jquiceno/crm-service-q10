# Patrón de Repositorio

Guía de referencia para entender la implementación y agregar nuevas features.

---

## Tabla de contenido

1. [Visión general](#1-visión-general)
2. [Estructura de capas](#2-estructura-de-capas)
3. [Terminología clave](#3-terminología-clave)
4. [Flujo de una petición](#4-flujo-de-una-petición)
5. [Componentes del patrón de repositorio](#5-componentes-del-patrón-de-repositorio)
6. [Result Pattern y manejo de errores](#6-result-pattern-y-manejo-de-errores)
7. [Paginación](#7-paginación)
8. [Implementar un nuevo contexto (feature)](#8-implementar-un-nuevo-contexto-feature)
9. [Registro de dependencias](#9-registro-de-dependencias)
10. [Ver también](#10-ver-también)

---

## 1. Visión general

El proyecto implementa **Clean Architecture** combinada con principios de **Domain-Driven Design (DDD)** y **Arquitectura Hexagonal**. La idea central es que el dominio no conoce ni depende de infraestructura: la base de datos, los frameworks y los adaptadores externos son detalles de implementación que se conectan desde afuera.

```
┌─────────────────────────────────────────────────────┐
│  API (Presentación)                                 │
│  Controllers · Filters · Results · Middleware       │
├─────────────────────────────────────────────────────┤
│  Application                                        │
│  Use Cases · Ports · DTOs · Mappings                │
├─────────────────────────────────────────────────────┤
│  Domain                                             │
│  Aggregates · Entities · Value Objects · Errors     │
├─────────────────────────────────────────────────────┤
│  Infrastructure                                     │
│  EF Core · Repositorios · UoW · Logging · Validación│
└─────────────────────────────────────────────────────┘
```

**Regla fundamental:** las dependencias apuntan siempre hacia adentro. El dominio no importa nada de infraestructura.

---

## 2. Estructura de capas

```
src/
├── Api/                          # Presentación (ASP.NET Core)
│   ├── Controllers/
│   ├── DependencyInjection/      # Registro de servicios por contexto
│   ├── Filters/                  # ValidateRequestFilter
│   ├── Results/                  # HttpOkResult, HttpCreatedResult, etc.
│   └── Mapping/                  # ErrorType → HTTP status code
│
├── Shared/
│   ├── Domain/                   # Primitivos reutilizables entre contextos
│   │   ├── Entities/             # Entity<TId>, auditoría automática
│   │   ├── Aggregates/           # AggregateRoot<TEntity, TId>
│   │   ├── Interfaces/           # IRepositoryBase, IAggregateRoot
│   │   ├── ValueObjects/         # ValueObject base
│   │   ├── Result/               # Result<T>, PagedResult<T>
│   │   ├── Errors/               # DomainError, ValidationError, ErrorType
│   │   └── Pagination/           # PageQuery
│   └── Application/
│       ├── Ports/                # IUnitOfWorkPort, ILoggerPort, IRequestValidatorPort
│       └── Dtos/                 # PageQueryInputDto, AddressInputDto/OutputDto
│
├── Contexts/                     # Bounded Contexts (uno por dominio de negocio)
│   └── WeatherForecast/
│       ├── Domain/
│       │   ├── Aggregates/
│       │   ├── Entities/
│       │   ├── ValueObjects/
│       │   ├── Ports/            # IWeatherForecastRepositoryPort
│       │   └── Errors/
│       └── Application/
│           ├── Ports/            # IGetWeatherForecastPort, ICreateWeatherForecastPort
│           └── UseCases/
│               ├── GetWeatherForecast/
│               └── CreateWeatherForecast/
│
└── Infrastructure/
    ├── Persistence/EntityFramework/
    │   ├── ApplicationDbContext.cs
    │   ├── Common/
    │   │   └── BaseAggregateRepository.cs   # Repositorio genérico
    │   └── WeatherForecast/Configurations/
    └── Adapters/
        ├── Persistence/
        │   ├── UnitOfWorkAdapter.cs
        │   └── WeatherForecast/WeatherForecastRepositoryAdapter.cs
        ├── Logging/SerilogLoggerAdapter.cs
        └── Validation/FluentRequestValidationAdapter.cs
```

---

## 3. Terminología clave

| Término | Definición |
|---|---|
| **Aggregate / Aggregate Root** | Objeto que agrupa una Entidad con su lógica de negocio. Es la única puerta de entrada para modificar el estado. Contiene los factory methods con validación. |
| **Entity** | Objeto con identidad única (`Id`). Almacena estado y tiene ciclo de vida. Hereda de `Entity<TId>` (incluye `CreatedAtUtc`, `UpdatedAtUtc`). |
| **Value Object** | Objeto sin identidad; se compara por valor (sus propiedades). Inmutable. Ejemplo: `Temperature`, `Address`. |
| **Bounded Context** | Área delimitada del dominio. Cada contexto tiene su propio lenguaje y sus propias reglas. En el código: una carpeta bajo `Contexts/`. |
| **Port (Puerto)** | Interfaz que define qué necesita o qué ofrece una capa. Existen puertos de dominio (`IRepositoryPort`) y puertos de aplicación (`IUseCasePort`). |
| **Adapter (Adaptador)** | Implementación concreta de un puerto. Vive en Infrastructure y conoce los detalles tecnológicos. |
| **Repository** | Abstracción de la capa de persistencia. Habla en términos del dominio (Aggregates), no de tablas. |
| **Unit of Work** | Coordina el commit de todos los cambios pendientes en una sola transacción (`SaveChangesAsync`). |
| **Result Pattern** | Alternativa a excepciones. Cada operación retorna `Result` (éxito o error) en lugar de lanzar. |
| **Use Case** | Clase que orquesta un caso de uso específico. Lee del repositorio, aplica reglas de negocio y persiste. |
| **DTO (Data Transfer Object)** | Objeto plano para transportar datos entre capas. `InputDto` entra, `OutputDto` sale. |

---

## 4. Flujo de una petición

El siguiente diagrama muestra el camino de `POST /api/v1/weather-forecasts`:

```
HTTP Request
    │
    ▼
Controller.Create(CreateWeatherForecastInputDto)
    │  [ValidateRequestFilter valida el DTO antes de entrar]
    ▼
CreateWeatherForecastUseCase.ExecuteAsync(input)
    ├── repository.ExistsForDateAsync(date)      ← Regla de negocio
    ├── input.ToAggregate()
    │       └── WeatherForecastAggregate.Create()
    │               ├── Temperature.Create()     ← Validación en Value Object
    │               └── Address.Create()         ← Validación en Value Object
    ├── repository.AddAsync(aggregate)           ← Marca para insertar en EF
    └── unitOfWork.CommitAsync()                 ← SaveChangesAsync
            │
            ▼
    ApplicationDbContext.SaveChangesAsync()
            │  [Actualiza UpdatedAtUtc automáticamente]
            ▼
    SQL Server / In-Memory
            │
            ▼
HTTP Response 201 Created { id, date, temperature, ... }
```

Cada paso retorna un `Result`. Si cualquier paso falla, el Use Case retorna el error con contexto enriquecido sin lanzar excepción.

---

## 5. Componentes del patrón de repositorio

### 5.1 Interfaz base — `IRepositoryBase<TAggregate, TId>`

```csharp
// Shared/Domain/Interfaces/IRepositoryBase.cs
public interface IRepositoryBase<TAggregate, TId>
    where TAggregate : IAggregateRoot
    where TId : notnull
{
    Task<Result<TAggregate>>      GetByIdAsync(TId id, CancellationToken ct = default);
    Task<PagedResult<TAggregate>> GetAllAsync(PageQuery page, CancellationToken ct = default);
    Task<Result>                  AddAsync(TAggregate aggregate, CancellationToken ct = default);
    Result                        Update(TAggregate aggregate);
    Result                        Remove(TAggregate aggregate);
}
```

Todos los métodos retornan `Result` — nunca lanzan excepciones al caller.

### 5.2 Puerto de repositorio del contexto

Extiende `IRepositoryBase` con queries específicas del dominio:

```csharp
// Contexts/WeatherForecast/Domain/Ports/IWeatherForecastRepositoryPort.cs
public interface IWeatherForecastRepositoryPort : IRepositoryBase<WeatherForecastAggregate, Guid>
{
    Task<Result<bool>> ExistsForDateAsync(DateTime date, CancellationToken ct = default);
}
```

### 5.3 Repositorio genérico — `BaseAggregateRepository<TAggregate, TEntity, TId>`

Implementación base en infraestructura. Maneja CRUD + paginación. Requiere implementar dos métodos abstractos:

```csharp
// Infrastructure/Persistence/EntityFramework/Common/BaseAggregateRepository.cs
public abstract class BaseAggregateRepository<TAggregate, TEntity, TId>(
    ApplicationDbContext context, ILoggerPort<object> logger)
    : IRepositoryBase<TAggregate, TId>
    where TAggregate : AggregateRoot<TEntity, TId>
    where TEntity    : Entity<TId>
    where TId        : notnull
{
    protected abstract TAggregate ToAggregate(TEntity entity);
    protected abstract TEntity    ToEntity(TAggregate aggregate);

    // Overridable if the context has its own "not found" error
    protected virtual DomainError GetNotFoundError(TId id)
        => SharedErrors.NotFound(typeof(TAggregate).Name, id!);
}
```

- `GetAllAsync` usa `GroupBy(x => 1)` para obtener el total y los items en una sola query.
- `AddAsync` solo hace `DbSet.AddAsync`; el commit ocurre en `UnitOfWorkAdapter`.
- Todos los métodos capturan excepciones y retornan `PersistenceErrors.Failure()`.

### 5.4 Adaptador concreto — `WeatherForecastRepositoryAdapter`

Hereda de `BaseAggregateRepository` e implementa las conversiones y queries extras:

```csharp
// Infrastructure/Adapters/Persistence/WeatherForecast/WeatherForecastRepositoryAdapter.cs
public sealed class WeatherForecastRepositoryAdapter(
    ApplicationDbContext context,
    ILoggerPort<WeatherForecastRepositoryAdapter> logger)
    : BaseAggregateRepository<WeatherForecastAggregate, WeatherForecastEntity, Guid>(context, logger),
      IWeatherForecastRepositoryPort
{
    protected override WeatherForecastAggregate ToAggregate(WeatherForecastEntity entity)
        => WeatherForecastAggregate.FromEntity(entity);

    protected override WeatherForecastEntity ToEntity(WeatherForecastAggregate aggregate)
        => aggregate.ToEntity();

    protected override DomainError GetNotFoundError(Guid id)
        => WeatherForecastErrors.NotFound(id);

    public async Task<Result<bool>> ExistsForDateAsync(DateTime date, CancellationToken ct = default)
    {
        try
        {
            var start = date.Date;
            var end   = start.AddDays(1);
            return await DbSet.AnyAsync(e => e.Date >= start && e.Date < end, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error checking forecast for date {Date}", date);
            return PersistenceErrors.Failure();
        }
    }
}
```

### 5.5 Unit of Work — `UnitOfWorkAdapter`

Único responsable de llamar `SaveChangesAsync`. Clasifica errores de SQL:

```csharp
// Infrastructure/Adapters/Persistence/UnitOfWorkAdapter.cs
public sealed class UnitOfWorkAdapter(ApplicationDbContext context, ILoggerPort<UnitOfWorkAdapter> logger)
    : IUnitOfWorkPort
{
    public async Task<Result> CommitAsync(CancellationToken ct = default)
    {
        try
        {
            await context.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (DbUpdateException ex)
        {
            logger.Error(ex, "Database update error");
            return SqlServerErrorClassifier.Classify(ex); // Converts DB constraints into DomainErrors
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Unexpected commit error");
            return PersistenceErrors.Failure();
        }
    }
}
```

---

## 6. Result Pattern y manejo de errores

### Tipos de Result

```csharp
Result                     // Success/failure with no value
Result<T>                  // Success with a value of type T
Result<TValue, TError>     // Success with T + typed error access
PagedResult<T>             // Paginated list + total count
```

### Crear y propagar errores

```csharp
// Return success
return Result.Success();
return Result<MyAggregate>.Success(aggregate);

// Return error
return Result.Failure(WeatherForecastErrors.NotFound(id));

// Enrich with context before propagating
return someError with { Context = WeatherForecastErrors.Context, Origin = nameof(MyUseCase) };
```

### Tipos de error (`ErrorType`)

| ErrorType | HTTP Status |
|---|---|
| `Validation` | 400 Bad Request |
| `DomainError` | 400 Bad Request |
| `NotFound` | 404 Not Found |
| `Conflict` | 409 Conflict |
| `Unauthorized` | 401 Unauthorized |
| `Forbidden` | 403 Forbidden |
| `Internal` | 500 Internal Server Error |

### Definir errores de un contexto

Centralizar todos los errores del contexto en una clase estática:

```csharp
// Contexts/MyContext/Domain/Errors/MyContextErrors.cs
public static class MyContextErrors
{
    public const string Context = "MyContext";

    public static DomainError NotFound(Guid id)
        => new($"Entity with id '{id}' was not found.", ErrorType.NotFound);

    public static readonly ValidationError NameRequired
        = new("Name is required.", ErrorType.Validation)
        {
            Property = "Name"   // Identifies the field that failed — always set on ValidationError
        };
}
```

### Campo `Property` en `ValidationError`

`ValidationError` expone el campo `Property` para que el cliente sepa exactamente qué campo es inválido. **Siempre debe asignarse** al definir un `ValidationError`, ya sea en un Value Object o en el Aggregate:

```csharp
// In a Value Object
public static readonly ValidationError InvalidTemperature
    = new($"Temperature must be between {MinCelsius} and {MaxCelsius}.", ErrorType.Validation)
    {
        Property   = nameof(Temperature),             // Name of the Value Object / property
        Attributes = new Dictionary<string, object?>
        {
            ["min"] = MinCelsius,
            ["max"] = MaxCelsius
        }
    };

// In an Aggregate (property that fails a business rule)
public static readonly ValidationError SummaryTooLong
    = new($"Summary cannot exceed {MaxSummaryLength} characters.", ErrorType.Validation)
    {
        Property   = nameof(Summary),
        Attributes = new Dictionary<string, object?> { ["maxLength"] = MaxSummaryLength }
    };
```

`Attributes` es opcional pero recomendado cuando hay parámetros relevantes (límites, longitudes máximas, etc.) — el cliente puede usarlos sin parsear el mensaje de error.

---

## 7. Paginación

### Entrada

```csharp
// Shared/Application/Dtos/PageQueryInputDto.cs
public sealed record PageQueryInputDto(int PageIndex = 0, int PageSize = 20)
{
    public const int MaxPageSize = 100;
}
```

El controller recibe `PageQueryInputDto` y lo convierte en `PageQuery`:

```csharp
new PageQuery(pagination.PageIndex, pagination.PageSize)
```

### `PageQuery`

```csharp
public sealed class PageQuery(int pageIndex, int pageSize)
{
    public int PageIndex { get; }
    public int PageSize  { get; }
    public int Skip => PageIndex * PageSize;   // Used directly in LINQ
}
```

### Salida

```csharp
PagedResult<T>.Success(items, totalCount)
// Serialized as: { items: [...], totalCount: N }
```

---

## 8. Implementar un nuevo contexto (feature)

Guía paso a paso usando `Product` como ejemplo.

### Paso 1 — Dominio (`Contexts/Product/Domain/`)

**1a. Value Objects necesarios:**

```csharp
// Contexts/Product/Domain/ValueObjects/Price.cs
public sealed class Price : ValueObject
{
    public const decimal MinValue = 0;

    public decimal Value { get; }

    private Price(decimal value) => Value = value;

    public static Result<Price, ValidationError> Create(decimal value)
    {
        if (value < MinValue)
            return ProductErrors.InvalidPrice;
        return new Price(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

**1b. Entidad:**

```csharp
// Contexts/Product/Domain/Entities/ProductEntity.cs
// The generic parameter of Entity<TId> is the primary key type of the entity (e.g. Guid, int, string)
public sealed class ProductEntity : Entity<Guid>
{
    public string Name  { get; private set; }
    public Price  Price { get; private set; }

    internal ProductEntity(Guid id, string name, Price price)
    {
        Id    = id;
        Name  = name;
        Price = price;
    }
}
```

**1c. Aggregate Root** (contiene la lógica de creación con validación):

```csharp
// Contexts/Product/Domain/Aggregates/ProductAggregate.cs
public sealed class ProductAggregate : AggregateRoot<ProductEntity, Guid>
{
    public string  Name  => _entity.Name;
    public decimal Price => _entity.Price.Value;

    private ProductAggregate(ProductEntity entity) : base(entity) { }

    public static Result<ProductAggregate> Create(Guid id, string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ProductErrors.NameRequired with
                { Context = ProductErrors.Context, Origin = nameof(Create) };

        var priceResult = Price.Create(price);
        if (priceResult.IsFailure)
            return priceResult.TypedError with
                { Context = ProductErrors.Context, Origin = nameof(Create) };

        var entity = new ProductEntity(id, name, priceResult.Value);
        return new ProductAggregate(entity);
    }

    public ProductEntity           ToEntity()                      => _entity;
    public static ProductAggregate FromEntity(ProductEntity entity) => new(entity);
}
```

**1d. Puerto de repositorio:**

```csharp
// Contexts/Product/Domain/Ports/IProductRepositoryPort.cs
public interface IProductRepositoryPort : IRepositoryBase<ProductAggregate, Guid>
{
    Task<Result<bool>> ExistsByNameAsync(string name, CancellationToken ct = default);
}
```

**1e. Errores:**

```csharp
// Contexts/Product/Domain/Errors/ProductErrors.cs
public static class ProductErrors
{
    public const string Context = "Product";

    public static DomainError NotFound(Guid id)
        => new($"Product with id '{id}' was not found.", ErrorType.NotFound);

    public static readonly ValidationError NameRequired
        = new("Product name is required.", ErrorType.Validation)
        {
            Property = nameof(ProductAggregate.Name)   // Always set Property on ValidationError
        };

    public static readonly ValidationError InvalidPrice
        = new($"Price must be greater than or equal to {Price.MinValue}.", ErrorType.Validation)
        {
            Property   = nameof(ProductAggregate.Price),
            Attributes = new Dictionary<string, object?> { ["min"] = Price.MinValue }
        };
}
```

---

### Paso 2 — Aplicación (`Contexts/Product/Application/`)

**2a. Puertos de casos de uso:**

```csharp
// Contexts/Product/Application/Ports/ICreateProductPort.cs
public interface ICreateProductPort
{
    Task<Result<CreateProductOutputDto>> ExecuteAsync(
        CreateProductInputDto input, CancellationToken ct = default);
}
```

**2b. DTOs + Mapping:**

```csharp
// Contexts/Product/Application/UseCases/CreateProduct/CreateProductInputDto.cs
public sealed record CreateProductInputDto(string? Name, decimal Price);

// Contexts/Product/Application/UseCases/CreateProduct/CreateProductOutputDto.cs
public sealed record CreateProductOutputDto(Guid Id, string Name, decimal Price, DateTime CreatedAt);

// Contexts/Product/Application/UseCases/CreateProduct/CreateProductMapping.cs
public static class CreateProductMapping
{
    public static Result<ProductAggregate> ToAggregate(this CreateProductInputDto input)
        => ProductAggregate.Create(Guid.NewGuid(), input.Name!, input.Price);

    public static CreateProductOutputDto ToOutputDto(this ProductAggregate aggregate)
        => new(aggregate.Id, aggregate.Name, aggregate.Price, aggregate.CreatedAtUtc);
}
```

**2c. Use Case:**

```csharp
// Contexts/Product/Application/UseCases/CreateProduct/CreateProductUseCase.cs
public sealed class CreateProductUseCase(
    IProductRepositoryPort repository,
    IUnitOfWorkPort unitOfWork) : ICreateProductPort
{
    private const string Origin = nameof(CreateProductUseCase);

    public async Task<Result<CreateProductOutputDto>> ExecuteAsync(
        CreateProductInputDto input, CancellationToken ct = default)
    {
        var existsResult = await repository.ExistsByNameAsync(input.Name!, ct);
        if (existsResult.IsFailure)
            return existsResult.Error with { Context = ProductErrors.Context, Origin = Origin };
        if (existsResult.Value)
            return ProductErrors.NameRequired with { Context = ProductErrors.Context, Origin = Origin };

        var aggregateResult = input.ToAggregate();
        if (aggregateResult.IsFailure)
            return aggregateResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        var addResult = await repository.AddAsync(aggregateResult.Value, ct);
        if (addResult.IsFailure)
            return addResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        var commitResult = await unitOfWork.CommitAsync(ct);
        if (commitResult.IsFailure)
            return commitResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        return aggregateResult.Value.ToOutputDto();
    }
}
```

---

### Paso 3 — Infraestructura

**3a. Configuración EF Core:**

```csharp
// Infrastructure/Persistence/EntityFramework/Product/Configurations/ProductEntityConfiguration.cs
public sealed class ProductEntityConfiguration : IEntityTypeConfiguration<ProductEntity>
{
    public void Configure(EntityTypeBuilder<ProductEntity> builder)
    {
        builder.OwnsOne(e => e.Price, p =>
        {
            p.Property(x => x.Value).HasColumnName("Price");
        });
    }
}
```

Agregar el `DbSet` al contexto:

```csharp
// ApplicationDbContext.cs
public DbSet<ProductEntity> Products => Set<ProductEntity>();
```

**3b. Adaptador de repositorio:**

```csharp
// Infrastructure/Adapters/Persistence/Product/ProductRepositoryAdapter.cs
public sealed class ProductRepositoryAdapter(
    ApplicationDbContext context,
    ILoggerPort<ProductRepositoryAdapter> logger)
    : BaseAggregateRepository<ProductAggregate, ProductEntity, Guid>(context, logger),
      IProductRepositoryPort
{
    protected override ProductAggregate ToAggregate(ProductEntity entity)
        => ProductAggregate.FromEntity(entity);

    protected override ProductEntity ToEntity(ProductAggregate aggregate)
        => aggregate.ToEntity();

    protected override DomainError GetNotFoundError(Guid id)
        => ProductErrors.NotFound(id);

    public async Task<Result<bool>> ExistsByNameAsync(string name, CancellationToken ct = default)
    {
        try
        {
            return await DbSet.AnyAsync(e => e.Name == name, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error checking product name {Name}", name);
            return PersistenceErrors.Failure();
        }
    }
}
```

---

### Paso 4 — Presentación (`Api/`)

**4a. Controller:**

```csharp
// Api/Controllers/ProductController.cs
[ApiController]
[Route("api/v1/[controller]")]
public sealed class ProductController : ControllerBase
{
    [HttpPost]
    [Tags("products")]
    [ValidateRequest]
    public async Task<HttpCreatedResult<CreateProductOutputDto>> Create(
        [FromBody] CreateProductInputDto input,
        ICreateProductPort createProduct,
        CancellationToken ct)
        => await createProduct.ExecuteAsync(input, ct);
}
```

**4b. Registro de dependencias:**

```csharp
// Api/DependencyInjection/ProductServiceExtensions.cs
public static class ProductServiceExtensions
{
    public static IServiceCollection AddProductServices(this IServiceCollection services)
    {
        services.AddScoped<ICreateProductPort, CreateProductUseCase>();
        services.AddScoped<IProductRepositoryPort, ProductRepositoryAdapter>();
        return services;
    }
}
```

Llamar desde `ApplicationServiceExtensions`:

```csharp
// Api/DependencyInjection/ApplicationServiceExtensions.cs
public static IServiceCollection AddApplicationServices(this IServiceCollection services)
{
    services.AddSharedServices();
    services.AddWeatherForecastServices();
    services.AddProductServices();   // ← add here
    return services;
}
```

---

## 9. Registro de dependencias

| Tipo | Lifetime | Por qué |
|---|---|---|
| Use Cases (`IXxxPort`) | `Scoped` | Un caso de uso por request HTTP |
| Repositorios (`IXxxRepositoryPort`) | `Scoped` | Comparten el mismo `DbContext` del request |
| `IUnitOfWorkPort` | `Scoped` | Mismo `DbContext` que los repositorios |
| `ILoggerPort<T>` | `Singleton` | Serilog es thread-safe |
| Validadores (`IRequestValidatorPort<T>`) | `Scoped` | Registrado automáticamente via reflection |

Los validadores de FluentValidation se registran automáticamente en `ValidatorRegistrationExtensions` escaneando todas las clases que implementan `IStructuralValidator<T>`.

---

## 10. Ver también

- Nomenclatura de Puertos y Adaptadores — nomenclatura de puertos, adaptadores y extensiones DI
- Validaciones — dónde van — dónde vive cada tipo de validación y regla de negocio
- Patrón Result — `Result<T>`, `ValidationError`, propagación de errores
- Value Objects — cuándo crear un Value Object y su anatomía
