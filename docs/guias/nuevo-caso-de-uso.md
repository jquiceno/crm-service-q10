# Guía: nuevo caso de uso

Pasos para agregar un caso de uso a un bounded context que ya existe.
El ejemplo agrega `UpdateProduct` al contexto `Product`.

> Para crear un contexto completo desde cero, ver [nuevo-contexto.md](nuevo-contexto.md).

---

## Paso 1 — Puerto de aplicación

Crear el puerto que el controller usará para invocar el caso de uso. El controller depende de esta interfaz, nunca de la implementación concreta.

```csharp
// Contexts/Product/Application/Ports/IUpdateProductPort.cs
using Product.Application.UseCases.UpdateProduct;
using Shared.Domain.Result;

namespace Product.Application.Ports;

public interface IUpdateProductPort
{
    Task<Result<UpdateProductOutputDto>> ExecuteAsync(
        Guid id, UpdateProductInputDto input, CancellationToken ct = default);
}
```

El puerto se ubica junto a los demás puertos del contexto: `Contexts/Product/Application/Ports/`.

---

## Paso 2 — DTOs

Crear los dos DTOs dentro de la carpeta del caso de uso: `Contexts/Product/Application/UseCases/UpdateProduct/`.

### DTO de entrada

```csharp
// Contexts/Product/Application/UseCases/UpdateProduct/UpdateProductInputDto.cs
using System.ComponentModel;

namespace Product.Application.UseCases.UpdateProduct;

public sealed record UpdateProductInputDto(
    [property: Description("Nuevo nombre del producto. Máximo 200 caracteres.")]
    string? Name,
    [property: Description("Nuevo precio del producto. Debe ser mayor o igual a 0.")]
    decimal Price);
```

`Name` es nullable para que el validador de entrada pueda reportar el error con su `Property` correcta en lugar de que el deserializador falle con un 400 genérico.

### DTO de salida

```csharp
// Contexts/Product/Application/UseCases/UpdateProduct/UpdateProductOutputDto.cs
namespace Product.Application.UseCases.UpdateProduct;

public sealed record UpdateProductOutputDto(
    Guid Id,
    string Name,
    decimal Price,
    DateTime UpdatedAt);
```

---

## Paso 3 — Mapping

Crear la clase de mapping en la misma carpeta del caso de uso. Para `UpdateProduct` solo se necesita `ToOutputDto()` — el aggregate no se construye desde el DTO sino que se carga del repositorio y se actualiza con un método del propio aggregate.

```csharp
// Contexts/Product/Application/UseCases/UpdateProduct/UpdateProductMapping.cs
using Product.Domain.Aggregates;

namespace Product.Application.UseCases.UpdateProduct;

public static class UpdateProductMapping
{
    public static UpdateProductOutputDto ToOutputDto(this ProductAggregate aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);

        return new UpdateProductOutputDto(
            aggregate.Id,
            aggregate.Name,
            aggregate.Price,
            aggregate.UpdatedAtUtc ?? aggregate.CreatedAtUtc);
    }
}
```

> A diferencia del caso de uso `CreateProduct`, aquí no existe `ToAggregate()`: el aggregate ya existe en la base de datos y se obtiene vía `repository.GetByIdAsync()`. Las modificaciones se aplican a través de un método del aggregate (`Update()`), no reemplazando la instancia.

---

## Paso 4 — Método de actualización en el Aggregate

Agregar el método `Update()` en `ProductAggregate`. Este método valida los nuevos valores y, si son correctos, actualiza el estado interno del aggregate y marca la entidad como modificada.

```csharp
// Contexts/Product/Domain/Aggregates/ProductAggregate.cs  (fragmento: agregar dentro de la clase)
public Result Update(string? name, decimal price)
{
    if (string.IsNullOrWhiteSpace(name))
        return ProductErrors.NameRequired with
            { Context = ProductErrors.Context, Origin = nameof(Update) };

    var priceResult = Price.Create(price);
    if (priceResult.IsFailure)
        return priceResult.TypedError with
            { Context = ProductErrors.Context, Origin = nameof(Update) };

    Entity.Name  = name;
    Entity.Price = priceResult.Value;
    Entity.SetUpdatedAtUtc();

    return Result.Success();
}
```

Puntos clave:

- Retorna `Result` (no `Result<T>`) porque la operación no produce un nuevo valor — solo indica éxito o fracaso.
- `Entity.SetUpdatedAtUtc()` actualiza la propiedad `UpdatedAtUtc` de la entidad base (`Entity<TId>`), que EF Core persiste en la columna de auditoría.
- La validación sigue el mismo patrón que `Create()`: falla rápido en el primer error, enriquece con `Context` y `Origin`.

