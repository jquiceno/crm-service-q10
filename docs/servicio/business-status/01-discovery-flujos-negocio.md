---
service: Crm
context: FlujosNegocio
doc: discovery
status: draft
source: jack @ db555c532f343a2b309be1b441f8b860d1d8b597
updated: 2026-08-14
---

# Discovery — Flujos de negocio

> Revisión 2. Incorpora el contraste con `discovery-business-state.md` (contexto `BusinessState`) y la verificación cruzada en un segundo tenant. Las divergencias entre ambos documentos y su resolución están en §12.

## §0 Insumos verificados

| Insumo | Qué aportó | Nivel de evidencia |
|---|---|---|
| `Q10.Jack/Data/Servicios/ServicioGestionComercial.cs`, región `#region Flujo de negocio` (1625-1686) y `ObtenerFlujosNegociosAsync` (2765-2775) | Nombres exactos de los 6 SPs, parámetros enviados, ausencia de caché | `[leído del código]` |
| `Q10.Jack/Areas/GestionComercial/Controllers/EstructuracionComercialController.cs`, región `#region Flujo De negocio` (204-311) | 7 rutas, regla de creación 0%/100%, paginación en memoria, ausencia de `[AllowAnonymous]` en esta región | `[leído del código]` |
| `Q10.Jack/Areas/GestionComercial/ViewModels/Negocio/FlujoNegocioViewModel.cs` | Mapeo del modelo, `[Required]`, `[Range(0,100)]`, default de color `CCCCCC` | `[leído del código]` |
| `Q10.Jack/Areas/API/v1/GestionComercial/Controllers/FlujoNegociosController.cs` y `Models/FlujoNegocios/FlujoNegocio.cs` | Segundo frente de consumo, contrato público, perfil de AutoMapper, `[AllowAnonymous]` | `[leído del código]` |
| `Core/Q10.Core/Data/DataAccess.cs` | Mecanismo real de propagación de errores de SP y de paginación | `[leído del código]` |
| Barrido `git grep` sobre archivos versionados `*.cs`, `*.cshtml`, `*.js`, excluyendo `Q10.Jack/obj/` | Mapa completo de consumidores: 49 archivos, 377 referencias, 25 sitios de lectura, 3 de escritura | `[leído del código]` |
| **BD tenant `udbzq10dbdesarrolloordenespago`** — `127.0.0.1,1434`, SQL Server 16.0.4222.2 | Esquema real, PK, ausencia de índices/CHECK/UNIQUE/triggers/defaults, FKs entrantes, **cuerpo completo de los 6 SPs**, dependencias vía `sys.sql_expression_dependencies` | `[verificado en BD]` |
| **BD tenant `udbzq10trabajos`** — `127.0.0.1,1433`, servidor `EC2AMAZ-IE1SCOG` | Contraverificación en un tenant de volumen realista: 12 estados, 299.937 negocios, **invariante de centinelas rota** | `[verificado en BD]` |
| **Muestreo de la invariante sobre 20 bases de tenant** — servidor `EC2AMAZ-IE1SCOG` | Distribución real de la invariante de centinelas, duplicados, nulos y colores vacíos. **Muestra, no censo**: de 1.225 bases en línea, 1.175 negaron acceso al login y 30 no tienen la tabla | `[verificado en BD]`, con la limitación de cobertura indicada en GAP-5 |
| `discovery-business-state.md` (contexto `BusinessState`) | Documento paralelo sobre el mismo dominio. Aportó la paginación en memoria, el contraste de `[AllowAnonymous]` entre regiones vecinas, el conflicto de FK como decisión de producto y el tenant `udbzq10trabajos` | Contrastado punto por punto en §12 |
| Telemetría / APM | — | `NO APLICA` — sin dataset. Ver GAP-11 |

### Reconciliación de commits

| Referencia | Estado |
|---|---|
| `db555c532f343a2b309be1b441f8b860d1d8b597` | Válido. HEAD de este análisis, rama `hotfix/JK-11196-api-pagos-greenpay`, 2026-08-14 |
| `af94d015f3a4f8f74e75d1df87144494a78dc36b` | Válido. Último cambio a `EstructuracionComercialController.cs`: 2022-06-21, «JH-3694 Se realizan correcciones en los estados de negocio». El controlador lleva más de cuatro años sin tocarse |
| `cf18c7fc267861eecdfea714390586dac40c3a7f` | **No existe en este clon.** Citado como HEAD por el documento paralelo. Debe reconciliarse antes de congelar: un discovery cuyo commit de origen no resuelve no es auditable |

Árbol de trabajo sucio al momento del análisis: 3 archivos modificados (`Core/Q10.Core/Azure/AzureHelpers.cs`, `Q10.Jack/Areas/Seguridad/Controllers/CredencialesController.cs`, `Q10.Jack/Web.config`). Ninguno pertenece al dominio.

Acceso a base de datos: **solo lectura**. Únicamente consultas al catálogo del sistema, `SELECT` sobre datos y `OBJECT_DEFINITION` sobre los SPs.

---

## §1 Resumen ejecutivo

**Qué hace.** El contexto administra el catálogo de etapas del embudo comercial de cada institución: la lista ordenada de estados por los que pasa un negocio hasta su cierre. Cada estado tiene nombre, porcentaje de avance, color hexadecimal para el Kanban y bandera de actividad. El porcentaje no es decorativo: **es el identificador semántico del estado**. Porcentaje 0 significa «Perdido» y porcentaje 100 significa «Ganado». No existe bandera ni columna que marque los terminales; toda la aplicación los localiza comparando el porcentaje.

**Tamaño.** El dominio propio es minúsculo: una tabla de 5 columnas, 6 SPs, entre 6 y 12 filas según el tenant, un ABM de 7 acciones y un endpoint de API de solo lectura. El acoplamiento es lo grande: 49 archivos y 377 referencias en el monolito, 25 sitios de lectura contra 3 de escritura, 37 objetos de base de datos que leen la tabla —31 ajenos al módulo— y, en el tenant de volumen realista, **299.937 negocios colgando de la FK**.

**El hallazgo que gobierna todo lo demás.** La invariante sobre la que se apoya el dominio entero —«existe exactamente un estado al 0% y exactamente uno al 100%»— **está rota en datos reales**. En `udbzq10trabajos` hay dos estados «Perdido» al 0% y dos «Ganado» al 100%, y además dos estados distintos al 30% `[verificado en BD]`. Peor: de los dos «Ganado», **uno está inactivo y otro activo**, y dos endpoints de API que marcan negocios como ganados consultan el catálogo **sin filtrar por estado activo** y resuelven con `FirstOrDefault` sobre un `ORDER BY negest_porcentaje` que **no tiene desempate**. El resultado es una asignación no determinista que puede dejar un negocio en un estado inactivo.

El muestreo sobre 20 bases de tenant matiza la frecuencia pero no la gravedad: **19 de 20 cumplen la invariante y una sola la rompe** — precisamente la que concentra el **99,7% de los negocios de la muestra**. Los 19 tenants sanos conservan el catálogo semilla de 5 estados casi sin editar; el que se rompió es el único con años de uso real. La deriva no es aleatoria: aparece donde el catálogo se edita.

Nada impide llegar ahí: no hay CHECK constraints, ni índices únicos, ni triggers, ni validación en los stored procedures de escritura, y la validación de la aplicación cubre el alta pero no la edición.

**Veredicto de migración.** Migrable como contexto propio reutilizando la tabla, y con una asimetría a favor: la escritura está concentrada en un único archivo, mientras la lectura está dispersa en 25 sitios de 6 áreas. Eso permite cortar primero la escritura y dejar la lectura en convivencia. Pero la migración **no es un CRUD de catálogo**. Antes de escribir el Plan hay que resolver tres cosas que no se responden leyendo código:

1. Qué hacer con los tenants que ya tienen la invariante rota (GAP-5, ahora cerrado en diagnóstico y abierto en remediación).
2. Los **11 stored procedures generados dinámicamente** que leen la tabla, no están versionados y varían por institución (GAP-16).
3. Si el nuevo servicio debe exponer desde el día uno un equivalente del endpoint anónimo `api/flujonegocios`, que es una decisión de producto y de seguridad, no técnica (GAP-1).

---

## §2 Vocabulario del negocio

