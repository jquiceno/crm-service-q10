---
service: crm-service
context: MediosPublicitarios
doc: discovery
status: draft
source: Q10.Jack (branch master) @ 379252902a1abe43902fd8fb556705c4150e88a0 y @ 2baacb6988b5b8ecb6f33b3ab82de58e762a7c61 (dos análisis independientes consolidados — ver §11)
updated: 2026-08-14
---

## 0. Insumos verificados

Este documento consolida dos análisis de Discovery independientes sobre el mismo módulo, realizados por distintas personas sin conocimiento mutuo, reconciliados en esta versión (ver §11 Changelog).

| Insumo | Valor |
|---|---|
| Repositorio legado | `Q10.Jack` — analizado localmente en `C:\Users\Brayan Gamboa\source\repos\jack` y en `C:\Users\Andres Perez\source\repos\jack` |
| Rama / commits analizados | `master` @ `379252902a1abe43902fd8fb556705c4150e88a0` (análisis 1) y `master` @ `2baacb6988b5b8ecb6f33b3ab82de58e762a7c61` (análisis 2). Los números de línea citados pueden variar en 1-5 líneas entre secciones según de qué análisis provienen — no representan un desacuerdo, sino el drift natural entre dos commits de la misma rama |
| BD de verificación inicial | `zudbzq10desarrollopagosregulares` en `tcp:127.0.0.1,1434`, SQL Server 2022 RTM-CU21-GDR (análisis 1) |
| BD fuente de verdad | `udbzq10trabajos` en `tcp:127.0.0.1,1433` — esquema y stored procedures verificados idénticos a la BD de verificación inicial (análisis 1). El análisis 2 verificó el mismo esquema contra una base no registrada (GAP-8 de ese análisis, cerrado en este documento porque el análisis 1 sí deja la traza completa) |
| Dataset de rendimiento | `SIN DATASET` — no hay telemetría disponible para este análisis |
| Servicio hermano de referencia | `NINGUNO` — `crm-service` no tiene todavía ningún bounded context de negocio real |
| Archivos de entrada declarados | `Q10.Jack\Areas\GestionComercial\Controllers\EstructuracionComercialController.cs` (controlador y servicio) |

## 1. Resumen ejecutivo

El módulo **Medios Publicitarios** (contexto `GestionComercial`) es un catálogo simple de "canales por los que un prospecto se enteró de la institución" (ej. Facebook, Referido, Volante). Tiene 3 columnas, 6 stored procedures y un CRUD estándar (listar, crear, editar, eliminar, detalle) expuesto tanto por una vista MVC dentro de EstructuracionComercial como por un endpoint REST público (`GET /api/mediospublicitarios`).

Su tamaño de implementación es pequeño, pero su radio de consumo es amplio: 46 archivos de código fuente lo referencian, principalmente como valor de una lista desplegable o como columna foránea (`opo_medpub_consecutivo`) dentro del módulo de Oportunidades, reportes comerciales e importación masiva. Oportunidad en sí no se migra en este cambio, pero al depender de este catálogo vía FK (y posiblemente vía un JOIN adicional no confirmado — ver §10), la estrategia de convivencia entre el catálogo migrado y el monolito debe resolverse antes del Plan de trabajo.

Existe un segundo dominio de negocio con el mismo nombre de superficie (`tbl_per_medios_publicitarios`, personalización de cliente "Formarte" en Establecimiento/Comunidad, gateada por `ZMEDIOS_PUBLITARIOS_FORMARTE = "63"`) que **no tiene relación estructural** con este módulo y que, por decisión del equipo, queda fuera de alcance de esta migración (ver §9).

**Veredicto de migración:** candidato de bajo riesgo y buen punto de partida para extraer como catálogo independiente — modelo de datos trivial, confirmado contra dos bases de datos, y sin lógica de negocio compleja. La condición para avanzar es resolver, de forma independiente al cronograma de esta migración, dos hallazgos de seguridad a nivel de plataforma (endpoint público sin autenticación de aplicación y credenciales embebidas en código — ver §7, D1/D2), decidir la estrategia de convivencia con Oportunidad (ver §10), y decidir si el nuevo servicio reproduce o corrige los comportamientos heredados de las SPs (sin unicidad de nombre, columnas `NULL`-ables tratadas como obligatorias solo en la UI, DELETE físico sin validar dependencias, sin auditoría).

