# Patrón Result

## ¿Por qué usar el Patrón Result?

En arquitecturas orientadas al dominio, los errores tienen dos naturalezas distintas:

* **Errores esperados**: negocio, validación, "no encontrado", conflicto. Son condiciones normales del flujo que el código llamante debe manejar explícitamente.
* **Errores excepcionales**: fallos de red, memoria agotada, bugs de programación. Son condiciones inesperadas que deben propagarse como excepciones.

El problema con usar **excepciones para errores esperados** es que:

* No son visibles en la firma del método — el llamante no sabe qué puede fallar.
* Son costosas en rendimiento cuando ocurren frecuentemente.
* El compilador no obliga a manejarlas, lo que lleva a errores silenciosos.

El **Patrón Result** resuelve esto representando el éxito o el fracaso como un valor de retorno tipado. El llamante **no puede ignorar** el resultado sin una decisión explícita.

```csharp
// ❌ Sin Result: el llamante no sabe que puede fallar
public async Task<WeatherForecastAggregate?> GetByIdAsync(Guid id);

// ✅ Con Result: el fracaso es parte del contrato
public async Task<Result<WeatherForecastAggregate>> GetByIdAsync(Guid id);
```


---

## Jerarquía de tipos Result

Todos los tipos viven en `Shared.Domain.Result` y heredan de `Result`:

```
Result
├── Result<T>
│   └── Result<TValue, TError>
└── PagedResult<T>
```

### `Result` — operación sin valor de retorno

Para operaciones que solo indican éxito o fracaso (`Update`, `Remove`, `Add`, `CommitAsync`).

```csharp
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public DomainError Error { get; }   // solo accesible si IsFailure

    public static Result Success();
    public static Result Failure(DomainError error);

    public static implicit operator Result(DomainError error) => Failure(error);
}
```

### `Result<T>` — operación con valor de retorno

Para operaciones que devuelven un valor en caso de éxito (`GetById`).

```csharp
public class Result<T> : Result
{
    public T Value { get; }   // solo accesible si IsSuccess

    public static Result<T> Success(T value);
    public static Result<T> Failure(DomainError error);

    public static implicit operator Result<T>(T value)           => Success(value);
    public static implicit operator Result<T>(DomainError error) => Failure(error);
}
```

### `Result<TValue, TError>` — error fuertemente tipado

Para operaciones donde el tipo concreto del error importa (Value Objects). Expone `.TypedError` para acceder al error sin casting, evitando la violación de LSP que tendría `new Error`.

```csharp
public sealed class Result<TValue, TError> : Result<TValue>
    where TError : DomainError
{
    public TError TypedError { get; }  // acceso tipado; lanza si IsSuccess

    public static Result<TValue, TError> Success(TValue value);
    public static Result<TValue, TError> Failure(TError error);

    public static implicit operator Result<TValue, TError>(TValue value)  => Success(value);
    public static implicit operator Result<TValue, TError>(TError error)  => Failure(error);
}
```

> `**TypedError**` **vs** `**Error**`: `.Error` (heredado de `Result`) devuelve `DomainError` y sigue siendo accesible. `.TypedError` devuelve el tipo concreto (`ValidationError`, etc.) sin necesidad de casting. Usar `.TypedError` solo cuando el tipo concreto importa.

### `PagedResult<T>` — resultado paginado

Extiende directamente `Result` para representar una respuesta paginada como un resultado de primera clase, sin el doble wrapping de `Result<PagedResult<T>>`.

```csharp
public sealed class PagedResult<T> : Result
{
    public IReadOnlyList<T> Items      { get; }   // solo accesible si IsSuccess
    public int              TotalCount { get; }   // solo accesible si IsSuccess

    public static PagedResult<T> Success(IReadOnlyList<T> items, int totalCount);
    public static PagedResult<T> Failure(DomainError error);

    public static implicit operator PagedResult<T>(DomainError error) => Failure(error);
}
```

Uso en el repositorio:

```csharp
// Antes: Task<Result<IReadOnlyList<TAggregate>>>
// Ahora: PagedResult<T> es el resultado directamente
Task<PagedResult<WeatherForecastAggregate>> GetAllAsync(PageQuery page, CancellationToken ct = default);
```


---

## Conversiones Implícitas

Las conversiones implícitas eliminan el ruido del código al evitar llamadas explícitas a `Success()` y `Failure()`.

### Desde un valor → Result exitoso

```csharp
// En lugar de: return Result<Temperature>.Success(new Temperature(celsius));
return new Temperature(celsius);   // implícito: T → Result<T>
```

### Desde un error → Result fallido