---

## Paso 5 — Use Case

Crear la implementación del puerto. El flujo estándar para un Update es: cargar el aggregate → si no existe, retornar NotFound → aplicar cambios → marcar como modificado en el repositorio → commit.

```csharp
// Contexts/Product/Application/UseCases/UpdateProduct/UpdateProductUseCase.cs
using Product.Application.Ports;
using Product.Domain.Errors;
using Product.Domain.Ports;
using Shared.Application.Ports;
using Shared.Domain.Result;

namespace Product.Application.UseCases.UpdateProduct;

public sealed class UpdateProductUseCase(
    IProductRepositoryPort repository,
    IUnitOfWorkPort unitOfWork) : IUpdateProductPort
{
    private const string Origin = nameof(UpdateProductUseCase);

    public async Task<Result<UpdateProductOutputDto>> ExecuteAsync(
        Guid id, UpdateProductInputDto input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        // 1. Cargar el aggregate desde la base de datos
        var getResult = await repository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (getResult.IsFailure)
            return getResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        var aggregate = getResult.Value;

        // 2. Aplicar cambios (valida dominio)
        var updateResult = aggregate.Update(input.Name, input.Price);
        if (updateResult.IsFailure)
            return updateResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        // 3. Marcar como modificado en el repositorio
        var updateRepoResult = repository.Update(aggregate);
        if (updateRepoResult.IsFailure)
            return updateRepoResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        // 4. Confirmar — Unit of Work persiste todo o nada
        var commitResult = await unitOfWork.CommitAsync(ct).ConfigureAwait(false);
        if (commitResult.IsFailure)
            return commitResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        return aggregate.ToOutputDto();
    }
}
```

El patrón de enriquecimiento `with { Context = ..., Origin = ... }` es consistente en todos los pasos: cada error que se propaga hacia arriba lleva el contexto del bounded context y el nombre del use case como origen. Si `GetByIdAsync` no encuentra el producto, retorna automáticamente `ProductErrors.NotFound(id)` (configurado en `GetNotFoundError()` del adaptador de repositorio), que el framework traduce a `404 Not Found`.

---

## Paso 6 — Controller y DI

### Action en el controller

Agregar el action `Update` en `ProductController`. Usa `[HttpPut("{id}")]` y retorna `HttpOkResult<T>` (`200 OK`) en lugar de `HttpCreatedResult<T>` (`201 Created`), ya que el recurso ya existía.

```csharp
// Api/Controllers/ProductController.cs  (fragmento: agregar dentro de la clase)
[HttpPut("{id}")]
[Tags("products")]
[ValidateRequest]
[ProducesResponseType(typeof(HttpOkResult<UpdateProductOutputDto>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[EndpointSummary("Update a product")]
[EndpointDescription("Updates the name and price of an existing product.")]
public async Task<HttpOkResult<UpdateProductOutputDto>> Update(
    [FromRoute] Guid id,
    [FromBody] UpdateProductInputDto input,
    IUpdateProductPort updateProduct,
    CancellationToken ct)
{
    ArgumentNullException.ThrowIfNull(updateProduct);
    ArgumentNullException.ThrowIfNull(input);

    return await updateProduct
        .ExecuteAsync(id, input, ct)
        .ConfigureAwait(false);
}
```

El puerto `IUpdateProductPort` se recibe como parámetro del action (inyección por parámetro), igual que los demás casos de uso del controller.

### Registro en DI

Agregar el nuevo use case en `ProductServiceExtensions`:

```csharp
// Api/DependencyInjection/ProductServiceExtensions.cs
using Product.Application.Ports;
using Product.Application.UseCases.CreateProduct;
using Product.Application.UseCases.UpdateProduct;
using Product.Domain.Ports;
using Infrastructure.Adapters.Persistence.Product;

namespace Api.DependencyInjection;

public static class ProductServiceExtensions
{
    public static IServiceCollection AddProductServices(this IServiceCollection services)
    {
        services.AddScoped<ICreateProductPort, CreateProductUseCase>();
        services.AddScoped<IUpdateProductPort, UpdateProductUseCase>();   // ← agregar
        services.AddScoped<IProductRepositoryPort, ProductRepositoryAdapter>();
        return services;
    }
}
```

Use Cases se registran como `Scoped` para que compartan el mismo `DbContext` durante el request junto con el repositorio y el Unit of Work.

---

## Ver también

- [patron-result.md](../patron-result.md) — patrón de manejo de errores en use cases
- [repositorio.md](../repositorio.md) — contratos de repositorio y Unit of Work
- [nuevo-contexto.md](nuevo-contexto.md) — crear un contexto completo desde cero