| Término (ES) | Definición | Cómo lo llama el legado |
|---|---|---|
| Flujo de negocio | El catálogo completo de etapas por las que avanza un negocio en una institución | Región `#region Flujo de negocio`; carpeta de vistas `FlujoNegocio`; clase `FlujoNegocioViewModel` |
| Estado del negocio / etapa del flujo | Una etapa individual. Es como lo llama el negocio y como se titula la pantalla | Tabla `tbl_opo_negocios_estados`; prefijo `negest_`; título «Estados de Negocio» |
| Porcentaje de avance | Valor de 0 a 100 que ordena las etapas y define la semántica de las terminales | `negest_porcentaje` |
| Ganado | Estado terminal de éxito. Sin marca propia: se identifica porque su porcentaje es 100 | `negest_porcentaje = 100` |
| Perdido | Estado terminal de fracaso. Se identifica porque su porcentaje es 0 | `negest_porcentaje = 0` |
| Etapa intermedia | Estado con porcentaje estrictamente entre 0 y 100. Son las únicas seleccionables al crear o editar un negocio | Filtro `negest_porcentaje != 0 && negest_porcentaje != 100`, repetido en 7 sitios |
| Color de etapa | Hexadecimal de 6 caracteres **sin** `#`, usado para pintar la columna del Kanban de negocios y la barra de progreso del detalle | `negest_color`; propiedad calculada `colorEstado`, que sustituye nulo por `CCCCCC` en tiempo de ejecución y nunca lo persiste |
| Activo / Inactivo | Si la etapa está disponible para asignarse | `negest_estado` |
| Negocio | La unidad comercial que transita por las etapas. Agregado consumidor, fuera de este contexto | `tbl_opo_negocios`; FK `neg_negest_consecutivo` |
| Oportunidad | Agrupador comercial que contiene uno o más negocios | Prefijo `opo_` |
| Causa de pérdida | Motivo obligatorio al llevar un negocio a Perdido | Prefijo `cau_`; FK `neg_cau_consecutivo` |
| Historial de transición de etapa | Registro de cuándo un negocio cambió de etapa. **Fuera de alcance de este contexto** | `tbl_opo_historial_negocio_estados`; `his_negest_consecutivo_anterior` / `_siguiente` |
| Consecutivo | Identificador numérico autoincremental. El sufijo `P` marca la clave primaria | Sufijo `_consecutivoP` |
| Institución | El cliente. Cada institución vive en su propia base de datos física | `aplent_codigoP` (código), `ent_bd` (nombre físico de la BD) |
| Q10 Master | Instancia interna donde Q10 gestiona su propia operación comercial con el mismo modelo | Propiedad `EnQ10Master` |

Solo el lado español. El mapeo a nombres técnicos en inglés es del Plan.

---

## §3 Estado actual

### 3.1 Arquitectura y multi-tenancy

Monolito ASP.NET MVC 5 con Web API 2 en el mismo proceso, organizado por áreas, sobre .NET Framework. Acceso a datos exclusivamente por stored procedures invocados con `Dictionary<string,object>` a través de Dapper, encapsulado en `Core/Q10.Core/Data/DataAccess.cs`. No hay ORM ni modelo de entidades.

El aislamiento entre clientes es **base de datos por tenant, y solo eso** `[verificado en BD]`. `InfoInstitucion.BaseDatos` construye el `DataAccess` con el nombre físico de la base (`ent_bd`) y una de cuatro cadenas de conexión ([`InfoInstitucion.cs:873-895`](../../../../jack/Q10.Jack/Areas/Seguridad/ViewModels/Cuenta/InfoInstitucion.cs)):

| Condición | Cadena de conexión |
|---|---|
| Institución en alianza | `AlianzaDB` |
| Con `serbd_config_name` definido | El valor de `serbd_config_name` |
| `EnQ10Master` | `MasterDB` |
| Resto | `AzureDB` |

Los servidores inspeccionados alojan 850 y 1.225 bases respectivamente `[verificado en BD]`, una por tenant.

**No existe un segundo nivel de aislamiento por columna.** Los seis stored procedures reciben `@aplent_codigoP`, lo que sugiere un discriminador dentro de la base, pero al leer los cuerpos se confirma que **ninguno lo usa jamás**, y la tabla **no tiene ninguna columna de institución** `[verificado en BD]`. Es un parámetro muerto heredado. Esto importa: cualquier diseño que asuma filtrado por institución dentro de la base estaría modelando algo que no existe.

### 3.2 Ubicación en el código

| Pieza | Ruta |
|---|---|
| Servicio de datos | `Q10.Jack/Data/Servicios/ServicioGestionComercial.cs`, región `#region Flujo de negocio` (1625-1686) |
| Servicio de datos, frente API | `Q10.Jack/Data/Servicios/ServicioGestionComercial.cs:2765-2775` |
| Controlador de UI interna | `Q10.Jack/Areas/GestionComercial/Controllers/EstructuracionComercialController.cs`, región `#region Flujo De negocio` (204-311) |
| Controlador de API pública | `Q10.Jack/Areas/API/v1/GestionComercial/Controllers/FlujoNegociosController.cs` |
| ViewModel | `Q10.Jack/Areas/GestionComercial/ViewModels/Negocio/FlujoNegocioViewModel.cs` |
| Modelo y contrato de API | `Q10.Jack/Areas/API/v1/GestionComercial/Models/FlujoNegocios/FlujoNegocio.cs`, incluye el `AutoMapper.Profile` y el `PartialResponseConfig` |
| Vistas | `Q10.Jack/Areas/GestionComercial/Views/EstructuracionComercial/FlujoNegocio/{Inicio,_Lista,_EditarCrear,_Eliminar}.cshtml` |
| Consumidor de reportes | `Q10.Jack/Reportes/Reportes/GestionComercialNew/OportunidadesComercialesNew/DetalleNegocioNew.cs` y `.../ActividadesOportunidadesNew/OportunidadesActiviadesNew.cs` |

### 3.3 Puntos de llamada expuestos

| Ruta | Verbo | Acción | Filtros aplicados |
|---|---|---|---|
| `FlujoNegocio/inicio` | GET, HEAD | `InicioFlujoNegocio` | — |
| `FlujoNegocio/Lista` | GET, HEAD | `ListaFlujoNegocio` | — |
| `FlujoNegocio/Crear` | GET, HEAD | `CrearFlujoNegocio` | `OnlyAjax` |
| `FlujoNegocio/ActualizarOportunidad` | POST | `ActualizarFlujoNegocio` | `OnlyAjax` |
| `FlujoNegocio/{id}/Editar` | GET | `EditarFlujoNegocio` | `OnlyAjax` |
| `FlujoNegocio/{id}/Eliminar` | GET, HEAD | `EliminarFlujoNegocio` | `OnlyAjax` |
| `FlujoNegocio/{id}/Eliminar` | POST | `EliminarFlujoNegocio` | `OnlyAjax` |
| `api/flujonegocios` | GET | `FlujoNegociosController.Get` | `AllowAnonymous`, `ValidateModel` |

El nombre `FlujoNegocio/ActualizarOportunidad` es un copiado del bloque de Tipos de Oportunidad inmediatamente superior en el mismo archivo (`EstructuracionComercialController.cs:45`) y no corresponde al recurso.

### 3.4 Habilitación y control de acceso

**La región de Flujo de negocio no tiene ninguna acción `[AllowAnonymous]`** `[leído del código]`. Es una precisión que vale la pena registrar porque no es la norma en su vecindario: dentro del mismo controlador, las regiones `Estado Oportunidad` (líneas 372, 382, 392), `Cargos` (497) y `Causas` (517, 528, 548, 561) sí exponen acciones anónimas. La exposición anónima en este controlador es una deriva por acción, no una política, y este catálogo quedó del lado correcto.

El control de acceso efectivo viene del filtro global `AutorizacionAttribute` ([`FilterConfig.cs:13`](../../../../jack/Q10.Jack/FilterConfig.cs)), que hereda de `System.Web.Mvc.AuthorizeAttribute` y delega en `SecurityHelpers.IsAuthorizedCore`. Ese método compara acción, controlador y área contra `SessionManager.PermisosUsuario`, y contrasta además contra `SessionManager.PermisosRevocadosUsuario` ([`SecurityHelpers.cs:50-63`](../../../../jack/Q10.Jack/Infrastructure/SecurityHelpers.cs)). Las filas del catálogo de permisos viven en base de datos y no fueron consultadas (GAP-7). Las vistas usan `Html.AuthorizedLink`, que es presentación y no control de acceso.

**Autorización del frente de API.** Un `DelegatingHandler` global, `ApiKeyAuthenticationHandler`, valida el header `X-Api-Key` contra un diccionario en memoria y construye un `ClaimsPrincipal` con el `aplentId` asociado. El controlador está marcado `[AllowAnonymous]`, lo que neutraliza el `AuthorizeAttribute` global registrado en [`WebApiConfig.cs:57`](../../../../jack/Q10.Jack/WebApiConfig.cs), y el handler **no rechaza peticiones sin header**: llama a `base.SendAsync` de forma incondicional. `BaseApiController.Initialize` resuelve la institución a partir del header `aplentId` sin autenticar (`BaseApiController.cs:46-68`).

**Habilitación funcional.** El modelo completo de negocios con flujo se activa por el parámetro de institución **381**, `NUEVO_MODELO_OPORTUNIDADES` (`Constantes.cs:1243`). Ver §6.

---

## §4 Modelo de datos y SPs

### 4.1 Tabla `dbo.tbl_opo_negocios_estados`

Todas las filas `[verificado en BD]`, idénticas en los dos tenants inspeccionados.

| # | Columna | Tipo | Nulabilidad | Identity | Default | Trampa |
|---|---|---|---|---|---|---|
| 1 | `negest_consecutivoP` | `int` | NOT NULL | Sí | — | — |
| 2 | `negest_nombre` | `varchar(200)` | **NULL** | No | — | La aplicación lo declara `[Required]` pero no valida la longitud de 200 ni en el modelo ni en la vista |
| 3 | `negest_estado` | `bit` | **NULL** | No | — | La aplicación lo mapea a `bool` no nullable. El formulario arranca en `true` por el valor inicial del ViewModel en `EstructuracionComercialController.cs:231`, no por default de columna |
| 4 | `negest_porcentaje` | **`decimal(20,5)`** | **NULL** | No | — | La UI y el contrato público lo tratan como entero 0-100, pero se persiste con 5 decimales |
| 5 | `negest_color` | `varchar(20)` | **NULL** | No | — | Hay filas reales con el valor vacío o nulo. La UI lo enmascara con `CCCCCC` calculado en runtime, nunca persistido |

**No existe columna de institución.**

### 4.2 Objetos asociados

