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
- **Propagar errores**: sellando con `Context` y `Origin` solo los que él mismo origina, y dejando intactos los que ya vienen sellados desde otra pieza (ver [sección 7](#7-propagación-de-errores-context-y-origin)).

Un caso de uso **no** es responsable de:

- Validar reglas de negocio — eso vive en el Aggregate o en los Value Objects ([entidades-y-agregados.md](entidades-y-agregados.md), [value-objects.md](value-objects.md)).
- Validar la estructura del request (campos requeridos, formatos) — eso vive en el validador de FluentValidation ([validaciones.md](validaciones.md)).
- Saber cómo se persiste algo — eso vive en el repositorio del contexto ([repositorio.md](repositorio.md)).

Si notas que un caso de uso necesita datos auxiliares que no son su Aggregate —validar contra un catálogo, resolver un nombre desde otra tabla, aplicar un valor por defecto— esa responsabilidad se extrae a un **Reader** o a un **Provider** en lugar de crecer el use case — ver [sección 6](#6-paso-opcional--reader-o-provider).

---

## 3. Cómo se usan

Ciclo de una request para cualquier caso de uso:

```
1. El Controller recibe las interfaces de sus casos de uso por el constructor primario
2. El Controller invoca useCase.ExecuteAsync(input, cancellationToken)
3. El Use Case orquesta: dominio → repositorio → commit
4. El Use Case retorna un Result<TOutputDto> / Result / PagedResult<TOutputDto>
5. El framework traduce el Result al contrato HTTP uniforme (ver contrato-api.md)
```

**Los casos de uso se inyectan en el constructor del controller**, no como parámetro de cada action:

```csharp
[ApiController]
[Route("[controller]")]
[Tags("Products")]
public sealed class ProductsController(
    IGetProductsUseCase getProductsUseCase,
    IUpdateProductUseCase updateProductUseCase) : ControllerBase   // ← solo conoce las interfaces
{
    [HttpPut("{id}")]
    public async Task<HttpOkResult<UpdateProductOutputDto>> UpdateProduct(
        [FromRoute] Guid id,
        [FromBody] UpdateProductInputDto input,
        CancellationToken cancellationToken = default)
    {
        return await updateProductUseCase.ExecuteAsync(id, input, cancellationToken).ConfigureAwait(false);
    }
}
```

La firma de la action queda con el contrato HTTP puro (ruta, query, body, `CancellationToken`) y sin dependencias mezcladas. El parámetro se nombra `{casoDeUso}UseCase` para que no colisione con nada del request.

Los casos de uso se registran como `Scoped` en el contenedor de DI, para que compartan el mismo `DbContext` del request junto con el repositorio y el Unit of Work.

### Retornar el resultado de forma implícita

`Result<T>` define `implicit operator Result<T>(T value)` e `implicit operator Result<T>(DomainError error)`, y `Result` define la conversión desde `DomainError`. Aprovecharlas es la forma canónica: **no envuelvas a mano con `Result<T>.Success(...)` / `Result.Failure(...)`**.

```csharp
// ✔ implícito
return aggregate.ToOutputDto();          // T          → Result<T>
return result.Error;                     // DomainError → Result<T>
return ProgramErrors.NotFound(code);     // DomainError → Result

// ✘ ruido innecesario
return Result<UpdateProductOutputDto>.Success(aggregate.ToOutputDto());
return Result<UpdateProductOutputDto>.Failure(result.Error);
```

Las dos excepciones legítimas, donde el compilador no puede inferir la conversión:

- `PagedResult<T>`, que se construye con dos argumentos: `PagedResult<T>.Success(items, totalCount)` y `PagedResult<T>.Failure(error)`.
- Cuando el valor se construye con una expresión de colección (`[.. items.Select(...)]`) y el tipo destino es una interfaz: se declara primero la variable tipada y se retorna, o se usa `Result<IReadOnlyList<T>>.Success(dtos)`.

```csharp
IReadOnlyList<AuditStatisticsSeriesDto> dtos = [.. result.Value.Select(s => s.ToDto())];
return Result<IReadOnlyList<AuditStatisticsSeriesDto>>.Success(dtos);
```

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
    CreateProductInputDto input, CancellationToken cancellationToken = default)
{
    var aggregateResult = input.ToAggregate();          // 1. construir (valida dominio)
    if (aggregateResult.IsFailure)
        return aggregateResult.Error with { Context = ProductErrors.Context, Origin = Origin };

    var addResult = await repository                    // 2. persistir
        .AddAsync(aggregateResult.Value, cancellationToken)
        .ConfigureAwait(false);
    if (addResult.IsFailure)
        return addResult.Error;                         //    ya viene sellado por el repositorio

    var commitResult = await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);  // 3. commit
    if (commitResult.IsFailure)
        return commitResult.Error;                      //    ya viene sellado por UnitOfWorkAdapter

    return aggregateResult.Value.ToOutputDto();         // 4. implícito → Result<CreateProductOutputDto>
}
```

Solo el error del paso 1 se sella: nace del dominio, que no conoce ni el contexto ni quién lo invocó. Los pasos 2 y 3 devuelven errores que el repositorio y el Unit of Work ya estamparon con su propio `Origin` — ver [sección 7](#7-propagación-de-errores-context-y-origin).

Cuando el `INSERT` necesita confirmarse dentro del repositorio (por ejemplo para recuperar una `IDENTITY`), el contrato del contexto expone `CreateAsync` en lugar de `AddAsync` y el caso de uso **no** inyecta `IUnitOfWorkPort`:

```csharp
public sealed class CreateAuditLogEntryUseCase(
    IAuditLogRepository repository,
    IPersonNameReader personNameReader) : ICreateAuditLogEntryUseCase
{
    public async Task<Result<CreateAuditLogEntryOutputDto>> ExecuteAsync(
        CreateAuditLogEntryInputDto input, CancellationToken cancellationToken = default)
    {
        var userFullName = await personNameReader
            .GetFullNameAsync(input.UserPersonCode, cancellationToken)
            .ConfigureAwait(false);

        var aggregateResult = input.ToAggregate(userFullName);
        if (aggregateResult.IsFailure)
            return aggregateResult.Error;

        var persistResult = await repository
            .CreateAsync(aggregateResult.Value, cancellationToken)
            .ConfigureAwait(false);
        if (persistResult.IsFailure)
            return persistResult.Error;

        return persistResult.Value.ToOutputDto();
    }
}
```

Ver [repositorio.md](repositorio.md#createasync--cuando-el-insert-debe-confirmarse-dentro-del-repositorio).

---

### 5.2 Comando de actualización (Update)

A diferencia de `Create`, el Aggregate no se construye — se carga del repositorio y se modifica a través de un método propio (`Update()`), nunca reemplazando la instancia.

**Interfaz del caso de uso:**

```csharp
// Contexts/Product/Application/UseCases/UpdateProduct/IUpdateProductUseCase.cs
public interface IUpdateProductUseCase
{
    Task<Result<UpdateProductOutputDto>> ExecuteAsync(
        Guid id, UpdateProductInputDto input, CancellationToken cancellationToken = default);
}
```

**DTOs** — **todas** las propiedades, tanto de entrada como de salida, llevan `[property: Description(...)]`; es lo que alimenta la documentación OpenAPI del contrato (ver [openapi.md](openapi.md#descripción-de-las-propiedades-de-los-dtos)):

```csharp
// Contexts/Product/Application/UseCases/UpdateProduct/UpdateProductInputDto.cs
using System.ComponentModel;

public sealed record UpdateProductInputDto(
    [property: Description("Nuevo nombre del producto. Máximo 200 caracteres.")]
    string? Name,
    [property: Description("Nuevo precio del producto. Debe ser mayor o igual a 0.")]
    decimal Price);

// Contexts/Product/Application/UseCases/UpdateProduct/UpdateProductOutputDto.cs
public sealed record UpdateProductOutputDto(
    [property: Description("Identificador del producto.")]
    Guid Id,
    [property: Description("Nombre del producto.")]
    string Name,
    [property: Description("Precio vigente del producto.")]
    decimal Price,
    [property: Description("Fecha de la última actualización, en UTC.")]
    DateTime UpdatedAt);
```

`Name` es nullable para que el validador de entrada pueda reportar el error con su `Property` correcta en lugar de que el deserializador falle con un 400 genérico.

**Mapping** — no hay `ToAggregate()` (el agregado ya existe); hay `ToUpdateArgs()`, que arma el `record` de argumentos del dominio, y `ToOutputDto()`:

```csharp
// Contexts/Product/Application/UseCases/UpdateProduct/UpdateProductMapping.cs
public static class UpdateProductMapping
{
    public static UpdateProductArgs ToUpdateArgs(this UpdateProductInputDto input)
        => new(input.Name!, input.Price);

    public static UpdateProductOutputDto ToOutputDto(this ProductAggregate aggregate)
        => new(aggregate.Id, aggregate.Name, aggregate.Price, (aggregate.UpdatedAt ?? aggregate.CreatedAt)!.Value);
}
```

`UpdateProductArgs` lleva **solo primitivos**; los Value Objects los construye el agregado por dentro (ver [entidades-y-agregados.md](entidades-y-agregados.md#args-records-de-argumentos-de-los-factories)).

**Método de actualización en el Aggregate** — valida los nuevos valores y, si son correctos, actualiza el estado interno y marca la entidad como modificada:

```csharp
// Contexts/Product/Domain/Aggregates/ProductAggregate.cs  (fragmento)
public Result Update(UpdateProductArgs input)
{
    var priceResult = Price.Create(input.Price);
    if (priceResult.IsFailure)
        return priceResult.TypedError;

    Name  = input.Name;
    Price = priceResult.Value.Value;
    SetUpdatedAt(DateTime.UtcNow);   // actualiza la columna de auditoría que EF Core persiste

    return Result.Success();
}
```

El agregado devuelve el error sin `Context` ni `Origin`: no sabe desde dónde lo invocan. Es el caso de uso el que lo sella al propagarlo.

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
        Guid id, UpdateProductInputDto input, CancellationToken cancellationToken = default)
    {
        var getResult = await repository                                    // 1. cargar
            .GetByIdAsync(id, cancellationToken)
            .ConfigureAwait(false);
        if (getResult.IsFailure)
            return getResult.Error;

        var aggregate = getResult.Value;

        var updateResult = aggregate.Update(input.ToUpdateArgs());          // 2. aplicar cambios
        if (updateResult.IsFailure)
            return updateResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        var updateRepoResult = repository.Update(aggregate);                // 3. marcar modificado
        if (updateRepoResult.IsFailure)
            return updateRepoResult.Error;

        var commitResult = await unitOfWork                                 // 4. commit
            .CommitAsync(cancellationToken)
            .ConfigureAwait(false);
        if (commitResult.IsFailure)
            return commitResult.Error;

        return aggregate.ToOutputDto();
    }
}
```

De las cuatro ramas de fallo, solo la del paso 2 vuelve a sellar el error: es la única que nace dentro del dominio. Las otras tres propagan tal cual — ver [sección 7](#7-propagación-de-errores-context-y-origin).

`aggregate.Update(...)` recibe un `record` de argumentos (`UpdateProductArgs`) construido por el mapping, no una lista de primitivos sueltos. Ver [entidades-y-agregados.md](entidades-y-agregados.md#args-records-de-argumentos-de-los-factories).

Si `GetByIdAsync` no encuentra el producto, el repositorio retorna `ProductErrors.NotFound(id) with { Origin = Origin }`, que el framework traduce a `404 Not Found`.

**Controller:**

```csharp
// Api/Controllers/ProductsController.cs  (fragmento)
[HttpPut("{id}")]
[ValidateRequest]
[ProducesResponseType(typeof(ApiSuccessResponse<UpdateProductOutputDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
public async Task<HttpOkResult<UpdateProductOutputDto>> UpdateProduct(
    [FromRoute] Guid id,
    [FromBody] UpdateProductInputDto input,
    CancellationToken cancellationToken = default)
{
    return await updateProductUseCase.ExecuteAsync(id, input, cancellationToken).ConfigureAwait(false);
}
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
    Task<Result> ExecuteAsync(Guid id, CancellationToken cancellationToken = default);
}
```

**Use Case** — cuando el `RemoveAsync` del repositorio ya confirma el borrado por su cuenta (borrado en cascada dentro de una transacción, `ExecuteDelete`, etc.), el caso de uso no inyecta `IUnitOfWorkPort` y se reduce a delegar:

```csharp
// Contexts/Product/Application/UseCases/DeleteProduct/DeleteProductUseCase.cs
public sealed class DeleteProductUseCase(IProductRepository repository) : IDeleteProductUseCase
{
    public Task<Result> ExecuteAsync(Guid id, CancellationToken cancellationToken = default) =>
        repository.RemoveAsync(id, cancellationToken);
}
```

Si en cambio `RemoveAsync` solo marca el agregado para borrado, el caso de uso agrega el commit:

```csharp
var removeResult = await repository.RemoveAsync(id, cancellationToken).ConfigureAwait(false);
if (removeResult.IsFailure)
    return removeResult.Error;

return await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
```

`RemoveAsync` recibe el id y resuelve el agregado internamente: si no existe retorna el `NotFoundError` del contexto, así que el caso de uso no necesita un `GetByIdAsync` previo solo para validar existencia (ver `IRootRepository` en [repositorio.md](repositorio.md)).

**Controller:**

```csharp
[HttpDelete("{id}")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
public async Task<HttpNoContentResult> DeleteProduct(
    [FromRoute] Guid id,
    CancellationToken cancellationToken = default)
{
    return await deleteProductUseCase.ExecuteAsync(id, cancellationToken).ConfigureAwait(false);
}
```

---

### 5.4 Consulta de un elemento (Get by Id)

Es solo lectura: no hay Aggregate que mutar ni `IUnitOfWorkPort` que inyectar. El Mapping solo necesita `ToOutputDto()`.

**Interfaz del caso de uso:**

```csharp
// Contexts/Product/Application/UseCases/GetProductById/IGetProductByIdUseCase.cs
public interface IGetProductByIdUseCase
{
    Task<Result<GetProductByIdOutputDto>> ExecuteAsync(
        Guid id, CancellationToken cancellationToken = default);
}
```

**Use Case** — no origina ningún error propio, así que no necesita ni la constante `Origin`:

```csharp
// Contexts/Product/Application/UseCases/GetProductById/GetProductByIdUseCase.cs
public sealed class GetProductByIdUseCase(IProductRepository repository) : IGetProductByIdUseCase
{
    public async Task<Result<GetProductByIdOutputDto>> ExecuteAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var result = await repository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            return result.Error;

        return result.Value.ToOutputDto();
    }
}
```

**Controller:**

```csharp
[HttpGet("{id}")]
[ProducesResponseType(typeof(ApiSuccessResponse<GetProductByIdOutputDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
[OutputCache(Duration = 60, Tags = [CacheTag])]
public async Task<HttpOkResult<GetProductByIdOutputDto>> GetProductById(
    [FromRoute] Guid id,
    CancellationToken cancellationToken = default)
{
    return await getProductByIdUseCase.ExecuteAsync(id, cancellationToken).ConfigureAwait(false);
}
```

---

### 5.5 Consulta de lista paginada (Get All / List)

Cuando el listado admite filtros, el contexto define un objeto de filtro propio (no genérico) y una query específica en su repositorio — la interfaz base `IRootRepository.GetAllAsync(PageQuery)` no conoce filtros de negocio.

**Objeto de filtro (dominio):**

```csharp
// Contexts/Product/Domain/Queries/ProductFilter.cs
public sealed record ProductFilter(string? NameContains, decimal? MinPrice, decimal? MaxPrice);
```

Los objetos de filtro viven en `Domain/Queries/`, junto con los demás modelos de consulta del contexto (filas de proyección, series). Si el filtro tiene reglas propias — un rango de fechas con ventana por defecto, por ejemplo — se construye con un factory `Create(...)` que retorna `Result<ProductFilter>` y valida, en lugar de un constructor libre.

**Repositorio extendido:**

```csharp
// Contexts/Product/Domain/Repositories/IProductRepository.cs  (fragmento)
public interface IProductRepository : IRootRepository<ProductAggregate, Guid>
{
    Task<PagedResult<ProductAggregate>> GetAsync(
        ProductFilter filter, PageQuery page, CancellationToken cancellationToken = default);
}
```

**Interfaz del caso de uso:**

```csharp
// Contexts/Product/Application/UseCases/GetProducts/IGetProductsUseCase.cs
public interface IGetProductsUseCase
{
    Task<PagedResult<GetProductsOutputDto>> ExecuteAsync(
        GetProductsInputDto input, PageQuery page, CancellationToken cancellationToken = default);
}
```

**Use Case** — construye el filtro desde el DTO de entrada y mapea cada item del resultado paginado:

```csharp
// Contexts/Product/Application/UseCases/GetProducts/GetProductsUseCase.cs
public sealed class GetProductsUseCase(IProductRepository repository) : IGetProductsUseCase
{
    public async Task<PagedResult<GetProductsOutputDto>> ExecuteAsync(
        GetProductsInputDto input, PageQuery page, CancellationToken cancellationToken = default)
    {
        var filter = new ProductFilter(input.NameContains, input.MinPrice, input.MaxPrice);

        var result = await repository.GetAsync(filter, page, cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
            return PagedResult<GetProductsOutputDto>.Failure(result.Error);

        return PagedResult<GetProductsOutputDto>.Success(
            [.. result.Items.Select(p => p.ToOutputDto())],
            result.TotalCount);
    }
}
```

`PagedResult<T>` es la excepción al retorno implícito: `Success` toma dos argumentos y `Failure` es necesario para propagar el error, porque la conversión implícita solo existe desde `DomainError`. En ambos casos el error del repositorio se propaga **sin** tocar `Context`/`Origin`. Ver [repositorio.md](repositorio.md#paginación) para el flujo de paginación de punta a punta (`PageQuery`, `PageQueryInputDto`, contrato de respuesta).

**Controller:**

```csharp
[HttpGet]
[ValidateRequest]
[ProducesResponseType(typeof(ApiSuccessResponse<PagedPayload<GetProductsOutputDto>>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
[OutputCache(Duration = 60, Tags = [CacheTag])]
public async Task<HttpOkPagedResult<GetProductsOutputDto>> GetProducts(
    [FromQuery] GetProductsInputDto filter,
    [FromQuery] PageQueryInputDto pagination,
    CancellationToken cancellationToken = default)
{
    return await getProductsUseCase.ExecuteAsync(
        filter,
        new PageQuery(pagination.PageIndex, pagination.PageSize),
        cancellationToken).ConfigureAwait(false);
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
    Task<Result> ExecuteAsync(
        Guid productId, Guid categoryId, CancellationToken cancellationToken = default);
}
```

**Use Case** — valida ambos lados antes de crear el vínculo, y falla explícitamente si ya existe (en lugar de un `INSERT` duplicado silencioso o un error de base de datos). Nótese qué se sella y qué no: los fallos de infraestructura (`.Error`) se propagan tal cual; las reglas que decide **este** caso de uso (`NotFound`, `AlreadyLinked`) sí se estampan con `Context` y `Origin`:

```csharp
// Contexts/Product/Application/UseCases/LinkProductCategory/LinkProductCategoryUseCase.cs
public sealed class LinkProductCategoryUseCase(
    IProductRepository productRepository,
    ICategoryRepository categoryRepository,
    IProductCategoryRepository linkRepository) : ILinkProductCategoryUseCase
{
    private const string Origin = nameof(LinkProductCategoryUseCase);

    public async Task<Result> ExecuteAsync(
        Guid productId, Guid categoryId, CancellationToken cancellationToken = default)
    {
        var productExists = await productRepository
            .ExistsAsync(productId, cancellationToken)
            .ConfigureAwait(false);
        if (productExists.IsFailure)
            return productExists.Error;
        if (!productExists.Value)
            return ProductErrors.NotFound(productId) with { Context = ProductErrors.Context, Origin = Origin };

        var categoryExists = await categoryRepository
            .ExistsAsync(categoryId, cancellationToken)
            .ConfigureAwait(false);
        if (categoryExists.IsFailure)
            return categoryExists.Error;
        if (!categoryExists.Value)
            return CategoryErrors.NotFound(categoryId) with { Context = ProductErrors.Context, Origin = Origin };

        var alreadyLinked = await linkRepository
            .ExistsAsync(productId, categoryId, cancellationToken)
            .ConfigureAwait(false);
        if (alreadyLinked.IsFailure)
            return alreadyLinked.Error;
        if (alreadyLinked.Value)
            return ProductErrors.CategoryAlreadyLinked(productId, categoryId)
                with { Context = ProductErrors.Context, Origin = Origin };

        return await linkRepository
            .CreateAsync(productId, categoryId, cancellationToken)
            .ConfigureAwait(false);
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
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
public async Task<HttpNoContentResult> LinkCategory(
    [FromRoute] Guid productId,
    [FromRoute] Guid categoryId,
    CancellationToken cancellationToken = default)
{
    return await linkProductCategoryUseCase
        .ExecuteAsync(productId, categoryId, cancellationToken)
        .ConfigureAwait(false);
}
```

El caso de uso de *Unlink* (desvincular) sigue la misma estructura, cambiando solo la última llamada por `linkRepository.RemoveAsync(...)` y, en vez de fallar si el vínculo ya existe, fallar (o retornar éxito idempotente, según la regla de negocio) si el vínculo **no** existe.

---

## 6. Paso opcional — Reader o Provider

Si el use case necesita datos auxiliares que **no son su Aggregate** — validar contra un catálogo, resolver un nombre desde una tabla foránea, aplicar un valor por defecto — esa responsabilidad no crece dentro del use case: se extrae. Cuál de las dos piezas corresponde depende de la fuente:

| La fuente es… | Pieza | Dónde vive |
|---|---|---|
| Un catálogo, una tabla foránea, una vista — cualquier cosa que **no** sea un repositorio | **Reader** (`I{Concepto}Reader`) | interfaz en `Application/Ports/`, implementación en `Persistence/EntityFramework/{Contexto}/` |
| **Solo** repositorios del propio servicio, y el fin es completar el input | **Provider** (`{Contexto}{Concepto}Provider`) | clase concreta en `Application/Providers/` |

En la práctica, el caso frecuente es el Reader. El use case lo recibe por constructor, igual que el repositorio:

```csharp
public sealed class CreateProgramUseCase(
    IProgramRepository repository,
    IProgramClassificationReader classificationReader) : ICreateProgramUseCase
{
    private const string Origin = nameof(CreateProgramUseCase);

    public async Task<Result<CreateProgramOutputDto>> ExecuteAsync(
        CreateProgramInputDto input, CancellationToken cancellationToken = default)
    {
        var aggregateResult = input.ToAggregate();
        if (aggregateResult.IsFailure)
            return aggregateResult.Error with { Context = ProgramErrors.Context, Origin = Origin };

        // Después del dominio: un body malformado responde 400 y nunca llega al catálogo.
        if (input.ClassificationId is int classificationId)
        {
            var classificationResult = await classificationReader
                .ExistsAsync(classificationId, cancellationToken)
                .ConfigureAwait(false);

            if (classificationResult.IsFailure)
                return classificationResult.Error;

            if (!classificationResult.Value)
                return ProgramErrors.ClassificationDoesNotExist(classificationId) with { Origin = Origin };
        }

        var persistResult = await repository
            .CreateAsync(aggregateResult.Value, cancellationToken)
            .ConfigureAwait(false);
        if (persistResult.IsFailure)
            return persistResult.Error;

        return persistResult.Value.ToOutputDto();
    }
}
```

El orden importa: **primero el dominio, después el Reader**. Validar el catálogo antes de construir el agregado gastaría una query para un request que de todas formas iba a responder 400.

> El árbol de decisión completo (Reader vs. Provider vs. Repository) está en [conceptos-reader-provider-repository.md](conceptos-reader-provider-repository.md).

### El Provider

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

## 7. Propagación de errores: `Context` y `Origin`

Todo `DomainError` lleva dos campos de trazabilidad: `Context` (el bounded context al que pertenece el error) y `Origin` (la clase que lo produjo). La regla es una sola:

> **Cada pieza sella los errores que ella misma origina, y no toca los que recibe.**

En consecuencia, dentro de un caso de uso hay dos tipos de rama de fallo y se tratan distinto:

| Origen del error | Qué hace el caso de uso | Por qué |
|---|---|---|
| **Propio o del dominio** — el agregado, un Value Object, o una regla que decide el propio use case | `return error with { Context = XErrors.Context, Origin = Origin };` | El dominio no conoce ni el contexto ni quién lo invocó; el use case es la primera capa que sí lo sabe |
| **De una pieza que ya lo selló** — repositorio, Reader, `UnitOfWorkAdapter`, Provider | `return result.Error;` | Reescribir el `Origin` borraría la traza real: el log diría `UpdateProgramUseCase` cuando el fallo ocurrió en `ProgramRepository` |

```csharp
// El agregado no sabe desde dónde lo llaman → el use case sella
var updateResult = aggregate.Update(input.ToUpdateArgs());
if (updateResult.IsFailure)
    return updateResult.Error with { Context = ProgramErrors.Context, Origin = Origin };

// El repositorio ya estampó Origin = "ProgramRepository" → se propaga intacto
var saveResult = repository.Update(aggregate);
if (saveResult.IsFailure)
    return saveResult.Error;

// El UnitOfWorkAdapter ya estampó Origin = "UnitOfWorkAdapter" → se propaga intacto
var commitResult = await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
if (commitResult.IsFailure)
    return commitResult.Error;
```

Lo mismo aplica del lado de infraestructura: el repositorio y los readers declaran `private const string Origin = nameof(MiClase)` y lo estampan tanto en los errores de persistencia (`PersistenceErrors.Failure(Origin)`, `SqlServerErrorClassifier.Classify(ex, Origin)`) como en los de negocio que ellos resuelven (`ProgramErrors.NotFound(id) with { Origin = Origin }`).

Los tests de casos de uso fijan esta decisión explícitamente: `result.Error.Origin.ShouldBe("ProgramRepository", "the use case does not replace the origin of the failure")`. Un caso de uso que no origina ningún error propio —una consulta simple— no necesita siquiera declarar la constante `Origin`.

---

## Ver también

- [patron-result.md](patron-result.md) — patrón de manejo de errores en use cases
- [conceptos-reader-provider-repository.md](conceptos-reader-provider-repository.md) — Reader vs. Provider vs. Repository
- [providers.md](providers.md) — cuándo y cómo extraer lógica auxiliar a un Provider
- [repositorio.md](repositorio.md) — contratos de repositorio, Unit of Work y paginación
- [puertos-y-adaptadores.md](puertos-y-adaptadores.md) — por qué la interfaz del caso de uso no lleva sufijo `Port`, y nomenclatura completa
- [controllers.md](controllers.md) — cómo cada caso de uso se expone como action HTTP
- [contextos.md](contextos.md) — crear un contexto completo desde cero
