# Value Objects

## Reglas

### 1 — No crear Value Object si solo se valida Required + MaxLength

Si una propiedad únicamente necesita validación de existencia o longitud máxima, **no merece un Value Object**. La validación vive en el DTO de entrada mediante FluentValidation.

```
✅ Primitivo en DTO  →  Summary: NotEmpty + MaximumLength(200)
❌ NO crear         →  SummaryValueObject con esas mismas dos reglas
```

Crea un Value Object cuando la propiedad tiene **lógica de negocio**: rangos, formatos específicos, cálculos derivados, o invariantes del dominio.

### 2 — Los Value Objects reutilizables van en Shared.Domain

Si el Value Object puede ser usado por más de un feature, pertenece a:

```
src/Shared/Domain/ValueObjects/<NombreValueObject>.cs
```

Si es exclusivo de un contexto, va dentro de ese contexto:

```
src/Contexts/<Contexto>/Domain/ValueObjects/<NombreValueObject>.cs
```

### 3 — Solo se instancian desde el agregado

El agregado es el único punto de entrada. Nadie fuera del dominio puede construir un Value Object directamente porque los constructores son privados. Toda creación pasa por el método factory `Create()` del propio Value Object, y ese método solo es invocado desde `AggregateRoot.Create()`.


---

## Estructura de carpetas

```
src/
├── Shared/
│   └── Domain/
│       └── ValueObjects/
│           ├── ValueObject.cs          ← clase base abstracta
│           └── Address.cs              ← VO compartido entre contextos
│
└── Contexts/
    └── <Contexto>/
        └── Domain/
            └── ValueObjects/
                └── Price.cs            ← VO exclusivo del contexto
```


---

## Flujo de vida

```
HTTP Request
     │
     ▼
[DTO]  CreateProductInputDto
     │
     ▼
[FluentValidation]  ¿Required? ¿MaxLength? ¿rango HTTP?
     │  falla → 400 Bad Request (errores estructurales)
     ▼
[Aggregate.Create()]  ProductAggregate.Create(primitivos)
     │
     ├─▶ Price.Create(price)
     │        falla → acumula ValidationError
     │
     ├─▶ Address.Create(street, city, zipCode)    [si aplica]
     │        falla → acumula ValidationError con Children
     │
     │  alguno falla → DomainError.FromValidationDomainErrors(errors)
     ▼
[AggregateRoot]  ProductAggregate — el agregado ES la entidad, ya validado y listo para persistir
```


---

## Anatomía de un Value Object

```csharp
public sealed class Price : ValueObject                // sealed, hereda ValueObject
{
    public const decimal MinValue = 0m;                 // constantes del dominio

    public decimal Value { get; }                       // propiedades solo lectura

    private Price(decimal value) { Value = value; }    // constructor privado

    public static Result<Price, ValidationError> Create(decimal value)
    {
        if (value < MinValue)
            return ProductErrors.InvalidPrice;           // error de dominio

        return new Price(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;                             // igualdad por valor
    }
}
```

| Elemento | Regla |
|----------|-------|
| Herencia | `sealed class X : ValueObject` |
| Constructor | `private` |
| Factory  | `public static Result<T, ValidationError> Create(...)` |
| Propiedades | `public T Prop { get; }` — sin setter |
| Igualdad | Implementar `GetEqualityComponents()` |


---

## Igualdad estructural

La clase base `ValueObject` implementa `Equals`, `GetHashCode` y los operadores `==` / `!=` usando `SequenceEqual` sobre los componentes que devuelve `GetEqualityComponents()`. Cada VO debe implementar ese método retornando una línea por propiedad que define la igualdad:

```csharp
protected override IEnumerable<object?> GetEqualityComponents()
{
    yield return Value;
}
```


---

## Decisión rápida: ¿VO o primitivo?

```
¿La propiedad tiene lógica de negocio más allá de Required/MaxLength?
    │
    ├── NO  →  Primitivo. Validar en el DTO con FluentValidation.
    │
    └── SÍ  →  Value Object.
                   │
                   ├── ¿Lo usan varios features?
                   │       SÍ  →  src/Shared/Domain/ValueObjects/
                   │       NO  →  src/Contexts/<Contexto>/Domain/ValueObjects/
                   │
                   └── Instanciar solo desde Aggregate.Create()
```


---

## Ver también

- [errores-dominio.md](errores-dominio.md) — cómo definir errores de dominio y acumularlos en el aggregate
- [validaciones.md](validaciones.md) — mapa de las cinco capas de validación
- [entidades-y-agregados.md](entidades-y-agregados.md) — quién construye los VOs: los factories del agregado, desde los records de argumentos
- [contextos.md](contextos.md) — flujo completo para modelar el dominio de un contexto