| Clase de objeto | Resultado | Marca |
|---|---|---|
| Clave primaria | `PK_tbl_opo_negocios_estados`, CLUSTERED sobre `negest_consecutivoP` | `[verificado en BD]` |
| Índices adicionales | Ninguno | `[verificado en BD]` |
| CHECK constraints | Ninguno | `[verificado en BD]` |
| UNIQUE constraints | Ninguno | `[verificado en BD]` |
| Triggers | Ninguno | `[verificado en BD]` |
| Default constraints | Ninguno | `[verificado en BD]` |

La base no impide dos estados al 100%, ni un porcentaje fuera de rango, ni nombres duplicados, ni nulos en nombre o estado. **Y no es hipotético: ya ocurrió.** Ver §4.4.

### 4.3 Claves foráneas entrantes

| Constraint | Tabla hija | Columna hija | Al borrar | Marca |
|---|---|---|---|---|
| `FK_tbl_opo_negocios_tbl_opo_negocios_estados` | `tbl_opo_negocios` | `neg_negest_consecutivo` | `NO_ACTION` | `[verificado en BD]` |
| `FK_..._historial_..._anterior` | `tbl_opo_historial_negocio_estados` | `his_negest_consecutivo_anterior` | `NO_ACTION` | `[verificado en BD]` |
| `FK_..._historial_..._siguiente` | `tbl_opo_historial_negocio_estados` | `his_negest_consecutivo_siguiente` | `NO_ACTION` | `[verificado en BD]` |

Las dos del historial se listan por completitud del esquema; el historial está fuera de alcance (§9).

### 4.4 Datos reales — dos tenants

#### `udbzq10dbdesarrolloordenespago` — desarrollo, 60 negocios

| `negest_consecutivoP` | `negest_nombre` | `negest_estado` | `negest_porcentaje` | `negest_color` | Negocios |
|---:|---|---|---:|---|---:|
| 1 | `Perdido ed` | True | `0,00000` | `000000` | 1 |
| 2 | `Presentación` | True | `20,00000` | `8beaff` | 17 |
| 4 | `En negociación` | True | `50,00000` | `fff400` | 12 |
| 3 | `Cierre` | True | `80,00000` | `b337ff` | 11 |
| 9 | `Preparación` | False | `90,00000` | `ffa2e5` | 0 |
| 5 | `Ganado` | True | `100,00000` | `49ff7c` | 19 |

Invariante cumplida: un solo estado al 0% y uno al 100%.

#### `udbzq10trabajos` — volumen realista, 299.937 negocios

| `negest_consecutivoP` | `negest_nombre` | `negest_estado` | `negest_porcentaje` | `negest_color` |
|---:|---|---|---:|---|
| 3 | `Perdido` | True | `0,00000` | `ff000f` |
| **17** | **`Perdido`** | **True** | **`0,00000`** | `555050` |
| 7 | `Nuevo` | True | `10,00000` | `c55af7` |
| 2 | `Presentación` | True | `15,00000` | `afcfff` |
| 18 | `Presentación` | False | `20,00000` | `15c9d9` |
| 1 | `Propuesta enviada` | False | `30,00000` | *(vacío)* |
| **6** | **`Contrato Enviado`** | **True** | **`30,00000`** | `3971ff` |
| 20 | `En negociación` | True | `50,00000` | `e0ce00` |
| 4 | `Propuesta aceptada` | True | `60,00000` | `a509a8` |
| 19 | `Cierre` | True | `80,00000` | `ffad00` |
| **5** | **`Ganado`** | **False** | **`100,00000`** | `26b30f` |
| **21** | **`Ganado`** | **True** | **`100,00000`** | `56c209` |

`[verificado en BD]`: 12 estados, 9 activos, 3 inactivos. **Dos al 0%, dos al 100%, dos al 30%.** Nombres duplicados: `Perdido` ×2, `Presentación` ×2, `Ganado` ×2. Una fila con color vacío.

**De los dos estados al 100%, uno está inactivo y otro activo.** Esa combinación, cruzada con el código, produce el defecto D-28.

#### Muestreo de la invariante sobre 20 bases de tenant

`[verificado en BD]`, servidor `EC2AMAZ-IE1SCOG`. **Es una muestra, no un censo**: de 1.225 bases en línea, 1.175 negaron acceso al login (`Cannot open database … The login failed`) y 30 no tienen la tabla. Además, cuatro de las 20 son copias de restauración o snapshots del mismo tenant, de modo que los tenants distintos efectivos son unos 16.

| Métrica sobre las 20 bases muestreadas | Resultado |
|---|---:|
| Con exactamente 1 estado al 0% y 1 al 100% | **19** |
| Con la invariante rota | **1** (`udbzq10trabajos`) |
| Con exactamente 5 estados, es decir el catálogo semilla sin editar | 19 |
| Con porcentajes duplicados | 1 |
| Con nombres duplicados | 1 |
| Con `negest_porcentaje` nulo | 0 |
| Con `negest_porcentaje` no entero | **0** |
| Con `negest_nombre` nulo | 0 |
| Con al menos un `negest_color` nulo o vacío | **18** |
| Negocios en `udbzq10trabajos` | 299.937 |
| Negocios en las otras 19 bases, sumadas | 782 |

Tres lecturas de esta tabla importan para el diseño:

1. **La invariante se rompe donde el catálogo se usa.** Los 19 tenants sanos tienen el catálogo semilla de 5 estados intacto. El único roto es el único con volumen real. La frecuencia baja no reduce el riesgo: reduce la cantidad de tenants a remediar, y concentra el problema en el más importante.
2. **No existe hoy ningún porcentaje fraccionario ni nulo en la muestra.** Eso baja el riesgo práctico inmediato del mapeo `decimal(20,5)` a `int?` descrito en D-21, aunque no elimina el riesgo estructural: nada impide insertarlos.
3. **El color vacío es la norma, no la excepción.** 18 de 20 bases tienen al menos un estado sin color, y en la mayoría son casi todos. El enmascaramiento a `CCCCCC` en tiempo de ejecución no es un caso borde: es el camino habitual. Ver D-20 y D-30.

### 4.5 Stored procedures

Los seis existen `[verificado en BD]`. El de APIs se creó el 06/03/2024 bajo la tarea JQ-12032 y recibió paginación el 07/03/2024; los otros cinco datan de 2019.

| SP | Frente | Propósito | Ejecución desde la app |
|---|---|---|---|
| `pa_opo_negocios_estados_retornar` | UI interna | Listar filtrando por estado y texto | `ExecuteQuery`, síncrono |
| `pa_opo_negocios_estados_detalle_retornar` | UI interna | Detalle por `negest_consecutivoP` | `ExecuteQuery` |
| `pa_opo_negocios_estados_ingresar` | UI interna | Alta | `ExecuteNonQuery` |
| `pa_opo_negocios_estados_modificar` | UI interna | Edición, incluye activar y desactivar | `ExecuteNonQuery` |
| `pa_opo_negocios_estados_eliminar` | UI interna | Borrado por id | `ExecuteNonQuery` |
| `pa_apis_opo_negocios_estados_retornar` | **API pública** | Listar paginado en el propio SP | `ExecuteQueryAsync` con página y tamaño |

#### Semántica y trampas, verificadas leyendo los cuerpos

| SP | Semántica `[verificado en BD]` | Trampa |
|---|---|---|
| `pa_opo_negocios_estados_retornar` | Devuelve las 5 columnas. `WHERE negest_estado = @filtros_estado OR @filtros_estado IS NULL`, y `negest_nombre LIKE '%' + ISNULL(@filtros_texto,'') + '%' OR @filtros_texto IS NULL`. `ORDER BY negest_porcentaje` | **El orden no tiene desempate.** Con dos filas al mismo porcentaje el resultado es no determinista. `@aplent_codigoP` no se usa |
| `pa_opo_negocios_estados_detalle_retornar` | Filtra solo por `negest_consecutivoP` | Ningún filtro de institución. `@aplent_codigoP` no se usa |
| `pa_opo_negocios_estados_ingresar` | `INSERT` de 4 campos y `RETURN SCOPE_IDENTITY()` | Devuelve el consecutivo por código de retorno. **No valida el porcentaje.** Sin parámetros de auditoría |
| `pa_opo_negocios_estados_modificar` | `UPDATE` de 4 campos por `negest_consecutivoP` | Los 4 parámetros tienen default `NULL` y el `SET` es directo, sin `COALESCE`: omitir uno borra el campo. **No valida el porcentaje.** Sin auditoría |
| `pa_opo_negocios_estados_eliminar` | `DELETE` físico por `negest_consecutivoP` | **Sin ninguna validación**: no protege terminales ni verifica referencias. Sin auditoría |
| `pa_apis_opo_negocios_estados_retornar` | `OFFSET (@PageIndex-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY` con `COUNT(*) OVER() AS total_count`. `ORDER BY negest_porcentaje` | Sin filtro de texto. `@PageSize` default `2147483647`. Mismo problema de desempate. `@aplent_codigoP` no se usa |

#### Manejo de errores: correcto, no es defecto

Los seis envuelven su cuerpo en `BEGIN TRY … BEGIN CATCH` que asigna `@NmbError` y `@MsgError` sin relanzar. Leído aisladamente parece que tragan los errores; no es así. `DataAccess` inyecta ambos como parámetros de salida, los lee al terminar y lanza `DatabaseException` si el código es distinto de cero, tanto en lectura ([`DataAccess.cs:323-327, 368-374`](../../../../jack/Core/Q10.Core/Data/DataAccess.cs)) como en escritura ([`:514-518, 547-553`](../../../../jack/Core/Q10.Core/Data/DataAccess.cs)). Es la convención de la casa y funciona.

