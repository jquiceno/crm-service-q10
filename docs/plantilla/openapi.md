# OpenAPI

Este documento describe cómo está integrado OpenAPI en la plantilla, cómo activarlo, cómo documentar endpoints en controllers y buenas prácticas.


---

## Resumen de la arquitectura de documentación

| Elemento | Paquete / rol |
|----------|---------------|
| **Generación del documento OpenAPI** | `Microsoft.AspNetCore.OpenApi` — documento JSON (por defecto OpenAPI 3.1) a partir de los endpoints descubiertos por el API Explorer. |
| **Interfaz interactiva** | `Scalar.AspNetCore` — sirve la UI y consume el JSON anterior. **No** se usa Swashbuckle ni Swagger UI: no están referenciados en ningún `.csproj`. |

El JSON se expone en `**/openapi/v1.json**` (documento por defecto llamado `v1`). La UI de Scalar se registra bajo el prefijo `/openapi` con el nombre de documento como segmento opcional, así que se abre en `**/openapi/v1**` (o `/openapi/`). Todo esto **solo en Development**.

No hay ninguna ruta `/swagger`: si buscás la UI, es `/openapi`.


---

## Por qué `Microsoft.AspNetCore.OpenApi` en lugar de Swashbuckle completo

**Dependencia por defecto**: Desde .NET 9 se ha dejado atrás Swagger en el ecosistema de dotnet y ha creado su propio paquete Microsoft.AspNetCore.OpenApi que está diseñado para ser mas rápido y soportar Native AOT (Ahead-of-Time compilation).

