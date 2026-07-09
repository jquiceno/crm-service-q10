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
                └── Temperature.cs      ← VO exclusivo del contexto
```


---

## Flujo de vida

```
HTTP Request
     │
     ▼
[DTO]  CreateWeatherForecastInputDto
     │
     ▼
[FluentValidation]  ¿Required? ¿MaxLength? ¿rango HTTP?
     │  falla → 400 Bad Request (errores estructurales)
     ▼
[Aggregate.Create()]  WeatherForecastAggregate.Create(primitivos)
     │
     ├─▶ Temperature.Create(celsius)
     │        falla → acumula ValidationError
     │
     ├─▶ Address.Create(street, city, zipCode)    [si aplica]
     │        falla → acumula ValidationError con Children
     │
     │  alguno falla → DomainError.FromValidationDomainErrors(errors)
     ▼
[Entity]  WeatherForecastEntity(id, date, temperature, summary, address?)
     │
     ▼
[AggregateRoot]  WeatherForecastAggregate wrappea la entity
```


---

## Anatomía de un Value Object

```csharp
public sealed class Temperature : ValueObject          // sealed, hereda ValueObject
{
    public const int MinCelsius = -60;                 // constantes del dominio
    public const int MaxCelsius = 60;

    public int Celsius { get; }                        // propiedades solo lectura
    public int Fahrenheit => (int)Math.Round(Celsius * 9.0 / 5.0 + 32);

    private Temperature(int celsius) { Celsius = celsius; }  // constructor privado

    public static Result<Temperature, ValidationError> Create(int celsius)
    {
        if (celsius < MinCelsius || celsius > MaxCelsius)
            return WeatherForecastErrors.TemperatureOutOfRange;  // error de dominio

        return new Temperature(celsius);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Celsius;                          // igualdad por valor
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
    yield return Celsius;
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
- [guias/nueva-entidad-dominio.md](guias/nueva-entidad-dominio.md) — flujo completo para modelar el dominio de un contexto
