# Validaciones — dónde van

Mapa de referencia para saber en qué capa implementar cada tipo de validación.


---

## Resumen rápido

| Tipo de validación | Capa | Mecanismo |
|--------------------|------|-----------|
| Formato del request (nulos, longitud, rango HTTP) | Presentación | `IStructuralValidator<TDto>` — FluentValidation |
| Invariante de una propiedad (regla de negocio) | Dominio — Value Object | `ValueObject.Create()` → `Result<T, ValidationError>` |
| Invariante que involucra múltiples propiedades | Dominio — Aggregate | `Aggregate.Create()` → acumula `ValidationError` |
| Regla que requiere consultar la base de datos | Aplicación — Use Case | Consulta al repositorio antes de crear el aggregate |
| Constraints estructurales de la base de datos | Base de datos | La DB misma — `SqlServerErrorClassifier` traduce los errores |


---

## Capa 1 — Validación estructural del request

**Qué valida:** que el payload HTTP tenga la forma mínima esperada: campos requeridos, longitudes, formatos, rangos básicos de entrada.

**Dónde vive:** en un `IStructuralValidator<TInputDto>` usando FluentValidation. Se ejecuta automáticamente con `[ValidateRequest]` antes de entrar al use case.

**Por qué aquí:** es la primera línea de defensa. Rechaza peticiones mal formadas sin involucrar el dominio. Los errores de esta capa no son errores de negocio, son errores de contrato HTTP.

```csharp
// Validation/FluentValidation/Product/CreateProductInputValidator.cs
public sealed class CreateProductInputValidator : AbstractValidator<CreateProductInputDto>,
    IStructuralValidator<CreateProductInputDto>
{
    public CreateProductInputValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0);
    }
}
```

> No crear un Value Object solo para validar `NotEmpty` + `MaxLength`. Si esas son las únicas reglas, FluentValidation es suficiente. Ver [value-objects.md](value-objects.md).


---

## Capa 2 — Invariantes del Value Object

**Qué valida:** reglas de negocio sobre una única propiedad que tienen significado en el dominio: rangos válidos, formatos específicos, cálculos que deben ser consistentes.

**Dónde vive:** en el método `Create()` del Value Object. Retorna `Result<T, ValidationError>`.

**Por qué aquí:** el Value Object es la única representación válida de ese concepto. Si se construyó, ya es correcto. Nadie puede crear un `Temperature` de 9000°C.

```csharp
// Contexts/Product/Domain/ValueObjects/Price.cs
public static Result<Price, ValidationError> Create(decimal value)
{
    if (value < MinValue)
        return ProductErrors.InvalidPrice;   // implicit conversion
    return new Price(value);
}
```

El error debe tener `Property` asignado para que el cliente sepa qué campo falló:

```csharp
// Contexts/Product/Domain/Errors/ProductErrors.cs
public static readonly ValidationError InvalidPrice
    = new($"Price must be greater than or equal to {Price.MinValue}.", ErrorType.Validation)
    {
        Property   = nameof(ProductAggregate.Price),
        Attributes = new Dictionary<string, object?> { ["min"] = Price.MinValue }
    };
```


---

## Capa 3 — Invariantes del Aggregate

**Qué valida:** reglas que involucran varias propiedades a la vez, o que necesitan el estado completo del objeto para determinarse.

**Dónde vive:** en el método `Create()` del Aggregate. Los errores se acumulan en una lista para retornar todos juntos, no uno a uno.

**Por qué aquí:** el Aggregate es el responsable de que el objeto nazca en estado válido. No puede existir un `WeatherForecastAggregate` inconsistente.