Como el paquete de Microsoft genera el JSON pero no trae visualizador, hace falta uno aparte. La plantilla usa [**Scalar**](https://scalar.com/), una interfaz más moderna que Swagger UI, en vez de arrastrar `Swashbuckle.AspNetCore.SwaggerUI`.

Referencia oficial: [Usar documentos OpenAPI generados](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/openapi/using-openapi-documents) y [Generar documentos OpenAPI](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/openapi/aspnetcore-openapi).


---

## Activación

En `Program.cs` la documentación interactiva y el endpoint del JSON **solo se registran en Development** (reduce fuga de información en producción).

### Registro de servicios

Ambos lados viven en `Api/DependencyInjection/OpenApiExtensions.cs`, y cada uno hace `return` temprano fuera de Development.

```csharp
// AddOpenApiDocumentation
services.AddOpenApi(options =>
{
    options.AddDocumentTransformer(...);   // Info: título, versión, contacto
    options.AddSchemaTransformer(...);     // documenta los valores de los enums
});
```

* `**AddOpenApi**`: registra la generación del documento (por defecto el nombre del documento es `**v1**`).
* El **document transformer** fija `Info` (título, versión, descripción, contacto) — es el lugar donde cada servicio pone sus datos.
* El **schema transformer** desenvuelve `Nullable<TEnum>` y lista los valores del enum en la descripción del schema, que si no aparecerían como enteros pelados.

### Pipeline HTTP

```csharp
// UseOpenApiDocumentation
app.MapOpenApi();
app.MapScalarApiReference("/openapi", options =>
{
    options.OpenApiRoutePattern = "/openapi/{documentName}.json";
});
app.MapGet("/openapi", () => Results.Redirect("/openapi/v1")).ExcludeFromDescription();
```

* `**MapOpenApi()**`: publica el JSON en `**/openapi/v1.json**` (ruta por convención).
* `**MapScalarApiReference**`: registra la UI bajo `/openapi` con el nombre de documento como segmento opcional (`/openapi/{documentName?}`), y el `OpenApiRoutePattern` le dice dónde buscar el JSON.
* El `MapGet` final hace que `/openapi` a secas lleve a `/openapi/v1`. Convive sin conflicto con la ruta de Scalar: la literal gana sobre la del parámetro opcional, no hay ambigüedad de enrutamiento.

Comprobado en Development contra la app corriendo:

| Ruta | Respuesta |
|------|-----------|
| `/openapi` | `302` → `/openapi/v1` |
| `/openapi/` | `302` → `/openapi/v1` |
| `/openapi/v1` | `200` (UI de Scalar) |
| `/openapi/v1.json` | `200` (documento JSON) |
| `/scalar` | `404` — no existe |


---

## Descripción de las propiedades de los DTOs

**Regla:** toda propiedad de un DTO de **entrada** y de **salida** lleva `[property: Description("...")]`. No es opcional ni "para los campos poco obvios": el generador de OpenAPI publica ese texto como la descripción del campo en el esquema, y un campo sin descripción sale al contrato sin explicación alguna.

Se usa `System.ComponentModel.DescriptionAttribute` con el prefijo `property:`, porque los DTOs son `record` posicionales y sin ese prefijo el atributo se aplicaría al parámetro del constructor en lugar de a la propiedad.

**DTO de entrada:**

```csharp
using System.ComponentModel;

public sealed record CreateProgramInputDto(
    [property: Description(
        "Client-assigned primary key. Up to 20 characters, letters, digits, hyphens, underscores and " +
        "spaces. A code already in use answers 409.")]
    string? Code,
    [property: Description("Program name.")]
    string Name,
    [property: Description("Whether the program is active.")]
    bool? IsActive,
    [property: Description("Short name printed on the student ID cards.")]
    string? Abbreviation = null);
```

**DTO de salida** — la misma regla, sin excepciones:

```csharp
public sealed record GetProgramsOutputDto(
    [property: Description("Code that identifies the program.")]
    string Code,
    [property: Description("Program name.")]
    string Name,
    [property: Description("Whether the program is offered for preinscriptions.")]
    bool? AppliesToPreinscription,
    [property: Description(
        "Evaluation type the program grades with. Serialized as the enum member's value.")]
    EvaluationType? EvaluationType);
```

Qué debe decir la descripción:

- **Qué es el campo en términos del negocio**, no su tipo (`"Short name printed on the student ID cards."`, no `"String opcional"`).
- **Las restricciones que el cliente necesita conocer**: longitudes máximas, rangos, catálogos de valores admitidos, y qué código HTTP produce violarlas (`"A code already in use answers 409."`).
- **Cómo se serializa** cuando no es evidente — enums que viajan como su valor numérico, fechas con zona, banderas con default.

Los comentarios XML (`/// <summary>`) del `record` completo siguen siendo útiles para explicar el DTO como conjunto (por qué los campos son nullable, qué columnas no existen), pero **no reemplazan** al `Description` de cada propiedad.

---

## Buenas prácticas


1. **El controller es un adaptador**: traduce HTTP ↔ DTOs de entrada/salida del caso de uso; la documentación OpenAPI describe ese contrato HTTP, no la lógica de dominio interna.
2. **Tipos de retorno explícitos**: preferir `ActionResult<T>` / `Task<ActionResult<T>>` sobre `IActionResult` cuando el éxito tenga un cuerpo estable; facilita inferencia de esquemas al especificar el tipo.
3. **Definir tipos y códigos de respuesta**: usar `[ProducesResponseType(typeof(MiDto), StatusCodes.Status200OK)]`, variantes para 400, 404, 422, etc.
4. **Especificar título y descripción en operaciones**: `[EndpointSummary("...")]`, `[EndpointDescription("...")]`
5. **Agrupación en la UI**: Usar tags por contexto o dominio, declarados **una vez a nivel de controller** — `[Tags("Programs")]` sobre la clase, no repetido en cada action
6. **Esquema de errores**: Definir también el tipo de error en los atributos de `[ProducesResponseType]`
7. **Describir cada propiedad de los DTOs de entrada y de salida** con `[property: Description("...")]` — ver [la sección anterior](#descripción-de-las-propiedades-de-los-dtos)


---

## Buenas prácticas de documentación (checklist)

### Contrato y precisión

- [ ] Usar **atributos EndpointSummary, EndpointDescription, y Tags** para describir la operación del API
- [ ] Declarar **todos los códigos HTTP relevantes** con `[ProducesResponseType]`.
- [ ] Usar **DTOs con nullable reference types** para distinguir obligatorios vs opcionales en el esquema.
- [ ] Usar **`[property: Description(...)]` en TODAS las propiedades** de los DTOs, tanto de **entrada** como de **salida**. Ninguna propiedad del contrato queda sin descripción.
- [ ] Declarar `[Tags(...)]` **a nivel de controller**, no por action.

### Metadatos "de producto"

- [ ] Ajustar **título, descripción, versión, contacto, licencia** del documento (transformadores en `AddOpenApi` o equivalente).
- [ ] Configurar `**servers**` para dev/stage/prod cuando el mismo binario se despliegue en varios entornos y el spec se consuma fuera de localhost.

### Seguridad

- [ ] Mantener **la UI de Scalar y** `**/openapi/\*.json**` **deshabilitados en producción**, salvo requisito explícito y que exista autenticación. La plantilla ya lo hace: `AddOpenApiDocumentation` y `UseOpenApiDocumentation` hacen `return` temprano fuera de Development.
- [ ] Cuando exista **JWT, API keys u OAuth**, registrar el **esquema de seguridad** en OpenAPI y marcar operaciones protegidas para que el botón de autorización de la UI sea coherente con la API real.


---

## Versionado de API y OpenAPI

**Hoy la plantilla no versiona las rutas.** El `v1` que aparece en `/openapi/v1.json` es el nombre del *documento* OpenAPI por defecto, no un segmento de versión en las rutas. Lo que sí llevan todos los endpoints es el prefijo de servicio (`RoutePrefix`, ej. `/service-template`): `GlobalRoutePrefixConvention` lo antepone a los controllers (`/service-template/info`) y `Program.cs` lo antepone a los minimal-API de health y OpenAPI (`/service-template/health/live`, `/service-template/openapi/v1.json`). No se usa `UsePathBase`; el prefijo forma parte de la ruta real que ve el enrutador.

Si en algún momento se necesita versionar, las opciones son un **prefijo en ruta** (`api/v1/...`) o un paquete como `Asp.Versioning.Mvc`, integrándolo en `Program.cs`. Nada de eso está cableado todavía.

Si coexisten varias versiones:

* Alinear **nombre del documento** (`AddOpenApi("v1")`, `AddOpenApi("v2")`) con las rutas o reglas de inclusión de endpoints.
* Registrar cada documento en Scalar para tener selector de versiones en la UI. El `OpenApiRoutePattern` que ya usa la plantilla (`/openapi/{documentName}.json`) está parametrizado por nombre de documento, así que sirve para varios sin cambios.


---
