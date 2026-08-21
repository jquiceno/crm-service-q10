---
service: crm-service-q10
context: loss-reasons (Causas de pérdida)
doc: discovery
status: draft
source: jack @ e9bbcb03f1416a6166c4ae3684f22bb01379707d (rama `proyectoFinal/juan-esteban-londono`)
updated: 2026-08-14
---

# Discovery — Causas de pérdida (CRM / GestionComercial)

> Documento generado con el formato `Jack module discovery` (`jack/.claude/services-workflow/formato-discovery-modulo-jack.md`).
> Contiene solo la verdad del legado. Cero diseño del servicio nuevo: lo que no existe hoy es `GAP` en §10 o decisión del Plan.

## Perímetro

| Dato | Valor |
|------|-------|
| Área | `Q10.Jack/Areas/GestionComercial/` |
| Nombre de ruta | `[RouteArea("GestionComercial", AreaPrefix = "")]` · `GestionComercialBaseController.cs:13` |
| Contexto | Causas de pérdida de un negocio (*loss reasons*) |
| Controller | `Areas/GestionComercial/Controllers/EstructuracionComercialController.cs` — región `#region Causas` (líneas 515–627) |
| Servicio de datos | `Data/Servicios/ServicioGestionComercial.cs` — región `#region Causas` (líneas 2214–2272) + métodos sueltos en 1302 y 2711 y 2753 |
| ViewModel | `Areas/GestionComercial/ViewModels/EstructuracionComercial/CausasViewModel.cs` |
| Vistas | `Areas/GestionComercial/Views/EstructuracionComercial/Causas/` (4 archivos) |
| Solución | `Q10 Jack Only.sln` |

El área **sí** tiene carpeta `Services/`, pero este contexto no la usa: todo su SQL vive en `Data/Servicios/ServicioGestionComercial.cs`. No hay JS propio bajo `Scripts/app/GestionComercial/` para Causas.

---

## 0. Insumos verificados

| Insumo | Qué aportó | Nivel de evidencia |
|--------|------------|--------------------|
| Repo `jack` | Controller, servicio, ViewModel, vistas, filtros globales, helpers de seguridad | `[leído del código]` · commit `e9bbcb03f14` · rama `proyectoFinal/juan-esteban-londono` |
| Dump de esquema `.claude/dbschema/127.0.0.1,1433_udbzq10trabajos` (BD `udbzq10trabajos`, dump 06/11/2026) | Columnas, tipos, nullability, PK, FK, índices, parámetros y **código fuente** de los SPs; conteo de filas | `[verificado en BD]` — ver caveats |
| Telemetría | — | `SIN DATASET` → §8 |

**Tres caveats sobre el nivel de evidencia, que condicionan todo lo marcado `[verificado en BD]`:**

1. **Es una sola institución.** Jack es multi-tenant por base de datos: cada institución tiene la suya. El esquema verificado es el de `udbzq10trabajos`; no prueba que las demás estén iguales. → `GAP-1`.
2. **Es un dump, no la BD viva.** Fechado 06/11/2026, anterior al commit analizado. Para el esquema de este contexto no hay indicios de divergencia (los SPs no cambian desde 2020–2024), pero es un dump al fin.
3. **El dump codifica `True` como cadena vacía.** `Dump-DbSchema.ps1:130` evalúa `$valor -eq [DBNull]::Value`; con `$valor = $true` PowerShell convierte el operando derecho a booleano y la comparación da verdadera, así que todo `True` se escribe como `''`. Las columnas `acepta_null` / `identity_` de `02-columnas.tsv` se leen entonces: **vacío = True, `False` = False**. Verificado contra un control conocido (`tbl_opo_negocios.neg_consecutivoP`: PK, `acepta_null=False`, `identity_=''`). Toda afirmación de nullability de este documento depende de esa lectura.

Sin esta sección ninguna afirmación del documento es auditable.

---

## 1. Resumen ejecutivo

**Qué es.** Un catálogo de razones por las cuales se perdió un negocio (*loss reasons*). Una tabla de tres columnas, un CRUD administrativo de cuatro pantallas, y un endpoint de API de solo lectura. El dato se consume asignándolo a un negocio: `tbl_opo_negocios.neg_cau_consecutivo`.

**Tamaño.**

| Métrica | Valor |
|---------|-------|
| Controllers | 1 (región de 113 líneas dentro de un controller de 829) |
| Acciones web | 7 |
| Endpoints de API | 1 (`GET /api/causas`, v1) |
| Métodos de servicio | 8 (5 del catálogo + 3 de la asignación negocio↔causa) |
| Vistas `.cshtml` | 4 propias + 1 ajena (`Negocios/_Estados.cshtml`) |
| SPs propios | 6 (5 web + 1 API) |
| SPs ajenos que leen la tabla | 8 (todos por `LEFT JOIN`) |
| Jobs Hangfire | 0 |
| Tablas | 1 (`tbl_opo_causas`, 3 columnas, **8 filas** en el tenant dumpeado) |
| Archivos consumidores fuera del servicio de datos | 7 (14 referencias) |

**Veredicto de migración.** Como dominio es trivial: un catálogo de datos de referencia, sin reglas de negocio, sin personalizaciones por cliente, sin jobs y sin caché. La dificultad no está en el catálogo sino en **tres cosas alrededor**:

1. **La propiedad del dato está partida.** El catálogo pertenece al CRM, pero la *asignación* (`neg_cau_consecutivo`) la escriben cuatro puntos, dos de ellos fuera de `GestionComercial`: `Areas/Comunidad/Controllers/PersonasController.cs:585` y `Areas/GestionQ10/Services/ServicioQ10.cs:351`. Cortar el catálogo sin cortar la asignación deja esos dos escritores apuntando al monolito.
2. **Control de acceso roto en el frente web.** Las 7 acciones son `[AllowAnonymous]` (§3.4, D1). La paridad literal replicaría un agujero; hay que decidir explícitamente que no se replica.
3. **Tres homónimos en el mismo producto.** `tbl_opo_causas`, `tbl_aca_causas` y `causeradi_*` comparten vocabulario y hasta nombres de columna. Ya causaron al menos un bug corregido en 2020 (§2). Es la trampa más probable de esta migración.