## 2. Vocabulario del negocio

| Término (ES) | Definición | Cómo lo llama el legado |
|---|---|---|
| Medio publicitario | Canal o fuente por el cual un prospecto (oportunidad) llegó a la institución; catálogo simple con nombre y estado activo/inactivo | `MediosPublicitarios` (rutas, vistas, controlador), `medpub_*` (columnas y parámetros de SP) |
| Oportunidad | Registro de un prospecto comercial en gestión (lead) que puede tener asociado un medio publicitario de origen | `tbl_opo_oportunidades`, columna `opo_medpub_consecutivo` |
| Institución / aplicación entidad | El tenant (cliente) del sistema SaaS | `aplent_codigoP` (parámetro recibido por las SPs del módulo, no usado en sus consultas — ver D6) |
| Estado (de un medio publicitario) | Bandera activo/inactivo; determina si el medio aparece en los formularios de selección | `medpub_estado` |
| Medio publicitario (homónimo, fuera de alcance) | Fuente de referencia usada en el flujo de Personas/Preinscripciones, con jerarquía padre-hijo y alcance por sede, propia de la personalización de cliente "Formarte" | `tbl_per_medios_publicitarios`, `metpub_*`, región `Personalización Formarte` en `PersonalizacionesController.cs:46`, gateada por `ZMEDIOS_PUBLITARIOS_FORMARTE = "63"` (`Constantes.cs:2045`) |

## 3. Estado actual

**Arquitectura y multi-tenancy.** Aplicación monolítica ASP.NET MVC + Web API (.NET Framework clásico, sin ORM). Patrón uniforme del monolito: controller delgado + capa `Servicio*` como acceso a datos vía stored procedures. El multi-tenant se resuelve a nivel de base de datos: cada institución tiene su propia base de datos (no hay columna de tenant en `tbl_opo_medios_publicitarios`). El parámetro `@aplent_codigoP` viaja a las 6 SPs del módulo pero no se usa en ninguna condición `[verificado en BD]` — es, de facto, un catálogo global dentro de cada base.

**Ubicación en código:**

| Capa | Archivo |
|---|---|
| Controlador MVC | `Areas/GestionComercial/Controllers/EstructuracionComercialController.cs` (región "Medios Publicitarios", líneas ≈109–201/206 según commit) |
| Controlador base (inyecta el servicio) | `Areas/GestionComercial/GestionComercialBaseController.cs:16` — `protected ServicioGestionComercial servicio = BaseServicio.Get<ServicioGestionComercial>();` |
| Controlador API pública | `Areas/API/v1/GestionComercial/Controllers/MediosPublicitariosController.cs` |
| Servicio | `Data/Servicios/ServicioGestionComercial.cs:1493-1565` |
| ViewModel MVC | `Areas/GestionComercial/ViewModels/EstructuracionComercial/MediosPublicitariosViewModel.cs` |
| Modelo API | `Areas/API/v1/GestionComercial/Models/MediosPublicitarios/MedioPublicitario.cs` |
| Vistas | `Areas/GestionComercial/Views/EstructuracionComercial/MediosPublicitarios/` (`Inicio.cshtml`, `_Lista.cshtml`, `_EditarCrear.cshtml`, `_Eliminar.cshtml`) |

**Puntos de llamada (rutas):**

| Ruta | Verbo | Acción |
|---|---|---|
| `MediosPublicitarios/New` | GET | Inicio de la sección — ruta de entrada que originó este Discovery (`InicioMediosPublicitarios()`, `EstructuracionComercialController.cs:112-121`) |
| `MediosPublicitarios/Lista/New` | GET/HEAD | Listado paginado con filtro de texto/estado |
| `MediosPublicitarios/Crear/New` | GET/HEAD | Formulario de creación |
| `MediosPublicitarios/ActualizarOportunidad/New` | POST | Crear o editar (según parámetro `tipo`) |
| `MediosPublicitarios/{id}/Editar/New` | GET/HEAD | Formulario de edición |
| `MediosPublicitarios/{id}/Eliminar` | GET/HEAD, POST | Confirmación y ejecución de borrado |
| `api/mediospublicitarios` | GET | Listado paginado vía API pública v1.0 |

