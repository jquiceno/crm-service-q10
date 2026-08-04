# Patrón de Repositorio

El proyecto implementa **Clean Architecture** combinada con principios de **Domain-Driven Design (DDD)** y **Arquitectura Hexagonal**; la capa de persistencia se conecta al dominio mediante repositorios (el contrato de persistencia del Aggregate) y adaptadores concretos. Para una visión completa de la arquitectura de capas, ver [arquitectura.md](arquitectura.md); para por qué el repositorio no se llama "Port", ver [puertos-y-adaptadores.md](puertos-y-adaptadores.md#2-por-qué-el-repositorio-no-es-un-port).


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
│   └── Product/
│       └── Domain/
│           └── Repositories/     # IProductRepository
│
└── Infrastructure/
    ├── Persistence/EntityFramework/
    │   ├── ApplicationDbContext.cs
    │   ├── Common/
    │   │   └── RepositoryBaseEF.cs   # Repositorio genérico
    │   └── Product/Configurations/
    └── Adapters/
        └── Persistence/
            ├── UnitOfWorkAdapter.cs
            └── Product/ProductRepositoryAdapter.cs
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
    Task<Result<bool>>            ExistsAsync(TId id, CancellationToken ct = default);
    Task<PagedResult<TAggregate>> GetAllAsync(PageQuery page, CancellationToken ct = default);
    Task<Result>                  AddAsync(TAggregate aggregate, CancellationToken ct = default);
    Result                        Update(TAggregate aggregate);
    Task<Result>                  RemoveAsync(TId id, CancellationToken ct = default);
}
```

Todos los métodos retornan `Result` — nunca lanzan excepciones al caller.

`RemoveAsync` recibe el identificador, no el agregado, y es asíncrono porque resolver ese identificador
es una llamada a base de datos. Si el agregado no existe, falla con el `NotFoundError` del contexto; el
caso de uso no necesita hacer un `GetByIdAsync` previo solo para poder borrar.

### Repositorio del contexto

Extiende `IRootRepository` con queries específicas del dominio. Vive en `Domain/Repositories/`, no en una carpeta `Ports/` — es un contrato de persistencia de Aggregate, no un `Port` (ver [puertos-y-adaptadores.md](puertos-y-adaptadores.md#2-por-qué-el-repositorio-no-es-un-port)):

```csharp
// Contexts/Product/Domain/Repositories/IProductRepository.cs
public interface IProductRepository : IRootRepository<ProductAggregate, Guid>
{
    Task<Result<bool>> ExistsByNameAsync(string name, CancellationToken ct = default);
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

    private string Origin => GetType().Name;