El esquema, en cambio, es más laxo de lo que el código sugiere: `cau_nombre` y `cau_estado` son **NULLABLE** en BD aunque el ViewModel los trate como obligatorios y no-nulos (§4.1, D2/D3).

---

## 2. Vocabulario del negocio

| Término (ES) | Definición | Cómo lo llama el legado |
|--------------|------------|-------------------------|
| Causa (de pérdida) | Razón por la cual un negocio no se concretó | tabla `tbl_opo_causas` · `cau_nombre` · UI: *"Causas"*, *"Causa perdida"*, *"Causa de perdida"* |
| Negocio | La unidad que se gana o se pierde (*deal*) | `tbl_opo_negocios` · `neg_consecutivoP` |
| Oportunidad | Agrupador comercial que contiene uno o más negocios | `tbl_opo_oportunidades` · `opo_consecutivoP` |
| Estado del negocio | Etapa del flujo comercial | `tbl_opo_negocios_estados` · `negest_consecutivoP` |
| Negocio perdido | Estado con `negest_porcentaje = 0` | `NegociosController.cs:485` (`todosPerdidos`) |
| Negocio ganado | Estado con `negest_porcentaje = 100` | `NegociosController.cs:477` |
| Causa activa / inactiva | Bandera de visibilidad del catálogo | `cau_estado` (bit) · UI: *"Activa"* / *"Inactiva"* (`CausasViewModel.cs:25`) |
| Asignación de la causa | Ligar una causa a un negocio | `tbl_opo_negocios.neg_cau_consecutivo` |

**La causa solo existe cuando se pierde.** No es un atributo neutro del negocio: `NegociosController.cs:477` anula la causa cuando el estado tiene `negest_porcentaje = 100`, y los dos escritores externos (`PersonasController.cs:585`, `ServicioQ10.cs:351`) llaman a `ModificarNegocioCausa(negocio, null)` con el comentario explícito *"Si el negocio estaba perdido y lo pasamos a ganado, retiramos su causa de perdida"*. La invariante del legado es: **negocio ganado ⇒ `neg_cau_consecutivo` NULL**. No está en la BD ni en un trigger: está replicada a mano en tres lugares.

### 2.1 Homónimos — la trampa principal de este contexto

Tres catálogos distintos del producto se llaman "causas", y dos comparten los nombres de columna:

| Catálogo | Tabla | Columnas | Dominio | Servicio |
|----------|-------|----------|---------|----------|
| **Causas de pérdida (este contexto)** | `tbl_opo_causas` | `cau_consecutivoP`, `cau_nombre`, `cau_estado` | CRM | `ServicioGestionComercial` |
| Causas de cancelación | `tbl_aca_causas` | `cau_consecutivoP`, `cau_nombre`, `cau_estado` | Académico | `ServicioEstructuraAcademica` · `Areas/EstructuraAcademica/Controllers/CausasCancelacionController.cs` |
| Causas de servicio adicional | *(por `causeradi_*`)* | `causeradi_consecutivoP`, `causeradi_nombre`, `causeradi_estado` | Back-office Q10 | `ServicioGestionQ10` · `Areas/GestionQ10/Controllers/ServiciosAdicionalesController.cs` |

Las dos primeras son **indistinguibles por nombre de columna**. Consecuencias verificadas:

* Ambos servicios exponen métodos con la misma firma aparente: `ObtenerCausas`, `ObtenerDetalleCausa`, `IngresarCausa`, `EliminarCausa` existen en `ServicioGestionComercial` **y** en `ServicioGestionQ10`. Una búsqueda por nombre de método devuelve los tres dominios mezclados.
* Ya hubo un bug por esto: `pa_inf_aca_cancelados_desertores.sql:146` documenta el 10/07/2020 *"Se modifica el JOIN con `tbl_opo_causas` por `tbl_aca_causas`"* — un reporte académico estaba leyendo el catálogo del CRM.

Cualquier búsqueda o conteo de este contexto tiene que filtrar por `tbl_opo_causas` / `pa_opo_causas_*`, nunca por la palabra "causa".

---

## 3. Estado actual

### 3.1 Arquitectura y multi-tenancy

Sin Entity Framework: Dapper + ADO.NET sobre stored procedures, vía `Q10.Core.Data.DataAccess`. Todas las llamadas de este contexto usan `institucion.BaseDatos` (la BD del tenant); ninguna toca la BD maestra, RDS ni Alianza.

| Punto de entrada | Resolución del tenant | Evidencia |
|------------------|----------------------|-----------|
| Web MVC | Ambiente implícito: `BaseServicio.Get<ServicioGestionComercial>()` en el campo del base controller | `GestionComercialBaseController.cs:16` |
| API v1 | Header `aplentId` o claims del bearer, resueltos por `BaseApiController.Initialize`, y paso **explícito** al servicio | `Areas/API/v1/GestionComercial/Controllers/CausasController.cs:26` |
| Jobs | No aplica — 0 referencias en `Q10.Jack.Jobs` | — |

**El aislamiento es por base de datos, no por columna.** `tbl_opo_causas` **no tiene columna `aplent_codigoP`** (§4.1). Los seis SPs declaran `@aplent_codigoP INT` como primer parámetro y el servicio lo envía siempre, pero **ninguno lo usa en el cuerpo** — verificado leyendo los seis. Es un parámetro muerto que imita la convención del producto (D5). Al migrar, la clave de tenant tendrá que venir de otro lado: hoy la da la cadena de conexión, no el dato.

Un detalle de forma: el servicio manda la clave del parámetro en minúscula (`"aplent_codigop"`) en `ObtenerCausas` y `ObtenerDetalleCausa`, y capitalizada (`"aplent_codigoP"`) en los tres de escritura. Funciona porque SQL Server no distingue mayúsculas en nombres de parámetro; se anota solo para que no se lea como dos parámetros distintos.

### 3.2 Ubicación en el código