```csharp
// Contexts/WeatherForecast/Domain/Aggregates/WeatherForecastAggregate.cs
public static Result<WeatherForecastAggregate> Create(
    Guid id, DateTime date, int temperature, string summary, ...)
{
    var errors = new List<ValidationError>();

    var tempResult = Temperature.Create(temperature);
    if (tempResult.IsFailure)
        errors.Add(tempResult.TypedError with
        {
            Property = nameof(Temperature),
            Value    = temperature
        });

    var addressResult = Address.Create(street, city, zipCode);
    if (addressResult.IsFailure)
        errors.Add(new ValidationError("Address is invalid.", ErrorType.Validation)
        {
            Property = nameof(Address),
            Children = addressResult.TypedError.Errors
        });

    if (errors.Count > 0)
        return DomainError.FromValidationDomainErrors(errors);

    return new WeatherForecastAggregate(entity);
}
```

> `Property` se asigna en el Aggregate, no en el error estático del Value Object, porque el mismo error puede reutilizarse desde distintos agregados con distintos nombres de propiedad. Ver [patron-result.md](patron-result.md).


---

## Capa 4 — Reglas que requieren persistencia

**Qué valida:** unicidad, existencia de entidades relacionadas, o cualquier regla que solo se puede verificar consultando la base de datos.

**Dónde vive:** en el Use Case, antes de invocar `Aggregate.Create()` o `repository.AddAsync()`.

**Por qué aquí:** el dominio puro no tiene acceso a persistencia. Esta es la capa más baja donde se puede combinar lógica de negocio con una consulta.

```csharp
// Application/UseCases/CreateProduct/CreateProductUseCase.cs
public async Task<Result<CreateProductOutputDto>> ExecuteAsync(
    CreateProductInputDto input, CancellationToken ct = default)
{
    // Business rule that requires a DB query
    var existsResult = await repository.ExistsByNameAsync(input.Name!, ct);
    if (existsResult.IsFailure)
        return existsResult.Error with { Context = ProductErrors.Context, Origin = Origin };
    if (existsResult.Value)
        return ProductErrors.NameAlreadyExists with { Context = ProductErrors.Context, Origin = Origin };

    // Only after all persistence-level rules pass, create the aggregate
    var aggregateResult = input.ToAggregate();
    ...
}
```


---

## Capa 5 — Base de datos

**Qué valida:** constraints que el motor de base de datos impone: PRIMARY KEY, UNIQUE, FOREIGN KEY, NOT NULL, longitud de columna.

**Dónde vive:** en la base de datos misma. El proyecto es **Database First** — las constraints no se duplican en las configuraciones de EF Core.

**Por qué aquí:** la DB es la última línea de defensa. `SqlServerErrorClassifier` (interno a infraestructura) intercepta los errores y los convierte en `DomainError` semánticos antes de que lleguen al caller:

| `SqlException.Number` | Causa | `ErrorType` resultante |
|---------------------|-------|----------------------|
| 2627 / 2601         | PRIMARY KEY / índice único violado | `Conflict`           |
| 547                 | FOREIGN KEY violada | `Conflict`           |
| 515                 | NULL en columna NOT NULL | `Validation`         |
| 8152                | Valor excede longitud máxima | `Validation`         |
| 1205                | Deadlock | `Internal`           |


---

## Decisión rápida

```
¿Qué tipo de regla es?
    │
    ├── ¿Es un check de formato/existencia del payload HTTP?
    │       → IStructuralValidator<TDto>  (FluentValidation)
    │
    ├── ¿Es una regla de negocio sobre una única propiedad?
    │       → Value Object  (Create() → Result<T, ValidationError>)
    │
    ├── ¿Involucra varias propiedades del mismo objeto?
    │       → Aggregate.Create()  (acumula ValidationError)
    │
    ├── ¿Necesita consultar la base de datos?
    │       → Use Case  (antes de crear el aggregate)
    │
    └── ¿Es una restricción estructural de la base de datos?
            → La DB misma  (SqlServerErrorClassifier la traduce)
```


---

## Ver también

* [value-objects.md](value-objects.md) — cuándo crear un Value Object y su anatomía
* [patron-result.md](patron-result.md) — `ValidationError`, `Result<T, ValidationError>`, acumulación de errores
* [repositorio.md](repositorio.md) — flujo completo de un use case con validación