**Cómo se habilita hoy.** El catálogo dentro de `EstructuracionComercial` está siempre disponible — no se identificó ninguna bandera de personalización (`ZPERSONALIZACION_*` / `ValidarPersonalizacionesNoPermitidas`) que lo condicione, a diferencia del dominio homónimo "Formarte" que sí es una personalización explícita (`ZMEDIOS_PUBLITARIOS_FORMARTE = "63"`). El acceso a las acciones del controlador MVC depende de la sesión web autenticada heredada de `GestionComercialBaseController`; los permisos específicos por rol se resuelven desde base de datos, no desde atributos en el código (confirmado por el equipo — GAP-1 cerrado, ver §10).

## 4. Modelo de datos y SPs

**Tabla `tbl_opo_medios_publicitarios`** `[verificado en BD, idéntico en zudbzq10desarrollopagosregulares y udbzq10trabajos]`

| Columna | Tipo | Nullable | Identity | Nota |
|---|---|---|---|---|
| medpub_consecutivoP | int | No | Sí (PK) | El alta hace `RETURN SCOPE_IDENTITY()` |
| medpub_nombre | varchar(100) | Sí | No | Sin `NOT NULL` a nivel de base — el campo "requerido" en la UI (`[Required]` en el ViewModel) no tiene respaldo en el esquema |
| medpub_estado | bit | Sí | No | Mismo caso — nulabilidad a nivel de base no reflejada en la UI |

Sin columna de tenant, sin columnas de auditoría (usuario/fecha de creación o modificación). FK entrante: `FK_tbl_opo_medios_publicitarios_tbl_opo_oportunidades` (`tbl_opo_oportunidades.opo_medpub_consecutivo` → `tbl_opo_medios_publicitarios.medpub_consecutivoP`) — es la única FK que referencia esta tabla.

**Stored procedures** `[verificado en BD]`

| SP | Semántica | Parámetros | Trampa detectada |
|---|---|---|---|
| `pa_opo_medios_publicitarios_retornar` | Lista filtrando por `filtro_estado` y `filtro_texto` (LIKE) | `@aplent_codigoP`, `@filtro_texto`, `@filtro_estado` | `@aplent_codigoP` recibido y no usado en el `WHERE` |
| `pa_apis_opo_medios_publicitarios_retornar` | Lista paginada (OFFSET/FETCH) para la API pública, ordenada por nombre | `@aplent_codigoP`, `@filtro_estado`, `@PageSize`, `@PageIndex` | No admite `filtro_texto`; `@aplent_codigoP` recibido y no usado |
| `pa_opo_medios_publicitarios_detalle_retornar` | Retorna un registro por `medpub_consecutivoP` | `@aplent_codigoP`, `@medpub_consecutivoP` | Sin manejo de "no encontrado" propio (deja el `SELECT` vacío); parámetro fantasma otra vez |
| `pa_opo_medios_publicitarios_ingresar` | Inserta y retorna `SCOPE_IDENTITY()` | `@aplent_codigoP`, `@medpub_nombre`, `@medpub_estado` | Sin validación de unicidad de `medpub_nombre` — permite duplicados exactos; no inserta `aplent_codigoP` porque la tabla ni tiene esa columna |
| `pa_opo_medios_publicitarios_modificar` | Actualiza nombre y estado por `medpub_consecutivoP` | `@aplent_codigoP`, `@medpub_consecutivoP`, `@medpub_nombre`, `@medpub_estado` | Sin validación de unicidad de `medpub_nombre`; sin verificar que el registro exista antes de actualizar |
| `pa_opo_medios_publicitarios_eliminar` | `DELETE` físico por `medpub_consecutivoP` | `@aplent_codigoP`, `@medpub_consecutivoP` | No valida la FK entrante desde `tbl_opo_oportunidades` antes de borrar; si el medio está en uso, el error de violación de constraint se captura en `@NmbError`/`@MsgError` mediante `TRY/CATCH` genérico y se propaga como excepción cruda hasta el usuario |