| Pieza | Ruta | Nota |
|-------|------|------|
| Base controller | `Areas/GestionComercial/GestionComercialBaseController.cs` | Carga `[RouteArea]` y el campo `servicio` |
| Controller | `Areas/GestionComercial/Controllers/EstructuracionComercialController.cs:515-627` | Región `Causas`, dentro de un controller compartido con otros 5 catálogos |
| Servicio de datos | `Data/Servicios/ServicioGestionComercial.cs:2214-2272` | CRUD del catálogo |
| ” | `Data/Servicios/ServicioGestionComercial.cs:1302-1313` | `ModificarNegocioCausa` (asignación, web) |
| ” | `Data/Servicios/ServicioGestionComercial.cs:2711-2722` | `ModificarNegocioCausaApi` (asignación, API) |
| ” | `Data/Servicios/ServicioGestionComercial.cs:2753-2763` | `ObtenerCausasAsync` (lectura paginada, API) |
| ViewModel | `Areas/GestionComercial/ViewModels/EstructuracionComercial/CausasViewModel.cs` | Hereda de `Q10.Core.Data.BaseViewModel` |
| Vistas | `Areas/GestionComercial/Views/EstructuracionComercial/Causas/{Inicio,_Lista,_EditarCrear,_Eliminar}.cshtml` | `_LayoutSimple` + `_ModalFormulario` + `_Modal` |
| Vista ajena | `Areas/GestionComercial/Views/Negocios/_Estados.cshtml:120-121` | Dropdown de causas al cambiar el estado del negocio |
| Modelo API | `Areas/API/v1/GestionComercial/Models/Causas/Causa.cs` | Perfil de AutoMapper ES→EN |
| Controller API | `Areas/API/v1/GestionComercial/Controllers/CausasController.cs` | |
| JS propio | — | No hay |
| Jobs | — | No hay |
| Recursos i18n | — | No hay; los textos están embebidos en las vistas y en `CausasViewModel` |

### 3.3 Rutas y acciones

Ruteo solo por atributos (`RouteConfig` únicamente llama a `MapMvcAttributeRoutes()`). Todas las rutas cuelgan del área `GestionComercial` con `AreaPrefix = ""`.

| Ruta | Verbos | Acción | Atributos | Retorno |
|------|--------|--------|-----------|---------|
| `Causas` | GET, HEAD | `InicioCausas` | `[AllowAnonymous]` | `View("Causas/Inicio")` + filtro `inactivos` |
| `Causas/Lista` | GET, HEAD | `ListaCausas` | `[AllowAnonymous]` `[DonutOutputCache(NoStore=true, Duration=0, VaryByParam="*")]` | `PartialView("Causas/_Lista")` |
| `Causas/Crear` | GET, HEAD | `CrearCausas` | `[OnlyAjax]` `[AllowAnonymous]` | `PartialView("Causas/_EditarCrear")` con `cau_estado = true` |
| `Causas/{id}/Editar` | GET, HEAD | `EditarCausas` | `[OnlyAjax]` `[AllowAnonymous]` | `PartialView("Causas/_EditarCrear")` |
| `Causas/Actualizar` | POST | `ActualizarCausas` | `[OnlyAjax]` `[AllowAnonymous]` | `Content(mensaje)` · 400 + partial en error |
| `Causas/{id}/Eliminar` | GET, HEAD | `EliminarCausas(int id)` | `[OnlyAjax]` `[AllowAnonymous]` | `PartialView("Causas/_Eliminar")` |
| `Causas/{id}/Eliminar` | POST | `EliminarCausas(CausasViewModel)` | `[OnlyAjax]` `[AllowAnonymous]` | `Content(mensaje)` · 400 + texto en error |
| `api/causas` | GET | `CausasController.Get` | `[AllowAnonymous]` `[ValidateModel]` `[ApiVersion("1.0")]` | `IPagedList<Causa>` |

Sigue la forma repetida del producto (`Inicio` → `Lista` → `Crear`/`Editar`/`Actualizar` → `Eliminar`) con **dos desviaciones**: no hay `Exportar` y no hay `Detalle`. El POST de creación y edición está unificado en `ActualizarCausas`, discriminado por un campo `tipo` de formulario (`"creacion"` / `"edicion"`) — no por el verbo ni por la ruta. Ese discriminador es el origen de D6.

La ruta `Causas/{id}/Eliminar` sirve GET y POST con el mismo patrón; el POST no consume el `{id}` de la ruta, sino `cau_consecutivoP` del cuerpo (campo oculto de `_Eliminar.cshtml:10`).

### 3.4 Habilitación de la funcionalidad

| Mecanismo | Estado en este contexto | Evidencia |
|-----------|-------------------------|-----------|
| `AutorizacionAttribute` (filtro global) | **Neutralizado.** Es un `AuthorizeAttribute`; `[AllowAnonymous]` en la acción lo saltea completo | `App_Start/FilterConfig.cs:13` · `Infrastructure/Attributes/AutorizacionAttribute.cs:10` |
| `tbl_seg_funciones` / `tbl_seg_roles_funciones` | No verificable con el dump (no trae datos) | `GAP-2` |
| `tbl_seg_menu` | No verificable con el dump | `GAP-2` |
| `Html.AuthorizedLink` | **Sí filtra**: si el usuario no tiene la función, devuelve `MvcHtmlString.Empty` y el enlace no se renderiza | `Infrastructure/SecurityHelpers.cs:278-303` · usado en `_Lista.cshtml:7,28,29` |
| Plan / paquete comercial | Sin filas verificables; el gating es opt-out, así que sin fila el catálogo se incluye | `GAP-2` |
| Parámetros de institución | Ninguno | §6 |
| Personalizaciones `Z*` | Ninguna | §6 |

**La consecuencia hay que decirla completa:** el menú y los botones se ocultan por permisos (vía `AuthorizedLink`), pero las URLs responden a cualquiera. Quien conozca `…/Causas/Crear` o haga POST a `…/Causas/Actualizar` opera el catálogo sin autenticarse. No es un descuido puntual de este contexto: `[AllowAnonymous]` aparece **83 veces en 9 controllers** del área `GestionComercial`, y en este controller entró con el commit original del CRUD (2019-04-10, *"se creó los crud de oportunidad de tipo, oportunidad de estado, negocio, causas…"*). Es un patrón del área, nunca revisado. → D1.

