# Casos de uso

## 1. Qué es un caso de uso

Un caso de uso (*Use Case*) es una clase de la capa **Application** que encapsula **una única operación de negocio**: crear un producto, actualizar su precio, listar productos filtrados, etc. Vive en `Contexts/{Contexto}/Application/UseCases/{NombreDelCasoDeUso}/`, junto con la interfaz que lo declara.

```
Controller  →  I{CasoDeUso}UseCase (interfaz)  →  {CasoDeUso}UseCase (implementación)  →  Dominio / Repositorio
```

El controller nunca conoce la implementación concreta, solo la interfaz del caso de uso (`I{CasoDeUso}UseCase`). Esto es lo que permite testear el controller y el caso de uso de forma aislada, y sustituir la implementación sin tocar la capa de presentación. Ver [puertos-y-adaptadores.md](puertos-y-adaptadores.md) para por qué esta interfaz no lleva sufijo `Port` y para la nomenclatura completa.

---

## 2. Propósito y cuándo usarlo

**Un caso de uso por operación, no por entidad.** `Product` no tiene una clase `ProductUseCase` con varios métodos; tiene `CreateProductUseCase`, `UpdateProductUseCase`, `GetProductByIdUseCase`, etc. — cada uno con una sola responsabilidad y un único método `ExecuteAsync`.

Un caso de uso es responsable de:

- **Orquestar** la operación: cargar datos, invocar al dominio, persistir, retornar.
- **Traducir** entre el mundo HTTP (DTOs) y el mundo de dominio (Aggregates).
- **Propagar errores** enriqueciéndolos con `Context` y `Origin` (ver [patron-result.md](patron-result.md)).

Un caso de uso **no** es responsable de:

- Validar reglas de negocio — eso vive en el Aggregate o en los Value Objects ([entidades-y-agregados.md](entidades-y-agregados.md), [value-objects.md](value-objects.md)).
- Validar la estructura del request (campos requeridos, formatos) — eso vive en el validador de FluentValidation ([validaciones.md](validaciones.md)).
- Saber cómo se persiste algo — eso vive en el adaptador de repositorio ([repositorio.md](repositorio.md)).

