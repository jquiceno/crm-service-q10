# Errores de dominio

## Jerarquía de tipos

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

### `ValidationError` — error con contexto de propiedad

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

## Dónde viven los errores

| Tipo | Clase | Ubicación |
|------|-------|-----------|
| Errores compartidos entre contextos | `SharedErrors` | `Shared.Domain.Errors` |
| Errores de persistencia | `PersistenceErrors` | `Infrastructure` — `internal` |
| Errores de un contexto específico | `{Contexto}Errors` | `Contexts/{Contexto}/Domain/Errors/` |

### `SharedErrors`

Solo contiene errores verdaderamente compartidos entre todos los contextos:

```csharp
public static class SharedErrors
{
    public static DomainError NotFound(string entityName, Guid id);
}
```

### `PersistenceErrors`

`internal` a la capa de infraestructura. No expone detalles técnicos del servidor por seguridad:

```csharp
internal static class PersistenceErrors
{
    internal static DomainError Failure() =>
        new("A persistence error occurred.", ErrorType.Internal);
}
```


---

## Definir errores de un contexto

Cada contexto centraliza sus errores en una clase estática `{Contexto}Errors` en `Domain/Errors/`.

**Campo `static readonly`** — para errores con mensaje fijo:

```csharp
public static class WeatherForecastErrors
{
    public const string Context = "WeatherForecast";

    public static readonly ValidationError DateRequired =
        new("Date is required.", ErrorType.Validation)
        {
            Property = nameof(WeatherForecastAggregate.Date)
        };

    public static readonly ValidationError TemperatureOutOfRange =
        new($"Temperature must be between {Temperature.MinCelsius} and {Temperature.MaxCelsius}.",
            ErrorType.Validation)
        {
            Property   = nameof(WeatherForecastAggregate.TemperatureCelsius),
            Attributes = new Dictionary<string, object?>
            {
                ["min"] = Temperature.MinCelsius,
                ["max"] = Temperature.MaxCelsius
            }
        };
}
```

**Método de fábrica** — cuando el mensaje depende de un valor en runtime:

```csharp
public static DomainError NotFound(Guid id)
    => new($"WeatherForecast with id '{id}' was not found.", ErrorType.NotFound);
```

### Campo `Property` en `ValidationError`

Siempre debe asignarse para que el cliente sepa qué campo falló. Se asigna en la definición del error (si el nombre de propiedad es fijo) o en el Aggregate al acumular errores (si el mismo error puede reutilizarse desde distintos agregados):

```csharp
// En el Aggregate — asignar Property al acumular
errors.Add(temperatureResult.TypedError with { Property = nameof(Temperature), Value = temperature });
```

### `Attributes`

Opcional pero recomendado cuando hay parámetros relevantes para el cliente (límites, longitudes máximas). El cliente puede usarlos sin parsear el mensaje de error.


---

## Enriquecer errores con Context y Origin

Los errores se definen sin `Context` ni `Origin` en el dominio. Al retornarlos desde un use case se enriquecen con `with`:

```csharp
return WeatherForecastErrors.DateAlreadyExists with
{
    Context = WeatherForecastErrors.Context,
    Origin  = nameof(CreateWeatherForecastUseCase)
};
```

- **`Context`** — nombre del bounded context (`"WeatherForecast"`). Facilita el filtrado en logs.
- **`Origin`** — nombre de la clase que retorna el error. Facilita el diagnóstico.


---

## Ver también

- [patron-result.md](patron-result.md) — tipos `Result<T>` que transportan estos errores
- [validaciones.md](validaciones.md) — mapa de las cinco capas de validación
- [value-objects.md](value-objects.md) — uso de `ValidationError` en Value Objects