```csharp
// En lugar de: return Result<Temperature>.Failure(WeatherForecastErrors.TemperatureOutOfRange);
return WeatherForecastErrors.TemperatureOutOfRange;   // implícito: DomainError → Result<T>

// También funciona con PagedResult<T>:
return PersistenceErrors.Failure();   // implícito: DomainError → PagedResult<T>
```

### Desde `Result<TValue, TError>` hacia `Result<TValue>`

`Result<TValue, TError>` hereda de `Result<TValue>`, por lo que se puede asignar directamente:

```csharp
Result<Temperature> result = Temperature.Create(celsius);
```

> **Restricción importante**: `Result<T>.Success(value)` lanza `ArgumentNullException` si `value` es null. El patrón asume que un resultado exitoso siempre tiene un valor.


---

## Jerarquía de Errores de Dominio

Todos los errores heredan de `DomainError`, que es un `record`:

```csharp
public record DomainError
{
    public string   Message  { get; }
    public ErrorType Type    { get; }
    public string   Context  { get; init; }
    public string   Origin   { get; init; }
    public IReadOnlyList<ErrorDetail> Details { get; init; }
}
```

### `ErrorType` — categorías de error

```csharp
public enum ErrorType
{
    None,         // usado únicamente por DomainError.None (resultado exitoso)
    Validation,   // datos inválidos, campos requeridos, formatos incorrectos
    NotFound,     // recurso no encontrado
    Conflict,     // violación de unicidad, restricción de FK
    Unauthorized, // sin autenticación
    Forbidden,    // sin autorización
    Internal,     // error de infraestructura (BD, red, deadlock)
    DomainError   // múltiples errores de dominio agregados
}
```

### `ValidationError` — error de validación con contexto de propiedad

```csharp
public sealed record ValidationError : DomainError
{
    public string   Property   { get; init; }
    public object?  Value      { get; init; }
    public IReadOnlyDictionary<string, object?>? Attributes { get; init; }
    public IReadOnlyList<ValidationError>?       Children   { get; init; }
}
```

### `ValidationErrorList` — colección de errores de validación

Agrupa múltiples `ValidationError` cuando un objeto tiene varias propiedades inválidas:

```csharp
public sealed record ValidationErrorList : DomainError
{
    public IReadOnlyList<ValidationError> Errors { get; }
}
```


---

## Definición de Errores

### Errores compartidos (`SharedErrors`)

Solo contiene errores verdaderamente compartidos entre todos los contextos. Los errores de persistencia **no pertenecen al dominio** y viven en infraestructura:

```csharp
// Shared.Domain.Errors.SharedErrors
public static class SharedErrors
{
    public static DomainError NotFound(string entityName, Guid id);
}
```

### Errores de infraestructura (`PersistenceErrors`)

Errores genéricos de persistencia, `internal` a la capa de infraestructura. No exponen detalles técnicos del servidor (`ex.Message`) por seguridad:

```csharp
// Infrastructure — internal, no expuesto al dominio
internal static class PersistenceErrors
{
    internal static DomainError Failure() =>
        new("A persistence error occurred.", ErrorType.Internal);
}
```

### Errores de dominio por contexto

Cada contexto define sus propios errores en `Domain.Errors`. Los errores estáticos son constantes de dominio; los métodos de fábrica se usan cuando el mensaje depende de un valor en runtime:

```csharp
public static class WeatherForecastErrors
{
    public static readonly ValidationError DateRequired =
        new("Date is required.", ErrorType.Validation);

    public static readonly ValidationError TemperatureOutOfRange =
        new($"Temperature must be between {Temperature.MinCelsius} and {Temperature.MaxCelsius}.",
            ErrorType.Validation)
        {
            Attributes = new Dictionary<string, object?>
            {
                ["min"] = Temperature.MinCelsius,
                ["max"] = Temperature.MaxCelsius
            }
        };

    public static readonly ValidationError DateAlreadyExists =
        new("A forecast for this date already exists.", ErrorType.Conflict);
}
```

### Enriquecimiento del error en el Application Layer

Los errores se definen sin `Context` ni `Origin` en el dominio. Al retornarlos desde un use case se enriquecen con `with`:

```csharp
return WeatherForecastErrors.DateAlreadyExists with
{
    Context = WeatherForecastErrors.Context,
    Origin  = nameof(CreateWeatherForecastUseCase)
};
```


---

## Uso en Value Objects

Los Value Objects usan `Result<TValue, TError>` para exponer el tipo exacto del error:

```csharp
public sealed class Temperature : ValueObject
{
    public static Result<Temperature, ValidationError> Create(int celsius)
    {
        if (celsius < MinCelsius || celsius > MaxCelsius)
            return WeatherForecastErrors.TemperatureOutOfRange;   // implícito
        return new Temperature(celsius);                           // implícito
    }
}
```

En el Aggregate, `.TypedError` permite acceder al error concreto sin casting y usar `with` para agregar contexto de propiedad:

