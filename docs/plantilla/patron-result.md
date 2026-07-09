# Patrón Result

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

> **`.TypedError` vs `.Error`**: `.Error` devuelve `DomainError` base. `.TypedError` devuelve el tipo concreto (`ValidationError`, etc.) sin casting. Usar `.TypedError` solo cuando el tipo concreto importa.

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


---

## Conversiones implícitas

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

## Uso en Use Cases

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


---

## Ver también

- [errores-dominio.md](errores-dominio.md) — jerarquía de errores, `ErrorType`, cómo definir errores por contexto
- [repositorio.md](repositorio.md) — contratos de repositorio, Unit of Work, paginación
- [value-objects.md](value-objects.md) — uso de `Result<T, ValidationError>` en Value Objects
- [validaciones.md](validaciones.md) — mapa de las cinco capas de validación