    // Overridable si el contexto tiene su propio error "not found"
    protected virtual NotFoundError GetNotFoundError(TId id)
        => SharedErrors.NotFound(typeof(TAggregate).Name, id!) with { Origin = Origin };
}
```

* `GetAllAsync` usa `GroupBy(x => 1)` para obtener el total y los items en una sola query.
* `AddAsync` solo hace `DbSet.AddAsync`; el commit ocurre en `UnitOfWorkAdapter`.
* `RemoveAsync` solo marca el agregado para borrado; el commit también ocurre en `UnitOfWorkAdapter`.
* Todos los métodos capturan excepciones y retornan `PersistenceErrors.Failure(Origin)`.
* `Origin` es `GetType().Name`, así que el error reporta el adaptador concreto
  (`ProductRepositoryAdapter`), no la clase base.

### Adaptador concreto — `ProductRepositoryAdapter`

Hereda de `RepositoryBaseEF` e implementa las queries extras del contexto:

```csharp
// Infrastructure/Adapters/Persistence/Product/ProductRepositoryAdapter.cs
public sealed class ProductRepositoryAdapter(
    ApplicationDbContext context,
    ILoggerPort<ProductRepositoryAdapter> logger)
    : RepositoryBaseEF<ProductAggregate, Guid>(context, logger),
      IProductRepository
{
    protected override NotFoundError GetNotFoundError(Guid id)
        => ProductErrors.NotFound(id);

    public async Task<Result<bool>> ExistsByNameAsync(string name, CancellationToken ct = default)
    {
        try
        {
            return await DbSet.AnyAsync(p => p.Name == name, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error checking product name {Name}", name);
            return PersistenceErrors.Failure(nameof(ProductRepositoryAdapter));
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
    private const string Origin = nameof(UnitOfWorkAdapter);

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
            return SqlServerErrorClassifier.Classify(ex, Origin); // Converts DB constraints into DomainErrors
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Unexpected commit error");
            return PersistenceErrors.Failure(Origin);
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
return Result.Failure(ProductErrors.NotFound(id));

// Enrich with context before propagating
return someError with { Context = ProductErrors.Context, Origin = nameof(MyUseCase) };
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
public static readonly ValidationError InvalidPrice
    = new($"Price must be greater than or equal to {Price.MinValue}.", ErrorType.Validation)
    {
        Property   = nameof(Price),             // Name of the Value Object / property
        Attributes = new Dictionary<string, object?> { ["min"] = Price.MinValue }
    };

// In an Aggregate (property that fails a business rule)
public static readonly ValidationError NameTooLong
    = new($"Name cannot exceed {MaxNameLength} characters.", ErrorType.Validation)
    {
        Property   = nameof(Name),
        Attributes = new Dictionary<string, object?> { ["maxLength"] = MaxNameLength }
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
GET /products?pageIndex=0&pageSize=20
        ↓
[FromQuery] PageQueryInputDto  →  [ValidateRequest] → 400 si inválido
        ↓
new PageQuery(input.PageIndex, input.PageSize)
        ↓
IGetAllProductsUseCase.ExecuteAsync(page)
        ↓
IProductRepository.GetAllAsync(page)
        ↓
SELECT … OFFSET page.Skip ROWS FETCH NEXT page.PageSize ROWS ONLY
COUNT(*) para el total
        ↓
PagedResult<ProductAggregate> { Items, TotalCount }
        ↓
PagedResult<GetAllProductsOutputDto> { Items, TotalCount }
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
    catch (DbUpdateException ex)                              { return SqlServerErrorClassifier.Classify(ex, Origin); }
    catch (Exception ex) when (ex is not OperationCanceledException) { return PersistenceErrors.Failure(Origin); }
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

Ambas sobrecargas reciben un `origin` que se estampa en el error devuelto, para que el log identifique qué componente lo produjo. Por convención es un `private const string Origin = nameof(MiClase)` en el llamador.

**Hay dos sobrecargas y elegir la correcta importa:**

```csharp
internal static DomainError Classify(DbUpdateException ex, string origin)
internal static DomainError Classify(SqlException ex, string origin)
```

`ExecuteDelete` / `ExecuteUpdate` no pasan por el change tracker, así que EF **no envuelve** la excepción del driver en `DbUpdateException`: lanza la `SqlException` cruda. Un repositorio que borra en bulk y solo captura `DbUpdateException` nunca entra a ese `catch`, cae en el genérico, y reporta un `Internal` (500) donde el contrato pide un `Conflict` (409). Para esos casos va la sobrecarga de `SqlException`:

```csharp
catch (SqlException ex)
{
    logger.Error(ex, "Error removing ...");
    return SqlServerErrorClassifier.Classify(ex, Origin);
}
catch (Exception ex) when (ex is not OperationCanceledException)
{
    logger.Error(ex, "Error removing ...");
    return PersistenceErrors.Failure(Origin);
}
```


---

## Registro de dependencias

| Tipo | Lifetime | Por qué |
|------|----------|---------|
| Casos de uso (`IXxxUseCase`) | `Scoped` | Un caso de uso por request HTTP |
| Repositorios (`IXxxRepository`) | `Scoped` | Comparten el mismo `DbContext` del request |
| `Port` específico de contexto (`IXxxPort`) | `Scoped` | Normalmente depende de servicios `Scoped` (opciones, HTTP client, etc.) |
| `IUnitOfWorkPort` | `Scoped` | Mismo `DbContext` que los repositorios |
| `ILoggerPort<T>` | `Singleton` | Serilog es thread-safe |
| Validadores (`IRequestValidatorPort<T>`) | `Scoped` | Registrado automáticamente via reflection |

Los validadores de FluentValidation se registran automáticamente en `ValidatorRegistrationExtensions` escaneando todas las clases que implementan `IStructuralValidator<T>`.


---

## Ver también

* [patron-result.md](patron-result.md) — jerarquía completa de tipos Result y errores de dominio
* [validaciones.md](validaciones.md) — mapa de las cinco capas de validación
* [puertos-y-adaptadores.md](puertos-y-adaptadores.md) — por qué el repositorio no se llama "Port", y nomenclatura completa
* [contextos.md](contextos.md) — guía paso a paso para implementar un nuevo contexto
