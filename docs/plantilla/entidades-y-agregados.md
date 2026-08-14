# Entidades y Agregados

## Jerarquía de tipos

```
Entity<TId>
└── AggregateRoot<TId>   ← heredado por cada agregado de contexto
```

Todos los tipos base viven en `Shared.Domain`.


---

## `Entity<TId>` — clase base de entidades

Define lo mínimo que hace a algo una entidad en DDD: identidad y la igualdad que se deriva de ella. No incluye auditoría ni ningún concepto de ciclo de vida de persistencia — eso es responsabilidad de `AggregateRoot<TId>` (ver abajo).

```csharp
public abstract class Entity<TId> where TId : notnull
{
    public TId Id { get; protected set; } = default!;

    protected Entity() { }

    // igualdad por Id
    public override bool Equals(object? obj) =>
        obj is Entity<TId> entity && EqualityComparer<TId>.Default.Equals(Id, entity.Id);
    public override int GetHashCode() => Id?.GetHashCode() ?? 0;
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => Equals(left, right);
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !Equals(left, right);
}
```

| Elemento | Descripción |
|----------|-------------|
| `Id` | Clave de identidad; define igualdad |
| Igualdad | Por `Id`, no por valor de propiedades |


---

## `AggregateRoot<TId>` — clase base de agregados

```csharp
public abstract class AggregateRoot<TId> : Entity<TId>, IAggregateRoot
    where TId : notnull
{
    public DateTime? CreatedAt { get; protected set; }
    public DateTime? UpdatedAt { get; protected set; }

    protected void SetCreatedAt(DateTime dateTime) => CreatedAt = dateTime;
    protected void SetUpdatedAt(DateTime dateTime) => UpdatedAt = dateTime;

    protected abstract void Created();
}
```

| Elemento | Descripción |
|----------|-------------|
| `Entity<TId>` | El agregado **es** una entidad; hereda `Id` e igualdad |
| `CreatedAt` | Asignado en `Created()` al crear el agregado; `null` si se reconstruye desde persistencia sin el campo |
| `UpdatedAt` | `null` hasta la primera actualización |
| `SetCreatedAt(dt)` | Llamado dentro de `Created()` al crear un agregado nuevo |
| `SetUpdatedAt(dt)` | Llamado dentro del método de mutación del agregado, antes de `repository.Update()` |
| `IAggregateRoot` | Interfaz marcadora; permite referenciar agregados sin conocer sus tipos concretos |
| `Created()` | Método abstracto **obligatorio**. Solo se llama desde el factory `Create()`, nunca desde `Reconstruct()` |

> `CreatedAt`/`UpdatedAt` viven en `AggregateRoot` y no en `Entity` porque la auditoría es un concepto del límite transaccional (el agregado como unidad de persistencia), no de cualquier entidad con identidad. Una entidad hija dentro de un agregado no tiene su propio ciclo de vida de auditoría — hereda el del agregado.

> El constructor del agregado concreto se declara `private`. Solo los métodos factory `Create()` y `Reconstruct()` pueden instanciarlo.


---

## Anatomía de un agregado concreto

```csharp
public sealed class ProductAggregate : AggregateRoot<Guid>
{
    // Propiedades propias del agregado
    public string  Name  { get; private set; } = string.Empty;
    public decimal Price { get; private set; }

    // Constructor privado — solo accesible desde los factories
    private ProductAggregate(Guid id, string name, decimal price)
    {
        Id    = id;
        Name  = name;
        Price = price;
    }

    // Factory para creación nueva — recibe un record de argumentos y llama Created()
    public static Result<ProductAggregate> Create(CreateProductArgs input)
    {
        var errors = new List<ValidationError>();

        // ... construir y validar VOs a partir de los primitivos de input, acumulando errores ...

        if (errors.Count > 0)
            return DomainError.FromValidationDomainErrors(errors);

        var aggregate = new ProductAggregate(Guid.NewGuid(), input.Name, input.Price);
        aggregate.Created();
        return aggregate;
    }

    // Factory para reconstrucción desde persistencia — sin validaciones ni auditoría
    public static ProductAggregate Reconstruct(Guid id, string name, decimal price)
        => new(id, name, price);

    // Implementación obligatoria de Created()
    protected override void Created()
    {
        SetCreatedAt(DateTime.UtcNow);
        SetUpdatedAt(DateTime.UtcNow);
    }

    // Mutación — marca la fecha de actualización
    public Result Update(UpdateProductArgs input)
    {
        // ... validar y actualizar campos ...
        Name  = input.Name;
        Price = input.Price;
        SetUpdatedAt(DateTime.UtcNow);
        return Result.Success();
    }
}
```


---

## Args: records de argumentos de los factories

