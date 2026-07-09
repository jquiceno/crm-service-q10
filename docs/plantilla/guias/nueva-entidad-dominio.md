# Guía: nueva entidad de dominio

Pasos para modelar el dominio de un bounded context: qué necesita Value Object, cómo se crea la entidad y cómo se construye el aggregate root.

> Esta guía cubre solo el dominio. Para el flujo completo (aplicación + infraestructura + API), ver [nuevo-contexto.md](nuevo-contexto.md).

El ejemplo usa `Product` con dos propiedades: `Name (string, max 200 chars)` y `Price (decimal, >= 0)`.

---

## Paso 1 — Decidir qué necesita Value Object

Antes de escribir código, determinar qué propiedades merecen un Value Object y cuáles pueden ser primitivos.

```
¿La propiedad tiene lógica de negocio más allá de Required/MaxLength?
    │
    ├── NO  →  Primitivo. Validar en el DTO con FluentValidation.
    │           Ejemplo: Name (solo NotEmpty + MaxLength → primitivo string)
    │
    └── SÍ  →  Value Object.
                   Ejemplo: Price (rango >= 0 con significado de negocio)
                   │
                   ├── ¿Lo usan varios contextos?
                   │       SÍ  →  src/Shared/Domain/ValueObjects/
                   │       NO  →  src/Contexts/<Contexto>/Domain/ValueObjects/
                   │
                   └── Instanciar solo desde Aggregate.Create()
```

Resultado para `Product`:

| Propiedad | Decisión | Motivo |
|-----------|----------|--------|
| `Name`    | Primitivo `string` | Solo requiere `NotEmpty` + `MaxLength(200)` — sin invariantes de negocio |
| `Price`   | Value Object `Price` | Tiene una regla de negocio: el precio no puede ser negativo |

`Name` se validará en el DTO de entrada con FluentValidation. `Price` se validará en el Value Object y se acumulará en el aggregate.

---

## Paso 2 — Errores del contexto

Crear `Contexts/Product/Domain/Errors/ProductErrors.cs` **antes** que los Value Objects y el Aggregate, porque ambos los necesitan al compilar.

```csharp
// Contexts/Product/Domain/Errors/ProductErrors.cs
using Shared.Domain.Errors;
using Product.Domain.Aggregates;
using Product.Domain.ValueObjects;

namespace Product.Domain.Errors;

public static class ProductErrors
{
    public const string Context = "Product";

    public static DomainError NotFound(Guid id)
        => new($"Product with id '{id}' was not found.", ErrorType.NotFound);

    public static readonly ValidationError NameRequired
        = new("Product name is required.", ErrorType.Validation)
        {
            Property = nameof(ProductAggregate.Name)
        };

    public static readonly ValidationError InvalidPrice
        = new($"Price must be greater than or equal to {Price.MinValue}.", ErrorType.Validation)
        {
            Property   = nameof(ProductAggregate.Price),
            Attributes = new Dictionary<string, object?> { ["min"] = Price.MinValue }
        };
}
```

### Cuándo usar `static readonly` vs método de fábrica

| Forma | Cuándo usarla | Ejemplo |
|-------|---------------|---------|
| `static readonly ValidationError` | El mensaje es fijo; no depende de ningún valor en tiempo de ejecución | `NameRequired`, `InvalidPrice` |
| Método de fábrica `static DomainError Method(...)` | El mensaje incluye un valor conocido solo en runtime, como un `id` | `NotFound(Guid id)` |

La distinción es importante: los campos `static readonly` se crean una sola vez al inicializar la clase. Si el mensaje necesita interpolar un valor que llega del exterior (`id`, `name`, etc.), debe ser un método que construya un nuevo `DomainError` cada vez.

---

## Paso 3 — Value Objects

Crear el Value Object para cada propiedad que lo requiera según la decisión del Paso 1.