```csharp
var temperatureResult = Temperature.Create(temperature);
if (temperatureResult.IsFailure)
    errors.Add(temperatureResult.TypedError with { Property = nameof(Temperature), Value = temperature });
//             ^^^^^^^^^^^^^^^^^
//             Tipo: ValidationError — acceso sin casting, correcto en LSP
```

### Agregación de errores en el Aggregate

```csharp
public static Result<WeatherForecastAggregate> Create(...)
{
    var errors = new List<ValidationError>();

    var temperatureResult = Temperature.Create(temperature);
    if (temperatureResult.IsFailure)
        errors.Add(temperatureResult.TypedError with { Property = nameof(Temperature), Value = temperature });

    var addressResult = Address.Create(street, city, zipCode);
    if (addressResult.IsFailure)
        errors.Add(new ValidationError("Address is invalid.", ErrorType.Validation)
        {
            Property = nameof(Address),
            Children = addressResult.TypedError.Errors
        });

    if (errors.Count > 0)
        return DomainError.FromValidationDomainErrors(errors);  // implícito → Result<Aggregate>

    return new WeatherForecastAggregate(entity);                // implícito → Result<Aggregate>
}
```


---

## Uso en Use Cases (Application Layer)

### Patrón estándar de manejo

```csharp
public async Task<Result<CreateWeatherForecastOutputDto>> ExecuteAsync(
    CreateWeatherForecastInputDto input, CancellationToken cancellationToken = default)
{
    // 1. Verificar precondición de negocio
    var existsResult = await repository.ExistsForDateAsync(input.Date, cancellationToken);
    if (existsResult.IsFailure)
        return existsResult.Error with { Context = WeatherForecastErrors.Context, Origin = Origin };
    if (existsResult.Value)
        return WeatherForecastErrors.DateAlreadyExists with
            { Context = WeatherForecastErrors.Context, Origin = Origin };

    // 2. Crear el Aggregate (validación de dominio)
    var aggregateResult = input.ToAggregate();
    if (aggregateResult.IsFailure)
        return aggregateResult.Error with { Context = WeatherForecastErrors.Context, Origin = Origin };

    // 3. Persistir — repositorio solo encola el cambio
    var addResult = await repository.AddAsync(aggregateResult.Value, cancellationToken);
    if (addResult.IsFailure)
        return addResult.Error with { Context = WeatherForecastErrors.Context, Origin = Origin };

    // 4. Confirmar — Unit of Work persiste todo o nada
    var commitResult = await unitOfWork.CommitAsync(cancellationToken);
    if (commitResult.IsFailure)
        return commitResult.Error with { Context = WeatherForecastErrors.Context, Origin = Origin };

    return aggregateResult.Value.ToCreateDto();   // implícito → Result<OutputDto>
}
```

### Propagación del error sin transformación

```csharp
var result = await repository.GetAllAsync(page, cancellationToken);
if (result.IsFailure)
    return result.Error;   // propaga tal como viene
```


---

## Ver también

- [repositorio.md](repositorio.md) — contratos de repositorio, Unit of Work, paginación
- [value-objects.md](value-objects.md) — anatomía de un Value Object con Create()
- [validaciones.md](validaciones.md) — mapa de las cinco capas de validación

---

## Reglas de uso

| Situación | Usar |
|-----------|------|
| Operación que puede no encontrar el recurso | `Result<T>` con `ErrorType.NotFound` |
| Validación de una sola propiedad en un Value Object | `Result<T, ValidationError>` |
| Validación de múltiples propiedades en un Aggregate | `Result<T>` con `DomainError.FromValidationDomainErrors(errors)` |
| Operación de repositorio de lectura/escritura | `Result<T>` / `Result` según corresponda |
| Listado con paginación | `PagedResult<T>` |
| Confirmar cambios en base de datos | `IUnitOfWorkPort.CommitAsync()` → `Result` |
| Fallo de base de datos al confirmar | Manejado automáticamente por `SqlServerErrorClassifier` en `UnitOfWorkAdapter` |
| Error irrecuperable / bug de programación | Lanzar excepción (`ArgumentNullException`, `InvalidOperationException`) |
| Cancelación de operación | Dejar propagar `OperationCanceledException` — **no** convertirla a `Result` |
| Acceder a `.Value` / `.Items` sin verificar `IsSuccess` | **Prohibido** — lanza `InvalidOperationException` |
| Acceder a `.Error` sin verificar `IsFailure` | **Prohibido** — lanza `InvalidOperationException` |
| Acceder a `.TypedError` sin verificar `IsFailure` | **Prohibido** — lanza `InvalidOperationException` |
| Exponer `ex.Message` en errores de infraestructura | **Prohibido** — usar `PersistenceErrors.Failure()` |