---

## 4. Modelo de datos y SPs

### 4.1 Tablas

**`tbl_opo_causas`** — 3 columnas, 8 filas en `udbzq10trabajos`.

| Columna | Tipo | Null | Identity | Evidencia | Notas y trampas |
|---------|------|------|----------|-----------|-----------------|
| `cau_consecutivoP` | `int` | NO | **SÍ** | `[verificado en BD]` | PK clustered `PK_tbl_opo_causas`. El `RETURN SCOPE_IDENTITY()` del SP de inserción depende de esto |
| `cau_nombre` | `varchar(200)` | **SÍ** | — | `[verificado en BD]` | La BD acepta NULL; el `[Required]` solo vive en el ViewModel (D2). Tres límites distintos conviven: 200 / 50 / 51 (D4) |
| `cau_estado` | `bit` | **SÍ** | — | `[verificado en BD]` | La BD acepta NULL; `CausasViewModel.cau_estado` es `bool` no-nullable (D3) |

Sin columna de tenant, sin auditoría (`aud_*`), sin fechas, sin borrado lógico propio más allá de `cau_estado`, sin valores por defecto declarados.

**Índices.** Solo la PK clustered. No hay índice sobre `cau_nombre` pese a que el listado ordena y filtra por él — irrelevante con 8 filas, relevante si alguna institución tiene un catálogo grande (`GAP-1`).

**Claves foráneas.**

| FK | Tabla origen | Columna | Referencia | Al eliminar |
|----|--------------|---------|------------|-------------|
| `FK_tbl_opo_causas_tbl_opo_negocios` | `tbl_opo_negocios` | `neg_cau_consecutivo` | `tbl_opo_causas.cau_consecutivoP` | `NO_ACTION` |

El nombre de la FK está invertido respecto de su contenido (la restricción vive en `tbl_opo_negocios`, no en `tbl_opo_causas`). Buscarla por nombre confunde; lo que importa es la dirección: **`tbl_opo_negocios` referencia a `tbl_opo_causas`**, con 299.937 filas del lado que referencia.

`NO_ACTION` + borrado físico es lo que produce D7: eliminar una causa en uso lanza el error 547 de SQL Server.

### 4.2 Stored procedures

**Propios del contexto (6).** Cada uno tiene exactamente **una** invocación en todo el repositorio, toda en `ServicioGestionComercial` — ninguno es compartido con otra área.

| SP | Método de servicio | Ejecución | BD | Trampas |
|----|--------------------|-----------|-----|---------|
| `pa_opo_causas_retornar` | `ObtenerCausas(nombre, estado)` :2216 | `ExecuteQuery<CausasViewModel>` | tenant | **Sin paginación** ni `total_count`. Filtro `LIKE '%'+@cau_nombre+'%'` no sargable (D8) |
| `pa_opo_causas_detalle_retornar` | `ObtenerDetalleCausa(consecutivo)` :2228 | `ExecuteQuery` + `.FirstOrDefault()` | tenant | Devuelve `null` si no existe; el controller no lo valida antes de pasarlo a la vista |
| `pa_opo_causas_ingresar` | `IngresarCausa(nombre, causa)` :2239 | `ExecuteNonQuery` | tenant | El SP hace `RETURN SCOPE_IDENTITY()`, pero el método es `void`: **el PK nuevo se descarta** (D9) |
| `pa_opo_causas_modificar` | `EditarCausa(consecutivo, nombre, estado)` :2250 | `ExecuteNonQuery` | tenant | `@cau_nombre` tiene default `NULL` → un update sin nombre lo borra (D2). Nombre de método fuera de convención: `Editar*` en vez de `Modificar*` |
| `pa_opo_causas_eliminar` | `EliminarCausa(consecutivo)` :2262 | `ExecuteNonQuery` | tenant | `DELETE` físico sin validación previa de uso (D7) |
| `pa_apis_opo_causas_retornar` | `ObtenerCausasAsync(estado, pagina, tamaño)` :2753 | `ExecuteQueryAsync` paginado | tenant | Único con paginación real: `COUNT(*) OVER() AS total_count` + `OFFSET/FETCH`. Contrato cumplido: `CausasViewModel : BaseViewModel` |

**De la asignación negocio↔causa (2).** Escriben `tbl_opo_negocios`, no el catálogo, pero pertenecen al mismo recorte funcional:

| SP | Método | Notas |
|----|--------|-------|
| `pa_opo_negocios_causa_modificar` | `ModificarNegocioCausa(consecutivo, causa)` :1302 | `UPDATE tbl_opo_negocios SET neg_cau_consecutivo`. Invalida Redis: `NEGOCIO_OPORTUNIDAD_NEW_DETALLE_{consecutivo}` |
| `pa_apis_opo_negocios_causa_modificar` | `ModificarNegocioCausaApi(consecutivo, causa, asesor, estado)` :2711 | Además cambia estado y asesor en la misma llamada. **No invalida Redis** — asimetría con el frente web (§5.3) |

**Ajenos que leen `tbl_opo_causas` por `LEFT JOIN` (8).** Ninguno filtra por causa; todos solo traen `cau_nombre` para mostrarlo. Todos son `LEFT`, así que un negocio sin causa no se pierde del resultado.

| SP | Para qué | Frente |
|----|----------|--------|
| `pa_opo_negocios_retornar` | Listado de negocios | Web |
| `pa_opo_negocios_detalle_retornar` | Detalle de negocio | Web |
| `pa_apis_opo_negocios_retornar` | Listado de negocios | API |
| `pa_apis_opo_negocios_detalle_retornar` | Detalle de negocio | API |
| `pa_apis_opo_negocios_favoritos_retornar` | Negocios favoritos | API |
| `pa_inf_opo_excel_oportunidades_dinamico` | Exportable de oportunidades | Reportes |
| `pa_inf_opo_excel_oportunidades_dinamico_VERSION_ANTERIOR` | Copia histórica | — |
| `pa_inf_opo_excel_oportunidades_dinamico_brayan` | Copia de trabajo | — |