Consecuencia práctica para el borrado: cuando el estado tiene negocios asociados, el `DELETE` viola la FK, SQL Server produce el error 547, el `CATCH` lo captura en los parámetros de salida y `DataAccess` lo convierte en `DatabaseException`. El controlador lo atrapa y responde 400 con el texto crudo de SQL Server. El usuario recibe un mensaje genérico de motor de base de datos, no «este estado está en uso». Ver D-29.

Hueco adyacente: si la excepción ocurre **fuera** del `CATCH` del SP —caída de conexión, timeout— `ExecuteQuery` la captura, consulta `HandleException` y, si no corresponde reintentar, **devuelve `null`** sin propagar nada ([`DataAccess.cs:329, 356-366, 376`](../../../../jack/Core/Q10.Core/Data/DataAccess.cs)). Ese `null` es el que convierte una falla de infraestructura en `NullReferenceException` en los sitios de D-07.

### 4.6 Paginación: dos patrones distintos

| Frente | Dónde pagina | Mecanismo | Tamaño |
|---|---|---|---|
| UI interna | **En memoria, en el controlador** | `BaseController.AplicarPaginacion` usa `ToPagedList` sobre el resultado completo del SP (`BaseController.cs:562-569`) | 12, por `Constantes.PAGINACION_TAMANO_PAGINA` |
| API pública | **En el SP** | `OFFSET/FETCH` con `@PageIndex` y `@PageSize` | Default 30, tope 1000 (`PaginationInfo.cs:14`) |

`[leído del código]`. El SP de la UI trae el conjunto completo que cumple el filtro. Con catálogos de 6 a 12 filas el impacto es nulo, pero es una divergencia de patrón entre los dos frentes.

### 4.7 Contrato público de la API

`[leído del código]`, desde `FlujoNegocio.cs`.

| Campo expuesto | Origen | Tipo declarado |
|---|---|---|
| `Consecutivo_estado_negocio` | `negest_consecutivoP` | `int` |
| `Nombre` | `negest_nombre` | `string` |
| `Porcentaje` | `negest_porcentaje` | `int` |
| `Codigo_color` | Propiedad calculada `colorEstado`, que sustituye nulo por `CCCCCC` | `string` |
| `Estado` | `negest_estado` | `bool?` |

El contrato declara `Porcentaje` como `int` sobre una columna `decimal(20,5)`, y `Codigo_color` nunca es nulo aunque la columna sí lo sea.

### 4.8 Catálogos y constantes

No hay enum ni lista fija de estados: el catálogo es enteramente editable por el usuario final. Los únicos valores especiales son los límites **0** y **100**, tratados por la regla de creación y por 7 filtros dispersos, no por una constante.

---

## §5 Frentes de consumo y mapa de consumidores

### 5.1 Volumen total en el monolito

`[leído del código]`

| Métrica | Valor |
|---|---|
| Archivos con referencias | 49 |
| Referencias totales | 377 |
| Archivos del módulo | 9 |
| Archivos fuera del módulo | 40 |
| Proyectos de la solución sin referencias | `Q10.Jack.Jobs`, `Q10.Jack.DataAccess`, `Core`, `Q10.Jack.Constants`, `Q10.Control`, `Q10.ID`, `WebSites`, `Asenof` |

Patrón: `negest_|FlujoNegocio|ObtenerFlujosNegocios` sobre archivos versionados `*.cs`, `*.cshtml`, `*.js`, excluyendo `Q10.Jack/obj/`.

### 5.2 Escrituras

**3 sitios, 1 archivo.** Todas en `EstructuracionComercialController.cs`, líneas 248 (`IngresarFlujoNegocio`), 260 (`EditarFlujoNegocio`) y 302 (`EliminarFlujoNegocio`). La API pública es de solo lectura: no expone POST, PUT ni DELETE.

### 5.3 Lecturas

**25 sitios.** Relación lectura/escritura de 8 a 1.

| Método | Sitios |
|---|---:|
| `ObtenerFlujosNegocios` | 20 |
| `ObtenerDetalleFlujoNegocio` | 4 |
| `ObtenerFlujosNegociosAsync` | 1 |

| Archivo | Líneas | Área | ¿Fuera del módulo? |
|---|---|---|---|
| `Areas/API/v1/GestionComercial/Controllers/NegociosController.cs` | 89, 371, 645, 750, 954 | API v1 | Sí |
| `Areas/GestionComercial/Controllers/OportunidadesNewController.cs` | 78, 1472, 1625, 1938 | GestionComercial | Sí |
| `Areas/GestionComercial/Controllers/EstructuracionComercialController.cs` | 221, 282, 291 | GestionComercial | No |
| `Areas/Comunidad/Controllers/PreinscripcionesController.cs` | 1630, 2200 | Comunidad | Sí |
| `Areas/GestionComercial/Controllers/NegociosController.cs` | 438, 474 | GestionComercial | Sí |
| `Areas/Comunidad/Controllers/PersonalizacionesController.cs` | 4172 | Comunidad | Sí |
| `Areas/Comunidad/Services/ServicioPersonas.cs` | 1280 | Comunidad | Sí |
| `Areas/GestionComercial/Controllers/ActividadesController.cs` | 761 | GestionComercial | Sí |
| `Areas/GestionComercial/ViewModels/Negocio/NegocioFormModel.cs` | 25 | GestionComercial | Sí |
| `Areas/GestionComercial/ViewModels/OportunidadesNew/ValidarInformacionJob.cs` | 126 | Job en background | Sí |
| `Areas/GestionQ10/Services/ServicioCotizacionesQ10.cs` | 379 | GestionQ10 | Sí |
| `Areas/GestionQ10/ViewModels/PagosLicencia/PagoSuscripcionCallback.cs` | 149 | GestionQ10, callback de pago | Sí |
| `Areas/GestionQ10/ViewModels/ServiciosEntidad/SuscripcionQ10Callback.cs` | 682 | GestionQ10, callback de pago | Sí |
| `Areas/API/v1/GestionComercial/Controllers/FlujoNegociosController.cs` | 28 | API v1 | No |

Seis áreas distintas consultan el catálogo. Dos lo hacen dentro del camino de cobro de suscripciones.

### 5.4 Escrituras de la clave foránea en negocios

No escriben el catálogo, pero dependen de su contenido.

| Método | Sitios | Ubicación |
|---|---:|---|
| `ModificarNegocioEstado` | 5 | `GestionComercial/Controllers/NegociosController.cs:470`; `GestionAcademica/Controllers/InscripcionesController.cs:204` y `350`; `Comunidad/Controllers/PersonasController.cs:584`; `Comunidad/Services/ServicioPersonas.cs:1285` |

### 5.5 Comparación de los dos frentes

| Aspecto | Frente A — UI interna | Frente B — API pública `api/flujonegocios` |
|---|---|---|
| Autenticación | Sesión autenticada. La región no tiene `[AllowAnonymous]` | `[AllowAnonymous]` a nivel de controlador |
| Operaciones | ABM completo | Solo lectura |
| Filtro de texto | Sí | No existe |
| Filtro de estado | Opcional, por defecto solo activos | **Obligatorio**: 400 si falta `Estado` |
| Paginación | En memoria, en el controlador, tamaño 12 | En el SP, default 30, tope 1000 |
| Nombres del contrato | `negest_*` | `Consecutivo_estado_negocio`, `Nombre`, `Porcentaje`, `Codigo_color`, `Estado` |
| Tipo de `Porcentaje` | `int?` en el ViewModel | `int` en el contrato |
| `Color` | Valor crudo, puede ser nulo o vacío | Nunca nulo: resuelve a `CCCCCC` |
| SP | `pa_opo_negocios_estados_retornar` | `pa_apis_opo_negocios_estados_retornar`, SP distinto y no un envoltorio |

Mismo catálogo, dos contratos y dos políticas de acceso. Esta asimetría es la que más dimensiona el cutover. Ver GAP-1.

### 5.6 Frente de base de datos

`[verificado en BD]`, vía `sys.sql_expression_dependencies`. Invisible desde el código C# y el que más pesa al dimensionar.

**37 objetos referencian la tabla. Seis son del módulo; 31 son ajenos.**

| Grupo | Cant. | Objetos |
|---|---:|---|
| Del módulo | 6 | Los seis SPs del catálogo |
| **Generados dinámicamente** | 11 | `pa_generado_dinamicamente_` 1, 2, 6, 7, 9, 13, 14, 15, 16, 20 |
| Negocios y oportunidades, web | 8 | `pa_opo_negocios_retornar`, `pa_opo_negocios_detalle_retornar`, `pa_opo_negocios_estado_modificar`, `pa_opo_negocios_actividades_modificar`, `pa_opo_oportunidades_retornar`, `pa_opo_oportunidades_informacion_retornar`, `pa_opo_oportunidades_por_identificacion_retornar`, `pa_opo_oportunidades_asesor_modificar` |
| APIs de negocios | 3 | `pa_apis_opo_negocios_retornar`, `pa_apis_opo_negocios_detalle_retornar`, `pa_apis_opo_negocios_favoritos_retornar` |
| Informes | 3 | `pa_inf_opo_oportunidades_comerciales`, `pa_inf_opo_actividades_oportunidades_comerciales`, `pa_inf_opo_excel_oportunidades` |
| Mailing y SMS | 4 | `pa_cor_destinatarios_lista_retornar`, `pa_sms_destinatarios_lista_retornar`, `FN_car_numero_personas_por_lista_filtros_correcto_retornar`, `FN_car_numero_personas_por_lista_filtros_correcto_sms_retornar` |
| Copias de seguridad | 1 | `pa_back_backup_new_oportunidades_retornar` |
| Estructura académica | 2 | `pa_par_sedes_jornadas_grados_modificar`, `pa_par_sedes_jornadas_programas_inactivar` |