**Hallazgo investigado por ambos análisis, no resuelto:** `OportunidadNewViewModel.cs:96-112` declara y usa una propiedad derivada `EditarMedioPublicitario` que depende de un campo `medpub_abreviatura` con valores esperados `"IN"/"RI"/"RD"`. Se confirmó `[verificado en BD]` que **`medpub_abreviatura` no es columna de ninguna tabla** en el esquema (ausente en `INFORMATION_SCHEMA.COLUMNS` / `sys.all_columns`). El campo nunca se puebla desde `tbl_opo_medios_publicitarios`; su origen real (probablemente una SP de Oportunidad, ej. `pa_opo_oportunidades_detalle_retornar` o `pa_opo_oportunidades_retornar`, no confirmadas) queda como decisión pendiente (ver §10, GAP-7).

## 5. Frentes de consumo y mapa de consumidores

46 archivos de código fuente real referencian el término "MediosPublicitarios/MedioPublicitario" (se excluyeron artefactos autogenerados en `obj/CodeGen`, duplicados de las vistas ya contadas). La clasificación por firma exacta de método es necesaria porque `ServicioGestionComercial` y `ServicioEstablecimiento` comparten nombres de método para entidades de negocio distintas (ver D9).

**Dentro del módulo** (todas las citas `[leído del código]`):

| Archivo | Línea | Método | Tipo |
|---|---|---|---|
| `EstructuracionComercialController.cs` | 128 | `servicio.ObtenerMediosPublicitarios(estado, texto)` | Lectura |
| `EstructuracionComercialController.cs` | 150 | `servicio.IngresarMedioPublicitario(nombre, estado)` | Escritura (alta) |
| `EstructuracionComercialController.cs` | 155 | `servicio.EditarMedioPublicitario(id, nombre, estado)` | Escritura (edición) |
| `EstructuracionComercialController.cs` | 173, 182 | `servicio.ObtenerDetalleMedioPublicitario(id)` | Lectura |
| `EstructuracionComercialController.cs` | 193 | `servicio.EliminarMedioPublicitario(id)` | Escritura (baja) |
| `MediosPublicitariosController.cs` (API v1) | 35 | `servicioGestionComercial.ObtenerMediosPublicitariosAsync(...)` | Lectura |

Más: `MediosPublicitariosViewModel.cs`, `MedioPublicitario.cs` (modelo API), `ServicioGestionComercial.cs` (definición) y 4 vistas `.cshtml` — son el módulo mismo, no consumidores.

**Consumidores externos dentro de GestionComercial** (leen el catálogo o la columna FK `opo_medpub_consecutivo`, fuera del CRUD propio):

| Archivo | Línea | Método / campo | Tipo |
|---|---|---|---|
| `Areas/API/v1/GestionComercial/Controllers/OportunidadesController.cs` | 360-367, 452-454 | `servicioGestionComercial.ObtenerDetalleMedioPublicitario(...)`, valida existencia si el parámetro de institución `MEDIO_PUBLICITARIO_OBLIGATORIO` está activo | Lectura |
| `Areas/GestionComercial/Controllers/OportunidadesNewController.cs` | ≈1623, 236-241 | `servicio.ObtenerMediosPublicitarios(true, null)`; columna `opo_medpub_consecutivo`/`medpub_nombre` en export/grilla | Lectura |
| `ValidarInformacionJob.cs` | 524-541 | Valida por nombre contra el listado (`ObtenerMediosPublicitarios`) al importar oportunidades en bulk; error si no existe o si es obligatorio y viene vacío | Lectura |
| `OportunidadNewFormModel.cs` | 29 | `servicio.ObtenerMediosPublicitarios(true, null)` — combo de selección | Lectura |
| `OportunidadNewViewModel.cs` | 91-112 | Campos `opo_medpub_consecutivo`, `medpub_nombre`, `medpub_abreviatura` (ver D8/GAP-7) | Lectura |
| `Constantes.cs`, `ImportarOportunidadesNewViewModel.cs`, `ImportarOportunidadNewJob.cs` | — | Referencian la columna en flujos de importación/exportación de Oportunidades | Lectura |
| `ServicioReporteOportunidadesComerciales.cs` (Reportes) | — | Agrupa oportunidades por medio publicitario | Lectura |
| 2 vistas (`_ListaImportacion.cshtml`, `_EditarMaster.cshtml`) | — | Renderizan la columna | Lectura |

**Dominio homónimo "Formarte" (fuera de alcance, no detallado por decisión del equipo — GAP-5):** 25 archivos entre `PersonalizacionesController.cs`, `ServicioEstablecimiento.cs`, 2 viewmodels, 6 vistas propias y 15 consumidores en Comunidad (Personas/Preinscripciones).