Las dos últimas son copias muertas que siguen desplegadas en la BD; no las invoca ningún código C# (0 referencias). Se listan porque cualquier inventario de dependencias hecho contra la BD las va a encontrar. → nota en §9.

`pa_inf_aca_cancelados_desertores` **menciona** `tbl_opo_causas` solo en un comentario de actualización: dejó de leerla el 10/07/2020. No es un consumidor actual.

**Contrato de error.** Los seis SPs propios siguen el patrón del producto: `@NmbError INT OUTPUT` + `@MsgError VARCHAR(100) OUTPUT` (200 en el de API), `BEGIN TRY / BEGIN CATCH` que asigna ambos sin relanzar. `DataAccess` lee esos parámetros y lanza `DatabaseException` cuando `@NmbError != 0`. Ninguno abre transacción explícita.

### 4.3 Catálogos y constantes

No hay enums `char`, ni códigos fijos, ni listas de exclusión en este contexto. `cau_estado` es un `bit` mapeado a `bool` y renderizado como texto `"Activa"` / `"Inactiva"` por una propiedad calculada del ViewModel (`CausasViewModel.cs:20-27`), no por un enum.

La única semántica implícita por valor está **fuera** del catálogo: `negest_porcentaje = 0` significa *perdido* y `= 100` significa *ganado* (`NegociosController.cs:477,485`). Números mágicos, sin constante ni enum.

---

## 5. Frentes de consumo y mapa de consumidores

Frentes activos: **Web MVC**, **API v1**, **reportes/exportables**. No hay jobs (0 referencias en `Q10.Jack.Jobs`), ni `.asmx`, ni JS propio.

### 5.1 Escrituras

**Del catálogo** — un único escritor, el CRUD:

| Quién | Desde dónde | Método | Refs |
|-------|-------------|--------|------|
| `EstructuracionComercialController` | Web | `IngresarCausa`, `EditarCausa`, `EliminarCausa` | 3 |

**De la asignación** (`tbl_opo_negocios.neg_cau_consecutivo`) — cuatro escritores, **dos fuera del área**:

| Quién | Archivo:línea | Qué hace | Área |
|-------|---------------|----------|------|
| `NegociosController.ActualizarNegocioEstado` | `Areas/GestionComercial/Controllers/NegociosController.cs:480` | Asigna la causa al cambiar el estado; la anula si el estado es ganado (`negest_porcentaje == 100`) | GestionComercial |
| `PersonasController` | `Areas/Comunidad/Controllers/PersonasController.cs:585` | Anula la causa al convertir el negocio | **Comunidad** |
| `ServicioQ10.ModificarInformacionComercial` | `Areas/GestionQ10/Services/ServicioQ10.cs:351` | Anula la causa al pasar el negocio a cliente | **GestionQ10** |
| `NegociosController` (API) | `Areas/API/v1/GestionComercial/Controllers/NegociosController.cs:757` | Marca el negocio como perdido y asigna la causa | API |

### 5.2 Lecturas

| Quién | Archivo:línea | Para qué | Dentro/fuera | Refs |
|-------|---------------|----------|--------------|------|
| `EstructuracionComercialController` | `EstructuracionComercialController.cs:536,565,605` | Listado, edición, confirmación de borrado | Dentro | 3 |
| `NegociosController` (web) | `Areas/GestionComercial/Controllers/NegociosController.cs:455` | `SelectList` del dropdown de causas | Dentro | 1 |
| `NegocioFormModel` | `Areas/GestionComercial/ViewModels/Negocio/NegocioFormModel.cs:31` | `SelectList` del dropdown de causas | Dentro | 1 |
| `NegociosController` (API) | `Areas/API/v1/GestionComercial/Controllers/NegociosController.cs:722` | Valida que la causa exista y esté activa | Dentro | 1 |
| `CausasController` (API) | `Areas/API/v1/GestionComercial/Controllers/CausasController.cs:31` | `GET /api/causas` | Dentro | 1 |
| 8 SPs por `LEFT JOIN` | §4.2 | Traen `cau_nombre` para mostrarlo | Fuera (SQL) | 8 |

**Conteo consolidado del catálogo:** 7 archivos consumidores fuera del propio servicio de datos, **14 referencias** en C#, 5 vistas (4 propias + `Negocios/_Estados.cshtml`), 15 objetos SQL que nombran la tabla (6 propios + 8 lectores + 1 solo en comentario).

Excluidos del conteo, y hay que decirlo porque una búsqueda ingenua los incluye: los 9 hits de `Areas/GestionQ10/**` y `ServicioGestionQ10` corresponden al homónimo `causeradi_*` (§2.1), y los de `EstructuraAcademica` al de `tbl_aca_causas`.

Las dos lecturas de validación (`NegociosController` API:722 y el `SelectList`) traen **el catálogo completo en memoria** y luego filtran con LINQ (`.ToList().Find(...)`). Con 8 filas es irrelevante; es un patrón a no replicar.

### 5.3 Diferencias entre frentes

| Aspecto | Web MVC | API v1 |
|---------|---------|--------|
| Resolución de tenant | `SessionManager` implícito | Header `aplentId` o claims, paso explícito |
| Autenticación | Ninguna — `[AllowAnonymous]` en las 7 acciones | Ninguna — `[AllowAnonymous]` en el controller |
| Operaciones | CRUD completo | Solo lectura |
| Paginación | **No** (`IList` completo) | Sí (`OFFSET/FETCH` + `total_count`) |
| Filtro por nombre | Sí (`LIKE`) | No |
| Filtro por estado | Opcional (`inactivos`, default solo activas) | **Obligatorio** — `400` si falta `Estado` (`CausasController.cs:22-23`) |
| Catálogo vacío | Renderiza el mensaje "no hay registros" | **`404`** vía `NotFoundError` (`CausasController.cs:33-34`) |
| Nombres de campo | `cau_*` (columnas de BD) | `Consecutivo_causa_perdida`, `Nombre_causa_perdida`, `Estado` (AutoMapper) |
| Invalidación de caché al asignar | Sí (`RedisCacheManager.InvalidateForInstitution`) | **No** |