Si notas que un caso de uso necesita resolver datos auxiliares antes de ejecutar su lógica principal (por ejemplo, "si el cliente no envía categorías, usa las categorías activas por defecto"), esa responsabilidad se extrae a un **Provider** en lugar de crecer el use case — ver [sección 6](#6-paso-opcional--provider).

---

## 3. Cómo se usan

Ciclo de una request para cualquier caso de uso:

```
1. El Controller recibe la interfaz del caso de uso por parámetro (inyección por parámetro, no por constructor)
2. El Controller invoca useCase.ExecuteAsync(input, ct)
3. El Use Case orquesta: dominio → repositorio → commit
4. El Use Case retorna un Result<TOutputDto> / Result / PagedResult<TOutputDto>
5. El framework traduce el Result al contrato HTTP uniforme (ver contrato-api.md)
```

```csharp
public async Task<HttpOkResult<UpdateProductOutputDto>> Update(
    [FromRoute] Guid id,
    [FromBody] UpdateProductInputDto input,
    IUpdateProductUseCase updateProduct,   // ← el controller solo conoce la interfaz
    CancellationToken ct)
{
    return await updateProduct.ExecuteAsync(id, input, ct).ConfigureAwait(false);
}
```

Los casos de uso se registran como `Scoped` en el contenedor de DI, para que compartan el mismo `DbContext` del request junto con el repositorio y el Unit of Work.

---

## 4. Anatomía común

Sin importar el tipo de operación (crear, leer, actualizar, eliminar, relacionar), todo caso de uso se construye con las mismas cuatro piezas, ubicadas junto a los demás elementos del contexto:

| Pieza | Ubicación | Responsabilidad |
|---|---|---|
| Interfaz + Use Case | `Application/UseCases/{CasoDeUso}/I{CasoDeUso}UseCase.cs` + `{CasoDeUso}UseCase.cs` | Contrato que el controller invoca, e implementación — coubicados |
| DTOs (input/output) | `Application/UseCases/{CasoDeUso}/` | Forma del request/response HTTP |
| Mapping | `Application/UseCases/{CasoDeUso}/{CasoDeUso}Mapping.cs` | Traducción DTO ↔ Aggregate |
| Controller + DI | `Api/Controllers/` y `Api/DependencyInjection/` | Exposición HTTP y registro en el contenedor |

Lo único que cambia entre variantes es **qué hace el Use Case por dentro** y, en consecuencia, la forma de los DTOs y de su interfaz. La [sección 5](#5-patrones-de-implementación-por-tipo-de-operación) muestra esas diferencias con ejemplos, todos sobre el mismo contexto de referencia `Product` (`Name: string`, `Price: decimal`).

---

## 5. Patrones de implementación por tipo de operación

### 5.1 Comando de creación (Create)

Construye un Aggregate nuevo a partir del DTO de entrada (`ToAggregate()`) y lo persiste con `repository.AddAsync()`.

> Ejemplo completo (dominio + aplicación + infraestructura + API) en [contextos.md](contextos.md), que usa `CreateProduct` como caso de uso guía para levantar un contexto de principio a fin.

Patrón resumido del Use Case:

```csharp
public async Task<Result<CreateProductOutputDto>> ExecuteAsync(
    CreateProductInputDto input, CancellationToken ct = default)
{
    var aggregateResult = input.ToAggregate();          // 1. construir (valida dominio)
    if (aggregateResult.IsFailure)
        return aggregateResult.Error with { Context = ProductErrors.Context, Origin = Origin };

    var addResult = await repository.AddAsync(aggregateResult.Value, ct);  // 2. persistir
    if (addResult.IsFailure)
        return addResult.Error with { Context = ProductErrors.Context, Origin = Origin };

    var commitResult = await unitOfWork.CommitAsync(ct);  // 3. commit
    if (commitResult.IsFailure)
        return commitResult.Error with { Context = ProductErrors.Context, Origin = Origin };

    return aggregateResult.Value.ToOutputDto();
}
```

---

### 5.2 Comando de actualización (Update)

A diferencia de `Create`, el Aggregate no se construye — se carga del repositorio y se modifica a través de un método propio (`Update()`), nunca reemplazando la instancia.

**Interfaz del caso de uso:**

```csharp
// Contexts/Product/Application/UseCases/UpdateProduct/IUpdateProductUseCase.cs
public interface IUpdateProductUseCase
{
    Task<Result<UpdateProductOutputDto>> ExecuteAsync(
        Guid id, UpdateProductInputDto input, CancellationToken ct = default);
}
```

**DTOs:**

```csharp
// Contexts/Product/Application/UseCases/UpdateProduct/UpdateProductInputDto.cs
public sealed record UpdateProductInputDto(
    [property: Description("Nuevo nombre del producto. Máximo 200 caracteres.")]
    string? Name,
    [property: Description("Nuevo precio del producto. Debe ser mayor o igual a 0.")]
    decimal Price);

// Contexts/Product/Application/UseCases/UpdateProduct/UpdateProductOutputDto.cs
public sealed record UpdateProductOutputDto(Guid Id, string Name, decimal Price, DateTime UpdatedAt);
```

`Name` es nullable para que el validador de entrada pueda reportar el error con su `Property` correcta en lugar de que el deserializador falle con un 400 genérico.

**Mapping** — solo `ToOutputDto()`, no hay `ToAggregate()`:

```csharp
// Contexts/Product/Application/UseCases/UpdateProduct/UpdateProductMapping.cs
public static class UpdateProductMapping
{
    public static UpdateProductOutputDto ToOutputDto(this ProductAggregate aggregate)
        => new(aggregate.Id, aggregate.Name, aggregate.Price, (aggregate.UpdatedAt ?? aggregate.CreatedAt)!.Value);
}
```

**Método de actualización en el Aggregate** — valida los nuevos valores y, si son correctos, actualiza el estado interno y marca la entidad como modificada:

```csharp
// Contexts/Product/Domain/Aggregates/ProductAggregate.cs  (fragmento)
public Result Update(string? name, decimal price)
{
    if (string.IsNullOrWhiteSpace(name))
        return ProductErrors.NameRequired with { Context = ProductErrors.Context, Origin = nameof(Update) };

    var priceResult = Price.Create(price);
    if (priceResult.IsFailure)
        return priceResult.TypedError with { Context = ProductErrors.Context, Origin = nameof(Update) };

    Name  = name;
    Price = priceResult.Value.Value;
    SetUpdatedAt(DateTime.UtcNow);   // actualiza la columna de auditoría que EF Core persiste

    return Result.Success();
}
```

El agregado **es** una entidad (`AggregateRoot<TId> : Entity<TId>`) — por eso `Update()` asigna las propiedades directamente y llama a `SetUpdatedAt()`, definido en `AggregateRoot<TId>`. Ver [entidades-y-agregados.md](entidades-y-agregados.md).

Retorna `Result` (no `Result<T>`) porque la operación no produce un nuevo valor — solo indica éxito o fracaso.

**Use Case** — cargar → aplicar cambios → marcar como modificado → commit:

```csharp
// Contexts/Product/Application/UseCases/UpdateProduct/UpdateProductUseCase.cs
public sealed class UpdateProductUseCase(
    IProductRepository repository,
    IUnitOfWorkPort unitOfWork) : IUpdateProductUseCase
{
    private const string Origin = nameof(UpdateProductUseCase);

    public async Task<Result<UpdateProductOutputDto>> ExecuteAsync(
        Guid id, UpdateProductInputDto input, CancellationToken ct = default)
    {
        var getResult = await repository.GetByIdAsync(id, ct).ConfigureAwait(false);   // 1. cargar
        if (getResult.IsFailure)
            return getResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        var aggregate = getResult.Value;

        var updateResult = aggregate.Update(input.Name, input.Price);                  // 2. aplicar cambios
        if (updateResult.IsFailure)
            return updateResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        var updateRepoResult = repository.Update(aggregate);                           // 3. marcar modificado
        if (updateRepoResult.IsFailure)
            return updateRepoResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        var commitResult = await unitOfWork.CommitAsync(ct).ConfigureAwait(false);      // 4. commit
        if (commitResult.IsFailure)
            return commitResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        return aggregate.ToOutputDto();
    }
}
```

Si `GetByIdAsync` no encuentra el producto, retorna automáticamente `ProductErrors.NotFound(id)` (configurado en `GetNotFoundError()` del adaptador de repositorio), que el framework traduce a `404 Not Found`.

**Controller:**

```csharp
// Api/Controllers/ProductController.cs  (fragmento)
[HttpPut("{id}")]
[Tags("products")]
[ValidateRequest]
[ProducesResponseType(typeof(HttpOkResult<UpdateProductOutputDto>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<HttpOkResult<UpdateProductOutputDto>> Update(
    [FromRoute] Guid id,
    [FromBody] UpdateProductInputDto input,
    IUpdateProductUseCase updateProduct,
    CancellationToken ct)
    => await updateProduct.ExecuteAsync(id, input, ct).ConfigureAwait(false);
```

Usa `[HttpPut]` y `HttpOkResult<T>` (`200 OK`) en lugar de `HttpCreatedResult<T>` (`201 Created`), ya que el recurso ya existía.

**DI:**

```csharp
services.AddScoped<IUpdateProductUseCase, UpdateProductUseCase>();
```

---

### 5.3 Comando de eliminación (Delete)

No hay DTO de salida ni Mapping — el flujo es puramente de orquestación: cargar (para confirmar existencia) → eliminar → commit.

**Interfaz del caso de uso:**

```csharp
// Contexts/Product/Application/UseCases/DeleteProduct/IDeleteProductUseCase.cs
public interface IDeleteProductUseCase
{
    Task<Result> ExecuteAsync(Guid id, CancellationToken ct = default);
}
```

**Use Case:**

```csharp
// Contexts/Product/Application/UseCases/DeleteProduct/DeleteProductUseCase.cs
public sealed class DeleteProductUseCase(
    IProductRepository repository,
    IUnitOfWorkPort unitOfWork) : IDeleteProductUseCase
{
    private const string Origin = nameof(DeleteProductUseCase);

    public async Task<Result> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var getResult = await repository.GetByIdAsync(id, ct).ConfigureAwait(false);   // 1. confirmar que existe
        if (getResult.IsFailure)
            return getResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        var removeResult = repository.Remove(getResult.Value);                          // 2. eliminar
        if (removeResult.IsFailure)
            return removeResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        return await unitOfWork.CommitAsync(ct).ConfigureAwait(false);                  // 3. commit
    }
}
```

`GetByIdAsync` cumple doble función: valida existencia (retorna `NotFound` automáticamente si no existe) y entrega la instancia que `Remove()` necesita — el repositorio elimina por Aggregate, no por id suelto (ver `IRootRepository` en [repositorio.md](repositorio.md)).

**Controller:**

```csharp
[HttpDelete("{id}")]
[Tags("products")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<HttpNoContentResult> Delete(
    [FromRoute] Guid id, IDeleteProductUseCase deleteProduct, CancellationToken ct)
    => await deleteProduct.ExecuteAsync(id, ct).ConfigureAwait(false);
```

---

### 5.4 Consulta de un elemento (Get by Id)

Es solo lectura: no hay Aggregate que mutar ni `IUnitOfWorkPort` que inyectar. El Mapping solo necesita `ToOutputDto()`.

**Interfaz del caso de uso:**

```csharp
// Contexts/Product/Application/UseCases/GetProductById/IGetProductByIdUseCase.cs
public interface IGetProductByIdUseCase
{
    Task<Result<GetProductByIdOutputDto>> ExecuteAsync(Guid id, CancellationToken ct = default);
}
```

**Use Case:**

```csharp
// Contexts/Product/Application/UseCases/GetProductById/GetProductByIdUseCase.cs
public sealed class GetProductByIdUseCase(IProductRepository repository) : IGetProductByIdUseCase
{
    private const string Origin = nameof(GetProductByIdUseCase);

    public async Task<Result<GetProductByIdOutputDto>> ExecuteAsync(Guid id, CancellationToken ct = default)
    {
        var getResult = await repository.GetByIdAsync(id, ct).ConfigureAwait(false);
        if (getResult.IsFailure)
            return getResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        return getResult.Value.ToOutputDto();
    }
}
```

**Controller:**

```csharp
[HttpGet("{id}")]
[Tags("products")]
[ProducesResponseType(typeof(HttpOkResult<GetProductByIdOutputDto>), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
public async Task<HttpOkResult<GetProductByIdOutputDto>> GetById(
    [FromRoute] Guid id, IGetProductByIdUseCase getProductById, CancellationToken ct)
    => await getProductById.ExecuteAsync(id, ct).ConfigureAwait(false);
```

---

### 5.5 Consulta de lista paginada (Get All / List)

Cuando el listado admite filtros, el contexto define un objeto de filtro propio (no genérico) y una query específica en su repositorio — la interfaz base `IRootRepository.GetAllAsync(PageQuery)` no conoce filtros de negocio.

**Objeto de filtro (dominio):**

```csharp
// Contexts/Product/Domain/Filters/ProductFilter.cs
public sealed record ProductFilter(string? NameContains, decimal? MinPrice, decimal? MaxPrice);
```

**Repositorio extendido:**

```csharp
// Contexts/Product/Domain/Repositories/IProductRepository.cs  (fragmento)
public interface IProductRepository : IRootRepository<ProductAggregate, Guid>
{
    Task<PagedResult<ProductAggregate>> SearchAsync(
        ProductFilter filter, PageQuery page, CancellationToken ct = default);
}
```

**Interfaz del caso de uso:**

```csharp
// Contexts/Product/Application/UseCases/GetAllProducts/IGetAllProductsUseCase.cs
public interface IGetAllProductsUseCase
{
    Task<PagedResult<GetAllProductsOutputDto>> ExecuteAsync(
        GetAllProductsInputDto input, PageQuery page, CancellationToken ct = default);
}
```

**Use Case** — construye el filtro desde el DTO de entrada y mapea cada item del resultado paginado:

```csharp
// Contexts/Product/Application/UseCases/GetAllProducts/GetAllProductsUseCase.cs
public sealed class GetAllProductsUseCase(IProductRepository repository) : IGetAllProductsUseCase
{
    public async Task<PagedResult<GetAllProductsOutputDto>> ExecuteAsync(
        GetAllProductsInputDto input, PageQuery page, CancellationToken ct = default)
    {
        var filter = new ProductFilter(input.NameContains, input.MinPrice, input.MaxPrice);

        var result = await repository.SearchAsync(filter, page, ct).ConfigureAwait(false);
        if (result.IsFailure)
            return PagedResult<GetAllProductsOutputDto>.Failure(result.Error);

        return PagedResult<GetAllProductsOutputDto>.Success(
            [.. result.Items.Select(p => p.ToOutputDto())],
            result.TotalCount);
    }
}
```

Al no retornar `Result<T>` sino `PagedResult<T>` directamente, no hay enriquecimiento manual de `Context`/`Origin` en este Use Case — `PagedResult.Failure` propaga el error tal como lo entrega el repositorio. Ver [repositorio.md](repositorio.md#paginación) para el flujo de paginación de punta a punta (`PageQuery`, `PageQueryInputDto`, contrato de respuesta).

**Controller:**

```csharp
[HttpGet]
[Tags("products")]
[ValidateRequest]
public async Task<HttpOkResult<PagedResult<GetAllProductsOutputDto>>> GetAll(
    [FromQuery] GetAllProductsInputDto input,
    [FromQuery] PageQueryInputDto pagination,
    IGetAllProductsUseCase getAllProducts,
    CancellationToken ct)
{
    var page = new PageQuery(pagination.PageIndex, pagination.PageSize);
    return await getAllProducts.ExecuteAsync(input, page, ct).ConfigureAwait(false);
}
```

---

### 5.6 Relación entre agregados existentes (Link / Unlink)

Cuando el caso de uso no crea ni modifica un Aggregate sino que **vincula o desvincula dos entidades que ya existen** (una relación N:M, por ejemplo asociar una `Category` existente a un `Product` existente), el patrón cambia: no se carga ningún Aggregate completo, se coordinan varios repositorios y se valida existencia de cada lado más la duplicidad del vínculo.

**Interfaz del caso de uso:**

```csharp
// Contexts/Product/Application/UseCases/LinkProductCategory/ILinkProductCategoryUseCase.cs
public interface ILinkProductCategoryUseCase
{
    Task<Result> ExecuteAsync(Guid productId, Guid categoryId, CancellationToken ct = default);
}
```

**Use Case** — valida ambos lados antes de crear el vínculo, y falla explícitamente si ya existe (en lugar de un `INSERT` duplicado silencioso o un error de base de datos):

```csharp
// Contexts/Product/Application/UseCases/LinkProductCategory/LinkProductCategoryUseCase.cs
public sealed class LinkProductCategoryUseCase(
    IProductRepository productRepository,
    ICategoryRepository categoryRepository,
    IProductCategoryRepository linkRepository) : ILinkProductCategoryUseCase
{
    private const string Origin = nameof(LinkProductCategoryUseCase);

    public async Task<Result> ExecuteAsync(Guid productId, Guid categoryId, CancellationToken ct = default)
    {
        var productExists = await productRepository.ExistsAsync(productId, ct).ConfigureAwait(false);
        if (productExists.IsFailure)
            return productExists.Error with { Context = ProductErrors.Context, Origin = Origin };
        if (!productExists.Value)
            return ProductErrors.NotFound(productId) with { Context = ProductErrors.Context, Origin = Origin };

        var categoryExists = await categoryRepository.ExistsAsync(categoryId, ct).ConfigureAwait(false);
        if (categoryExists.IsFailure)
            return categoryExists.Error with { Context = ProductErrors.Context, Origin = Origin };
        if (!categoryExists.Value)
            return CategoryErrors.NotFound(categoryId) with { Context = ProductErrors.Context, Origin = Origin };

        var alreadyLinked = await linkRepository.ExistsAsync(productId, categoryId, ct).ConfigureAwait(false);
        if (alreadyLinked.IsFailure)
            return alreadyLinked.Error with { Context = ProductErrors.Context, Origin = Origin };
        if (alreadyLinked.Value)
            return ProductErrors.CategoryAlreadyLinked(productId, categoryId) with { Context = ProductErrors.Context, Origin = Origin };

        return await linkRepository.CreateAsync(productId, categoryId, ct).ConfigureAwait(false);
    }
}
```

Puntos clave:

- Cada `ExistsAsync` es una query ligera (`SELECT 1 ... WHERE ...`), no una carga completa del Aggregate — el caso de uso solo necesita confirmar existencia, no manipular estado.
- El orden de validación importa para el mensaje de error: primero el lado que falta con más frecuencia o es más barato de comprobar.
- `IProductCategoryRepository` es el repositorio de la tabla de relación; puede vivir en el contexto que "posee" la relación (`Product`, en este ejemplo) sin necesidad de un Aggregate propio.

**Controller:**

```csharp
[HttpPost("{productId}/categories/{categoryId}")]
[Tags("products")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public async Task<HttpNoContentResult> LinkCategory(
    [FromRoute] Guid productId, [FromRoute] Guid categoryId,
    ILinkProductCategoryUseCase linkProductCategory, CancellationToken ct)
    => await linkProductCategory.ExecuteAsync(productId, categoryId, ct).ConfigureAwait(false);
```

El caso de uso de *Unlink* (desvincular) sigue la misma estructura, cambiando solo la última llamada por `linkRepository.RemoveAsync(...)` y, en vez de fallar si el vínculo ya existe, fallar (o retornar éxito idempotente, según la regla de negocio) si el vínculo **no** existe.

---

## 6. Paso opcional — Provider

Si el use case necesita resolver datos auxiliares antes de ejecutar su lógica principal (por ejemplo, aplicar un valor por defecto cuando el cliente no envía ciertos campos), extrae esa responsabilidad a un Provider en lugar de manejarla dentro del use case.

**Señales de que necesitas un Provider:**

- El use case depende de más de un repositorio.
- Hay una condición de tipo "si viene vacío, busca en BD" que enturbia el flujo principal.
- La misma lógica de resolución podría reutilizarse desde otro use case.

```csharp
// Application/Providers/ProductCategoriesProvider.cs
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

El use case recibe el Provider por constructor y lo invoca al inicio, antes de construir el aggregate:

```csharp
public sealed class CreateProductUseCase(
    IProductRepository repository,
    ProductCategoriesProvider categoriesProvider) : ICreateProductUseCase
{
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

Registrar el Provider como `Scoped` antes de los use cases en el `*ServiceExtensions`:

```csharp
services.AddScoped<ProductCategoriesProvider>();
services.AddScoped<ICreateProductUseCase, CreateProductUseCase>();
```

> Ver [providers.md](providers.md) para el patrón completo y convenciones de naming.

---

## Ver también

- [patron-result.md](patron-result.md) — patrón de manejo de errores en use cases
- [providers.md](providers.md) — cuándo y cómo extraer lógica auxiliar a un Provider
- [repositorio.md](repositorio.md) — contratos de repositorio, Unit of Work y paginación
- [puertos-y-adaptadores.md](puertos-y-adaptadores.md) — por qué la interfaz del caso de uso no lleva sufijo `Port`, y nomenclatura completa
- [controllers.md](controllers.md) — cómo cada caso de uso se expone como action HTTP
- [contextos.md](contextos.md) — crear un contexto completo desde cero