**Conteo consolidado:**

| | Archivos | Lectura | Escritura |
|---|---|---|---|
| Núcleo del módulo en alcance | 10 | Sí | Sí (2 archivos) |
| Consumidores externos dentro de GestionComercial | 11 | Sí | No |
| Dominio homónimo "Formarte" (fuera de alcance) | 25 | Sí | Sí (2 archivos, no detallados) |
| **Total** | **46** | **42 archivos de solo lectura** | **4 archivos con escritura confirmada** |

## 6. Parámetros y personalizaciones

| Parámetro/personalización | Efecto | Dónde |
|---|---|---|
| `aplent_codigoP` (código de institución) | Ninguno sobre el filtrado — se recibe en las 6 SPs pero no se usa en ningún `WHERE`/`INSERT`/`DELETE` (ver D6) | Las 6 SPs `pa_opo_medios_publicitarios_*` |
| `MEDIO_PUBLICITARIO_OBLIGATORIO` (parámetro de institución, booleano) | Si está activo, exige que la oportunidad tenga un medio publicitario asociado al crearla vía API, validando su existencia | `Areas/API/v1/GestionComercial/Controllers/OportunidadesController.cs:360-367,452-454` |
| Cualquier `ZPERSONALIZACION_*` sobre este catálogo | Ninguna — está siempre habilitado para toda institución (ausencia verificada por búsqueda en `Constantes.cs` y en los archivos del módulo) | N/A |

**Nota de contraste (fuera de alcance, solo para no perder la referencia):** el dominio homónimo de Formarte sí está gateado por `ZMEDIOS_PUBLITARIOS_FORMARTE = "63"` (`Constantes.cs:2045`). Por decisión del equipo (GAP-5), sus personalizaciones no se detallan en este documento.

## 7. Defectos e inconsistencias

| # | Defecto | Severidad | Fuente | Veredicto |
|---|---|---|---|---|
| D1 | Endpoint `GET api/mediospublicitarios` marcado `[AllowAnonymous]`; `BaseApiController.Initialize` acepta el header `aplentId` sin exigir credencial cuando está presente, exponiendo el catálogo de cualquier institución conociendo su GUID | Alta | `MediosPublicitariosController.cs:13`; `BaseApiController.cs:46-68` `[leído del código]` | **Riesgo aceptado** — mitigado por Gateway en producción. Se recomienda que el nuevo servicio no dependa solo de esa mitigación externa |
| D2 | API keys de clientes reales (incluye un tenant marcado "PRODUCCIÓN") y un bypass Basic-Auth hardcodeados en el filtro de autenticación de la API, del cual depende la protección real de D1 | Alta | `Infrastructure/WebApi/ApiKeyAuthenticationHandler.cs:16-37,77-97` `[leído del código]` | **Diferido** — hallazgo transversal de seguridad, no específico de este módulo; no bloquea esta migración puntual |
| D3 | `pa_opo_medios_publicitarios_eliminar` ejecuta `DELETE` físico sin validar la FK entrante desde Oportunidades; el error de constraint se propaga sin traducir a mensaje de negocio | Media | SP `[verificado en BD]`; `EstructuracionComercialController.cs:188-200` `[leído del código]` | **Se corrige** en el nuevo servicio |
| D4 | Sin validación de unicidad de `medpub_nombre` en ingreso/modificación | Media | SPs `[verificado en BD]` | **Se corrige** en el nuevo servicio |
| D5 | `medpub_nombre` y `medpub_estado` son `NULL`-ables a nivel de columna aunque la UI/ViewModel los trata como obligatorios | Baja | `INFORMATION_SCHEMA.COLUMNS` `[verificado en BD]` | **Se corrige** — la tabla nueva agrega `NOT NULL` real (ver GAP-9) |
| D6 | Parámetro `@aplent_codigoP` recibido en las 6 SPs pero no usado en ninguna condición | Baja | 6 SPs `[verificado en BD]` | **Se replica** — comportamiento inocuo, consistente con el modelo de BD por institución |
| D7 | Sin columnas de auditoría (usuario/fecha de creación o modificación) en `tbl_opo_medios_publicitarios` | Baja | Esquema de columnas `[verificado en BD]` | **Se corrige** en el nuevo servicio |
| D8 | La propiedad derivada `EditarMedioPublicitario` en `OportunidadNewViewModel.cs:98-112` depende de `medpub_abreviatura`, columna inexistente; la regla de negocio (bloquear edición para códigos "IN"/"RI"/"RD") nunca se ejecuta y siempre retorna `true` | Media | `OportunidadNewViewModel.cs:96-112` `[leído del código]` | **Diferido** — solo se presenta en `master`; no se considera para esta migración por ahora (ver GAP-7) |
| D9 | Colisión de nombres de método entre `ServicioGestionComercial` y `ServicioEstablecimiento` (`ObtenerMediosPublicitarios`, `ObtenerDetalleMedioPublicitario`, `IngresarMedioPublicitario`, `EliminarMedioPublicitario`) para dos entidades de negocio distintas | Media | `ServicioGestionComercial.cs` vs. `ServicioEstablecimiento.cs` `[leído del código]` | PENDIENTE — ver §10 (GAP-10) |
| D10 | Colisión de vocabulario/dominio: dos implementaciones independientes de "Medios Publicitarios" sin relación estructural entre sí | Media | Ver §2 `[leído del código]` | **Diferido** — queda fuera de alcance por decisión del equipo (GAP-5) |