Los once generados dinámicamente son el mayor riesgo: se construyen en tiempo de ejecución para los filtros dinámicos de Mailing y SMS, no están versionados y varían por institución.

---

## §6 Parámetros y personalizaciones

### 6.1 Parámetro de institución que gobierna el dominio

| Parámetro | Código | Constante | Efecto |
|---|---|---|---|
| Nuevo modelo de oportunidades | **381** | `Constantes.NUEVO_MODELO_OPORTUNIDADES` (`Constantes.cs:1243`) | Habilita el modelo de negocios con flujo de estados. Sin él, el dominio no se ejerce |

`[leído del código]`. Se consulta con `Institucion.ObtenerParametro<bool>(...)` desde 22 archivos:

| Archivo | Ocurrencias |
|---|---:|
| `Areas/Mailing/Controllers/FiltrosController.cs` | 6 |
| `Areas/GestionComercial/Controllers/OportunidadesController.cs` | 5 |
| `Areas/SMS/Controllers/FiltrosController.cs` | 5 |
| `Areas/Comunidad/Controllers/PreinscripcionesController.cs` | 4 |
| `Areas/Mailing/Views/Filtros/_EditarCrearFiltro.cshtml` | 4 |
| `Areas/SMS/Views/Filtros/_EditarCrearFiltro.cshtml` | 4 |
| `Areas/Comunidad/Controllers/PersonalizacionesController.cs` | 2 |
| `Areas/GestionComercial/Views/Negocios/_EditarCrear.cshtml` | 2 |
| `Q10.Jack.Jobs/Jobs/BackupsJob.cs` | 1 |
| `Areas/Mailing/ViewModels/Filtros/FiltroDetalleFormModel.cs` | 1 |
| Otras 12 vistas | 1 cada una |

**Precisión importante.** El parámetro 381 gobierna si el modelo de negocios existe, no el comportamiento interno del catálogo. Ningún parámetro modula cómo se comporta el ABM de estados: no hay variantes de validación, de orden ni de exposición por institución. Ambas afirmaciones son ciertas y conviene no confundirlas: **el catálogo no tiene parámetros propios, pero vive dentro de una funcionalidad que sí está detrás de un interruptor**.

### 6.2 Otros parámetros presentes en los mismos flujos

Aparecen junto al 381 en las rutas que crean negocios desde preinscripciones, sin gobernar el catálogo: `Constantes.TIPO_PAGO` (`PersonalizacionesController.cs:4153`) y `Constantes.USAN_PAGOS_EN_LINEA` (`PreinscripcionesController.cs:1590`).

### 6.3 Banderas de instancia que alteran el comportamiento

`[leído del código]`, desde `InfoInstitucion.cs`.

| Bandera | Definición | Dónde afecta |
|---|---|---|
| `EnQ10Master` | `ent_bd` contiene `dbzq10master` o `zudbzzpruebaq10master` (`:1068`) | Rótulo del filtro (`ActividadesController.cs:766`); lista de asesores (`NegocioFormModel.cs:33`); bloqueo del avance cuando el negocio está ganado (`_Estados.cshtml:31`) |
| `EsColegio` | — | Plantilla de importación que embebe los estados (`OportunidadesNewController.cs:1477`) |

### 6.4 Personalizaciones por cliente

`[leído del código]`. Dos tocan el dominio:

| Personalización | Ubicación | Qué hace |
|---|---|---|
| Flujo ISER de preinscripciones | `Areas/Comunidad/Controllers/PersonalizacionesController.cs:4172` | Resuelve el primer estado intermedio para crear el negocio derivado de una preinscripción |
| Formulario de entidad Q10 | `Areas/GestionComercial/Views/Negocios/Personalizaciones/Q10/_FormularioEntidad.cshtml:8` | Arrastra `negest_consecutivoP` como campo oculto |

Existen carpetas de personalización para `Cajica` y `Comfama` bajo `Areas/Comunidad/Views/`, sin referencias al catálogo. La búsqueda cubre código versionado; no descarta procedimientos personalizados en bases de producción (GAP-9).

---

## §7 Defectos e inconsistencias

Los veredictos son **propuestas**. Su asignación formal está pendiente: GAP-17.

