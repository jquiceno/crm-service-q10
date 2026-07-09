# Patrón de Repositorio

El proyecto implementa **Clean Architecture** combinada con principios de **Domain-Driven Design (DDD)** y **Arquitectura Hexagonal**; la capa de persistencia se conecta al dominio mediante puertos y adaptadores. Para una visión completa de la arquitectura de capas, ver [arquitectura.md](arquitectura.md).


---

## Estructura de capas

```
src/
├── Shared/
│   ├── Domain/
│   │   ├── Interfaces/           # IRootRepository, IAggregateRoot
│   │   ├── Result/               # Result<T>, PagedResult<T>
│   │   └── Pagination/           # PageQuery
│   └── Application/
│       ├── Ports/                # IUnitOfWorkPort
│       └── Dtos/                 # PageQueryInputDto
│
├── Contexts/
│   └── WeatherForecast/
│       └── Domain/
│           └── Ports/            # IWeatherForecastRepositoryPort
│
└── Infrastructure/
    ├── Persistence/EntityFramework/
    │   ├── ApplicationDbContext.cs
    │   ├── Common/
    │   │   └── RepositoryBaseEF.cs   # Repositorio genérico
    │   └── WeatherForecast/Configurations/
    └── Adapters/
        └── Persistence/
            ├── UnitOfWorkAdapter.cs
            └── WeatherForecast/WeatherForecastRepositoryAdapter.cs
```


---

## Componentes del patrón de repositorio

### `IRootRepository<TAggregate, TId>`

