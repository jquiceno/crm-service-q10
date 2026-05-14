# OpenAPI y documentación de API

Este documento describe cómo está integrado OpenAPI en la plantilla, cómo activarlo, cómo documentar endpoints en controllers y buenas prácticas.

---

## Resumen de la arquitectura de documentación

| Elemento                             | Paquete / rol                                                                                                                                                              |
| ------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Generación del documento OpenAPI** | `Microsoft.AspNetCore.OpenApi` — documento JSON (por defecto OpenAPI 3.1) a partir de los endpoints descubiertos por el API Explorer.                                      |
| **Interfaz Swagger UI**              | `Swashbuckle.AspNetCore.SwaggerUI` — solo la UI embebida; **no** se usa `Swashbuckle.AspNetCore` completo (sin SwaggerGen ni middleware `UseSwagger` para servir el JSON). |

El JSON oficial se expone en **`/openapi/v1.json`** (documento por defecto llamado `v1`). Swagger UI se expone en **`/swagger`** y apunta a ese JSON.

---

## Por qué `Microsoft.AspNetCore.OpenApi` en lugar de Swashbuckle completo

**Dependencia por defecto**: Desde .NET 9 se ha dejado atrás Swagger en el ecosistema de dotnet y ha creado su propio paquete Microsoft.AspNetCore.OpenApi que está diseñado para ser mas rápido y soportar Native AOT (Ahead-of-Time compilation).

Se sigue usando Swagger UI ya que el paquete de Microsoft no trae por defecto un visualizador. Otra alternativa a considerar es usar [**Scalar**](https://scalar.com/) que es una interfaz mas moderna y completa que Swagger UI

Referencia oficial: [Usar documentos OpenAPI generados](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/openapi/using-openapi-documents) y [Generar documentos OpenAPI](https://learn.microsoft.com/es-es/aspnet/core/fundamentals/openapi/aspnetcore-openapi).

---

## Activación

En `Program.cs` la documentación interactiva y el endpoint del JSON **solo se registran en Development** (reduce fuga de información en producción).

### Registro de servicios

```csharp
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi();
}
```

- **`AddEndpointsApiExplorer`**: necesario para que el API Explorer describa los endpoints.
- **`AddOpenApi`**: registra la generación del documento (por defecto el nombre del documento es **`v1`**).

### Pipeline HTTP

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
}
```

- **`MapOpenApi()`**: publica el JSON en **`/openapi/v1.json`** (ruta por convención).
- **`UseSwaggerUI`**: sirve la UI en `/swagger` y carga el JSON anterior.

---

## Buenas prácticas

1. **El controller es un adaptador**: traduce HTTP ↔ DTOs de entrada/salida del caso de uso; la documentación OpenAPI describe ese contrato HTTP, no la lógica de dominio interna.
2. **Tipos de retorno explícitos**: preferir `ActionResult<T>` / `Task<ActionResult<T>>` sobre `IActionResult` cuando el éxito tenga un cuerpo estable; facilita inferencia de esquemas al especificar el tipo.
3. **Definir tipos y códigos de respuesta**: usar `[ProducesResponseType(typeof(MiDto), StatusCodes.Status200OK)]`, variantes para 400, 404, 422, etc.
4. **Especificar título y descripción en operaciones**: `[EndpointSummary("...")]`, `[EndpointDescription("...")]`
5. **Agrupación en Swagger UI**: Usar tags preferiblemente por contexto o dominio `[Tags("dominio")]`
6. **Esquema de errores**: Definir también el tipo de error en los atributos de `[ProducesResponseType]`

---

## Buenas prácticas de documentación (checklist)

### Contrato y precisión

- [ ] Usar **atributos EndpointSummary, EndpointDescription, y Tags** para describir la operación del API
- [ ] Declarar **todos los códigos HTTP relevantes** con `[ProducesResponseType]`.
- [ ] Usar **DTOs con nullable reference types** para distinguir obligatorios vs opcionales en el esquema.
- [ ] Usar **Atributo Description** en las propiedades del DTO para documentar la propiedad.

### Metadatos “de producto”

- [ ] Ajustar **título, descripción, versión, contacto, licencia** del documento (transformadores en `AddOpenApi` o equivalente).
- [ ] Configurar **`servers`** para dev/stage/prod cuando el mismo binario se despliegue en varios entornos y el spec se consuma fuera de localhost.

### Seguridad

- [ ] Mantener **Swagger UI y `/openapi/*.json` deshabilitados en producción**, salvo requisito explícito y que exista autenticación.
- [ ] Cuando exista **JWT, API keys u OAuth**, registrar el **esquema de seguridad** en OpenAPI y marcar operaciones protegidas para que “Authorize” en Swagger UI sea coherente con la API real.

---

## Versionado de API y OpenAPI

La plantilla puede versionar por **prefijo en ruta** (`api/v1/...`) y/o por paquetes como `Asp.Versioning.Mvc` cuando se integren en `Program.cs`.

Si coexisten varias versiones:

- Alinear **nombre del documento** (`AddOpenApi("v1")`, `AddOpenApi("v2")`) con las rutas o reglas de inclusión de endpoints.
- En Swagger UI, registrar **varios** `SwaggerEndpoint` (uno por documento) para un selector de versiones en la UI.

---