| # | Sev. | Defecto | Evidencia | Veredicto propuesto |
|---|---|---|---|---|
| D-27 | **Crítico** | **La invariante de estados centinela está rota en datos reales.** En `udbzq10trabajos` hay 2 estados al 0%, 2 al 100% y 2 al 30%, con nombres duplicados. Nada lo impide: ni CHECK, ni UNIQUE, ni validación en los SPs, ni validación en la edición. El muestreo da 1 de 20 bases afectadas, pero esa base concentra el 99,7% de los negocios de la muestra | `[verificado en BD]` §4.4 | **Se corrige** en el servicio. La remediación de los datos existentes es una decisión aparte: GAP-5 |
| D-28 | **Crítico** | **Se puede asignar un estado «Ganado» inactivo, de forma no determinista.** `API/…/NegociosController.cs:371` y `:954` resuelven el estado ganado con `ObtenerFlujosNegocios(null, null)` —sin filtro de actividad— y `FirstOrDefault(m => m.negest_porcentaje == 100)`. En `udbzq10trabajos` hay dos filas al 100%: la 5 inactiva y la 21 activa. El SP ordena solo por `negest_porcentaje`, sin desempate, así que cuál devuelve primero es indefinido | `[leído del código]` + `[verificado en BD]` | **Se corrige.** La resolución de terminales filtra por activo y falla explícitamente ante ambigüedad |
| D-31 | **Alto** | `ORDER BY negest_porcentaje` sin criterio de desempate en los dos SPs de lectura. Con porcentajes repetidos el orden de la lista, del Kanban y de la barra de progreso es no determinista entre ejecuciones | `[verificado en BD]` | **Se corrige.** Orden total explícito por porcentaje y consecutivo |
| D-21 | **Alto** | `negest_porcentaje` es `decimal(20,5)` pero se mapea a `int?` en `FlujoNegocioViewModel.cs:20` y `NegocioViewModel.cs:49`; solo `OportunidadNewViewModel.cs:123` lo declara `decimal`. El contrato público lo declara `int`. Además, la veintena de comparaciones contra 0 y 100 se hacen por igualdad exacta sobre un decimal de 5 posiciones. **Riesgo estructural, no manifestado**: el muestreo no encontró ningún porcentaje fraccionario ni nulo en 20 bases | `[verificado en BD]` + `[leído del código]` | **Se corrige.** Tipo real y comparación por rango o tolerancia |
| D-02 | **Alto** | `NegociosController` web declara `[AllowAnonymous]` a nivel de clase (`:36`) más seis a nivel de acción. Incluye `ListaEstados` (`:432`), que lee el catálogo completo, y `ActualizarNegocioEstado` (`:462`), que escribe el estado de un negocio. `[OnlyAjax]` solo exige el header `X-Requested-With` | `[leído del código]` | **Se corrige** en el servicio. El endpoint del monolito pertenece al contexto Negocios: **diferido** a esa migración |
| D-03 | **Alto** | `FlujoNegociosController` es `[AllowAnonymous]` y `ApiKeyAuthenticationHandler` no rechaza peticiones sin `X-Api-Key`: solo asigna principal cuando la clave es válida y llama a `base.SendAsync` incondicionalmente | `[leído del código]` | **Se corrige**, sujeto a la decisión de producto de GAP-1 |
| D-04 | **Alto** | Catorce claves de API con su `aplentId`, y una credencial `Basic`, escritas en el código fuente (`ApiKeyAuthenticationHandler.cs:20-36`, `:81`) | `[leído del código]` | **Se corrige.** Las credenciales salen del código |
| D-05 | **Alto** | La creación bloquea porcentaje 0 y 100 (`EstructuracionComercialController.cs:246`); la edición no (`:258-263`). La vista solo oculta el campo. `pa_opo_negocios_estados_modificar` tampoco valida y no hay CHECK. **Es la causa raíz demostrada de D-27** | `[leído del código]` + `[verificado en BD]` | **Se corrige.** La invariante se valida en el servicio, en alta y en edición |
| D-06 | **Alto** | `EliminarFlujoNegocio` no valida que el estado sea terminal; la vista solo oculta el enlace. `pa_opo_negocios_estados_eliminar` es un `DELETE` físico sin guarda. Los terminales están protegidos solo mientras haya negocios apuntándolos | `[leído del código]` + `[verificado en BD]` | **Se corrige.** Los terminales no se pueden borrar |
| D-07 | **Alto** | Resolución de terminales o del primer estado intermedio sin guarda de nulo en 8 sitios: `API/…/NegociosController.cs:371→373`, `:750→751`, `:954→956`; `ServicioPersonas.cs:1280`; `ServicioCotizacionesQ10.cs:383`; `PersonalizacionesController.cs:4172`; `PreinscripcionesController.cs:1630` y `:2200`. Contraejemplos correctos: `SuscripcionQ10Callback.cs:682-688` y `OportunidadesNewController.cs:1473-1474`. Agrava que `ExecuteQuery` devuelva `null` ante excepción no reintentable | `[leído del código]` | **Se corrige.** La resolución pasa a ser una operación única del dominio que falla explícitamente |
| D-22 | **Alto** | `pa_opo_negocios_estados_modificar` declara sus 4 parámetros de datos con default `NULL` y hace `SET` directo sin `COALESCE`. Omitir un parámetro borra el campo | `[verificado en BD]` | **Se corrige** en el servicio. El SP legado queda igual mientras haya convivencia |
| D-29 | Media | El conflicto de borrado por FK no se clasifica. El error 547 llega al usuario como texto crudo de SQL Server, sin distinguir «el estado está en uso» de cualquier otro fallo. En `udbzq10trabajos` un solo estado tiene 299.649 negocios dependientes, así que el camino es frecuente, no hipotético | `[verificado en BD]` + `[leído del código]` | **Se corrige.** Conflicto de dominio explícito, respondido como 409 |
| D-23 | Media | `negest_nombre varchar(200) NULL` y `negest_estado bit NULL` en BD, contra `[Required]` y `bool` no nullable en la app. Sin validación de longitud del nombre ni del color | `[verificado en BD]` + `[leído del código]` | **Se replica** la nulabilidad en el modelo de lectura, porque 37 objetos leen esas columnas y la tabla no cambia. **Se corrige** la validación de entrada |
| D-11 | Media | Sin unicidad de porcentaje ni de nombre. Cero CHECK, cero UNIQUE, cero triggers. Evidencia real: `Perdido` ×2, `Presentación` ×2, `Ganado` ×2 | `[verificado en BD]` | **Se corrige** para el porcentaje, que sostiene la semántica. Para el nombre, **se replica**: agregar unicidad sería mejora de producto, no corrección. Ver GAP-2 |
| D-24 | Media | `@aplent_codigoP` se recibe en los 6 SPs y ninguno lo usa | `[verificado en BD]` | **Se replica** mientras el servicio invoque los SPs legados. **Se corrige** al retirarlos |
| D-09 | Media | Dos convenciones para resolver «Ganado»: el resto compara `porcentaje == 100`; `PagoSuscripcionCallback.cs:149-153` usa `OrderByDescending(porcentaje).First()`, que devuelve el porcentaje más alto exista o no el 100% | `[leído del código]` | **Se corrige.** Una sola forma de resolver el terminal |
| D-12 | Media | `ObtenerFlujosNegocios` no cachea pese a 20 sitios de lectura sobre una tabla que cambia casi nunca. El método vecino `ObtenerDetalleNegocio` sí cachea en Redis a 5 minutos (`:1190-1202`) | `[leído del código]` | **Se corrige.** El catálogo es candidato natural a caché |
| D-25 | Media | Asimetría entre frentes: el SP de APIs no soporta filtro de texto, y el endpoint exige `Estado` aunque el SP acepta nulo | `[verificado en BD]` + `[leído del código]` | **Se corrige.** Filtros homogéneos |
| D-14 | Media | Crear con porcentaje 0 o 100 devuelve HTTP 200 con `tipoMensaje = "info"` y no crea nada (`:252-256`) | `[leído del código]` | **Se corrige.** Una violación de invariante responde con error |
| D-30 | Media | El contrato público resuelve `Codigo_color` siempre a `CCCCCC` cuando la columna es nula, mientras la UI interna muestra y persiste el valor crudo, incluido vacío. Mismo dato, dos representaciones. **No es un caso borde**: 18 de 20 bases muestreadas tienen al menos un estado sin color, y en la mayoría son casi todos | `[leído del código]` + `[verificado en BD]` | **Se replica** en el frente que se conserve. Depende de GAP-1 |
| D-15 | Baja | `EditarFlujoNegocio(int id)` (`:280`) y `EliminarFlujoNegocio(int id)` (`:289`) no verifican nulo antes de pasar a la vista; `_Eliminar.cshtml:12` lo desreferencia | `[leído del código]` | **Se corrige.** Identificador inexistente responde 404 |
| D-16 | Baja | Los 3 SPs de escritura no reciben ni registran auditoría, a diferencia de los de negocios, que sí reciben `aud_usuario_sesion`, `aud_ip`, `aud_cat_codigo`, `aud_usuario_q10` y `aud_interno` (`:1231-1235`) | `[verificado en BD]` + `[leído del código]` | **Diferido.** Se decide junto con la política de auditoría del servicio |
| D-26 | Baja | `pa_opo_negocios_estados_ingresar` devuelve el consecutivo por `RETURN SCOPE_IDENTITY()`; `ExecuteNonQuery` lo propaga (`DataAccess.cs:546, 554`) pero `IngresarFlujoNegocio` es `void` y lo descarta | `[verificado en BD]` + `[leído del código]` | **Se corrige.** El alta devuelve el identificador |
| D-17 | Baja | Casing inconsistente: `aplent_codigop` en lecturas (`:1631`, `:1643`), `aplent_codigoP` en escrituras (`:1653`, `:1666`, `:1680`) y en el asíncrono (`:2769`) | `[leído del código]` | **Riesgo aceptado.** Los SPs ignoran el parámetro |
| D-19 | Baja | La ruta del POST es `FlujoNegocio/ActualizarOportunidad`, copiada del bloque vecino (`:45`) | `[leído del código]` | **Riesgo aceptado** en el legado; el servicio define sus rutas |
| D-20 | Baja | `negest_color` sin validación de formato. El default `CCCCCC` está duplicado en `FlujoNegocioViewModel.cs:29-32`, `EstructuracionComercialController.cs:231` y `OportunidadNewViewModel.cs:224-225` | `[leído del código]` | **Se replica** el formato sin `#`, porque los consumidores lo anteponen. **Se corrige** la validación y la duplicación |

D-10 de la revisión preliminar queda subsumido en D-21.

### Defecto detectado que pertenece a otro contexto

`IngresarHistorialNegocioEstados` recibe el consecutivo del **estado** en la posición del parámetro que espera el del **negocio**, en `GestionAcademica/Controllers/InscripcionesController.cs:206` y `:352` y en `Comunidad/Controllers/PersonasController.cs:586`. La firma está en `ServicioGestionComercial.cs:1361`; el sitio correcto de comparación es `GestionComercial/Controllers/NegociosController.cs:481`. Severidad crítica, corrupción de datos. **Pertenece al Discovery de Negocios.** Se deja constancia para que tenga dueño.

---

## §8 Rendimiento

**NO APLICA.** No se entregó dataset de telemetría, por lo que no hay ventana de observación, ni ranking de endpoints, ni tiempos medidos. Sin datos no se puede afirmar qué es lento. GAP-11.

Hallazgos cualitativos, con evidencia estructural y no de medición:

| Hallazgo | Evidencia | Confianza |
|---|---|---|
| El catálogo es diminuto —6 a 12 filas— y casi inmutable: 3 sitios de escritura en todo el monolito | `[verificado en BD]` + `[leído del código]` | Alta |
| Se lee sin caché desde 20 sitios, varios en listados y detalle, uno dentro de un job de validación masiva (`ValidarInformacionJob.cs:126`) y otro en la generación de plantillas de importación (`OportunidadesNewController.cs:1625`) | `[leído del código]` | Alta |
| El módulo vecino sí cachea: `ObtenerDetalleNegocio` usa Redis a 5 minutos. La ausencia de caché aquí es omisión, no decisión documentada | `[leído del código]` | Alta |
| La UI interna pagina **en memoria** sobre el resultado completo del SP, mientras la API pagina en el SP. Con 12 filas el impacto es nulo, pero es una divergencia de patrón entre frentes | `[leído del código]` | Alta |
| La consulta de lista usa `LIKE '%' + texto + '%'`, que impide uso de índice. Irrelevante al volumen actual | `[verificado en BD]` | Media |
| La tabla no tiene más índice que la PK, y toda consulta ordena por `negest_porcentaje` | `[verificado en BD]` | Alta |

Ninguno justifica trabajo de optimización por sí solo. Se registran para contrastarlos contra medición real cuando aparezca el dataset, en vez de rehacer el análisis.

---

## §9 Alcance y fuera de alcance

### Dentro del alcance

| Elemento | Detalle |
|---|---|
| Tabla | `dbo.tbl_opo_negocios_estados` |
| Stored procedures | Los seis, incluido `pa_apis_opo_negocios_estados_retornar` |
| Servicio | Región `#region Flujo de negocio` y `ObtenerFlujosNegociosAsync` |
| Superficie web | Las 7 acciones de la región y sus 4 vistas |
| Superficie de API | `GET api/flujonegocios`, su modelo y su perfil de mapeo |
| Reglas de negocio | Semántica de 0 y 100 como terminales, orden por porcentaje, filtro de etapas intermedias, invariante de unicidad de terminales |
| Conflicto de borrado | Clasificación del estado en uso |

### Fuera de alcance de esta iteración

Pendiente de decisión, no descartado:

| Elemento | Depende de |
|---|---|
| Si el nuevo servicio expone o no un equivalente del endpoint anónimo `api/flujonegocios` | GAP-1 |
| Remediación de los tenants con la invariante rota | GAP-5 |
| Unicidad de `negest_nombre` | GAP-2 |

### Fuera de alcance de forma permanente