```csharp
// Contexts/Product/Domain/ValueObjects/Price.cs
using Shared.Domain.Errors;
using Shared.Domain.Result;
using Shared.Domain.ValueObjects;
using Product.Domain.Errors;

namespace Product.Domain.ValueObjects;

public sealed class Price : ValueObject
{
    public const decimal MinValue = 0m;

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

Puntos clave:

- La clase es `sealed`: los Value Objects no se heredan.
- El constructor es `private`: nadie fuera del propio VO puede construir una instancia directamente.
- `Create()` retorna `Result<Price, ValidationError>`: el tipo de error es concreto (`ValidationError`), lo que permite al aggregate acceder a `.TypedError` sin casting para usar `with` al enriquecer la propiedad.
- Las conversiones implícitas de `Result<TValue, TError>` hacen que `return ProductErrors.InvalidPrice` y `return new Price(value)` compilen sin llamadas explícitas a `Success()` o `Failure()`.

> Si `Price` fuera un concepto compartido entre varios contextos (por ejemplo, tanto `Product` como `Order` lo usan), iría en `Shared/Domain/ValueObjects/Price.cs` en lugar de dentro del contexto. Ver [value-objects.md](../value-objects.md).

---

## Paso 4 — Aggregate Root

El aggregate es el único punto de entrada al dominio. Toda creación, validación y reconstrucción pasa por él. El aggregate **es** directamente la entidad que EF Core mapea — no hay una clase entidad separada.

```csharp
// Contexts/Product/Domain/Aggregates/ProductAggregate.cs
using Shared.Domain.Aggregates;
using Shared.Domain.Errors;
using Shared.Domain.Result;
using Product.Domain.Errors;
using Product.Domain.ValueObjects;

namespace Product.Domain.Aggregates;

public sealed class ProductAggregate : AggregateRoot<Guid>
{
    public string  Name  { get; private set; } = string.Empty;
    public decimal Price { get; private set; }

    private ProductAggregate(Guid id, string name, decimal price)
    {
        Id    = id;
        Name  = name;
        Price = price;
    }

    public static Result<ProductAggregate> Create(string name, decimal price)
    {
        var errors = new List<ValidationError>();

        // Validar Name — primitivo, se valida aquí si llega vacío
        if (string.IsNullOrWhiteSpace(name))
            errors.Add(ProductErrors.NameRequired);

        // Validar Price — Value Object, acumular si falla
        var priceResult = Price.Create(price);
        if (priceResult.IsFailure)
            errors.Add(priceResult.TypedError with { Property = nameof(Price), Value = price });

        // Si hay al menos un error, retornar todos juntos
        if (errors.Count > 0)
            return DomainError.FromValidationDomainErrors(errors);

        var aggregate = new ProductAggregate(Guid.NewGuid(), name, priceResult.Value.Value);
        aggregate.Created();
        return aggregate;
    }

    // Reconstruye el aggregate desde persistencia — sin validaciones ni auditoría
    public static ProductAggregate Reconstruct(Guid id, string name, decimal price)
        => new(id, name, price);

    protected override void Created()
    {
        SetCreatedAt(DateTime.UtcNow);
        SetUpdatedAt(DateTime.UtcNow);
    }
}
```

### Patrón de acumulación de errores en `Create()`

El aggregate no retorna al primer error: recorre todas las validaciones y acumula los fallos. Esto permite que el llamante reciba todos los problemas de una sola vez en lugar de tener que corregir y volver a intentar campo por campo.

```
Create(name, price)
    │
    ├─▶ ¿Name vacío?              → acumular NameRequired
    │
    ├─▶ Price.Create(price)
    │       falla                 → acumular TypedError con { Property, Value }
    │       éxito                 → guardar priceResult.Value
    │
    ├─▶ ¿errors.Count > 0?
    │       SÍ  →  return DomainError.FromValidationDomainErrors(errors)
    │               (conversión implícita DomainError → Result<ProductAggregate>)
    │
    └─▶ new ProductAggregate(Guid.NewGuid(), name, price)
        aggregate.Created()        ← asigna CreatedAt y UpdatedAt
        return aggregate
        (conversión implícita ProductAggregate → Result<ProductAggregate>)
```

### `Create()` vs `Reconstruct()`

| Método | Cuándo usarlo | Llama `Created()` |
|--------|---------------|:-----------------:|
| `Create(...)` | Nueva entidad de negocio | Sí |
| `Reconstruct(...)` | Reconstruir desde persistencia | No |

`Reconstruct()` lo llama el repositorio al leer de la base de datos. Los datos ya son válidos y las fechas las trae la BD directamente a través de EF Core.

---

## Ver también

- [entidades-y-agregados.md](../entidades-y-agregados.md) — clase base `EntityRoot<TId>`, `AggregateRoot<TId>`, auditoría
- [value-objects.md](../value-objects.md) — anatomía completa de un Value Object
- [patron-result.md](../patron-result.md) — jerarquía de errores y Result&lt;T, ValidationError&gt;
- [nuevo-contexto.md](nuevo-contexto.md) — continúa con aplicación, infraestructura y API
