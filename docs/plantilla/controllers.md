# Controllers

## 1. Qué es un controller

Un controller es el **adaptador de entrada HTTP**: traduce una request (ruta, query, body) en la llamada a un caso de uso, y traduce el `Result` que ese caso de uso retorna en una respuesta HTTP. Vive en `Api/Controllers/`.

```
HTTP Request  →  Controller.Action  →  I{CasoDeUso}UseCase (interfaz)  →  UseCase  →  Result<T> / Result / PagedResult<T>
                                                                                              │
HTTP Response  ←───────────────────────────────────────────────────────────────────────────┘
```

El controller no conoce la implementación del caso de uso, solo su interfaz — la misma regla descrita en [casos-de-uso.md](casos-de-uso.md). No contiene lógica de negocio ni de persistencia: si una action empieza a acumular `if`s sobre datos de dominio, esa lógica está en el lugar equivocado.

---

## 2. Propósito y cuándo crear uno nuevo

**Un controller por recurso HTTP**, no necesariamente uno por contexto. La convención de esta plantilla:

| Situación | Decisión |
|---|---|
| Todas las operaciones de `Product` (crear, actualizar, listar, eliminar) | Un solo `ProductController`, con una action por operación |
| Un sub-recurso con su propio ciclo de rutas anidadas (ej. los adjuntos de un anuncio: `/announcements/{id}/attachments`) | Un controller aparte para ese sub-recurso, con la ruta padre como prefijo — ver [5.6](#56-sub-recurso--relación-anidada-linkunlink) |
| Un endpoint sin modelo de dominio propio (ej. info del servicio) | Un controller liviano que expone un único caso de uso — ver el contexto `ServiceInfo` en este mismo servicio |

Un controller nunca invoca a otro controller ni a un Use Case de un contexto distinto al que le da nombre — si necesita datos de otro contexto o de una tabla ajena al Aggregate, eso se consulta desde dentro del Use Case (vía Reader o Provider, ver [conceptos-reader-provider-repository.md](conceptos-reader-provider-repository.md)), no desde la capa HTTP.

Los DTOs de entrada y de salida que el controller expone deben documentar **todas** sus propiedades con `[property: Description(...)]`: es lo que el generador de OpenAPI publica como descripción de cada campo del contrato. Ver [openapi.md](openapi.md#descripción-de-las-propiedades-de-los-dtos).

---

## 3. Cómo se usan

**Los casos de uso se inyectan por el constructor primario del controller**, no como parámetro de cada action. Así la firma de la action queda con el contrato HTTP puro y las dependencias se declaran en un solo lugar:

```csharp
[ApiController]
[Route("[controller]")]
[Tags("Products")]
public sealed class ProductsController(
    IGetProductsUseCase getProductsUseCase,
    IGetProductByIdUseCase getProductByIdUseCase,
    ICreateProductUseCase createProductUseCase,
    IUpdateProductUseCase updateProductUseCase,
    IDeleteProductUseCase deleteProductUseCase) : ControllerBase
{
    private const string CacheTag = "products";
    // …actions…
}
```

Convenciones que se ven en ese encabezado:

- `[Route("[controller]")]` — la ruta sale del nombre del controller, no se escribe a mano. Solo se escribe literal cuando el recurso no coincide con el nombre de la clase (`[Route("logs")]`, o una ruta anidada).
- `[Tags("…")]` **a nivel de controller**, no repetido en cada action: todas las actions de un controller pertenecen al mismo grupo de OpenAPI.
- El tag de caché se declara como `private const string CacheTag` y se reutiliza en `[OutputCache]` y `[OutputCacheInvalidate]`, para que lectura e invalidación no puedan desalinearse.
- Los parámetros del constructor se nombran `{casoDeUso}UseCase`.

El otro mecanismo clave es que `Result<T>`, `Result` y `PagedResult<T>` tienen **conversiones implícitas** hacia `HttpOkResult<T>` / `HttpCreatedResult<T>` / `HttpNoContentResult` / `HttpOkPagedResult<T>`. Por eso una action puede retornar directamente lo que el caso de uso devuelve, sin traducir el resultado a mano:

```csharp
[HttpPut("{id}")]
public async Task<HttpOkResult<UpdateProductOutputDto>> UpdateProduct(
    [FromRoute] Guid id,
    [FromBody] UpdateProductInputDto input,
    CancellationToken cancellationToken = default)
{
    return await updateProductUseCase.ExecuteAsync(id, input, cancellationToken).ConfigureAwait(false);
    //     ^ Result<UpdateProductOutputDto> se convierte implícitamente a HttpOkResult<UpdateProductOutputDto>
}
```

El `CancellationToken` se nombra `cancellationToken` y se declara con `= default` al final de la lista de parámetros.

Ciclo completo de una request:

```
1. ASP.NET Core enruta la request a la action según [Http{Verbo}] y la ruta del controller
2. [ValidateRequest] ejecuta la validación estructural del DTO antes de entrar a la action (ver validaciones.md)
3. La action invoca useCase.ExecuteAsync(...)
4. El HttpXResult envuelve el Result en el contrato uniforme { data, statusCode } / { error, statusCode }
5. Si el Result es exitoso, HttpXResult escribe el status code de éxito de esa action (200 / 201 / 204)
6. Si el Result falló, HttpXResult traduce el ErrorType del DomainError al status code correspondiente (400 / 404 / 409 / …)
```

El mapeo `ErrorType → status code` y la forma exacta del JSON están documentados en [contrato-api.md](contrato-api.md); este documento no lo repite.

---

## 4. Anatomía común de una action

Toda action, sin importar el tipo de operación, combina las mismas piezas:

| Pieza | Para qué |
|---|---|
| `[Http{Verbo}("{ruta}")]` | Verbo HTTP y segmento de ruta relativo al `[Route]` del controller |
| `[ValidateRequest]` | Solo en actions con `[FromBody]` o `[FromQuery]` que requieren FluentValidation — ver [validaciones.md](validaciones.md) |
| `[ProducesResponseType(...)]` | Un atributo por cada status code posible, con el tipo exacto de la envoltura (`ApiSuccessResponse<T>` / `ApiErrorResponse`) |
| `[EndpointSummary(...)]` / `[EndpointDescription(...)]` | Título y descripción de la operación en OpenAPI |
| `[OutputCache(...)]` / `[OutputCacheInvalidate(...)]` | Solo en lecturas cacheables / escrituras que invalidan esa caché — ver [cache.md](cache.md) |
| Parámetros: solo ruta, query o body + `CancellationToken cancellationToken = default` | Los casos de uso **no** son parámetros de la action: se inyectan por el constructor del controller |
| Tipo de retorno `HttpXResult<T>` | Determina el status code de éxito; el de error lo decide el `ErrorType` del `DomainError` |

`[Tags("...")]` no aparece en la tabla porque se declara **una vez a nivel de controller**, no por action — ver [sección 3](#3-cómo-se-usan).

Las secciones siguientes muestran esta anatomía aplicada a cada tipo de operación, usando los mismos casos de uso definidos en [casos-de-uso.md](casos-de-uso.md) sobre el contexto `Product`.

---

## 5. Patrones de implementación por tipo de operación

### 5.1 Crear un recurso (POST → 201 Created)

```csharp
[HttpPost]
[ValidateRequest]
[EndpointSummary("Create product")]
[EndpointDescription("Creates a product with the client-assigned code and returns the created resource.")]
[ProducesResponseType(typeof(ApiSuccessResponse<CreateProductOutputDto>), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
[OutputCacheInvalidate(CacheTag)]
public async Task<HttpCreatedResult<CreateProductOutputDto>> CreateProduct(
    [FromBody] CreateProductInputDto input,
    CancellationToken cancellationToken = default)
{
    return await createProductUseCase.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
}
```

`HttpCreatedResult<T>` responde `201` en éxito. No hay un `Location` header hacia el recurso creado por convención en esta plantilla — el cliente ya recibe el recurso completo en `data`.

### 5.2 Actualizar un recurso (PUT → 200 OK)

```csharp
[HttpPut("{id}")]
[ValidateRequest]
[EndpointSummary("Update product")]
[EndpointDescription("Updates the product with the given id. An unknown id answers 404.")]
[ProducesResponseType(typeof(ApiSuccessResponse<UpdateProductOutputDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
[OutputCacheInvalidate(CacheTag)]
public async Task<HttpOkResult<UpdateProductOutputDto>> UpdateProduct(
    [FromRoute] Guid id,
    [FromBody] UpdateProductInputDto input,
    CancellationToken cancellationToken = default)
{
    return await updateProductUseCase.ExecuteAsync(id, input, cancellationToken).ConfigureAwait(false);
}
```

`HttpOkResult<T>` (`200`) en lugar de `HttpCreatedResult<T>` (`201`) — el recurso ya existía. El `404` se produce solo si el `Result` que retorna el Use Case falla con `ErrorType.NotFound`; el controller no lo comprueba explícitamente.

### 5.3 Eliminar un recurso (DELETE → 204 No Content)

```csharp
[HttpDelete("{id}")]
[EndpointSummary("Delete product")]
[EndpointDescription("Deletes the product with the given id.")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
[OutputCacheInvalidate(CacheTag)]
public async Task<HttpNoContentResult> DeleteProduct(
    [FromRoute] Guid id,
    CancellationToken cancellationToken = default)
{
    return await deleteProductUseCase.ExecuteAsync(id, cancellationToken).ConfigureAwait(false);
}
```

`Result` (sin valor) se convierte implícitamente a `HttpNoContentResult` — no hay `[ProducesResponseType(typeof(ApiSuccessResponse<...>), ...)]` para el 204 porque un `204` nunca lleva cuerpo.

### 5.4 Consultar un elemento (GET /{id} → 200 OK)

```csharp
[HttpGet("{id}")]
[EndpointSummary("Get product by id")]
[EndpointDescription("Returns the product with the given id.")]
[ProducesResponseType(typeof(ApiSuccessResponse<GetProductByIdOutputDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
[OutputCache(Duration = 60, Tags = [CacheTag])]
public async Task<HttpOkResult<GetProductByIdOutputDto>> GetProductById(
    [FromRoute] Guid id,
    CancellationToken cancellationToken = default)
{
    return await getProductByIdUseCase.ExecuteAsync(id, cancellationToken).ConfigureAwait(false);
}
```

No lleva `[ValidateRequest]` porque no hay DTO de entrada que validar — solo un `id` de ruta. `[OutputCache]` es opcional y solo aplica a lecturas; ver [cache.md](cache.md) para la invalidación por tags.

### 5.5 Consultar una lista paginada (GET → 200 OK)

```csharp
[HttpGet]
[ValidateRequest]
[EndpointSummary("Get products")]
[EndpointDescription("Returns a paginated and filtered list of products.")]
[ProducesResponseType(typeof(ApiSuccessResponse<PagedPayload<GetProductsOutputDto>>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
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

`[FromQuery]` se usa dos veces con DTOs distintos: uno con los filtros propios del contexto (`GetProductsInputDto`) y otro genérico de paginación (`PageQueryInputDto`, ver [repositorio.md](repositorio.md#paginación)). `HttpOkPagedResult<T>` envuelve el `PagedResult<T>` en `{ data: { items, totalCount }, statusCode }`.

**Lecturas que no se deben cachear.** La política base de output cache varía por tenant y headers, **no** por los parámetros de filtro de la query. En un listado filtrado eso significaría servir el resultado de un filtro para otro, así que ese endpoint se excluye explícitamente:

```csharp
[HttpGet]
[OutputCache(NoStore = true)]   // los datos cambian demasiado / el filtro no participa de la clave
[ValidateRequest]
```

### 5.6 Sub-recurso / relación anidada (Link/Unlink)

Cuando el recurso es la relación entre dos entidades que ya existen (ver [casos-de-uso.md §5.6](casos-de-uso.md#56-relación-entre-agregados-existentes-link--unlink)), el controller vive bajo la ruta del recurso padre y no tiene DTO de entrada — los identificadores de ambos lados vienen de la ruta:

```csharp
[ApiController]
[Route("products/{productId}/categories")]
[Tags("Products")]
public sealed class ProductCategoriesController(
    ILinkProductCategoryUseCase linkProductCategoryUseCase,
    IUnlinkProductCategoryUseCase unlinkProductCategoryUseCase) : ControllerBase
{
    private const string CacheTag = "products";

    [HttpPost("{categoryId}")]
    [EndpointSummary("Link category to product")]
    [EndpointDescription("Links an existing category to the given product.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [OutputCacheInvalidate(CacheTag)]
    public async Task<HttpNoContentResult> Link(
        [FromRoute] Guid productId,
        [FromRoute] Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        return await linkProductCategoryUseCase
            .ExecuteAsync(productId, categoryId, cancellationToken)
            .ConfigureAwait(false);
    }

    [HttpDelete("{categoryId}")]
    [EndpointSummary("Unlink category from product")]
    [EndpointDescription("Removes the link between the given product and category.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [OutputCacheInvalidate(CacheTag)]
    public async Task<HttpNoContentResult> Unlink(
        [FromRoute] Guid productId,
        [FromRoute] Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        return await unlinkProductCategoryUseCase
            .ExecuteAsync(productId, categoryId, cancellationToken)
            .ConfigureAwait(false);
    }
}
```

El `[Route]` a nivel de controller fija el prefijo del recurso padre (`products/{productId}`); cada action solo agrega el segmento propio (`categories/{categoryId}`). Aquí la ruta se escribe literal, porque no coincide con el nombre del controller. Las rutas no llevan prefijo de versión — ver [openapi.md](openapi.md#versionado-de-api-y-openapi). El `409 Conflict` en `Link` corresponde al caso "el vínculo ya existe" que el Use Case retorna con `ErrorType.Conflict`.

---

## 6. Invalidación de caché en escrituras

Toda action que crea, actualiza, elimina o modifica una relación debe invalidar la caché de las lecturas afectadas con `[OutputCacheInvalidate("{tag}")]`, usando el mismo tag que la lectura correspondiente declaró en `[OutputCache(Tags = [...])]`. El detalle completo (por qué, cómo se propaga el tag, diferencia entre invalidación L1 y L2) vive en [cache.md](cache.md) — este documento solo señala que la invalidación se declara a nivel de action, junto a los demás atributos.

---

## Ver también

- [casos-de-uso.md](casos-de-uso.md) — los casos de uso que cada action invoca
- [puertos-y-adaptadores.md](puertos-y-adaptadores.md) — por qué la interfaz del caso de uso no lleva sufijo `Port`
- [contrato-api.md](contrato-api.md) — forma exacta del JSON de éxito y error
- [openapi.md](openapi.md) — buenas prácticas de documentación OpenAPI por action
- [validaciones.md](validaciones.md) — qué valida `[ValidateRequest]` y qué no
- [cache.md](cache.md) — `[OutputCache]`, `[OutputCacheInvalidate]`, L1 vs L2
- [contextos.md](contextos.md) — dónde vive el controller respecto al resto del contexto