| Elemento | Razón |
|---|---|
| Historial de transición: `tbl_opo_historial_negocio_estados`, los SPs `pa_opo_historial_negocio_estados_*`, `ModificarFechaGanado` y las columnas `his_*` | Registra la trayectoria de un negocio, no la definición del catálogo. Pertenece al contexto Negocios |
| El defecto de corrupción del historial descrito al final de §7 | Consecuencia de lo anterior. Se traslada, no se descarta |
| Asignación del estado a un negocio: `ModificarNegocioEstado`, `pa_opo_negocios_estado_modificar`, la vista `_Estados.cshtml` | Comportamiento de Negocios. Su acoplamiento se documenta en §5 porque condiciona el orden de corte |
| Causas de pérdida, tipos de oportunidad, medios publicitarios, medios de contacto, cargos, colas y estados de oportunidad | Catálogos hermanos del mismo controlador, con su propio ciclo |
| Los 11 `pa_generado_dinamicamente_*` | Se generan en runtime y varían por institución. Nunca serán código del servicio; hay que convivir con ellos |
| Informes `pa_inf_opo_*` y backup `pa_back_backup_new_oportunidades_retornar` | Leen la tabla directamente y seguirán haciéndolo. La tabla permanece |
| SPs de estructura académica `pa_par_sedes_jornadas_*` | Otro dominio; tocan la tabla de forma incidental |
| El mecanismo de claves de API del monolito | Infraestructura compartida por 119 controladores. Corrección transversal, no de este contexto |

---

## §10 Decisiones pendientes y GAPs

### Cerrados durante la investigación

| GAP | Resolución |
|---|---|
| Esquema real de la tabla | `dbo.tbl_opo_negocios_estados`, 5 columnas, PK clustered identity, sin índices, CHECK, UNIQUE, triggers ni defaults |
| Cuerpos de los stored procedures | Los seis extraídos con `OBJECT_DEFINITION` y analizados |
| Tipo real de `negest_porcentaje` | `decimal(20,5)` nullable. Originó D-21 |
| Comportamiento del borrado | `DELETE` físico sin validación. Agravó D-06; el conflicto de FK originó D-29 |
| Existencia de columna de institución | No existe, y los SPs ignoran `@aplent_codigoP`. Cierra la duda sobre un segundo nivel de multi-tenancy: no lo hay. Originó D-24 |
| Contenido de la cadena de autorización web | Filtro global `AutorizacionAttribute` → `SecurityHelpers.IsAuthorizedCore` → `SessionManager.PermisosUsuario` y `PermisosRevocadosUsuario`. Queda pendiente solo el contenido del catálogo de permisos: GAP-7 |
| **Diagnóstico de la invariante de centinelas** | **Rota en datos reales.** `udbzq10trabajos` tiene 2 estados al 0% y 2 al 100%, uno de ellos inactivo. Originó D-27 y D-28. Muestreo sobre 20 bases: 19 sanas, 1 rota, y la rota concentra el 99,7% de los negocios. La remediación y la cobertura completa siguen abiertas abajo |

### Abiertos

⚠️ **GAP-5 (BLOQUEANTE)**: Dos cosas pendientes. Primero, **cobertura**: el muestreo alcanzó 20 de 1.225 bases porque el login usado no puede abrir las otras 1.175; falta correr la misma verificación con credenciales que cubran el parque completo. Segundo, **remediación**: con qué criterio se elige el estado superviviente donde haya dos terminales del mismo porcentaje · **Afecta**: migración de datos, fase 1 del Plan · **Confirmar con**: DBA y Product Owner de Gestión Comercial
**Recomendación por defecto**: repetir la verificación con un login de solo lectura sobre todo el parque antes del cutover, y tratar los casos rotos como datos a corregir, no como casos que el código nuevo deba tolerar. La muestra sugiere que el volumen de remediación es bajo en cantidad de tenants y alto en importancia: los catálogos semilla sin editar están sanos y los editados son los que derivan. El criterio de supervivencia propuesto es conservar el estado **activo** con más negocios referenciándolo y reasignar los del descartado, porque preserva el histórico sin dejar negocios apuntando a un estado inactivo. Nada de esto es automatizable sin confirmación: reasignar el estado de un negocio cambia información comercial.

⚠️ **GAP-16 (BLOQUEANTE)**: Contenido y mecanismo de generación de los 11 procedimientos `pa_generado_dinamicamente_*` que leen la tabla · **Afecta**: viabilidad de mover el catálogo sin romper Mailing y SMS · **Confirmar con**: DBA y responsable de Mailing
**Recomendación por defecto**: volcar los 11 cuerpos y localizar el generador antes de escribir el Plan. Un catálogo consumido por SQL construido en tiempo de ejecución no se puede reubicar sin conocer al generador. La extracción es mecánica y puede hacerse en la misma sesión de base de datos.

⚠️ **GAP-1 (BLOQUEANTE)**: Decidir si el nuevo contexto expone desde el día uno un equivalente del endpoint público `api/flujonegocios` —solo lectura, anónimo, filtro `Estado` obligatorio— o si ese frente se sigue sirviendo desde Jack hasta una fase posterior · **Afecta**: contrato HTTP del contexto y su política de autenticación · **Confirmar con**: Product Owner de GestionComercial y responsable de seguridad de APIs públicas
**Recomendación por defecto**: no exponer el equivalente anónimo en esta iteración. El servicio expone su lectura autenticada como el resto de la plantilla, y `api/flujonegocios` sigue apuntando a Jack sobre la misma tabla hasta que se decida migrar ese frente. Convertir el endpoint anónimo en autenticado es un cambio de contrato para consumidores externos desconocidos, y esa decisión no es técnica.

⚠️ **GAP-6 (BLOQUEANTE)**: Nivel de protección real de los endpoints anónimos de D-02 y D-03. El análisis estático indica que son alcanzables sin autenticación, pero puede existir un WAF, una regla de red o una restricción de plataforma fuera del repositorio · **Afecta**: severidad de D-02 y D-03 · **Confirmar con**: Seguridad e Infraestructura
**Recomendación por defecto**: tratarlos como efectivamente públicos y verificarlo empíricamente en un entorno no productivo: `GET api/flujonegocios` con header `aplentId` y sin `X-Api-Key`, y `POST Negocio/Actualizar/Estados` con header `X-Requested-With` y sin sesión. Diez minutos de prueba que cierran el GAP.

⚠️ **GAP-17 (BLOQUEANTE)**: Asignación formal de los veredictos de §7. Los consignados son propuestas del análisis, no decisiones del equipo · **Afecta**: alcance de la construcción · **Confirmar con**: Tech Lead y Product Owner
**Recomendación por defecto**: revisar la columna «Veredicto propuesto» y confirmarla en bloque. Los más probables de cambiar son D-16 (propuesto como diferido) y D-17 y D-19 (propuestos como riesgo aceptado).

⚠️ **GAP-18 (BLOQUEANTE)**: Reconciliar el commit de origen. El documento paralelo cita `cf18c7fc267861eecdfea714390586dac40c3a7f` como HEAD y ese commit no existe en este clon · **Afecta**: auditabilidad de ambos documentos · **Confirmar con**: quien produjo el documento paralelo
**Recomendación por defecto**: adoptar `db555c53…` como origen único y verificable, y descartar la referencia no resoluble. Un discovery cuyo commit no resuelve no es auditable.

⚠️ **GAP-2 (ABIERTO)**: Confirmar si `negest_nombre` debe tener unicidad de negocio, dado que ya hay nombres duplicados en tenants reales · **Afecta**: si el Plan agrega una validación que el legado nunca tuvo · **Confirmar con**: dueño funcional del pipeline comercial
**Recomendación por defecto**: no agregarla. Mantener paridad con el legado. Agregar unicidad de nombre sería mejora de producto, no corrección de defecto, y rompería el alta en tenants que hoy tienen duplicados. La unicidad que sí importa es la del **porcentaje terminal**, tratada en D-27.

⚠️ **GAP-7 (ABIERTO)**: Catálogo de permisos aplicable a `EstructuracionComercial` y a la acción `FlujoNegocio` · **Afecta**: mapeo de autorización · **Confirmar con**: DBA y Seguridad
**Recomendación por defecto**: extraer las filas de permisos cuyo controlador sea `EstructuracionComercial` y mapearlas una a una. Es consultable en las bases ya disponibles. El único permiso del dominio visible en código es `PermitirGanarNegocio` sobre el controlador `Negocios` (`_Estados.cshtml:17`), y pertenece a Negocios.

⚠️ **GAP-9 (ABIERTO)**: Personalizaciones por cliente sobre el catálogo. En código solo aparecen las dos de §6.4 · **Afecta**: alcance y sorpresas por institución · **Confirmar con**: Product Owner y Soporte
**Recomendación por defecto**: asumir que son las únicas y validarlo con Soporte. La búsqueda estática no descarta procedimientos personalizados en bases de producción.

⚠️ **GAP-10 (ABIERTO)**: Adopción real del parámetro 381 · **Afecta**: dimensionamiento y estrategia de despliegue · **Confirmar con**: Product Owner y DBA
**Recomendación por defecto**: contar instituciones con el parámetro activo. Si la adopción es parcial, el servicio debe convivir con el modelo anterior durante la transición.

⚠️ **GAP-11 (ABIERTO)**: Ausencia de telemetría, que deja §8 sin medición · **Afecta**: priorización de caché y validación de los hallazgos cualitativos · **Confirmar con**: Infraestructura y SRE
**Recomendación por defecto**: pedir 30 días de Application Insights filtrando `EstructuracionComercial/FlujoNegocio*`, `api/flujonegocios` y `Negocio/*/Estados`. Si no existe, documentar D-12 como riesgo cualitativo y medirlo tras el cutover. La paginación server-side del nuevo servicio se mantiene igual: es el patrón de la plantilla y su costo adicional es nulo.