Cuatro rupturas de paridad ya presentes hoy entre los dos frentes: un resultado vacío es 200 en web y 404 en API; el filtro de estado es opcional en uno y obligatorio en el otro; la paginación existe en uno solo; y la asignación por API deja el detalle del negocio cacheado en Redis con la causa vieja. La última es la única con impacto funcional real y es difícil de atribuir en producción, porque el síntoma aparece en el frente web después de una escritura por API.

---

## 6. Parámetros y personalizaciones

| Mecanismo | Qué decide | Dónde |
|-----------|------------|-------|
| `ObtenerParametro<T>(Constantes.XXX)` | — | Ninguno |
| `ValidarPersonalizacion(Constantes.Z...)` | — | Ninguno |
| `EsComfama` / `EsColomboAmericano` / … | — | Ninguno |
| `CustomText(...)` / `TextosParametrizables` | — | Ninguno |
| `ServicioPersonalizaciones` | — | Ninguno |
| Web services por cliente (`IServiceCaller`) | — | Ninguno |

`NO APLICA` — **verificado, no asumido**: búsqueda de `ValidarPersonalizacion|ObtenerParametro|CustomText|Es[A-Z]…` sobre la región `Causas` del controller, la región `Causas` del servicio, las 4 vistas y el controller de API: **0 coincidencias**. Los textos de UI están embebidos en las vistas (`"Causas"`, `"Crear Causa"`), no pasan por `CustomText` ni por `.resx`, así que ningún cliente los tiene renombrados.

Esto es la mejor noticia del discovery: es el lugar donde este tipo de módulo suele esconder sorpresas por cliente, y acá no hay ninguna.

Una salvedad de alcance: el enunciado de la única regla de negocio del contexto (*ganado ⇒ sin causa*) sí depende de datos por institución — `negest_porcentaje` sale de `tbl_opo_negocios_estados`, que cada institución configura. El umbral 0/100 está hardcodeado en el código, pero **qué estados tienen esos porcentajes es dato del tenant**. → `GAP-3`.

---

## 7. Defectos e inconsistencias

> Los veredictos de esta tabla son **propuestos**, no firmados. `GAP-5` cubre su asignación formal por el equipo.

| # | Defecto | Evidencia | Severidad | Veredicto propuesto |
|---|---------|-----------|-----------|---------------------|
| D1 | **Las 7 acciones web del CRUD son `[AllowAnonymous]`.** El filtro global `AutorizacionAttribute` es un `AuthorizeAttribute`, así que MVC lo saltea por completo: crear, editar y eliminar causas no exige autenticación ni permiso. `AuthorizedLink` solo oculta los enlaces, no protege las URLs. El endpoint `GET /api/causas` también es anónimo | `EstructuracionComercialController.cs:517,528,548,561,570,601,611` · `FilterConfig.cs:13` · `SecurityHelpers.cs:278` · `CausasController.cs:12` | **Alta** | **Se corrige** — no se replica al servicio nuevo |
| D2 | **`cau_nombre` acepta NULL en BD** y `pa_opo_causas_modificar` lo declara `@cau_nombre VARCHAR(200) = NULL`. Una llamada al SP sin nombre deja la causa sin nombre. El `[Required]` existe solo en el ViewModel, y D1 permite llegar al POST sin pasar por la UI | `02-columnas.tsv` (`tbl_opo_causas`) · `pa_opo_causas_modificar.sql:10` · `CausasViewModel.cs:11` | Alta | **Se corrige** — `NOT NULL` en el modelo nuevo |
| D3 | **`cau_estado` acepta NULL en BD** pero `CausasViewModel.cau_estado` es `bool` no-nullable. Una fila con NULL rompe el mapeo de Dapper y tumba el listado completo, no solo esa fila | `02-columnas.tsv` · `CausasViewModel.cs:18` | Media | **Se corrige** — `NOT NULL` + default en la migración |
| D4 | **Tres límites de longitud distintos para el mismo campo:** BD `varchar(200)`, ViewModel `[MaxLength(50)]`, input HTML `maxlength = 51`. El `51` deja escribir exactamente un carácter de más para que la validación de cliente dispare; el `200` significa que por API o por POST directo entran nombres que la UI nunca podría crear | `02-columnas.tsv` · `CausasViewModel.cs:13` · `_EditarCrear.cshtml:21` | Media | **Se corrige** — un único límite, decidido en el Plan |
| D5 | **`@aplent_codigoP` declarado y nunca usado** en los 6 SPs propios. Parámetro muerto que sugiere un aislamiento por columna que no existe | los 6 `.sql` de §4.2 | Baja | **No se replica** — el tenant se resuelve fuera del SP |
| D6 | **Un fallo de validación al editar convierte la edición en creación.** `ActualizarCausas` responde 400 con `PartialView("_EditarCrear", new CausasViewModel())`; `jack.onFormError` → `app.refreshModal` reemplaza el modal con ese HTML. Los helpers (`HiddenFor`) releen ModelState y conservan el id, pero `var Tipo = Model.cau_consecutivoP == 0 ? "creacion" : "edicion"` lee el **Model vacío** y el campo `tipo` es un `<input>` crudo que no consulta ModelState: queda `"creacion"`. Al reenviar, el POST llama `IngresarCausa` y **duplica la causa en vez de actualizarla**. Alcanzable desde la UI por D4: `maxlength=51` permite escribir 51 caracteres y `[MaxLength(50)]` los rechaza | `EstructuracionComercialController.cs:595-596` · `_EditarCrear.cshtml:4,13` · `_ModalFormulario.cshtml:3` · `main.js:1888-1896` | **Alta** | **Se corrige** — `[leído del código]`, pendiente reproducción en runtime |
| D7 | **Borrado físico sin validación de uso.** `pa_opo_causas_eliminar` hace `DELETE` directo; la FK es `NO_ACTION` sobre una tabla de ~300.000 filas. Borrar una causa asignada lanza el error 547, que `ManejarError` traduce a un mensaje genérico. No hay un `Validar*` previo, ni borrado lógico, pese a que `cau_estado` existe justamente para eso | `pa_opo_causas_eliminar.sql:17-18` · `04-claves-foraneas.tsv` · `EstructuracionComercialController.cs:617` | Media | **Se corrige** — validar uso antes de borrar, o borrado lógico |
| D8 | **Filtro no sargable:** `WHERE cau_nombre LIKE '%'+@cau_nombre+'%'`. Con 8 filas es irrelevante; se registra porque el patrón se copia | `pa_opo_causas_retornar.sql:21` | Baja | **Riesgo aceptado** |
| D9 | **El PK nuevo se descarta.** `pa_opo_causas_ingresar` hace `RETURN SCOPE_IDENTITY()` y `IngresarCausa` es `void`: el consumidor no puede saber qué creó | `pa_opo_causas_ingresar.sql:22` · `ServicioGestionComercial.cs:2239` | Baja | **Se corrige** — el endpoint nuevo devuelve el recurso creado |
| D10 | **Nomenclatura fuera de convención:** `EditarCausa` donde el producto usa `Modificar*` (862 usos frente a un puñado de `Editar*`). Buscar por la convención no encuentra este método | `ServicioGestionComercial.cs:2250` | Baja | **No se replica** |
| D11 | **La asignación por API no invalida Redis.** `ModificarNegocioCausaApi` no llama a `RedisCacheManager.InvalidateForInstitution`, mientras que `ModificarNegocioCausa` sí. El detalle del negocio queda cacheado con la causa anterior | `ServicioGestionComercial.cs:1312` vs `:2711-2722` | Media | **Se corrige** |
| D12 | **La invariante *ganado ⇒ sin causa* está replicada a mano en tres lugares** y no existe en la BD. Si un cuarto escritor la olvida, quedan negocios ganados con causa de pérdida. El frente API no la aplica: asigna causa y estado en una sola llamada sin verificar el porcentaje | `NegociosController.cs:477` · `PersonasController.cs:585` · `ServicioQ10.cs:351` · `pa_apis_opo_negocios_causa_modificar.sql` | Media | **Se corrige** — invariante en el dominio |

