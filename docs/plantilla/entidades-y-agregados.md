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

    // Factory para creación nueva — llama Created() para inicializar auditoría
    public static Result<ProductAggregate> Create(string name, decimal price)
    {
        var errors = new List<ValidationError>();

        // ... validar VOs y acumular errores ...

        if (errors.Count > 0)
            return DomainError.FromValidationDomainErrors(errors);

        var aggregate = new ProductAggregate(Guid.NewGuid(), name, price);
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
    public Result Update(string name, decimal price)
    {
        // ... validar y actualizar campos ...
        Name  = name;
        Price = price;
        SetUpdatedAt(DateTime.UtcNow);
        return Result.Success();
    }
}
```


---

## `Create()` vs `Reconstruct()`

| Método | Cuándo usarlo | Llama `Created()` |
|--------|---------------|:-----------------:|
| `Create(...)` | Nueva entidad de negocio | Sí |
| `Reconstruct(...)` | Reconstruir desde persistencia o lectura de BD | No |

- `Create()` dispara la lógica de inicialización del dominio: asigna `CreatedAt`, `UpdatedAt` y cualquier evento de dominio.
- `Reconstruct()` solo reensambla el estado ya existente; los datos ya son válidos y las fechas las trae la BD.


---

## Auditoría: cuándo llamar a `SetUpdatedAt()`

`SetUpdatedAt()` debe llamarse **dentro del método de mutación del agregado**, antes de que el Use Case llame a `repository.Update()`:

```csharp
// En el agregado
public Result Update(string name, decimal price)
{
    Name  = name;
    Price = price;
    SetUpdatedAt(DateTime.UtcNow);   // <-- aquí, antes de salir del dominio
    return Result.Success();
}

// En el Use Case
var updateResult = aggregate.Update(input.Name, input.Price);
if (updateResult.IsFailure) return updateResult.Error;

repository.Update(aggregate);
```

`CreatedAt` se asigna una sola vez en `Created()` y nunca se modifica.


---

## Ver también

- [value-objects.md](value-objects.md) — Value Objects que viven dentro del agregado
- [errores-dominio.md](errores-dominio.md) — cómo definir y acumular errores de dominio
- [repositorio.md](repositorio.md) — `IRootRepository`, `RepositoryBaseEF`, Unit of Work
- [guias/nueva-entidad-dominio.md](guias/nueva-entidad-dominio.md) — flujo paso a paso para modelar el dominio de un contexto