⚠️ **GAP-12 (ABIERTO)**: Ausencia de servicio hermano ya migrado como referencia · **Afecta**: consistencia del servicio nuevo · **Confirmar con**: Tech Lead
**Recomendación por defecto**: usar `docs/plantilla/` de este repositorio como fuente de convenciones y `Announcements-service` como ejemplo de `docs/servicio/` completo, según indica `docs/servicio/README.md`.

⚠️ **GAP-13 (ABIERTO)**: Consumidores externos a la base: Power BI, ETL y el replicador `zudbzq10replicador` (`BaseServicio.cs:21`). El frente interno ya está cuantificado en 37 objetos · **Confirmar con**: DBA y equipo de Datos
**Recomendación por defecto**: asumir que existen y planificar convivencia: la tabla se mantiene y el servicio nuevo pasa a ser la única vía de escritura.

---

## §11 Changelog

| Fecha | Cambio | Origen |
|---|---|---|
| 2026-08-14 | Creación del documento a partir del análisis de la región «Flujo de negocio» del legado y la verificación de esquema y SPs en el tenant `udbzq10dbdesarrolloordenespago` | Sesión de discovery del contexto `FlujosNegocio` |
| 2026-08-14 | Revisión 2. Contraste con `discovery-business-state.md`; verificación cruzada en el tenant `udbzq10trabajos`; muestreo de la invariante sobre 20 bases de tenant; cierre del diagnóstico de la invariante de centinelas; alta de D-27, D-28, D-29, D-30 y D-31; alta de GAP-1, GAP-2 y GAP-18; incorporación de la paginación en memoria y del contraste de `[AllowAnonymous]` entre regiones vecinas | Contraste de documentos paralelos |

---

## §12 Contraste con el documento paralelo

`discovery-business-state.md` cubre el mismo dominio bajo el nombre `BusinessState`. Este anexo registra en qué coinciden, en qué difieren y cómo se resolvió cada diferencia, para que ninguna de las dos investigaciones se pierda.

### Coincidencias

Esquema de la tabla y sus cinco columnas; `decimal(20,5)` tratado como entero por la aplicación; nulabilidad de columnas contra `[Required]`; color nulo enmascarado con `CCCCCC` en runtime; convención de 0 y 100 como terminales; la edición no replica la validación del alta; ausencia de unicidad de nombre; FK `NO_ACTION` desde `tbl_opo_negocios`; API anónima de solo lectura con `Estado` obligatorio; dos SPs distintos por frente; §8 sin telemetría.

### Divergencias y resolución

| Tema | Este documento | `discovery-business-state.md` | Resolución |
|---|---|---|---|
| Tenant inspeccionado | `udbzq10dbdesarrolloordenespago`, 6 estados, 60 negocios | `udbzq10trabajos`, 12 estados, 299.937 negocios | **Ambos correctos, tenants distintos.** El suyo es más representativo. Se verificó directamente y se incorporó en §4.4 |
| Invariante de centinelas | «Se cumple, pero nada la fuerza» | «Hay 2 filas en 0% y 2 en 100%» | **El documento paralelo tiene razón; esta investigación la subestimó** por trabajar sobre un tenant de desarrollo. Verificado y elevado a D-27 |
| Multi-tenancy | Solo por base de datos; `@aplent_codigoP` es parámetro muerto | «Dos niveles: por BD y por columna», con GAP-4 abierto | **Resuelto a favor de este documento**, leyendo los cuerpos de los 6 SPs y el esquema: ninguno usa el parámetro y no hay columna de institución. Cierra su GAP-4 |
| Cuerpos de los SPs | Los 6 extraídos y analizados | «El SP en sí no fue inspeccionado, solo su firma» | Este documento es más profundo. De ahí salen D-22, D-26, D-31 y la precisión de D-29 |
| Mapa de consumidores | 25 lecturas, 3 escrituras, 49 archivos, 377 referencias, 40 archivos fuera del módulo | «2 dentro del módulo, 2 fuera» | **Subconteo por un factor cercano a 6** en el documento paralelo. Prevalece §5 de este documento |
| Objetos de BD que leen la tabla | 37, de los cuales 31 son ajenos y 11 generados dinámicamente | No detectado | Solo en este documento. Es el frente que más cambia el dimensionamiento |
| Parámetros de institución | Parámetro 381 `NUEVO_MODELO_OPORTUNIDADES`, 22 archivos | «No se encontró ningún parámetro» | Prevalece este documento, con la precisión de §6.1: el catálogo no tiene parámetros propios, pero vive detrás del interruptor 381 |
| Personalizaciones | Dos detectadas: ISER y Q10 | «Ninguna» | Prevalece este documento |
| Cadena de autorización web | `AutorizacionAttribute` → `SecurityHelpers.IsAuthorizedCore` → `SessionManager.PermisosUsuario` | «No se confirmó el contenido de `BaseController`», GAP-4 | Prevalece este documento; cierra su GAP-4 salvo el contenido del catálogo de permisos, que sigue como GAP-7 |
| `[AllowAnonymous]` | Detectado en `NegociosController` a nivel de clase, que lee el catálogo y escribe el estado | Solo menciona el de la API; señala que la región de FlujoNegocio no lo tiene | **Complementarios.** Se adoptó su precisión sobre las regiones vecinas en §3.4, y se conserva D-02 |
| Paginación de la UI | No lo había señalado | En memoria, vía `AplicarPaginacion` | **Su aporte, adoptado.** Verificado en `BaseController.cs:562-569`, tamaño 12. Incorporado en §4.6 y §8 |
| Mecanismo del error de FK | — | «Falla con SQL 547 y ese error se propaga tal cual» | **Correcto en el efecto, impreciso en el mecanismo.** El SP captura el error en parámetros de salida y `DataAccess` lo relanza como `DatabaseException`. Refinado e incorporado como D-29 |
| Conflicto de borrado como decisión | — | Lo clasifica como conflicto de dominio a responder con 409 | **Su aporte, adoptado** en el veredicto de D-29 |
| Claves de API en el código | Detectado, 14 claves y una credencial `Basic` | No detectado | Solo en este documento |
| Contrato divergente del color | — | Lo formula como divergencia entre frentes | **Su aporte, adoptado** como D-30 |
| Unicidad de nombre como decisión de producto | La trataba como defecto a corregir | La separa como GAP con recomendación de no agregarla | **Su encuadre es mejor.** Adoptado: D-11 se divide entre porcentaje, que se corrige, y nombre, que se replica. Ver GAP-2 |
| Exposición del endpoint anónimo | Solo como defecto de seguridad | Como decisión de producto previa al diseño | **Su encuadre es mejor.** Adoptado como GAP-1 bloqueante |
| Commit de origen | `db555c53…`, verificable | `af94d015…` válido; `cf18c7fc…` no existe en este clon | Ver GAP-18 |
| Cita del valor inicial del formulario | `negest_estado = true` en `EstructuracionComercialController.cs:231` | «`opotip_estado = true`» | Identificador equivocado en el documento paralelo: `opotip_` pertenece a Tipos de Oportunidad. Corregido en §4.1 |
| Estructura del documento | Front-matter YAML válido; GAPs en el formato acordado | Front-matter desarmado por un `---` prematuro; GAPs envueltos en bloques de código | Se conserva la estructura de este documento |
| Criterio de cierre | No lo tenía | Sección final con checklist de congelamiento | **Su aporte, adoptado** abajo |

### Hallazgo nuevo, producto del contraste

Ninguno de los dos documentos había detectado **D-28**. Surgió al cruzar el dato del tenant del documento paralelo —dos estados al 100%, uno inactivo— con el código que este documento había mapeado: dos endpoints de API resuelven el estado ganado sin filtrar por actividad, sobre un orden sin desempate. Es el defecto más grave del dominio y solo aparece con las dos mitades juntas.

El contraste también motivó el muestreo de §4.4, que ninguno de los dos había hecho. Ese muestreo cambia el encuadre de la remediación: el documento paralelo presentaba la invariante rota como un hecho aislado de su tenant, y esta investigación la había presentado como cumplida en el suyo. Ambas descripciones eran correctas y ambas eran incompletas. Con 20 bases medidas queda claro que la rotura correlaciona con el uso, no con el azar.

---

## Criterio de cierre

El Discovery pasa a `frozen` cuando:

| Condición | Estado |
|---|---|
| Las once secciones escritas o justificadas con `NO APLICA` y razón | **Cumplido.** §8 con razón documentada |
| Cada afirmación cita su fuente y declara `[verificado en BD]` o `[leído del código]` | **Cumplido** |
| Cada defecto de §7 tiene veredicto | **Parcial.** Los 25 defectos tienen veredicto **propuesto**; falta asignación formal. Ver GAP-17 |
| Cada GAP bloqueante tiene dueño y ticket | **Parcial.** Los seis bloqueantes tienen dueño propuesto; **faltan los tickets** |
| El commit de origen resuelve | **Parcial.** Ver GAP-18 |
| El tech lead firmó | **Pendiente.** El documento está en `status: draft` |

**No se empieza el Plan con GAPs bloqueantes abiertos.** Hay seis: GAP-5, GAP-16, GAP-1, GAP-6, GAP-17 y GAP-18. Los tres que exigen respuesta de negocio o de datos, y no de análisis, son GAP-5, GAP-16 y GAP-1.