---

## 8. Rendimiento

`NO APLICA` — **razón:** no se dispone de dataset de telemetría (App Insights u otro) para este análisis. No se midieron llamadas, latencias ni top endpoints. → `GAP-4`.

Hallazgos cualitativos por lectura de código, que no reemplazan la medición:

| Hallazgo | Evidencia | Impacto esperado |
|----------|-----------|------------------|
| El listado web no pagina: `ObtenerCausas` devuelve `IList` completo y `_Lista.cshtml` itera todo | `ServicioGestionComercial.cs:2216` · `EstructuracionComercialController.cs:536` | Bajo con 8 filas; crece linealmente y sin techo |
| Dos lecturas traen el catálogo completo a memoria para buscar un elemento con LINQ | `Areas/API/v1/.../NegociosController.cs:722` · `NegocioFormModel.cs:31` | Bajo hoy; patrón a no replicar |
| `LIKE '%…%'` impide usar índice; además no hay índice sobre `cau_nombre` | `pa_opo_causas_retornar.sql:21` · `03-indices.tsv` | Bajo hoy |
| El catálogo se relee en cada apertura del modal de estados del negocio, sin caché | `NegociosController.cs:455` | Bajo, pero es la lectura más frecuente del contexto |
| `[DonutOutputCache(NoStore=true, Duration=0)]` en `ListaCausas` desactiva el caché explícitamente | `EstructuracionComercialController.cs:530` | Deliberado; se registra para no leerlo como caché activo |

El tamaño real es lo que hace todo esto de bajo impacto: 8 filas frente a 299.937 negocios. La medición sigue siendo necesaria para el frente de lectura por JOIN, que sí corre sobre la tabla grande.

---

## 9. Alcance y fuera de alcance

### Dentro del alcance

* El catálogo `tbl_opo_causas` y su CRUD (7 acciones web).
* Los 6 SPs propios (§4.2).
* `CausasViewModel` y el modelo de API `Causa` con su perfil de AutoMapper.
* El endpoint `GET /api/causas`.
* La relación negocio↔causa (`neg_cau_consecutivo`) **en modo lectura**: el servicio nuevo tiene que poder responder qué causa tiene un negocio.

### Fuera de alcance de esta iteración

* **La escritura de la asignación** (`ModificarNegocioCausa` / `ModificarNegocioCausaApi`): pertenece al agregado *Negocio*, no al catálogo. Se nombra explícitamente porque tiene 4 escritores, dos fuera del área, y porque la invariante D12 vive ahí. Migrar el catálogo sin resolver esto deja el monolito escribiendo la FK. → decisión del Plan.
* Las 4 vistas Razor y el dropdown de `Negocios/_Estados.cshtml`.
* Los 8 SPs ajenos que leen la tabla por `LEFT JOIN`: siguen en el monolito y siguen necesitando `cau_nombre` en el mismo resultset. Es la dependencia que condiciona el orden de corte.
* El exportable `pa_inf_opo_excel_oportunidades_dinamico`.

### Fuera de alcance de forma permanente

* `pa_inf_opo_excel_oportunidades_dinamico_VERSION_ANTERIOR` y `…_brayan`: copias muertas en la BD, 0 referencias en código. No se migran; se deberían eliminar del esquema en su propio ticket.
* `tbl_aca_causas` y las causas de servicios adicionales (`causeradi_*`): otros dominios (§2.1). Se nombran solo para excluirlos explícitamente.

---

## 10. Decisiones pendientes y GAPs

`⚠️ GAP-1 (ABIERTO): No se verificó si el esquema de tbl_opo_causas es idéntico en todas las instituciones ni cuál es el tamaño real del catálogo fuera de udbzq10trabajos (8 filas) · Afecta: dimensionamiento, decisión de paginación e índices · Confirmar con: DBA / Infraestructura`
`Recomendación por defecto: consultar COUNT(*) y el esquema en 5–10 tenants representativos antes de cerrar el Plan. El riesgo es bajo (los SPs no cambian desde 2020 y no hay migraciones por cliente detectadas), pero el costo de comprobarlo también.`