## 8. Rendimiento

`NO APLICA` — no hay dataset de Application Insights disponible para este análisis (GAP-3, confirmado por el equipo). Hallazgos cualitativos derivados del código:

- Catálogo administrativo de bajo volumen esperado — no hay evidencia de un volumen alto de escritura (solo 3 puntos de escritura, todos manuales desde el panel admin).
- El listado admin (`ListaMediosPublicitarios`, `EstructuracionComercialController.cs:124-131`) no tiene ningún atributo de cache (`OutputCache`/`DonutOutputCache`) — a diferencia del homónimo de Formarte, que sí lo tiene. Dato cualitativo, no señal de problema de performance por sí solo.
- El endpoint API v1 (`Get`, `MediosPublicitariosController.cs:19-46`) ya está paginado (`PaginationInfo`) — diseño preparado para volumen, sin indicio de que hoy lo necesite.
- Las consultas de listado hacen `SELECT` directo sobre una tabla de 3 columnas sin índices adicionales documentados más allá de la PK; dado el tamaño típico de este catálogo (decenas de registros por institución), no se anticipan problemas de performance propios del módulo.

## 9. Alcance y fuera de alcance

**Dentro de alcance:** el catálogo `tbl_opo_medios_publicitarios` y su CRUD completo (MVC + API pública), incluyendo las 6 SPs asociadas y el parámetro de institución `MEDIO_PUBLICITARIO_OBLIGATORIO`.

**No entra ahora, no es exclusión permanente:** Oportunidad (`tbl_opo_oportunidades`) depende de este catálogo vía FK y, posiblemente, vía un consumo de datos adicional no confirmado (ver §10, GAP-11). Oportunidad no se migra como parte de este cambio; su estrategia de convivencia con el catálogo ya migrado se resuelve en el Plan de trabajo, no en este Discovery.

**Fuera de alcance (permanente, por decisión del equipo):**
- El dominio homónimo `tbl_per_medios_publicitarios` y su personalización de cliente "Formarte" (`PersonalizacionesController.cs`, `ServicioEstablecimiento.cs`, y los 25 archivos consumidores en Comunidad/Establecimiento listados en §5) — no se tendrán en cuenta personalizaciones (GAP-5).
- La corrección del hardcodeo de API keys en `ApiKeyAuthenticationHandler.cs` (D2) — se gestiona como iniciativa de seguridad transversal, no como parte de esta migración.

**Fuera de alcance de este documento (no de la migración):** el diseño de la nueva API, DTOs y decisiones de arquitectura — corresponden al Plan de trabajo, no al Discovery.

## 10. Decisiones pendientes y GAPs

