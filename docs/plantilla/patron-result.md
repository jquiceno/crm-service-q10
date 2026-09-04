# Patrón Result

## Jerarquía de tipos Result

Todos los tipos viven en `Shared.Results` (proyecto `src/Shared/Results/`) y heredan de `Result`:

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

Las conversiones implícitas eliminan el ruido del código al evitar llamadas explícitas a `Success()` y `Failure()`. **Usarlas es la convención**, no una opción estilística: en Value Objects, agregados, repositorios y casos de uso se retorna el valor o el error directamente.

### Desde un valor → Result exitoso

```csharp
// En lugar de: return Result<Price>.Success(new Price(value));
return new Price(value);   // implícito: T → Result<T>
```

### Desde un error → Result fallido

```csharp
// En lugar de: return Result<Price>.Failure(ProductErrors.InvalidPrice);
return ProductErrors.InvalidPrice;   // implícito: DomainError → Result<T>

// También funciona con PagedResult<T>:
return PersistenceErrors.Failure();   // implícito: DomainError → PagedResult<T>
```

### Desde `Result<TValue, TError>` hacia `Result<TValue>`

`Result<TValue, TError>` hereda de `Result<TValue>`, por lo que se puede asignar directamente:

```csharp
Result<Price> result = Price.Create(value);
```

### Cuándo sí hace falta `Success(...)` explícito

Dos casos, y solo dos:

- **`PagedResult<T>`** — `Success` recibe dos argumentos (`items`, `totalCount`) y no hay conversión implícita desde `T`; para el error, `PagedResult<T>.Failure(error)` o la conversión implícita desde `DomainError`.
- **Cuando el compilador no puede inferir la conversión**, típicamente al construir el valor con una expresión de colección hacia una interfaz:

```csharp
IReadOnlyList<AuditStatisticsSeriesDto> dtos = [.. result.Value.Select(s => s.ToDto())];
return Result<IReadOnlyList<AuditStatisticsSeriesDto>>.Success(dtos);
```

Fuera de esos dos casos, envolver a mano es ruido:

```csharp
return Result<UpdateProductOutputDto>.Success(aggregate.ToOutputDto());   // ✘
return aggregate.ToOutputDto();                                          // ✔
```

> **Restricción importante**: `Result<T>.Success(value)` lanza `ArgumentNullException` si `value` es null. El patrón asume que un resultado exitoso siempre tiene un valor.


---

## Uso en Use Cases

### Patrón estándar de manejo

```csharp
public async Task<Result<CreateProductOutputDto>> ExecuteAsync(
    CreateProductInputDto input, CancellationToken cancellationToken = default)
{
    // 1. Crear el Aggregate (validación de dominio) — el error nace en el dominio: se sella
    var aggregateResult = input.ToAggregate();
    if (aggregateResult.IsFailure)
        return aggregateResult.Error with { Context = ProductErrors.Context, Origin = Origin };

    // 2. Precondición de negocio — el fallo de infraestructura se propaga tal cual;
    //    la regla que decide este use case sí se sella
    var existsResult = await repository
        .ExistsByNameAsync(input.Name!, cancellationToken)
        .ConfigureAwait(false);
    if (existsResult.IsFailure)
        return existsResult.Error;
    if (existsResult.Value)
        return ProductErrors.NameAlreadyExists with
            { Context = ProductErrors.Context, Origin = Origin };

    // 3. Persistir — repositorio solo encola el cambio
    var addResult = await repository
        .AddAsync(aggregateResult.Value, cancellationToken)
        .ConfigureAwait(false);
    if (addResult.IsFailure)
        return addResult.Error;

    // 4. Confirmar — Unit of Work persiste todo o nada
    var commitResult = await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
    if (commitResult.IsFailure)
        return commitResult.Error;

    return aggregateResult.Value.ToOutputDto();   // implícito → Result<OutputDto>
}
```

### Quién sella `Context` y `Origin`

**Cada pieza sella los errores que ella misma origina, y no toca los que recibe.** El repositorio, los readers y el `UnitOfWorkAdapter` ya estampan su propio `Origin` (`PersistenceErrors.Failure(Origin)`, `SqlServerErrorClassifier.Classify(ex, Origin)`, `ProductErrors.NotFound(id) with { Origin = Origin }`); reescribirlo desde el caso de uso borraría la traza real del fallo.

El caso de uso solo sella:

- los errores que devuelve el agregado o un Value Object (el dominio no conoce el contexto ni al llamador), y
- los errores que él mismo decide (`NotFound` tras un `ExistsAsync` en falso, un conflicto de negocio).

El desarrollo completo de la regla, con la tabla de decisión, está en [casos-de-uso.md](casos-de-uso.md#7-propagación-de-errores-context-y-origin).

### Propagación del error sin transformación

```csharp
var result = await repository.GetAllAsync(page, cancellationToken).ConfigureAwait(false);
if (result.IsFailure)
    return result.Error;   // propaga tal como viene — este es el caso por defecto
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