`⚠️ GAP-2 (BLOQUEANTE): No se verificaron las filas de tbl_seg_funciones, tbl_seg_roles_funciones ni tbl_seg_menu para este contexto — el dump no incluye datos · Afecta: definición del modelo de autorización del servicio nuevo · Confirmar con: Tech lead + Seguridad`
`Recomendación por defecto: el CRUD debe exigir un permiso administrativo del CRM y la lectura del catálogo un permiso de consulta. Consultar las filas reales sirve para saber qué roles lo tienen hoy y no romper a nadie, pero el diseño no debe partir de [AllowAnonymous] bajo ninguna circunstancia.`

`⚠️ GAP-3 (ABIERTO): La regla "negocio ganado ⇒ sin causa de pérdida" depende de negest_porcentaje (0 = perdido, 100 = ganado), un dato configurable por institución, contra umbrales hardcodeados en el código · Afecta: modelado de la invariante D12 · Confirmar con: Producto / dueño funcional del CRM`
`Recomendación por defecto: tratar 0 y 100 como semántica del dominio (perdido/ganado) y no como configuración, replicando el comportamiento actual; validar con Producto que ninguna institución use porcentajes intermedios con ese significado.`

`⚠️ GAP-4 (ABIERTO): Sin dataset de telemetría, §8 no tiene medición · Afecta: decisiones de caché y paginación del servicio nuevo · Confirmar con: quien administre App Insights`
`Recomendación por defecto: no diseñar caché para el catálogo (8 filas, lectura barata) y sí paginar desde el inicio en el contrato nuevo, que es reversible; medir después del cutover.`

`⚠️ GAP-5 (BLOQUEANTE): Los 12 veredictos de §7 son propuestos, no firmados · Afecta: criterios de aceptación de todos los flujos (paridad vs comportamiento nuevo) · Confirmar con: Tech lead`
`Recomendación por defecto: aprobar los propuestos. Los tres que realmente cambian el comportamiento observable y necesitan decisión consciente son D1 (deja de ser anónimo), D4 (un único límite de longitud: 200 de BD frente a 50 de UI) y D7 (borrado lógico o validación previa en vez del 547 crudo).`

`⚠️ GAP-6 (ABIERTO): No está decidido si la escritura de neg_cau_consecutivo migra con el catálogo o se queda en el monolito · Afecta: orden de corte y los 4 escritores de §5.1 · Confirmar con: Tech lead + dueños de Comunidad y GestionQ10`
`Recomendación por defecto: el catálogo migra solo y la asignación se queda, porque pertenece al agregado Negocio y sus dos escritores externos (Comunidad, GestionQ10) están fuera del alcance de este servicio. Consecuencia asumida: el monolito sigue escribiendo la FK contra una tabla que ya no le pertenece, y eso hay que resolverlo cuando migre Negocios.`

---

## 11. Changelog

| Fecha | Cambio | Origen |
|-------|--------|--------|
| 2026-08-14 | Versión inicial | Discovery sobre `jack@e9bbcb03f14` + dump `udbzq10trabajos` |

---

## Anexo — Comandos de recolección usados

```
# Región del contexto en el controller
rg -n "region\s+Causas|endregion" Q10.Jack/Areas/GestionComercial/Controllers/EstructuracionComercialController.cs

# Métodos del catálogo y su invocación de SPs
rg -n "Causa" Q10.Jack/Data/Servicios/ServicioGestionComercial.cs

# Consumidores (filtrando homónimos a mano — ver §2.1)
rg -c "ObtenerCausas\(|ObtenerCausasAsync|ObtenerDetalleCausa\(|IngresarCausa\(|EditarCausa\(|EliminarCausa\(|ModificarNegocioCausa" Q10.Jack Q10.Jack.Jobs
rg -c "neg_cau_consecutivo" Q10.Jack

# Un SP a la vez, para saber si es compartido
rg -n "pa_opo_causas_|pa_apis_opo_causas_|pa_opo_negocios_causa_modificar" Q10.Jack --glob "*.cs"

# Personalizaciones y parámetros (0 coincidencias)
rg -n "ValidarPersonalizacion|ObtenerParametro|CustomText" Q10.Jack/Areas/GestionComercial/Controllers/EstructuracionComercialController.cs
```

Esquema, sobre el dump en `jack/.claude/dbschema/127.0.0.1,1433_udbzq10trabajos`:

```
01-objetos.tsv          → existencia de tbl_opo_causas y de los 6 SPs
02-columnas.tsv         → tipos, longitudes, nullability (recordar: vacío = True)
03-indices.tsv          → solo PK clustered
04-claves-foraneas.tsv  → FK_tbl_opo_causas_tbl_opo_negocios (NO_ACTION)
05-parametros.tsv       → firmas de los SPs
06-conteo-filas.tsv     → 8 filas en tbl_opo_causas, 299.937 en tbl_opo_negocios
modules/dbo.pa_opo_causas_*.sql  → cuerpo real de los SPs
```

Pendiente de consultar en una BD viva (no está en el dump): `tbl_seg_funciones`, `tbl_seg_roles_funciones`, `tbl_seg_menu` para `fun_controlador = 'EstructuracionComercial'`. → `GAP-2`.

---

## Criterio de cierre

Discovery pasa a `frozen` cuando:

- [x] Las once secciones están escritas o justificadas con `NO APLICA` + razón.
- [x] Cada afirmación cita su fuente y declara `[verificado en BD]` o `[leído del código]`.
- [x] El área, su carpeta de servicios y su `[RouteArea]` están identificados en el perímetro.
- [x] Cada acción de §3.3 está declarada, con su estado de autorización real.
- [x] Cada SP de §4.2 declara BD destino y si es compartido con otra área (ninguno lo es).
- [x] Cada defecto de §7 tiene veredicto **propuesto**.
- [ ] Los veredictos de §7 están firmados → `GAP-5`.
- [ ] `GAP-2` y `GAP-6` tienen dueño y ticket.
- [ ] El tech lead firmó.

**No se empieza el Plan con GAPs bloqueantes abiertos.** Hoy quedan dos: `GAP-2` (modelo de autorización) y `GAP-5` (firma de veredictos).
