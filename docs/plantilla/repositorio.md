# Patrón de Repositorio

El proyecto implementa **Clean Architecture** combinada con principios de **Domain-Driven Design (DDD)** y **Arquitectura Hexagonal**; la capa de persistencia se conecta al dominio mediante repositorios (el contrato de persistencia del Aggregate) y adaptadores concretos. Para una visión completa de la arquitectura de capas, ver [arquitectura.md](arquitectura.md); para por qué el repositorio no se llama "Port", ver [puertos-y-adaptadores.md](puertos-y-adaptadores.md#2-por-qué-el-repositorio-no-es-un-port).


---

## Estructura de capas

```
src/
├── Shared/
│   ├── Domain/
│   │   ├── Interfaces/           # IRootRepository, IAggregateRoot
│   │   └── Pagination/           # PageQuery
│   ├── Results/                  # Result<T>, PagedResult<T>
│   └── Application/
│       ├── Ports/                # IUnitOfWorkPort
│       └── Dtos/                 # PageQueryInputDto
│
├── Contexts/
│   └── Product/
│       └── Domain/
│           ├── Repositories/     # IProductRepository
│           └── Queries/          # ProductFilter — objeto de filtro del contexto
│
└── Infrastructure/
    ├── Persistence/EntityFramework/
    │   ├── ApplicationDbContext.cs
    │   ├── Common/
    │   │   ├── PersistenceErrors.cs
    │   │   └── RepositoryBaseEF.cs   # Repositorio genérico (ver nota más abajo)
    │   └── Products/
    │       ├── ProductRepository.cs        # implementación del repositorio
    │       ├── Entities/Product.cs         # entidad de persistencia (fila de la tabla)
    │       ├── Configurations/ProductConfiguration.cs
    │       └── Mappers/ProductRepositoryMapper.cs
    └── Adapters/
        └── Persistence/
            └── UnitOfWorkAdapter.cs
```

Nótese que **la implementación del repositorio no vive en `Adapters/`**: vive junto a su entidad, su configuración y su mapper, dentro de `Persistence/EntityFramework/{Contexto}/`. En `Adapters/Persistence/` solo queda lo que es transversal a todos los contextos (`UnitOfWorkAdapter`, `SqlServer/SqlServerErrorClassifier`). Ver [Ubicación y naming del repositorio](#ubicación-y-naming-del-repositorio).


---

## Componentes del patrón de repositorio

### `IRootRepository<TAggregate, TId>`

```csharp
// Shared/Domain/Interfaces/IRootRepository.cs
public interface IRootRepository<TAggregate, TId>
    where TAggregate : IAggregateRoot
    where TId : notnull
{
    Task<Result<TAggregate>>      GetByIdAsync(TId id, CancellationToken cancellationToken = default);
    Task<Result<bool>>            ExistsAsync(TId id, CancellationToken cancellationToken = default);
    Task<PagedResult<TAggregate>> GetAllAsync(PageQuery page, CancellationToken cancellationToken = default);
    Task<Result>                  AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);
    Result                        Update(TAggregate aggregate);
    Task<Result>                  RemoveAsync(TId id, CancellationToken cancellationToken = default);
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
    Task<Result<bool>> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<PagedResult<ProductAggregate>> GetAsync(
        ProductFilter filter, PageQuery page, CancellationToken cancellationToken = default);

    Task<Result<ProductAggregate>> CreateAsync(
        ProductAggregate aggregate, CancellationToken cancellationToken = default);
}
```

Los añadidos frecuentes son `GetAsync(filter, page)` para el listado filtrado del contexto y `CreateAsync` cuando el `INSERT` debe confirmarse dentro del repositorio (ver [más abajo](#createasync--cuando-el-insert-debe-confirmarse-dentro-del-repositorio)).

### Ubicación y naming del repositorio

| | Regla |
|---|---|
| Nombre de la clase | `{Aggregate}Repository` — **sin** sufijo `Adapter` |
| Ubicación | `Infrastructure/Persistence/EntityFramework/{Contexto}/{Aggregate}Repository.cs` |
| Qué implementa | Directamente `I{Contexto}Repository` (que a su vez extiende `IRootRepository<TAggregate, TId>`) |
| `Origin` | `private const string Origin = nameof({Aggregate}Repository)` |

El repositorio **no** va en `Infrastructure/Adapters/` ni termina en `Adapter`. Aunque conceptualmente sea el adaptador del puerto de persistencia, en la práctica es una pieza de EF Core inseparable de su entidad, su `IEntityTypeConfiguration<>` y su mapper — y vive con ellos. `Adapters/Persistence/` queda para lo transversal (`UnitOfWorkAdapter`, `SqlServer/SqlServerErrorClassifier`).

La misma regla aplica a los **Readers** del contexto: `Infrastructure/Persistence/EntityFramework/{Contexto}/{Concepto}Reader.cs`. Ver [conceptos-reader-provider-repository.md](conceptos-reader-provider-repository.md).

### El agregado no es la entidad de EF Core — entidad de persistencia + mapper

Los servicios son **Database First** sobre esquemas heredados: los nombres de tabla y de columna, la nulabilidad real y las columnas que el dominio no modela no se pueden imponer desde el agregado. Por eso el repositorio trabaja con **dos tipos distintos**:

| Tipo | Dónde vive | Qué es |
|---|---|---|
| `{Aggregate}` (ej. `ProgramAggregate`) | `Contexts/{Contexto}/Domain/Aggregates/` | El modelo de negocio, con invariantes y factories |
| `{Entidad}` (ej. `Program`) | `Infrastructure/Persistence/EntityFramework/{Contexto}/Entities/` | La fila de la tabla: propiedades públicas mutables, sin reglas |

y un mapper estático que traduce en ambos sentidos:

```csharp
// Infrastructure/Persistence/EntityFramework/AcademicPrograms/Mappers/ProgramRepositoryMapper.cs
public static class ProgramRepositoryMapper
{
    public static ProgramAggregate ToDomain(Entities.Program document) =>
        ProgramAggregate.Reconstruct(document.Code, document.Name, document.IsActive /* … */);

    public static Entities.Program ToDocument(ProgramAggregate aggregate) =>
        new()
        {
            Code = aggregate.Id,
            Name = aggregate.Name,
            IsActive = aggregate.IsActive,

            // La columna es NOT NULL y el dominio no la modela: el valor pertenece a persistencia.
            AvailableInJobOffer = AvailableInJobOfferOnCreate,
            // …
        };
}
```

Convenciones:

- Naming del mapper: `{Aggregate}RepositoryMapper`, en `.../{Contexto}/Mappers/`. Métodos `ToDomain(...)` y `ToDocument(...)`.
- La lectura usa `ProgramAggregate.Reconstruct(...)`, nunca `Create(...)`: los datos persistidos ya son válidos y no se re-validan ([entidades-y-agregados.md](entidades-y-agregados.md)).
- Las columnas que existen en la tabla pero no en el agregado (auditoría legacy, flags fuera de alcance) se **mapean en la entidad** y las rellena el mapper — quitarlas del modelo rompería el `INSERT` en columnas `NOT NULL`.
- La entidad refleja la **nulabilidad real** de la base de datos, no la deseada: leer un `NULL` en una propiedad no anulable hace que SqlClient lance `SqlNullValueException` para la query entera. La tolerancia vive en la entidad y en el mapper.

### `RepositoryBaseEF<TAggregate, TId>` — solo para agregados que sí son la entidad

La plantilla incluye `Infrastructure/Persistence/EntityFramework/Common/RepositoryBaseEF.cs`, una implementación genérica de `IRootRepository` que asume que `TAggregate : AggregateRoot<TId>` es el tipo que EF Core mapea directamente (`context.Set<TAggregate>()`).

Con el patrón de entidad de persistencia separada esa premisa no se cumple, así que **ningún repositorio de los servicios levantados hasta hoy hereda de `RepositoryBaseEF`**: cada uno implementa `IRootRepository` a mano. La clase se conserva para un escenario Code First, donde el agregado sí puede ser la entidad mapeada. Si la usas:

* `GetAllAsync` usa `GroupBy(x => 1)` para obtener el total y los items en una sola query.
* `AddAsync` solo hace `DbSet.AddAsync`; el commit ocurre en `UnitOfWorkAdapter`.
* `RemoveAsync` solo marca el agregado para borrado; el commit también ocurre en `UnitOfWorkAdapter`.
* Todos los métodos capturan excepciones y retornan `PersistenceErrors.Failure(Origin)`.
* `Origin` es `GetType().Name`, así que el error reporta la clase concreta, no la base.
* `GetNotFoundError(TId)` es `virtual` para que el contexto devuelva su propio `NotFoundError`.

### Repositorio concreto — `ProgramRepository`

Implementa el contrato del contexto directamente. Cada método envuelve su query en `try/catch`, loguea y devuelve un `Result`; nunca deja escapar una excepción al caso de uso:

```csharp
// Infrastructure/Persistence/EntityFramework/AcademicPrograms/ProgramRepository.cs
public sealed class ProgramRepository(
    ApplicationDbContext context,
    ILoggerPort<ProgramRepository> logger) : IProgramRepository
{
    private const string Origin = nameof(ProgramRepository);

    private readonly DbSet<Program> _programs = context.Set<Program>();

    public async Task<Result<ProgramAggregate>> GetByIdAsync(
        string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await _programs
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Code == id, cancellationToken)
                .ConfigureAwait(false);

            if (document is null)
                return ProgramErrors.NotFound(id) with { Origin = Origin };

            return ProgramRepositoryMapper.ToDomain(document);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error retrieving Program with code {Code}", id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    // GetAsync(filter, page), ExistsAsync, CreateAsync, AddAsync, Update, RemoveAsync…
}
```

Puntos a respetar:

- **El repositorio estampa `Origin` en sus propios errores.** El caso de uso los propaga tal cual, sin reescribirlos — ver [casos-de-uso.md](casos-de-uso.md#7-propagación-de-errores-context-y-origin).
- Las lecturas van con `AsNoTracking()`.
- Los `OrderBy` de listados paginados deben desempatar con una columna única (típicamente la clave), o `OFFSET/FETCH` puede repetir o saltar filas entre páginas.
- Un método de `IRootRepository` que el contexto no puede servir con seguridad no se implementa a medias: se responde con un `InternalError` explícito y un `logger.Warning`, documentando por qué (ejemplo real: `ProgramRepository.GetAllAsync`, que no puede aplicar el alcance por persona y rol que exige el listado).

### `CreateAsync` — cuando el `INSERT` debe confirmarse dentro del repositorio

`IRootRepository.AddAsync` solo encola el `INSERT`; el `SaveChangesAsync` lo hace el Unit of Work. Eso no sirve cuando el caso de uso necesita el valor que genera la base de datos (una `IDENTITY`) o cuando el `INSERT` debe clasificar sus propios errores de constraint. Para esos casos el contrato del contexto agrega `CreateAsync`, que persiste y devuelve el agregado ya completo:

```csharp
public async Task<Result<AuditLogEntryAggregate>> CreateAsync(
    AuditLogEntryAggregate aggregate, CancellationToken cancellationToken = default)
{
    try
    {
        var entity = aggregate.ToDocument();
        await context.AuditLogs.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // La IDENTITY se puebla después de SaveChanges; se devuelve al agregado.
        aggregate.AssignId(entity.Id);
        return aggregate;
    }
    catch (DbUpdateException ex)
    {
        logger.Error(ex, "Database error inserting audit log entry.");
        return SqlServerErrorClassifier.Classify(ex, Origin);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        logger.Error(ex, "Error inserting audit log entry.");
        return PersistenceErrors.Failure(Origin);
    }
}
```

Un caso de uso que persiste con `CreateAsync` **no** inyecta `IUnitOfWorkPort` ni llama a `CommitAsync`: el commit ya ocurrió. `AddAsync` sigue existiendo para las operaciones que sí participan de una transacción mayor coordinada por el Unit of Work.

### Relaciones: se modelan por navegación

Las relaciones entre entidades de persistencia se declaran en el `IEntityTypeConfiguration<>` con una **propiedad de navegación** y su clave foránea, no consultando por código suelto:

```csharp
// Infrastructure/Persistence/EntityFramework/AcademicPrograms/Entities/ProgramAdministrative.cs
public sealed class ProgramAdministrative
{
    public int Id { get; set; }
    public string? ProgramCode { get; set; }
    public string? PersonCode { get; set; }
    public Program? Program { get; set; }      // ← navegación
}
```

```csharp
// …/Configurations/ProgramAdministrativeConfiguration.cs
builder.HasOne(p => p.Program)
    .WithMany()
    .HasForeignKey(p => p.ProgramCode)
    .OnDelete(DeleteBehavior.Restrict);
```

Reglas:

- **Navegación en un solo lado por defecto.** `HasOne(x => x.Padre).WithMany()` sin colección inversa: el agregado no necesita ver a sus referenciadores y la colección inversa invita a cargas accidentales. Se declara la colección (`HasMany(x => x.Hijos).WithOne()`) solo cuando el repositorio la va a materializar con `Include`.
- **`OnDelete(DeleteBehavior.Restrict)` por defecto** en esquemas heredados. El `DeleteBehavior` por convención de EF (`Cascade`) haría que EF asuma un borrado en cascada que la base de datos real no tiene; `Restrict` evita que EF invente ese plan.
- **`HasOne<T>().WithMany()` sin navegación** cuando la FK existe pero ninguna de las dos entidades necesita ver a la otra (p. ej. una auto-referencia de tipo "padre").
- Si el esquema legacy **no tiene FK real** y la relación se declara solo para poder hacer `Include`, decláralo en un comentario junto a la configuración y borra los hijos explícitamente en el repositorio — EF no ordenará los `DELETE` por ti si no conoce la dependencia.
- Cargar hijos es `Include(...)` sobre la navegación, no una segunda query manual:

```csharp
var document = await context.Set<SubjectEntity>()
    .AsNoTracking()
    .Include(s => s.EvaluationParameters)
    .FirstOrDefaultAsync(s => s.Code == id, cancellationToken)
    .ConfigureAwait(false);
```

> No configurar restricciones de base de datos (`HasMaxLength`, `IsRequired`) como si fueran validación: el proyecto es Database First y las validaciones viven en el dominio y en la presentación. En la configuración se declaran únicamente para que EF genere el tipo de parámetro correcto contra el esquema real (`varchar` vs `nvarchar`, longitudes, `IsFixedLength`).

### Unit of Work — `UnitOfWorkAdapter`

Único responsable de llamar `SaveChangesAsync`. Clasifica errores de SQL:

```csharp
// Infrastructure/Adapters/Persistence/UnitOfWorkAdapter.cs
public sealed class UnitOfWorkAdapter(ApplicationDbContext context, ILoggerPort<UnitOfWorkAdapter> logger)
    : IUnitOfWorkPort
{
    private const string Origin = nameof(UnitOfWorkAdapter);

    public async Task<Result> CommitAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
IGetProductsUseCase.ExecuteAsync(filter, page)
        ↓
IProductRepository.GetAsync(filter, page)
        ↓
SELECT … ORDER BY <columna> , <clave>  -- desempate obligatorio
OFFSET page.Skip ROWS FETCH NEXT page.PageSize ROWS ONLY
COUNT(*) para el total
        ↓
Mapper.ToDomain(entidad)  por cada fila
        ↓
PagedResult<ProductAggregate> { Items, TotalCount }
        ↓
PagedResult<GetProductsOutputDto> { Items, TotalCount }
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
| 547                 | Conflicto con un constraint | `Conflict` |
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

**Cuando el duplicado tiene un mensaje propio,** el repositorio pregunta primero y delega el resto:

```csharp
catch (DbUpdateException ex)
{
    logger.Error(ex, "Database error creating Product with code {Code}", aggregate.Id);

    if (SqlServerErrorClassifier.IsUniqueViolation(ex))
        return ProductErrors.CodeAlreadyExists(aggregate.Id) with { Origin = Origin };

    return SqlServerErrorClassifier.Classify(ex, Origin);
}
```

`IsUniqueViolation` no expone los números al llamador: el repositorio solo sabe "esto fue un duplicado" y decide si puede nombrar el valor culpable. Si no tiene un mensaje mejor que el genérico, se omite el `if` y se llama directo a `Classify`.

**El 547 no dice qué constraint falló.** SQL Server lo levanta igual para FOREIGN KEY, REFERENCE y CHECK, así que el mensaje que devuelve el clasificador no puede prometer más que "conflicto con un registro relacionado". Si un endpoint necesita nombrar el valor culpable — *"la clasificación 9 no existe"* —, eso se valida **en el caso de uso**, con una consulta de existencia previa; no se adivina desde el número del error. Traducir el 547 dentro del repositorio parece más corto, pero atribuye a una FK concreta cualquier violación de constraint de la tabla, y empieza a mentir en cuanto haya una segunda. Por eso el clasificador expone `IsUniqueViolation` (2627/2601 sí identifican el duplicado) y deliberadamente **no** un predicado equivalente para el 547.


---

## Registro de dependencias

| Tipo | Lifetime | Por qué |
|------|----------|---------|
| Casos de uso (`IXxxUseCase`) | `Scoped` | Un caso de uso por request HTTP |
| Repositorios (`IXxxRepository`) | `Scoped` | Comparten el mismo `DbContext` del request |
| Readers (`IXxxReader`) | `Scoped` | Mismo `DbContext` del request que el repositorio |
| Providers (tipo concreto, sin interfaz) | `Scoped` | Dependen de repositorios `Scoped` |
| `Port` específico de contexto (`IXxxPort`) | `Scoped` | Normalmente depende de servicios `Scoped` (opciones, HTTP client, etc.) |
| `IUnitOfWorkPort` | `Scoped` | Mismo `DbContext` que los repositorios |
| `ILoggerPort<T>` | `Singleton` | Serilog es thread-safe |
| Validadores (`IRequestValidatorPort<T>`) | `Scoped` | Registrado automáticamente via reflection |

Los validadores de FluentValidation se registran automáticamente en `ValidatorRegistrationExtensions` escaneando todas las clases que implementan `IStructuralValidator<T>`.

Dentro del `Add{Contexto}Services` el orden es: primero repositorio y readers, después los casos de uso que los consumen.

```csharp
// Api/DependencyInjection/AcademicProgramServiceExtensions.cs
services.AddScoped<IProgramRepository, ProgramRepository>();
services.AddScoped<IProgramClassificationReader, ProgramClassificationReader>();

services.AddScoped<IGetProgramsUseCase, GetProgramsUseCase>();
services.AddScoped<ICreateProgramUseCase, CreateProgramUseCase>();
// …
```


---

## Ver también

* [patron-result.md](patron-result.md) — jerarquía completa de tipos Result y errores de dominio
* [conceptos-reader-provider-repository.md](conceptos-reader-provider-repository.md) — cuándo es Repository y cuándo un Reader
* [entidades-y-agregados.md](entidades-y-agregados.md) — `Create()` / `Reconstruct()` y los records de argumentos
* [validaciones.md](validaciones.md) — mapa de las cinco capas de validación
* [puertos-y-adaptadores.md](puertos-y-adaptadores.md) — por qué el repositorio no se llama "Port", y nomenclatura completa
* [contextos.md](contextos.md) — guía paso a paso para implementar un nuevo contexto
