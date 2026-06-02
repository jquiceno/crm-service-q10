# Entidades y Agregados

## Jerarquía de tipos

```
Entity
└── Entity<TId>
        └── (implementada por cada entidad de contexto)

AggregateRoot<TEntity, TId>   ← wrappea una Entity<TId>
        └── (implementado por cada agregado de contexto)
```

Todos los tipos base viven en `Shared.Domain`.


---

## `Entity<TId>` — clase base de entidades

```csharp
public abstract class Entity<TId> : Entity where TId : notnull
{
    public TId      Id            { get; set; } = default!;
    protected Entity() { }

    // igualdad por Id
    public override bool Equals(object? obj) =>
        obj is Entity<TId> entity && EqualityComparer<TId>.Default.Equals(Id, entity.Id);
    public override int GetHashCode() => Id?.GetHashCode() ?? 0;
    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) => Equals(left, right);
    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !Equals(left, right);
}
```

La clase `Entity` no genérica aporta los campos de auditoría:

```csharp
public abstract class Entity
{
    public DateTime  CreatedAtUtc  { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc  { get; private set; }
    public void SetUpdatedAtUtc() => UpdatedAtUtc = DateTime.UtcNow;
}
```

| Elemento | Descripción |
|----------|-------------|
| `Id` | Clave de identidad; define igualdad |
| `CreatedAtUtc` | Se asigna automáticamente al construir la entidad |
| `UpdatedAtUtc` | `null` hasta la primera actualización |
| `SetUpdatedAtUtc()` | El agregado lo llama antes de `repository.Update()` |
| Igualdad | Por `Id`, no por valor de propiedades |

> `Entity<TId>` tiene el constructor `protected`. En cada contexto el constructor de la entidad se declara `internal` para que solo la infraestructura pueda reconstruirla.


---

## `AggregateRoot<TEntity, TId>` — clase base de agregados

```csharp
public abstract class AggregateRoot<TEntity, TId> : IAggregateRoot
    where TEntity : Entity<TId>
    where TId : notnull
{
    protected TEntity Entity { get; }

    protected AggregateRoot(TEntity entity)
    {
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));
    }

    public TId Id => Entity.Id;
}
```

| Elemento | Descripción |
|----------|-------------|
| `Entity` | La entidad que wrappea; `protected` — solo accesible desde el agregado |
| `Id` | Delegado a `Entity.Id` |
| Constructor | `protected`; lanza `ArgumentNullException` si la entidad es null |
| `IAggregateRoot` | Interfaz marcadora; permite referenciar agregados sin conocer sus tipos concretos |

> El constructor del agregado concreto se declara `private`. Solo los métodos factory `Create()` y `FromEntity()` pueden instanciarlo.


---

## Anatomía de un agregado concreto

```csharp
public sealed class ProductAggregate : AggregateRoot<ProductEntity, Guid>
{
    // Propiedades expuestas desde la entidad
    public string  Name  => Entity.Name;
    public decimal Price => Entity.Price;

    // Constructor privado — solo accesible desde los factories
    private ProductAggregate(ProductEntity entity) : base(entity) { }

    // Factory para creación nueva
    public static Result<ProductAggregate> Create(string name, decimal price)
    {
        var errors = new List<ValidationError>();

        // ... validar VOs y acumular errores ...

        if (errors.Count > 0)
            return DomainError.FromValidationDomainErrors(errors);

        var entity = new ProductEntity
        {
            Id    = Guid.NewGuid(),
            Name  = name,
            Price = price
        };
        return new ProductAggregate(entity);
    }

    // Factory para reconstrucción desde persistencia
    public static ProductAggregate FromEntity(ProductEntity entity)
        => new(entity);

    // Conversión inversa para persistencia
    public ProductEntity ToEntity() => Entity;

    // Mutación — marca la fecha de actualización
    public Result Update(string name, decimal price)
    {
        // ... validar y actualizar campos en Entity ...
        Entity.SetUpdatedAtUtc();
        return Result.Success();
    }
}
```


---

## Relación entre entidad y agregado

```
[Base de datos]
      │
      ▼
  ProductEntity          ← modelo de persistencia (EF Core)
      │
      ▼
  ProductAggregate       ← cara pública del dominio
      │                     expone propiedades, contiene métodos Create/Update
      ▼
  [Use Case / Repository]
```

- La **entidad** es el modelo que EF Core mapea a la tabla.
- El **agregado** es lo que el resto de la aplicación ve; oculta los detalles de la entidad.
- El repositorio recibe y devuelve **agregados**; usa `ToEntity()` / `FromEntity()` internamente.

### `ToAggregate` y `ToEntity` en el repositorio

`BaseAggregateRepository` declara dos métodos abstractos que cada repositorio concreto implementa:

```csharp
protected abstract TAggregate ToAggregate(TEntity entity);
protected abstract TEntity    ToEntity(TAggregate aggregate);
```

```csharp
// Implementación típica en ProductRepository
protected override ProductAggregate ToAggregate(ProductEntity entity)
    => ProductAggregate.FromEntity(entity);

protected override ProductEntity ToEntity(ProductAggregate aggregate)
    => aggregate.ToEntity();
```


---

## Auditoría: cuándo llamar a `SetUpdatedAtUtc()`

`SetUpdatedAtUtc()` debe llamarse **dentro del método de mutación del agregado**, antes de que el Use Case llame a `repository.Update()`:

```csharp
// En el agregado
public Result Update(string name, decimal price)
{
    Entity.Name  = name;
    Entity.Price = price;
    Entity.SetUpdatedAtUtc();   // <-- aquí, antes de salir del dominio
    return Result.Success();
}

// En el Use Case
var updateResult = aggregate.Update(input.Name, input.Price);
if (updateResult.IsFailure) return updateResult.Error ...;

var repoResult = repository.Update(aggregate);
```

`CreatedAtUtc` se asigna automáticamente en el constructor de `Entity` y nunca se modifica.


---

## Ver también

- [value-objects.md](value-objects.md) — Value Objects que viven dentro del agregado
- [errores-dominio.md](errores-dominio.md) — cómo definir y acumular errores de dominio
- [repositorio.md](repositorio.md) — `IRepositoryBase`, `BaseAggregateRepository`, Unit of Work
- [guias/nueva-entidad-dominio.md](guias/nueva-entidad-dominio.md) — flujo paso a paso para modelar el dominio de un contexto
