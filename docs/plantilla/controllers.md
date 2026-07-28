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

Un controller nunca invoca a otro controller ni a un Use Case de un contexto distinto al que le da nombre — si necesita datos de otro contexto, ese otro contexto se consulta desde dentro del Use Case (vía Provider, ver [providers.md](providers.md)), no desde la capa HTTP.

---

## 3. Cómo se usan

El mecanismo clave es que `Result<T>`, `Result` y `PagedResult<T>` tienen **conversiones implícitas** hacia `HttpOkResult<T>` / `HttpCreatedResult<T>` / `HttpNoContentResult` / `HttpOkPagedResult<T>`. Por eso una action puede retornar directamente lo que el caso de uso devuelve, sin traducir el resultado a mano:

```csharp
public async Task<HttpOkResult<UpdateProductOutputDto>> Update(
    [FromRoute] Guid id,
    [FromBody] UpdateProductInputDto input,
    IUpdateProductUseCase updateProduct,   // ← caso de uso por parámetro
    CancellationToken ct)
    => await updateProduct.ExecuteAsync(id, input, ct).ConfigureAwait(false);
    //     ^ Result<UpdateProductOutputDto> se convierte implícitamente a HttpOkResult<UpdateProductOutputDto>
```

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
| `[Tags("...")]` | Agrupación en la UI de OpenAPI (Scalar) — ver [openapi.md](openapi.md) |
| `[ValidateRequest]` | Solo en actions con `[FromBody]` o `[FromQuery]` que requieren FluentValidation — ver [validaciones.md](validaciones.md) |
| `[ProducesResponseType(...)]` | Un atributo por cada status code posible, con el tipo exacto de la envoltura (`ApiSuccessResponse<T>` / `ApiErrorResponse`) |
| `[EndpointSummary(...)]` / `[EndpointDescription(...)]` | Título y descripción de la operación en OpenAPI |
| `[OutputCache(...)]` / `[OutputCacheInvalidate(...)]` | Solo en lecturas cacheables / escrituras que invalidan esa caché — ver [cache.md](cache.md) |
| Parámetros: ruta, query o body + interfaz del caso de uso + `CancellationToken` | La interfaz se recibe **por parámetro**, nunca por constructor — permite que cada action dependa solo del caso de uso que usa |
| Tipo de retorno `HttpXResult<T>` | Determina el status code de éxito; el de error lo decide el `ErrorType` del `DomainError` |

Las secciones siguientes muestran esta anatomía aplicada a cada tipo de operación, usando los mismos casos de uso definidos en [casos-de-uso.md](casos-de-uso.md) sobre el contexto `Product`.

---

## 5. Patrones de implementación por tipo de operación

### 5.1 Crear un recurso (POST → 201 Created)

```csharp
[HttpPost]
[Tags("products")]
[ValidateRequest]
[ProducesResponseType(typeof(ApiSuccessResponse<CreateProductOutputDto>), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
[EndpointSummary("Create a new product")]
[EndpointDescription("Creates a new product in the database.")]
public async Task<HttpCreatedResult<CreateProductOutputDto>> Create(
    [FromBody] CreateProductInputDto input,
    ICreateProductUseCase createProduct,
    CancellationToken ct)
    => await createProduct.ExecuteAsync(input, ct).ConfigureAwait(false);
```

`HttpCreatedResult<T>` responde `201` en éxito. No hay un `Location` header hacia el recurso creado por convención en esta plantilla — el cliente ya recibe el recurso completo en `data`.

### 5.2 Actualizar un recurso (PUT → 200 OK)

```csharp
[HttpPut("{id}")]
[Tags("products")]
[ValidateRequest]
[ProducesResponseType(typeof(ApiSuccessResponse<UpdateProductOutputDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
[EndpointSummary("Update a product")]
[EndpointDescription("Updates the name and price of an existing product.")]
public async Task<HttpOkResult<UpdateProductOutputDto>> Update(
    [FromRoute] Guid id,
    [FromBody] UpdateProductInputDto input,
    IUpdateProductUseCase updateProduct,
    CancellationToken ct)
    => await updateProduct.ExecuteAsync(id, input, ct).ConfigureAwait(false);
```

`HttpOkResult<T>` (`200`) en lugar de `HttpCreatedResult<T>` (`201`) — el recurso ya existía. El `404` se produce solo si el `Result` que retorna el Use Case falla con `ErrorType.NotFound`; el controller no lo comprueba explícitamente.

### 5.3 Eliminar un recurso (DELETE → 204 No Content)