```csharp
// Shared/Domain/Interfaces/IRootRepository.cs
public interface IRootRepository<TAggregate, TId>
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

### Puerto de repositorio del contexto

Extiende `IRootRepository` con queries específicas del dominio:

```csharp
// Contexts/WeatherForecast/Domain/Ports/IWeatherForecastRepositoryPort.cs
public interface IWeatherForecastRepositoryPort : IRootRepository<WeatherForecastAggregate, Guid>
{
    Task<Result<bool>> ExistsForDateAsync(DateTime date, CancellationToken ct = default);
}
```

### Repositorio genérico — `RepositoryBaseEF<TAggregate, TId>`

Implementación base en infraestructura. El agregado **es** la entidad que EF Core mapea directamente, por lo que no hay conversiones intermedias. Maneja CRUD + paginación:

```csharp
// Infrastructure/Persistence/EntityFramework/Common/RepositoryBaseEF.cs
public abstract class RepositoryBaseEF<TAggregate, TId>(
    ApplicationDbContext context, ILoggerPort<object> logger)
    : IRootRepository<TAggregate, TId>
    where TAggregate : AggregateRoot<TId>
    where TId        : notnull
{
    protected DbSet<TAggregate> DbSet { get; } = context.Set<TAggregate>();

    // Overridable si el contexto tiene su propio error "not found"
    protected virtual NotFoundError GetNotFoundError(TId id)
        => SharedErrors.NotFound(typeof(TAggregate).Name, id!);
}
```

* `GetAllAsync` usa `GroupBy(x => 1)` para obtener el total y los items en una sola query.
* `AddAsync` solo hace `DbSet.AddAsync`; el commit ocurre en `UnitOfWorkAdapter`.
* Todos los métodos capturan excepciones y retornan `PersistenceErrors.Failure()`.

### Adaptador concreto — `WeatherForecastRepositoryAdapter`

Hereda de `RepositoryBaseEF` e implementa las queries extras del contexto:

```csharp
// Infrastructure/Adapters/Persistence/WeatherForecast/WeatherForecastRepositoryAdapter.cs
public sealed class WeatherForecastRepositoryAdapter(
    ApplicationDbContext context,
    ILoggerPort<WeatherForecastRepositoryAdapter> logger)
    : RepositoryBaseEF<WeatherForecastAggregate, Guid>(context, logger),
      IWeatherForecastRepositoryPort
{
    protected override NotFoundError GetNotFoundError(Guid id)
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

### Unit of Work — `UnitOfWorkAdapter`

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

## Result Pattern y manejo de errores

> Ver también: [patron-result.md](patron-result.md) — jerarquía completa de tipos Result y errores de dominio.

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
|-----------|-------------|
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

## Paginación

### `PageQuery` — parámetros de consulta (dominio)

Objeto de dominio que encapsula los parámetros de paginación. Vive en `Shared.Domain.Pagination`:

```csharp
public sealed record PageQuery(int PageIndex, int PageSize)
{
    public int Skip => PageIndex * PageSize;
}
```

### `PageQueryInputDto` — entrada HTTP (aplicación)

DTO de entrada para endpoints paginados. Vive en `Shared.Application.Dtos`. Se valida automáticamente por `PageQueryInputValidator` al usar `[ValidateRequest]`:

```csharp
public sealed record PageQueryInputDto(int PageIndex = 0, int PageSize = 20)
{
    public const int MaxPageSize = 100;
}
```

Reglas de validación:

* `PageIndex >= 0`
* `PageSize` entre `1` y `PageQueryInputDto.MaxPageSize` (100)

### Flujo de paginación de extremo a extremo

```
GET /api/v1/weatherforecast?pageIndex=0&pageSize=20
        ↓
[FromQuery] PageQueryInputDto  →  [ValidateRequest] → 400 si inválido
        ↓
new PageQuery(input.PageIndex, input.PageSize)
        ↓
IGetWeatherForecastPort.ExecuteAsync(page)
        ↓
IWeatherForecastRepositoryPort.GetAllAsync(page)
        ↓
SELECT … OFFSET page.Skip ROWS FETCH NEXT page.PageSize ROWS ONLY
COUNT(*) para el total
        ↓
PagedResult<WeatherForecastAggregate> { Items, TotalCount }
        ↓
PagedResult<GetWeatherForecastOutputDto> { Items, TotalCount }
        ↓
{ data: { items: [...], totalCount: N }, statusCode: 200 }
```

Para agregar paginación a un nuevo endpoint basta con:

1. Recibir `[FromQuery] PageQueryInputDto pagination` en el action
2. Añadir `[ValidateRequest]` al action
3. Construir `new PageQuery(pagination.PageIndex, pagination.PageSize)` y pasarlo al use case


---

## Unit of Work

### `IUnitOfWorkPort`

La persistencia de cambios está separada del repositorio para respetar el patrón Unit of Work:

```csharp
public interface IUnitOfWorkPort
{
    Task<Result> CommitAsync(CancellationToken cancellationToken = default);
}
```

`CommitAsync` captura tanto `DbUpdateException` (clasificada por `SqlServerErrorClassifier`) como cualquier otra excepción de infraestructura. La cancelación se deja propagar:

```csharp
public async Task<Result> CommitAsync(CancellationToken cancellationToken = default)
{
    try   { await context.SaveChangesAsync(cancellationToken); return Result.Success(); }
    catch (DbUpdateException ex)                              { return SqlServerErrorClassifier.Classify(ex); }
    catch (Exception ex) when (ex is not OperationCanceledException) { return PersistenceErrors.Failure(); }
}
```

### Clasificación de errores de SQL Server

`SqlServerErrorClassifier` (interno a infraestructura) traduce `SqlException.Number` a errores de dominio semánticos sin exponer mensajes del servidor:

| `SqlException.Number` | Causa | `ErrorType` |
|---------------------|-------|-----------|
| 2627                | Violación de PRIMARY KEY | `Conflict` |
| 2601                | Fila duplicada en índice único | `Conflict` |
| 547                 | Violación de FOREIGN KEY | `Conflict` |
| 515                 | INSERT de NULL en columna NOT NULL | `Validation` |
| 8152                | Valor excede la longitud máxima | `Validation` |
| 1205                | Víctima de deadlock | `Internal` |
| otros               | Error genérico de persistencia | `Internal` |


---

## Registro de dependencias

| Tipo | Lifetime | Por qué |
|------|----------|---------|
| Use Cases (`IXxxPort`) | `Scoped` | Un caso de uso por request HTTP |
| Repositorios (`IXxxRepositoryPort`) | `Scoped` | Comparten el mismo `DbContext` del request |
| `IUnitOfWorkPort` | `Scoped` | Mismo `DbContext` que los repositorios |
| `ILoggerPort<T>` | `Singleton` | Serilog es thread-safe |
| Validadores (`IRequestValidatorPort<T>`) | `Scoped` | Registrado automáticamente via reflection |

Los validadores de FluentValidation se registran automáticamente en `ValidatorRegistrationExtensions` escaneando todas las clases que implementan `IStructuralValidator<T>`.


---

## Ver también

* [patron-result.md](patron-result.md) — jerarquía completa de tipos Result y errores de dominio
* [validaciones.md](validaciones.md) — mapa de las cinco capas de validación
* [guias/nuevo-contexto.md](guias/nuevo-contexto.md) — guía paso a paso para implementar un nuevo contexto