Los factories del agregado (`Create`, `Update`) **no reciben una lista de primitivos sueltos**: reciben un `record` de argumentos declarado en el mismo contexto, `Domain/Aggregates/{Contexto}Args.cs`.

```csharp
// Contexts/AcademicProgram/Domain/Aggregates/ProgramArgs.cs
public sealed record CreateProgramArgs(
    string? Code,
    string Name,
    bool IsActive,
    string? Abbreviation = null,
    string? ResolutionNumber = null,
    DateTime? ResolutionDate = null,
    EvaluationType? EvaluationType = null,
    int? ClassificationId = null);

public sealed record UpdateProgramArgs(
    string Name,
    bool IsActive,
    string? Abbreviation = null,
    string? ResolutionNumber = null,
    DateTime? ResolutionDate = null,
    int? ClassificationId = null);
```

Reglas:

- **Los Args llevan solo primitivos** (y enums del dominio). Nunca Value Objects: el `Create` los construye por dentro, y así el llamador —la capa de aplicación— no necesita conocer los tipos de dominio ni manejar sus `Result`.
- **Un record por operación**: `Create{Contexto}Args` y `Update{Contexto}Args` son distintos, porque los campos inmutables tras la creación (un código asignado por el cliente, un tipo de evaluación que se escribe una sola vez) están en el primero y no en el segundo.
- Ambos viven en `Domain/Aggregates/`, junto al agregado que los consume; pueden compartir archivo (`{Contexto}Args.cs`).
- El mapping del caso de uso es quien los construye, desde el DTO de entrada: `input.ToAggregate()` / `input.ToUpdateArgs()`.

```csharp
public static Result<ProgramAggregate> Create(CreateProgramArgs input)
{
    var codeResult = ProgramCode.Create(input.Code);       // ← el VO se crea aquí dentro

    if (codeResult.IsFailure)
        return DomainError.FromValidationDomainErrors(
            [codeResult.TypedError with { Value = input.Code }]);

    var aggregate = new ProgramAggregate(name: input.Name, isActive: input.IsActive /* … */)
    {
        Id = codeResult.Value.Value
    };

    aggregate.Created();

    return aggregate;
}
```

Añadir un campo al agregado se reduce a añadir una propiedad al record: la firma del factory no cambia y ningún llamador se rompe.

---

## `Create()` vs `Reconstruct()`

| Método | Cuándo usarlo | Llama `Created()` |
|--------|---------------|:-----------------:|
| `Create(args)` | Nueva entidad de negocio | Sí |
| `Reconstruct(...)` | Reconstruir desde persistencia o lectura de BD | No |

- `Create()` dispara la lógica de inicialización del dominio: asigna `CreatedAt`, `UpdatedAt` y cualquier evento de dominio.
- `Reconstruct()` solo reensambla el estado ya existente; los datos ya son válidos y las fechas las trae la BD. **Quien lo invoca es el mapper del repositorio** (`{Aggregate}RepositoryMapper.ToDomain`), al traducir la entidad de persistencia al agregado — ver [repositorio.md](repositorio.md#el-agregado-no-es-la-entidad-de-ef-core--entidad-de-persistencia--mapper).
- A diferencia de `Create`, `Reconstruct` recibe los valores sueltos (con defaults para los opcionales), no un record de argumentos: no valida nada y su único llamador es el mapper.
- Un valor persistido que el dominio no sabe interpretar (un código fuera del catálogo, por ejemplo) se mapea a `null` en lugar de lanzar: `Reconstruct` no valida estado persistido.


---

## Auditoría: cuándo llamar a `SetUpdatedAt()`

`SetUpdatedAt()` debe llamarse **dentro del método de mutación del agregado**, antes de que el Use Case llame a `repository.Update()`:

```csharp
// En el agregado
public Result Update(UpdateProductArgs input)
{
    Name  = input.Name;
    Price = input.Price;
    SetUpdatedAt(DateTime.UtcNow);   // <-- aquí, antes de salir del dominio
    return Result.Success();
}

// En el Use Case
var updateResult = aggregate.Update(input.ToUpdateArgs());
if (updateResult.IsFailure)
    return updateResult.Error with { Context = ProductErrors.Context, Origin = Origin };

repository.Update(aggregate);
```

`CreatedAt` se asigna una sola vez en `Created()` y nunca se modifica.


---

## Ver también

- [value-objects.md](value-objects.md) — Value Objects que viven dentro del agregado
- [errores-dominio.md](errores-dominio.md) — cómo definir y acumular errores de dominio
- [repositorio.md](repositorio.md) — `IRootRepository`, `RepositoryBaseEF`, Unit of Work
- [contextos.md](contextos.md) — flujo paso a paso para modelar el dominio de un contexto nuevo