```csharp
[HttpDelete("{id}")]
[Tags("products")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
[EndpointSummary("Delete a product")]
[EndpointDescription("Deletes the product with the given id.")]
public async Task<HttpNoContentResult> Delete(
    [FromRoute] Guid id,
    IDeleteProductUseCase deleteProduct,
    CancellationToken ct)
    => await deleteProduct.ExecuteAsync(id, ct).ConfigureAwait(false);
```

`Result` (sin valor) se convierte implícitamente a `HttpNoContentResult` — no hay `[ProducesResponseType(typeof(ApiSuccessResponse<...>), ...)]` para el 204 porque un `204` nunca lleva cuerpo.

### 5.4 Consultar un elemento (GET /{id} → 200 OK)

```csharp
[HttpGet("{id}")]
[Tags("products")]
[ProducesResponseType(typeof(ApiSuccessResponse<GetProductByIdOutputDto>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
[OutputCache(Duration = 60, Tags = ["products"])]
[EndpointSummary("Get product by id")]
[EndpointDescription("Returns the product with the given id.")]
public async Task<HttpOkResult<GetProductByIdOutputDto>> GetById(
    [FromRoute] Guid id,
    IGetProductByIdUseCase getProductById,
    CancellationToken ct)
    => await getProductById.ExecuteAsync(id, ct).ConfigureAwait(false);
```

No lleva `[ValidateRequest]` porque no hay DTO de entrada que validar — solo un `id` de ruta. `[OutputCache]` es opcional y solo aplica a lecturas; ver [cache.md](cache.md) para la invalidación por tags.

### 5.5 Consultar una lista paginada (GET → 200 OK)

```csharp
[HttpGet]
[Tags("products")]
[ValidateRequest]
[ProducesResponseType(typeof(ApiSuccessResponse<PagedPayload<GetAllProductsOutputDto>>), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
[OutputCache(Duration = 60, Tags = ["products"])]
[EndpointSummary("Get all products")]
[EndpointDescription("Returns a paginated and filtered list of products.")]
public async Task<HttpOkPagedResult<GetAllProductsOutputDto>> GetAll(
    [FromQuery] GetAllProductsInputDto filter,
    [FromQuery] PageQueryInputDto pagination,
    IGetAllProductsUseCase getAllProducts,
    CancellationToken ct)
{
    var page = new PageQuery(pagination.PageIndex, pagination.PageSize);
    return await getAllProducts.ExecuteAsync(filter, page, ct).ConfigureAwait(false);
}
```

`[FromQuery]` se usa dos veces con DTOs distintos: uno con los filtros propios del contexto (`GetAllProductsInputDto`) y otro genérico de paginación (`PageQueryInputDto`, ver [repositorio.md](repositorio.md#paginación)). `HttpOkPagedResult<T>` envuelve el `PagedResult<T>` en `{ data: { items, totalCount }, statusCode }`.

### 5.6 Sub-recurso / relación anidada (Link/Unlink)

Cuando el recurso es la relación entre dos entidades que ya existen (ver [casos-de-uso.md §5.6](casos-de-uso.md#56-relación-entre-agregados-existentes-link--unlink)), el controller vive bajo la ruta del recurso padre y no tiene DTO de entrada — los identificadores de ambos lados vienen de la ruta:

```csharp
[ApiController]
[Route("v1/products/{productId}/categories")]
public sealed class ProductCategoriesController : ControllerBase
{
    [HttpPost("{categoryId}")]
    [Tags("products")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [EndpointSummary("Link category to product")]
    [EndpointDescription("Links an existing category to the given product.")]
    [OutputCacheInvalidate("products")]
    public async Task<HttpNoContentResult> Link(
        [FromRoute] Guid productId,
        [FromRoute] Guid categoryId,
        ILinkProductCategoryUseCase linkProductCategory,
        CancellationToken ct)
        => await linkProductCategory.ExecuteAsync(productId, categoryId, ct).ConfigureAwait(false);

    [HttpDelete("{categoryId}")]
    [Tags("products")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status404NotFound)]
    [OutputCacheInvalidate("products")]
    public async Task<HttpNoContentResult> Unlink(
        [FromRoute] Guid productId,
        [FromRoute] Guid categoryId,
        IUnlinkProductCategoryUseCase unlinkProductCategory,
        CancellationToken ct)
        => await unlinkProductCategory.ExecuteAsync(productId, categoryId, ct).ConfigureAwait(false);
}
```

El `[Route]` a nivel de controller fija el prefijo del recurso padre (`products/{productId}`); cada action solo agrega el segmento propio (`categories/{categoryId}`). El `409 Conflict` en `Link` corresponde al caso "el vínculo ya existe" que el Use Case retorna con `ErrorType.Conflict`.

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