| GAP | Estado | Detalle | Dueño | Recomendación por defecto |
|---|---|---|---|---|
| GAP-1 | Cerrado | Permisos se asignan desde base de datos, no desde atributos de código | Líder técnico GestionComercial | Replicar el modelo de permisos data-driven en el nuevo servicio; documentar la tabla/esquema de permisos en el Plan de trabajo |
| GAP-2 | Cerrado | Existe Gateway en producción que mitiga la exposición de D1 | Equipo de plataforma/seguridad | El nuevo servicio no debe depender únicamente del Gateway; exigir autenticación explícita a nivel de aplicación |
| GAP-3 | Cerrado — NO APLICA | Sin dataset de rendimiento | — | — |
| GAP-4 | Cerrado — NO APLICA | Sin servicio hermano de referencia | — | — |
| GAP-5 | Cerrado | No se tendrán en cuenta personalizaciones (incluida "Formarte") | Product Owner GestionComercial/Comunidad | Excluir `tbl_per_medios_publicitarios` de todo trabajo de migración futuro salvo que se abra explícitamente |
| GAP-6 | Cerrado | Esquema verificado contra `udbzq10trabajos` como fuente de verdad; idéntico al de `zudbzq10desarrollopagosregulares` | — | — |
| GAP-7 | Cerrado — diferido | D8 (`medpub_abreviatura`) solo se presenta en `master`; el equipo decide no considerarlo todavía | Product Owner GestionComercial | Retomar en una iteración futura si `master` se estabiliza/despliega y la regla de negocio resulta necesaria |
| GAP-8 | Cerrado | Confirmado por el equipo: ninguna institución tiene columnas adicionales en `tbl_opo_medios_publicitarios` — el esquema de 3 columnas es el mismo en todas | — | El modelo de datos del nuevo servicio se congela en las 3 columnas verificadas, sin margen a variantes por cliente |
| GAP-9 | Cerrado | El equipo confirma agregar `NOT NULL` real en la tabla nueva para `medpub_nombre` y `medpub_estado` | Arquitectura/DBA | La tabla nueva declara ambas columnas `NOT NULL` con default; solo se tolera `NULL` durante la carga inicial de datos legacy, si aplica |
| ⚠️ GAP-10 (ABIERTO) | Pendiente | D9: colisión de nombres de método entre `ServicioGestionComercial` y `ServicioEstablecimiento` en el legado · Afecta: riesgo de que alguien use el código del dominio equivocado como referencia durante la migración · Confirmar con: equipo del monolito legado | Riesgo aceptado — no se toca el legado; se documenta para que el equipo nuevo no confunda ambos servicios al leer código viejo como referencia |
| ⚠️ GAP-11 (BLOQUEANTE) | Pendiente | Estrategia de convivencia entre el catálogo migrado y Oportunidad (que permanece en el monolito) sin decidir; tampoco se confirmó si Oportunidad consume el catálogo por un camino adicional no identificado (ej. JOIN directo en alguna SP de Oportunidad) · Afecta: todo el diseño del Plan de trabajo · Confirmar con: Arquitectura/Product Owner | Mantener el catálogo en la misma base de datos que el monolito durante la convivencia (o exponerlo vía el servicio nuevo y hacer que Oportunidad lo consuma por API) — decisión a tomar explícitamente antes de iniciar el Plan, no por defecto |

## 11. Changelog

- **2026-08-14** — Documento consolidado a partir de dos análisis de Discovery independientes sobre el mismo módulo (uno de Brayan Gamboa sobre el commit `379252902a1abe43902fd8fb556705c4150e88a0`, otro de Andrés Pérez sobre el commit `2baacb6988b5b8ecb6f33b3ab82de58e762a7c61`), realizados sin conocimiento mutuo. Se reconcilió la nomenclatura de destino (`service: crm-service`, `context: MediosPublicitarios`, descartando la variante `crm-service-q10`/`AdvertisingMedia` del segundo análisis) por decisión explícita del equipo. Se fusionaron los mapas de consumidores (§5), se incorporaron los defectos D5 (nullability vs. UI) y D9 (colisión de nombres de método) del segundo análisis, y se agregó GAP-11 (estrategia de convivencia con Oportunidad), ausente en el primer análisis. Se cerró el GAP-8 original del segundo análisis (trazabilidad de BD) porque el primer análisis sí registra las bases consultadas.
- **2026-08-14** — Cerrados GAP-7, GAP-8 y GAP-9 tras confirmación del equipo: D8 (`medpub_abreviatura`) se difiere por ser exclusivo de `master`; se confirmó que el esquema de `tbl_opo_medios_publicitarios` es idéntico en todas las instituciones (sin columnas adicionales); D5 (nullability vs. UI) se corrige agregando `NOT NULL` real en la tabla nueva.
