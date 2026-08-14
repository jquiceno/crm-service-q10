# Providers

## Qué es un Provider

Un Provider es un application service enfocado que encapsula lógica de **resolución auxiliar** que no pertenece al use case pero que este necesita antes de ejecutar su flujo principal.

El caso típico es un valor con fallback: si el cliente envía datos, úsalos; si no, búscalos en la base de datos. Esa decisión es lógica de aplicación — depende de un repositorio — y extraerla al Provider mantiene el use case enfocado en orquestación.

> **Antes de crear un Provider, verifica la fuente.** Un Provider **solo puede leer de repositorios**. Si el dato viene de un catálogo, una tabla foránea o una vista —es decir, de algo que no es un Aggregate y por tanto no tiene repositorio— la pieza correcta es un **Reader**, no un Provider. El árbol de decisión completo está en [conceptos-reader-provider-repository.md](conceptos-reader-provider-repository.md).
>
> En la práctica, los servicios levantados hasta hoy (`audits-service`, `academic-service`) resuelven toda su lectura auxiliar con Readers y no han necesitado ningún Provider.


---

## Cuándo extraer un Provider

Extraer un Provider cuando el use case cumple alguna de estas condiciones:

- Depende de más de un repositorio para resolver su input.
- Contiene una condición de tipo "si vacío, busca en BD" que oscurece el flujo principal.
- La misma lógica de resolución podría reutilizarse desde otro use case.

No extraer un Provider por anticipación. Si solo hay un use case que necesita esa lógica y el flujo es simple, mantenerlo inline es preferible.


---

## Qué NO es un Provider

| Concepto | Diferencia |
|----------|------------|
| **Domain Service** | Opera solo sobre objetos del dominio, sin repositorios ni infraestructura. |
| **Repository** | Contrato de acceso a datos del Aggregate — el Provider puede usarlo, pero no es uno (ver [puertos-y-adaptadores.md](puertos-y-adaptadores.md)). |
| **Reader** | Lee de una fuente que **no** es un repositorio (catálogo, tabla foránea, vista) o sirve una lectura al exterior. Tiene interfaz en `Application/Ports/` e implementación en infraestructura; el Provider no tiene interfaz y vive entero en aplicación. |
| **Resolver (Infrastructure)** | En este proyecto los Resolvers son implementaciones de puertos o repositorios de infraestructura. |
| **Use Case** | El use case orquesta; el Provider solo resuelve un dato puntual. |


---

## Estructura y nomenclatura

```
Contexts/{Contexto}/Application/Providers/{Contexto}{Concepto}Provider.cs
```

Ejemplos:

| Clase | Resuelve |
|-------|----------|
| `ProductCategoriesProvider` | Lista de categorías: las del input o todas las activas |

### Reglas de naming

- Sufijo `Provider`.
- Prefijo con el nombre del bounded context.
- El sustantivo del medio describe el dato que provee, no la operación (`Categories`, no `GetCategories`).


---

## Anatomía

```csharp
// Contexts/Product/Application/Providers/ProductCategoriesProvider.cs
using Product.Domain.Repositories;
using Shared.Results;

namespace Product.Application.Providers;

public sealed class ProductCategoriesProvider(ICategoryRepository repository)
{
    public async Task<Result<IReadOnlyList<string>>> GetAsync(
        IReadOnlyList<string>? categories,
        CancellationToken ct = default)
    {
        if (categories is { Count: > 0 })
            return Result<IReadOnlyList<string>>.Success(categories);

        var result = await repository.GetAllAsync(isActive: true, ct).ConfigureAwait(false);
        if (result.IsFailure)
            return result.Error;

        IReadOnlyList<string> resolved = result.Value
            .Select(c => c.Code)
            .Distinct()
            .ToList();

        return Result<IReadOnlyList<string>>.Success(resolved);
    }
}
```

Puntos clave:

- Retorna `Result<T>` — propaga errores del repositorio sin lanzar excepciones.
- Método único `GetAsync` — un Provider resuelve un concepto, no múltiples.
- `sealed` — no diseñado para herencia.
- Sin interfaz propia — es un helper interno de la capa de aplicación, no un puerto.


---

## Uso en el use case

El Provider se recibe por constructor y se invoca al inicio, antes de construir el aggregate:

```csharp
public sealed class CreateProductUseCase(
    IProductRepository repository,
    ProductCategoriesProvider categoriesProvider) : ICreateProductUseCase
{
    private const string Origin = nameof(CreateProductUseCase);

    public async Task<Result<CreateProductOutputDto>> ExecuteAsync(
        CreateProductInputDto input, CancellationToken ct = default)
    {
        var categoriesResult = await categoriesProvider.GetAsync(input.Categories, ct).ConfigureAwait(false);
        if (categoriesResult.IsFailure)
            return categoriesResult.Error with { Origin = Origin };

        input = input with { Categories = categoriesResult.Value };

        var aggregateResult = input.ToAggregate();
        // ...
    }
}
```


---

## Registro en DI

Registrar como `Scoped` — los Providers dependen de repositorios que son `Scoped`. Ubicarlo antes de los use cases que lo consumen:

```csharp
public static IServiceCollection AddProductServices(this IServiceCollection services)
{
    services.AddScoped<ProductCategoriesProvider>();              // Provider primero
    services.AddScoped<ICreateProductUseCase, CreateProductUseCase>();
    // ...
    return services;
}
```

Los Providers se registran como tipo concreto (sin interfaz), igual que otros helpers internos de aplicación.


---

## Testing

Ver la sección **Providers** en [testing.md](testing.md) para el patrón completo.

Resumen:

- Crear un archivo de tests propio: `tests/UnitTests/Contexts/{Contexto}/Application/Providers/{Nombre}ProviderTests.cs`.
- Instanciar el Provider con el mock del repositorio — no mockear el Provider.
- Cubrir: datos provistos, datos nulos, lista vacía, error del repositorio, y deduplicación si aplica.


---

## Ver también

- [conceptos-reader-provider-repository.md](conceptos-reader-provider-repository.md) — cómo distinguir Reader, Provider y Repository
- [arquitectura.md](arquitectura.md) — estructura de capas y carpetas
- [casos-de-uso.md](casos-de-uso.md) — cuándo y cómo agregar un Provider a un use case existente
- [testing.md](testing.md) — patrón de testing para Providers
