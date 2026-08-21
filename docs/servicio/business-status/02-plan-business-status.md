---
service: crm
context: Business status
doc: plan
status: draft
source: ./01-discovery-flujos-negocio.md
updated: 2026-08-21
---

# Plan de construcción — contexto `BusinessStatus`

Fuente única del legado: [`docs/servicio/business-status/01-discovery-flujos-negocio.md`](./01-discovery-flujos-negocio.md) (Discovery rev. 2, commit de origen `db555c53…`). Este documento **no re-transcribe** el esquema: lo mapea.

---

## §0 Cómo ejecutar este plan

Dirigido al agente ejecutor.

1. **Antes de ejecutar nada, verifica el plan.** Recorre todos los pasos de §8 y confirma que cada uno tiene `id`, un `depende_de` que existe, `estado`, `Fuente:`, `Hecho cuando:` y `Verificar:`. Confirma que ninguna decisión de §2 que afecte tu fase está en `estado: propuesta`, y que no quedan GAPs `BLOQUEANTE` abiertos en §9.2. Si algo falta, **detente y repórtalo**: no ejecutes un plan incompleto ni completes tú lo que falte.
2. **Ejecuta los pasos en orden de `id`, respetando `depende_de`.** No inicies pasos con `estado: blocked`.
3. **Al terminar un paso, corre su comando de `Verificar`**, y solo entonces cambia `estado: pending` → `done` en este mismo archivo.
4. **Si la realidad del repositorio contradice el plan** (el archivo ya existe, la interfaz tiene otra firma, la tabla tiene otras columnas): detente, no improvises. Reporta con el formato `⚠️ GAP` y espera instrucción.
5. **No agregues alcance.** Si detectas una mejora, anótala como riesgo en §9.1; no la implementes.

Reglas operativas de la rama y del PR: la rama es de la **tarea**, no del paso; base `main` (servicio nuevo). Un PR no mezcla dominio, infraestructura y API. Tope por PR: **≤ 400 líneas de diff de producción o ≤ 10 archivos de `src/`**. Los archivos de `tests/` no cuentan para ese tope.

---

## §1 Contexto y alcance

### 1.1 Qué se construye

El contexto `BusinessStatus` administra el **catálogo de etapas del embudo comercial** de cada institución: la lista ordenada de estados por los que pasa un negocio hasta su cierre. Cada estado tiene nombre, porcentaje de avance, color y bandera de actividad. El porcentaje es el identificador semántico: 0 = «Perdido», 100 = «Ganado» ([Discovery §1](./01-discovery-flujos-negocio.md)).

Se construye sobre la tabla legada `dbo.tbl_opo_negocios_estados`, sin DDL y sin migración de datos (D1), con el stack de la plantilla: Clean Architecture + DDD + hexagonal, EF Core Database First, `Result<T>`, multi-tenancy por base de datos.

| Entregable | Detalle |
|---|---|
| Dominio | `BusinessStatusAggregate` (constantes de rango y validación de porcentaje propias, sin VO para el porcentaje), VO `StatusColor`, errores del contexto, 4 invariantes |
| Persistencia | Entidad de fila, configuración EF, mapper, repositorio con lecturas filtradas y paginadas y escrituras |
| Aplicación | 5 casos de uso (listar, detalle, crear, editar, eliminar) + resolución única de terminales |
| API | `business-statuses`: ABM completo, paginación server-side, caché L1 con invalidación por tag |
| Caché | L2 cache-aside particionado por tenant sobre el listado |
| Pruebas | Unitarias de dominio, VOs y casos de uso; integración con Testcontainers sobre la tabla real |

### 1.2 Fuera de alcance

| Elemento | Razón |
|---|---|
| Endpoint anónimo `api/flujonegocios` y su contrato (`Consecutivo_estado_negocio`, `Codigo_color`, `CCCCCC`) | GAP-1 resuelto: sigue sirviéndose desde Jack sobre la misma tabla (D8, D14) |
| Informes `pa_inf_opo_*`, backup `pa_back_*` y los 11 `pa_generado_dinamicamente_*` | Ajuste de alcance del desarrollador. Siguen leyendo la tabla, que permanece (D1) |
| Historial de transición `tbl_opo_historial_negocio_estados` y sus SPs | Pertenece al contexto Negocios (Discovery §9) |
| Asignación del estado a un negocio (`ModificarNegocioEstado`) y los defectos D-02, D-07 en sitios de Negocios | Contexto Negocios |
| Catálogos hermanos (causas, tipos de oportunidad, cargos, colas, estados de oportunidad) | Ciclo propio |
| Remediación de tenants con la invariante rota | GAP-5 marcado `NO APLICA` por el desarrollador. El servicio **no corrompe más** y **no repara** lo existente (ver R-1) |
| Corte de la escritura del monolito hacia el servicio (flag, despliegue de Jack) | GAP-A marcado `NO APLICA`. Este plan entrega el servicio; el corte se planifica aparte (D13) |
| Autenticación y autorización | GAP-D marcado `NO APLICA`: servicio interno no expuesto a internet (D15) |
| Migraciones de EF Core | D1: no se generan ni se aplican migraciones en ningún paso |

### 1.3 Ajustes de alcance ya cerrados

| Ajuste | Origen |
|---|---|
| No tocar informes/reportes ni SPs generados dinámicamente | desarrollador, 2026-08-14 |
| No exponer equivalente del endpoint anónimo | GAP-1, recomendación aceptada |
| Sin authn/authz en el servicio | GAP-D, `NO APLICA` |
| Sin remediación de datos ni verificación de cobertura del parque | GAP-5 y GAP-16, `NO APLICA` |
| Sin unicidad de `negest_nombre` | GAP-2, paridad con el legado |

---

## §2 Decisiones cerradas (ADR)

Las 16 decisiones fueron firmadas por el desarrollador el 2026-08-14. **Ninguna queda en `propuesta`, por lo que ninguna fase de §8 arranca bloqueada.**

### D1 — Reutilizar la tabla legada sin DDL ni migraciones
`estado: aprobada · firmó: desarrollador · fecha: 2026-08-14 · origen: Discovery §5.6`
- **Decisión:** mapear `dbo.tbl_opo_negocios_estados` tal como existe, con EF Core Database First. No se generan migraciones, no se crean índices, CHECK ni UNIQUE.
- **Alternativas descartadas:** tabla propia del servicio + sincronización, porque 37 objetos de base de datos leen la tabla y 31 son ajenos al módulo · vista de compatibilidad, porque no resuelve la escritura · agregar constraints, porque exige DDL sobre ~1.225 bases de tenant.
- **Consecuencias:** la base **no** protege ninguna invariante; toda regla vive en el dominio (D3). Durante la convivencia hay dos escritores sobre la misma tabla (Jack y el servicio) — ver R-2.
- **Afecta:** §4 · §5 · §8 fases 2 y 6 · pasos F2.1–F2.6.

### D2 — Nombre técnico del contexto: `BusinessStatus`
`estado: aprobada · firmó: desarrollador · fecha: 2026-08-14 · origen: equipo`
- **Decisión:** contexto `BusinessStatus`, agregado `BusinessStatusAggregate`, recurso HTTP `business-statuses`, proyectos `BusinessStatus.Domain` y `BusinessStatus.Application`.
- **Alternativas descartadas:** `BusinessStage` / `BusinessFlow`, más cercanos a «etapa del flujo», pero desalineados del front-matter `context: Business status` y del documento paralelo.
- **Consecuencias:** el término de negocio «flujo de negocio» (el catálogo) no tiene tipo propio: el catálogo es el conjunto de agregados, no una entidad.
- **Afecta:** §3 · §5 · §6 · todos los pasos.

### D3 — El agregado es el estado individual; la invariante se sostiene con guardas, no con lectura del catálogo
`estado: aprobada · firmó: desarrollador · fecha: 2026-08-14 · origen: Discovery §7 D-27, D-05, D-06`
- **Decisión:** `BusinessStatusAggregate` modela una fila. La invariante de terminales se preserva con tres guardas que **no requieren leer el resto del catálogo**: no se puede crear con porcentaje 0 ni 100, no se puede llevar un estado existente a 0 ni 100, y un terminal no se puede borrar.
- **Alternativas descartadas:** agregado = catálogo completo, que daría consistencia transaccional real pero rompe `IRootRepository`, la paginación y el ABM de la plantilla · validar contando terminales antes de escribir, que abre una condición de carrera y no aporta nada dado que ningún camino puede crear un terminal.
- **Consecuencias:** el servicio **no puede crear estados terminales**; en un tenant sin terminales, el catálogo semilla los provee. Un tenant que ya tiene la invariante rota sigue roto: el servicio no la repara (R-1). Sin lectura previa, no hay carrera que mitigar.
- **Afecta:** §5.4 invariantes · §6 · pasos F1.4, F5, F6, F7.

### D4 — Un solo Value Object propio: `StatusColor`. El porcentaje se valida en el agregado, sin VO
`estado: aprobada · firmó: desarrollador · fecha: 2026-08-14 · origen: Discovery §7 D-20, D-21 · revisión: desarrollador, 2026-08-14`
- **Decisión:** el color se modela como VO (`StatusColor`, constructor privado, factory `Create` → `Result<T, ValidationError>`). El porcentaje **no** se modela como VO: `BusinessStatusAggregate` declara las constantes `MinPercentage = 0m` y `MaxPercentage = 100m`, y un método propio (`ValidatePercentage`, privado) que valida rango, que sea entero y la reserva de los límites (INV-1), y genera el `ValidationError` correspondiente. La semántica de terminal (`IsWon`, `IsLost`, `IsIntermediate`, `IsTerminal`) se expone como propiedades calculadas del propio agregado, no de un VO.
- **Alternativas descartadas:** VO para ambos campos (diseño original), descartado por el desarrollador para no introducir un tipo cuyo único consumidor es el propio agregado · primitivos con validación repetida en cada caso de uso, que es exactamente la causa de los 7 filtros dispersos y del default `CCCCCC` triplicado en el legado (sigue descartada para el color).
- **Consecuencias:** una constante y un método menos que mantener como tipo aparte; la validación de porcentaje y la semántica de terminal quedan **dentro** de `BusinessStatusAggregate` (§5.1), no repartidas entre un VO y el agregado.
- **Afecta:** §5.1 · §5.2 · pasos F1.3, F1.4 (ya no existe F1.2, ver Fase 1).

### D5 — `int?` en el agregado; `decimal` solo en los bordes de entrada y persistencia
`estado: aprobada · firmó: desarrollador · fecha: 2026-08-14 · origen: Discovery §7 D-21, D-23 · revisión: desarrollador, 2026-08-14 (segunda revisión: `int`, no `decimal`, dentro del agregado)`
- **Decisión:** `BusinessStatusAggregate.Percentage` es `int?` — el significado de negocio siempre fue un entero 0-100, y el agregado no necesita conocer que la columna real es `decimal(20,5)`. El `decimal` solo aparece en dos bordes: (a) **entrada** — `ValidatePercentageForCreate`/`ValidatePercentageForUpdate` reciben `decimal` para poder emitir `PercentageMustBeInteger` como error de dominio propio en vez de que un valor como `50,5` lo rechace el *model binding* de ASP.NET con un 400 genérico sin `Property`; (b) **persistencia** — la entidad EF (`Entities.BusinessStatus`) sigue en `decimal?` porque es la columna real, y el **mapper** (F2.3) hace la conversión en ambos sentidos. Al ser `int` dentro del agregado, las comparaciones contra 0 y 100 son de **igualdad exacta** — ya no hace falta una constante de tolerancia. **Escritura:** solo enteros en `0 < x < 100`. **Lectura:** un `NULL`, o un valor persistido que no sea un entero exacto (dato sucio), se reconstruye como «sin porcentaje» (`Percentage` nulo) — nunca se redondea a 0 ni a 100, porque eso lo clasificaría como terminal por un artefacto de conversión, no por el dato real.
- **Alternativas descartadas:** `decimal?` con tolerancia dentro del agregado (primera revisión de este documento, 2026-08-14), descartada por el desarrollador porque la tolerancia solo existía para blindar una comparación de punto flotante que un `int` ya no tiene · truncar/redondear en el mapper en vez de anular, porque un `0,4` sucio pasaría a valer «Perdido» · exponer `decimal` en el contrato, porque el contrato ya consumido declara `int` · envolver el valor en un VO (D4), descartado por el desarrollador.
- **Consecuencias:** el modelo tolera datos que el servicio nunca produce; el listado nunca falla por una fila sucia, y el agregado nunca compara con tolerancia. El contrato de salida expone el porcentaje como número entero nullable, ahora sin ninguna conversión intermedia entre el agregado y el DTO de salida (antes se «proyectaba redondeado» desde un `decimal`; ahora ya es `int` desde el agregado — ver F3).
- **Afecta:** §4 · §5.1 · §6.3 · pasos F1.4, F2.3, F3.

### D6 — La resolución de terminales es una sola operación de dominio, sin endpoint
`estado: aprobada · firmó: desarrollador · fecha: 2026-08-14 · origen: Discovery §7 D-28, D-07, D-09`
- **Decisión:** existe un único punto que resuelve «cuál es el estado Ganado / Perdido»: `TerminalBusinessStatusProvider`, apoyado en `IBusinessStatusRepository.GetTerminalAsync`. Filtra por activo, ordena con desempate y **falla explícitamente ante ambigüedad** en vez de elegir al azar. No se expone como endpoint.
- **Alternativas descartadas:** exponer `GET /business-statuses/terminal`, porque hoy no tiene consumidor (Negocios no está migrado) y sería alcance nuevo · replicar `FirstOrDefault` sin filtro de actividad, que es el defecto crítico D-28.
- **Consecuencias:** el provider queda **sin consumidor dentro de este servicio** hasta que migre Negocios — decisión consciente del desarrollador, registrada como R-3. En `udbzq10trabajos` esa resolución fallaría con `AmbiguousTerminalStatus`, que es el comportamiento buscado.
- **Afecta:** §5.3 · §5.4 · pasos F2.5, F8.1.

### D7 — Orden primario por porcentaje, con desempate técnico por identificador
`estado: aprobada · firmó: desarrollador · fecha: 2026-08-14 · origen: desarrollador + Discovery §7 D-31`
- **Decisión:** se **conserva el criterio de ordenamiento actual** — ascendente por `negest_porcentaje` —, y se le agrega `negest_consecutivoP` como segundo criterio de desempate. El orden visible no cambia salvo entre filas con el mismo porcentaje, donde hoy es indefinido.
- **Alternativas descartadas:** paridad literal con un solo criterio, porque `OFFSET/FETCH` (D8) puede repetir o saltar filas entre páginas cuando el orden no es total — lo advierte `docs/plantilla/repositorio.md` · ordenar por nombre o por identificador, que cambiaría el orden percibido.
- **Consecuencias:** el desempate es una lectura de la instrucción «conservar el mismo ordenamiento» como *mismo criterio primario*, no como *misma cláusula literal*. Si el desarrollador quería paridad literal, esta decisión debe revertirse y D8 pierde su garantía de paginación estable. Registrado como R-4.
- **Afecta:** §5.3 · §6.2 · pasos F2.5, F9.2.

### D8 — Contrato nuevo en inglés con paginación server-side de la plantilla
`estado: aprobada · firmó: desarrollador · fecha: 2026-08-14 · origen: template + Discovery §7 D-25`
- **Decisión:** recurso `business-statuses` con ABM completo y listado paginado en la consulta (`PageQueryInputDto`: `pageIndex` ≥ 0, `pageSize` 1–100) devuelto como `HttpOkPagedResult`. Filtros homogéneos y todos opcionales: nombre, actividad y tipo de etapa.
- **Alternativas descartadas:** paginación en memoria tamaño 12 del legado (D-25, y `AplicarPaginacion` no existe en la plantilla) · replicar el contrato del frente anónimo, que no se migra (GAP-1) · exigir el filtro de actividad como hace hoy la API pública.
- **Consecuencias:** una sola convención de paginación en todo el servicio. Ningún consumidor externo se rompe porque ningún consumidor externo apunta todavía al servicio.
- **Afecta:** §6 · paso F3.

### D9 — Borrado físico con dos guardas y conflicto clasificado como 409
`estado: aprobada · firmó: desarrollador · fecha: 2026-08-14 · origen: Discovery §7 D-06, D-29`
- **Decisión:** se conserva el `DELETE` físico. Antes de borrar se verifica que el estado no sea terminal (`BusinessStatusAggregate.IsTerminal` → 409; D4 — ya no vive en un VO). Si la base rechaza el borrado por la FK de negocios, `SqlServerErrorClassifier` traduce el error 547 a `Conflict` → **409**, nunca texto crudo de SQL Server. El servicio **no lee** `tbl_opo_negocios`.
- **Alternativas descartadas:** contar negocios antes de borrar, porque exige acceso a una tabla ajena al contexto para un mensaje apenas mejor · borrado lógico, porque cambia una semántica que 37 objetos de base de datos ya asumen.
- **Consecuencias:** el 409 no puede nombrar la FK culpable (`repositorio.md` lo advierte). En esta tabla el 547 solo puede venir de las 3 FKs entrantes, así que el mensaje genérico «el estado está en uso» es correcto. `ExecuteDelete` no envuelve la excepción en `DbUpdateException`: el repositorio debe capturar también `SqlException`.
- **Afecta:** §6.4 · pasos F2.6, F7.

### D10 — El servicio no invoca los seis stored procedures legados
`estado: aprobada · firmó: desarrollador · fecha: 2026-08-14 · origen: template + Discovery §4.5`
- **Decisión:** todo el acceso a datos es EF Core contra la tabla. Los seis SPs siguen existiendo y sirviendo a Jack durante la convivencia.
- **Alternativas descartadas:** envolver los SPs desde el servicio, que arrastraría el `SET` sin `COALESCE` (D-22), el `@aplent_codigoP` muerto (D-24) y el identity devuelto por código de retorno (D-26), y contradice la plantilla.
- **Consecuencias:** las trampas de los SPs quedan fuera del servicio pero vivas en Jack. El alta usa `CreateAsync` para recuperar el `IDENTITY` (D-26 corregido).
- **Afecta:** §4.2 · §7.3 · pasos F2.5, F2.6.

### D11 — Multi-tenancy solo por base de datos, con el mecanismo de la plantilla
`estado: aprobada · firmó: desarrollador · fecha: 2026-08-14 · origen: Discovery §3.1, D-24`
- **Decisión:** el aislamiento lo resuelve la plantilla: header `X-Entity-Code`, `TenantMiddleware`, `TenantResolverServiceClient` y descifrado del connection string por petición. Ninguna consulta filtra por institución y `@aplent_codigoP` no se replica en ninguna forma.
- **Alternativas descartadas:** filtrar por institución dentro de la base, porque la columna no existe `[verificado en BD]` y los seis SPs ignoran el parámetro.
- **Consecuencias:** una petición sin tenant resuelto no alcanza la tabla; el error lo produce la plantilla, no este contexto.
- **Afecta:** §7.1 · §7.2 · paso F9.2.

### D12 — Caché en dos niveles sobre un catálogo casi inmutable
`estado: aprobada · firmó: desarrollador · fecha: 2026-08-14 · origen: Discovery §7 D-12 + template`
- **Decisión:** L1 `[OutputCache]` sobre el detalle por id (tag `business-statuses`) con invalidación por tag en las tres mutaciones; L2 cache-aside con `ICacheStore`, llave `CacheKey.For("businessstatus").Tenant(...)`, TTL 10 minutos, invalidación post-commit. El listado filtrado **no** entra en L1 (`NoStore`), porque la política base no varía por los parámetros de filtro.
- **Alternativas descartadas:** no cachear, que es el defecto D-12 sobre 20 sitios de lectura · cachear solo L1, que no cubre las lecturas que no pasan por HTTP.
- **Consecuencias:** se cachea un **snapshot serializable**, nunca el agregado (constructor privado — `cache.md` lo advierte). Un tenant con el catálogo editado ve el cambio a lo sumo tras la invalidación post-commit, que es inmediata para las escrituras del servicio; una escritura hecha por Jack durante la convivencia **no invalida** la caché del servicio (R-2).
- **Afecta:** §7.2 · pasos F3, F4, F5, F6, F7 (una slice por endpoint, cada una con su propia caché o invalidación — ver §8).

### D13 — Estrategia de corte: la escritura primero
`estado: aprobada · firmó: desarrollador · fecha: 2026-08-14 · origen: Discovery §1`
- **Decisión:** cuando se ejecute el corte, se migran primero las 3 escrituras (1 solo archivo del monolito) y las 25 lecturas quedan en convivencia sobre la misma tabla. **La ejecución del corte no forma parte de este plan** y no se construye ningún feature flag en el servicio (GAP-A `NO APLICA`).
- **Alternativas descartadas:** corte simultáneo de lectura y escritura, que tocaría 25 sitios en 6 áreas · flag propio del servicio, descartado por el desarrollador.
- **Consecuencias:** al terminar este plan el servicio está completo pero **sin consumidores**: nadie escribe todavía por él. La ventana de doble escritor queda abierta hasta que se planifique el corte (R-2).
- **Afecta:** §1.2 · §7.3 · §9.1.

### D14 — Paridad deliberada con el legado en cuatro puntos
`estado: aprobada · firmó: desarrollador · fecha: 2026-08-14 · origen: Discovery §7 D-11, D-16, D-23, D-30 + GAP-2`
- **Decisión:** se **replica** el legado en: (a) sin unicidad de `negest_nombre`; (b) sin columnas ni parámetros de auditoría; (c) color crudo, sin persistir nunca el default `CCCCCC`; (d) nulabilidad real replicada en la entidad de persistencia.
- **Alternativas descartadas:** endurecer estos cuatro puntos, que sería mejora de producto y no corrección de defecto, y en el caso de la unicidad de nombre rompería el alta en tenants que hoy tienen duplicados.
- **Consecuencias:** dos estados pueden llamarse igual. El contrato de salida devuelve `color: null` cuando la columna es nula, a diferencia del frente anónimo que resuelve `CCCCCC` — pero ese frente no se migra (GAP-1), así que no hay contrato roto.
- **Afecta:** §4.1 · §5.4 · §6.3 · pasos F1.4, F2.1, F2.3.

### D15 — El servicio no implementa autenticación ni autorización
`estado: aprobada · firmó: desarrollador · fecha: 2026-08-14 · origen: equipo (GAP-D → NO APLICA)`
- **Decisión:** no se agrega authn/authz. Justificación del desarrollador: es un servicio interno, no expuesto a internet. La plantilla tampoco registra `UseAuthentication`/`UseAuthorization` en `Program.cs`.
- **Alternativas descartadas:** replicar el catálogo de permisos de `EstructuracionComercial` (GAP-7 `NO APLICA`) · exigir API key como el frente anónimo del monolito, que además es el defecto D-03/D-04.
- **Consecuencias:** el ABM completo queda accesible para cualquier llamador que alcance la red del servicio y presente un `X-Entity-Code` válido. Registrado como R-5: si el servicio se expone alguna vez fuera de la red interna, esta decisión debe revisarse **antes**.
- **Afecta:** §6 · §7.1 · fase 4.

### D16 — Se adoptan en bloque los veredictos propuestos en Discovery §7
`estado: aprobada · firmó: desarrollador · fecha: 2026-08-14 · origen: equipo (GAP-17)`
- **Decisión:** los 25 veredictos de la columna «Veredicto propuesto» de Discovery §7 se toman como decisión del equipo, tal como están: se corrige lo marcado «se corrige», se replica lo marcado «se replica», y D-16, D-17, D-19 y D-02 quedan diferidos o con riesgo aceptado.
- **Alternativas descartadas:** revisar defecto por defecto, que retrasaría el arranque sin cambiar el alcance construido.
- **Consecuencias:** el alcance de §8 queda cerrado. Cada defecto tiene un destino explícito en §4.3.
- **Afecta:** §4.3 · §8 completo.

---

## §3 Glosario y trazabilidad

### 3.1 Término de negocio (ES) → nombre técnico (EN)

| Término (ES) | Legado | Nombre técnico | Dónde vive |
|---|---|---|---|
| Estado del negocio / etapa del flujo | `tbl_opo_negocios_estados`, prefijo `negest_` | `BusinessStatusAggregate` | `Contexts/BusinessStatus/Domain/Aggregates/` |
| Flujo de negocio (el catálogo) | región `#region Flujo de negocio` | — (es el conjunto, no un tipo) | — |
| Porcentaje de avance | `negest_porcentaje` | `Percentage` (`int?` en el agregado, sin VO — D4/D5; la columna real es `decimal(20,5)`, la conversión vive en el mapper) | `BusinessStatusAggregate` |
| Color de etapa | `negest_color` | `StatusColor` | `Domain/ValueObjects/` |
| Activo / Inactivo | `negest_estado` | `IsActive` | `BusinessStatusAggregate` |
| Nombre de la etapa | `negest_nombre` | `Name` | `BusinessStatusAggregate` |
| Consecutivo | `negest_consecutivoP` | `Id` | `BusinessStatusAggregate` |
| Ganado | `negest_porcentaje = 100` | `BusinessStatusAggregate.IsWon` | `BusinessStatusAggregate` |
| Perdido | `negest_porcentaje = 0` | `BusinessStatusAggregate.IsLost` | `BusinessStatusAggregate` |
| Etapa intermedia | `porcentaje != 0 && != 100` | `BusinessStatusAggregate.IsIntermediate` | `BusinessStatusAggregate` |
| Estado terminal | — (sin marca en el legado) | `BusinessStatusAggregate.IsTerminal` | `BusinessStatusAggregate` |
| Tipo de etapa (filtro) | — | `BusinessStatusKind` (`All`, `Intermediate`, `Terminal`) | `Domain/Enums/` |
| Institución | `aplent_codigoP`, `ent_bd` | tenant (`X-Entity-Code`) | plantilla, `TenantMiddleware` |
| Negocio | `tbl_opo_negocios` | — (fuera de contexto) | — |

Regla de nombres: cuerpo del documento en español; **todo artefacto técnico en inglés**. Las tablas, columnas y SPs legados se citan tal cual existen. Prohibidos `data`, `info`, `temp`, `manager`, `helper`.

### 3.2 Trazabilidad Discovery → plan

| Discovery | Sección del plan |
|---|---|
| §1 Resumen ejecutivo y veredicto de migración | §1.1, D13 |
| §2 Vocabulario | §3.1 |
| §3.1 Multi-tenancy | D11, §7.1 |
| §3.3 Puntos de llamada expuestos | §7.3 |
| §4.1 Tabla y columnas | §4.1 |
| §4.2 Objetos asociados (sin índices, CHECK, UNIQUE, triggers) | D1, D3, R-1 |
| §4.3 Claves foráneas entrantes | D9, §6.4 |
| §4.4 Datos reales y muestreo | D5, R-1 |
| §4.5 Stored procedures y sus trampas | §4.2, D10 |
| §4.6 Paginación en dos patrones | D8 |
| §4.7 Contrato público de la API | §1.2 (fuera de alcance), D14 |
| §5 Frentes de consumo y mapa de consumidores | §7.3, R-2 |
| §6 Parámetros y personalizaciones | §7.1 |
| §7 Defectos y veredictos | §4.3, D16 |
| §8 Rendimiento | D12 |
| §9 Alcance y fuera de alcance | §1.1, §1.2 |
| §10 GAPs | §9.2 |

---

## §4 Mapeo legado → modelo

### 4.1 Columnas

Esquema en Discovery §4.1 `[verificado en BD]`. Acá se mapea, no se re-transcribe.

| Columna legada | Tipo BD | Propiedad de dominio | Tipo dominio | Configuración de persistencia | Trampa que resuelve |
|---|---|---|---|---|---|
| `negest_consecutivoP` | `int` NOT NULL, identity, PK clustered | `Id` | `int` | `HasColumnName("negest_consecutivoP")`, `ValueGeneratedOnAdd()`, clave | El alta debe devolver el identity: `CreateAsync`, no `AddAsync` (D-26) |
| `negest_nombre` | `varchar(200)` **NULL** | `Name` | `string` (no nulo) | `string?` en la entidad, `HasColumnName("negest_nombre")`, `HasMaxLength(200)`, `IsUnicode(false)`. El mapper convierte `null → ""` | D-23: `[Required]` en la app contra columna nullable. **La longitud máxima (200) se valida en el propio agregado** (`Create`/`Update`, F1.4), no solo en el validador estructural de cada slice de escritura (F5/F6) — así un `INSERT`/`UPDATE` nunca llega a truncar contra `varchar(200)` sin que el dominio lo haya rechazado antes con `NameTooLong` |
| `negest_estado` | `bit` **NULL** | `IsActive` | `bool` (no nulo) | `bool?` en la entidad, `HasColumnName("negest_estado")`. Mapper `null → false` | D-23: el `true` inicial del legado viene del ViewModel, no de un default de columna |
| `negest_porcentaje` | `decimal(20,5)` **NULL** | `Percentage` | `int?` (sin VO propio — D4/D5; validado y almacenado como entero en el agregado, comparación por igualdad exacta) | `decimal?` en la **entidad** (`Entities.BusinessStatus`, refleja la columna real), `HasColumnName("negest_porcentaje")`, `HasPrecision(20, 5)`. El **mapper** convierte: `null` o no-entero → `Percentage` nulo en el dominio; entero exacto → `(int)valor` | D-21: la app ya trataba esto como `int`; ahora el agregado también lo es, y la conversión decimal↔int queda centralizada en un solo lugar (el mapper), no repartida |
| `negest_color` | `varchar(20)` **NULL** | `Color` | `StatusColor?` | `string?`, `HasColumnName("negest_color")`, `HasMaxLength(20)`, `IsUnicode(false)` | D-20/D-30: `CCCCCC` es un default de runtime que nunca se persiste; 18 de 20 bases tienen colores vacíos |

**Columnas que no cruzan al dominio: ninguna.** La tabla tiene cinco columnas y las cinco se modelan. No existe columna de institución `[verificado en BD]`, y el parámetro `@aplent_codigoP` de los SPs no tiene equivalente en el servicio (D11, D-24).

**Claves foráneas entrantes** (Discovery §4.3): `tbl_opo_negocios.neg_negest_consecutivo` y las dos del historial, todas `NO_ACTION`. **No se declaran navegaciones** hacia ellas: el agregado no necesita ver a sus referenciadores y el historial está fuera de contexto. Su único efecto en el servicio es el 547 al borrar (D9).

### 4.2 Stored procedures → casos de uso

Ninguno se invoca (D10). La columna «Reemplazo» indica qué pieza del servicio cubre su propósito.

| SP legado | Reemplazo | Trampa del SP que queda resuelta |
|---|---|---|
| `pa_opo_negocios_estados_retornar` | `GetBusinessStatusesUseCase` | Orden sin desempate (D-31 → D7); filtro de texto y de actividad opcionales; paginación en la consulta (D-25 → D8) |
| `pa_opo_negocios_estados_detalle_retornar` | `GetBusinessStatusByIdUseCase` | Identificador inexistente responde 404 en vez de desreferenciar un nulo (D-15) |
| `pa_opo_negocios_estados_ingresar` | `CreateBusinessStatusUseCase` | Valida el porcentaje (D-05, D-14) y devuelve el identificador asignado (D-26) |
| `pa_opo_negocios_estados_modificar` | `UpdateBusinessStatusUseCase` | Sin `SET` destructivo por parámetro omitido (D-22); valida la invariante también en edición (D-05) |
| `pa_opo_negocios_estados_eliminar` | `DeleteBusinessStatusUseCase` | Protege terminales y clasifica el conflicto por FK como 409 (D-06, D-29) |
| `pa_apis_opo_negocios_estados_retornar` | **sin reemplazo** | Fuera de alcance (GAP-1). Sigue sirviendo a `api/flujonegocios` desde Jack |

### 4.3 Destino de cada defecto de Discovery §7

Veredictos adoptados en bloque por D16.

| Defecto | Destino en este plan |
|---|---|
| D-27 invariante rota | Guardas de D3 (no crear ni editar a 0/100, no borrar terminales). No se repara lo existente — R-1 |
| D-28 «Ganado» inactivo no determinista | D6: `TerminalBusinessStatusProvider` filtra por activo y falla ante ambigüedad. Paso F8.1 |
| D-31 orden sin desempate | D7. Paso F2.5 |
| D-21 `decimal(20,5)` tratado como `int` | D5: `int?` sin VO dentro del agregado (comparación por igualdad exacta); el `decimal` real de la columna solo vive en la entidad de persistencia y se convierte en el mapper. Pasos F1.4, F2.3 |
| D-05 la edición no valida lo que valida el alta | D3: la misma invariante en `Create` y en `Update`. Pasos F1.4, F5, F6 |
| D-06 borrado de terminales | D9. Pasos F1.4, F7 |
| D-07 resolución sin guarda de nulo | D6: la operación retorna `Result`, nunca un nulo. Paso F8.1 |
| D-22 `SET` sin `COALESCE` | D10: no se invoca el SP; `Update` escribe el agregado completo. Paso F2.6 |
| D-29 conflicto de FK sin clasificar | D9: 409 vía `SqlServerErrorClassifier`. Paso F2.6 |
| D-23 nulabilidad contra `[Required]` | D14 (a) replicar en persistencia + D5. Pasos F2.1, F2.3 |
| D-11 sin unicidad | Porcentaje: cubierto por las guardas de D3. Nombre: **se replica** (D14 a) |
| D-24 `@aplent_codigoP` muerto | D11: no existe en el servicio |
| D-09 dos formas de resolver «Ganado» | D6: una sola |
| D-12 sin caché | D12. Pasos F3, F4, F5, F6, F7 |
| D-25 asimetría de filtros entre frentes | D8: filtros homogéneos y opcionales |
| D-14 alta inválida con HTTP 200 | D8 + §6.4: la violación de invariante responde 400 |
| D-30 dos representaciones del color | D14 (c): el contrato del servicio expone el valor crudo. El frente que resuelve `CCCCCC` no se migra |
| D-15 identificador inexistente | 404 explícito. Paso F4 |
| D-26 identity descartado | `CreateAsync` devuelve el id. Paso F2.6 |
| D-20 color sin validación de formato | `StatusColor` valida 6 hex sin `#`. Paso F1.3 |
| D-16 sin auditoría | **Diferido** (D14 b) |
| D-17 casing de `aplent_codigop` · D-19 ruta mal nombrada | **Riesgo aceptado** en el legado; no existen en el servicio |
| D-02, D-03, D-04 exposición anónima y claves en código | Pertenecen a Jack / a Negocios. Fuera de alcance (§1.2) |

---

## §5 Dominio

### 5.1 Aggregate Root

`Contexts/BusinessStatus/Domain/Aggregates/BusinessStatusAggregate.cs` — hereda `AggregateRoot<int>`.

```csharp
public sealed class BusinessStatusAggregate : AggregateRoot<int>
{
    public const int MinPercentage = 0;
    public const int MaxPercentage = 100;
    public const int MaxNameLength = 200;

    public string Name { get; private set; }
    public int? Percentage { get; private set; }
    public StatusColor? Color { get; private set; }
    public bool IsActive { get; private set; }

    public bool IsWon         => Percentage == MaxPercentage;
    public bool IsLost        => Percentage == MinPercentage;
    public bool IsTerminal    => IsWon || IsLost;
    public bool IsIntermediate => !IsTerminal;

    public static Result<BusinessStatusAggregate> Create(CreateBusinessStatusArgs args);
    public Result<BusinessStatusAggregate> Update(UpdateBusinessStatusArgs args);
    public Result EnsureCanBeDeleted();
    public void AssignId(int id);   // el IDENTITY que asigna la base en el alta (F2.6)

    public static BusinessStatusAggregate Reconstruct(
        int id, string name, int? percentage, string? color, bool isActive);

    protected override void Created();

    private static ValidationError? ValidateName(string? name);                        // NameRequired, NameTooLong (> MaxNameLength)
    private static Result<int, ValidationError> ValidatePercentageForCreate(decimal value); // PercentageOutOfRange, PercentageMustBeInteger, TerminalPercentageNotAllowed (INV-1) — valida Y convierte
    private Result<int, ValidationError> ValidatePercentageForUpdate(decimal value);    // idem + TerminalPercentageIsImmutable si IsTerminal y el nuevo valor difiere del almacenado (INV-2)
}
```

- `Create` y `Update` **acumulan** errores de validación y retornan todos juntos vía `DomainError.FromValidationDomainErrors`. Eso incluye `ValidateName` (requerido y longitud máxima) y la validación de porcentaje — **ninguna de las dos queda solo del lado del validador estructural** de cada slice (`GetBusinessStatusesInputValidator`/`CreateBusinessStatusInputValidator`/`UpdateBusinessStatusInputValidator`, Fases 3/5/6): el agregado las repite, porque es la única capa que un futuro caller interno del propio servicio no puede saltarse.
- El porcentaje **no tiene VO propio** (D4) y se almacena como `int?`, no `decimal?` (revisión 2026-08-14): la columna real es `decimal(20,5)` (§4.1), pero el significado de negocio siempre fue entero 0-100 — el `decimal` solo existe en el borde de entrada (`ValidatePercentageForCreate`/`ValidatePercentageForUpdate` reciben `decimal` para poder distinguir "no es entero" de "está fuera de rango" con un error de dominio propio, en vez de que lo rechace el *model binding* de ASP.NET) y en el borde de persistencia (el mapper, F2.3). Una vez validado, el agregado solo conoce `int`. Al ser `int`, `IsWon`/`IsLost`/`IsTerminal`/`IsIntermediate` comparan por **igualdad exacta** — ya no hace falta tolerancia (`decimal` no tiene el problema de precisión de punto flotante que la justificaba).
- El color **sí** sigue siendo VO (`StatusColor`, sin cambios): `Create`/`Update` llaman `StatusColor.Create(args.Color)` cuando el valor no es nulo/vacío y propagan `InvalidColorFormat` si falla; un valor nulo o vacío no genera VO ni error (ausencia de color, D14 c).
- Los `Args` son records con **solo primitivos** (`CreateBusinessStatusArgs(string? Name, decimal Percentage, string? Color, bool IsActive)` — `Percentage` sigue en `decimal` para preservar `PercentageMustBeInteger` en el borde); el agregado construye el VO de color por dentro, valida y convierte el porcentaje con sus propios métodos.
- `Reconstruct` no valida ni marca auditoría: los datos persistidos ya existen. Recibe `int?` directamente — la conversión `decimal? → int?` de un dato potencialmente sucio (`decimal(20,5)` con residuo, ej. `0,4`) es responsabilidad del **mapper** (F2.3), no del agregado: si el valor persistido no es un entero exacto, el mapper lo trata como ausencia de porcentaje (`null`) en vez de redondearlo — evita clasificar un dato sucio como Ganado/Perdido por un redondeo (D5).
- `Created()` fija `CreatedAt`/`UpdatedAt` en UTC. La tabla no tiene columnas de auditoría (D14 b): las fechas viven solo en memoria y el mapper no las persiste.

**Sub-entidades:** ninguna. El catálogo es plano.

### 5.2 Value Objects

El porcentaje **no** tiene VO propio (D4, revisión 2026-08-14): sus constantes, su validación y la semántica de terminal viven en `BusinessStatusAggregate` (§5.1). El único VO del contexto es el color:

| VO | Reglas | Miembros relevantes |
|---|---|---|
| `StatusColor` | Exactamente 6 caracteres hexadecimales, **sin** `#`. Opcional: la ausencia se modela con `null`, no con `CCCCCC` (D14 c) | `Value`, `Length = 6` |

La semántica de terminal (`IsWon`/`IsLost`/`IsTerminal`/`IsIntermediate`), por igualdad exacta de `int` (D5, D-21), está en el agregado, no acá — ver §5.1.

### 5.3 Contratos de persistencia y consulta

```csharp
// Domain/Repositories/IBusinessStatusRepository.cs
public interface IBusinessStatusRepository : IRootRepository<BusinessStatusAggregate, int>
{
    Task<PagedResult<BusinessStatusAggregate>> GetAsync(
        BusinessStatusFilter filter, PageQuery page, CancellationToken cancellationToken = default);

    Task<Result<BusinessStatusAggregate>> CreateAsync(
        BusinessStatusAggregate aggregate, CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<BusinessStatusAggregate>>> GetActiveTerminalsAsync(
        TerminalKind kind, CancellationToken cancellationToken = default);
}

// Domain/Queries/BusinessStatusFilter.cs
public sealed record BusinessStatusFilter(string? Name, bool? IsActive, BusinessStatusKind Kind);
```

- `GetAsync` ordena `percentage ASC, id ASC` (D7) y pagina con `OFFSET/FETCH`.
- `GetActiveTerminalsAsync` devuelve **todas** las coincidencias activas; decidir sobre la ambigüedad es del provider (D6), no del repositorio.
- `GetAllAsync` de `IRootRepository` no se puede servir con seguridad sin filtro: se implementa delegando en `GetAsync` con filtro vacío, tal como habilita `repositorio.md`.
- `TerminalKind` (`Won`, `Lost`) y `BusinessStatusKind` (`All`, `Intermediate`, `Terminal`) viven en `Domain/Enums/`.

### 5.4 Invariantes y errores

| # | Invariante | Dónde se aplica | Error | HTTP |
|---|---|---|---|---|
| INV-1 | Un estado creado o editado nunca tiene porcentaje 0 ni 100 | `Create`, `Update` | `TerminalPercentageNotAllowed` | 400 |
| INV-2 | El porcentaje de un estado terminal existente es inmutable | `Update` | `TerminalPercentageIsImmutable` | 400 |
| INV-3 | Un estado terminal no se puede eliminar | `EnsureCanBeDeleted` | `TerminalCannotBeDeleted` | 409 |
| INV-4 | La resolución de un terminal exige exactamente un candidato activo | `TerminalBusinessStatusProvider` | `AmbiguousTerminalStatus` / `TerminalStatusNotFound` | 409 / 404 |

`Contexts/BusinessStatus/Domain/Errors/BusinessStatusErrors.cs` centraliza todos los errores, con `public const string Context = "BusinessStatus"`. Todo `ValidationError` fija `Property` y, cuando hay límites, `Attributes`.

| Error | Tipo | Property |
|---|---|---|
| `NotFound(int id)` | `NotFound` | — |
| `NameRequired` | `Validation` | `Name` |
| `NameTooLong` (max 200) | `Validation` | `Name` |
| `PercentageOutOfRange` (0–100) | `Validation` | `Percentage` |
| `PercentageMustBeInteger` | `Validation` | `Percentage` |
| `TerminalPercentageNotAllowed` | `Validation` | `Percentage` |
| `TerminalPercentageIsImmutable` | `Validation` | `Percentage` |
| `InvalidColorFormat` | `Validation` | `Color` |
| `TerminalCannotBeDeleted` | `Conflict` | — |
| `StatusInUse(int id)` | `Conflict` | — |
| `AmbiguousTerminalStatus(TerminalKind)` | `Conflict` | — |
| `TerminalStatusNotFound(TerminalKind)` | `NotFound` | — |

**Enums del dominio:** `BusinessStatusKind`, `TerminalKind`. No hay enum de estados: el catálogo es editable por el usuario final (Discovery §4.8).

### 5.5 Auditoría de `Shared` (regla: nada se crea si ya existe)

| Necesidad | ¿Existe? | Ruta | Veredicto |
|---|---|---|---|
| Base de agregado con `CreatedAt`/`UpdatedAt` | Sí | `src/Shared/Domain/Aggregates/AggregateRoot.cs` | **Reutilizar** |
| Identidad e igualdad por Id | Sí | `src/Shared/Domain/Entities/Entity.cs` | **Reutilizar** |
| Base de Value Object | Sí | `src/Shared/Domain/ValueObjects/ValueObject.cs` | **Reutilizar** |
| `Result`, `Result<T>`, `PagedResult<T>` | Sí | `src/Shared/Results/` | **Reutilizar** |
| Errores tipados y `ErrorType` | Sí | `src/Shared/Results/Errors/` (`ValidationError`, `NotFoundError`, `ConflictError`, `InternalError`) | **Reutilizar** |
| Acumulación de errores de validación | Sí | `DomainError.FromValidationDomainErrors` | **Reutilizar** |
| Mapeo error → HTTP | Sí | `src/Shared/Infrastructure/Presentation/Mapping/ErrorHttpMapper.cs` | **Reutilizar** |
| Contrato de repositorio de agregado | Sí | `src/Shared/Domain/Interfaces/IRootRepository.cs` | **Extender** en `IBusinessStatusRepository` (dentro del contexto) |
| Paginación de punta a punta | Sí | `PageQuery`, `PageQueryInputDto`, `Infrastructure/Validation/FluentValidation/Shared/PageQueryInputValidator.cs`, `HttpOkPagedResult` | **Reutilizar** |
| Unit of Work | Sí | `src/Shared/Application/Ports/IUnitOfWorkPort.cs` + `Adapters/Persistence/UnitOfWorkAdapter.cs` | **Reutilizar** |
| Clasificación de errores SQL (547 → Conflict) | Sí | `Adapters/Persistence/SqlServer/SqlServerErrorClassifier.cs` | **Reutilizar** — habilita D9 |
| Errores de persistencia genéricos | Sí | `Persistence/EntityFramework/Common/PersistenceErrors.cs` | **Reutilizar** |
| Caché L2, llaves y degradación | Sí | `ICacheStore`, `Shared/Application/Caching/CacheKey.cs`, `RedisCacheStore`, `NoOpCacheStore` | **Reutilizar** |
| Caché L1 e invalidación por tag | Sí | `[OutputCache]`, `Presentation/Filters/OutputCacheInvalidateAttribute.cs` | **Reutilizar** |
| Validación estructural auto-registrada | Sí | `IStructuralValidator<T>`, `[ValidateRequest]`, `Api/Filters/ValidateRequestFilter.cs` | **Reutilizar** |
| Resultados HTTP tipados | Sí | `HttpOkResult`, `HttpCreatedResult`, `HttpNoContentResult`, `HttpOkPagedResult` | **Reutilizar** |
| Logging estructurado | Sí | `ILoggerPort<T>` + `SerilogLoggerAdapter` | **Reutilizar** |
| Multi-tenancy | Sí | `Shared/Infrastructure/MasterAccess/**`, `Api/Middleware/TenantMiddleware.cs` | **Reutilizar** |
| Ruteo kebab-case | Sí | `Presentation/Routing/KebabCaseParameterTransformer.cs` | **Reutilizar** |
| Repositorio genérico EF | Sí, **no aplica** | `Persistence/EntityFramework/Common/RepositoryBaseEF.cs` | **No usar**: el agregado no es la entidad mapeada (`repositorio.md`) |
| VOs de porcentaje y color, errores del contexto, filtro, repositorio, casos de uso | No | — | **Crear dentro del contexto** |

**No se requiere extender `Shared`. Este plan no tiene PR de `Shared`.** No hay servicio hermano migrado en el repositorio (GAP-12 omitido): la única fuente de convenciones es `docs/plantilla/`.

### 5.6 `DESVIACIÓN-1` — nombre de la entidad de persistencia · **RECHAZADA (2026-08-21)**

La plantilla nombra la entidad de persistencia como el concepto (`Product`). Acá el contexto se llama `BusinessStatus` (D2), de modo que el **namespace** `BusinessStatus.Domain.*` y un **tipo** `BusinessStatus` coexistirían en los archivos de Infrastructure que usan ambos, con ambigüedad de resolución.

- **Desviación propuesta:** llamar `BusinessStatusRow` a la entidad de persistencia.
- **Resolución:** **rechazada por el desarrollador.** La entidad se llama `BusinessStatus`, en `Infrastructure/Persistence/EntityFramework/BusinessStatuses/Entities/BusinessStatus.cs`, y la configuración `BusinessStatusConfiguration` — la convención de `repositorio.md` sin sufijos. La colisión se resuelve **calificando cada uso** (`Entities.BusinessStatus`), como el ejemplo de `contextos.md`.
- **Consecuencia práctica:** ningún archivo de Infrastructure importa `...BusinessStatuses.Entities` con un `using` simple. Si lo hiciera, el nombre simple `BusinessStatus` resolvería al **namespace** del contexto —que el compilador encuentra en el espacio global antes de considerar los tipos importados— y fallaría con `CS0118`. En los archivos de prueba, cuyo namespace es `UnitTests.Infrastructure.*`, la calificación completa tampoco sirve (el prefijo `Infrastructure.` resolvería a `UnitTests.Infrastructure`), así que usan el alias `using Entities = Infrastructure.Persistence.EntityFramework.BusinessStatuses.Entities;`, que se resuelve en el namespace global y deja el código leyéndose igual que en producción.

---

## §6 Contratos de API

Controller `BusinessStatusesController`, ruta base `business-statuses` (derivada por `KebabCaseParameterTransformer`), `[Tags("business-statuses")]` a nivel de clase, casos de uso inyectados por constructor. Sin atributos de autorización (D15).

### 6.1 Endpoints

| Verbo | Ruta | Caso de uso | Éxito | Errores posibles |
|---|---|---|---|---|
| GET | `/business-statuses` | `IGetBusinessStatusesUseCase` | 200 `HttpOkPagedResult` | 400 |
| GET | `/business-statuses/{id}` | `IGetBusinessStatusByIdUseCase` | 200 `HttpOkResult` | 400, 404 |
| POST | `/business-statuses` | `ICreateBusinessStatusUseCase` | 201 `HttpCreatedResult` | 400 |
| PUT | `/business-statuses/{id}` | `IUpdateBusinessStatusUseCase` | 200 `HttpOkResult` | 400, 404 |
| DELETE | `/business-statuses/{id}` | `IDeleteBusinessStatusUseCase` | 204 `HttpNoContentResult` | 400, 404, 409 |

Caché (D12): `GET /{id}` lleva `[OutputCache(Duration = 300, Tags = [CacheTag], VaryByRouteValueNames = ["id"])]`; `GET /` lleva `[OutputCache(NoStore = true)]` porque la política base no varía por los filtros; los tres verbos de mutación llevan `[OutputCacheInvalidate(CacheTag)]` con `private const string CacheTag = "business-statuses"`.

### 6.2 Convención de paginación — una sola

`[FromQuery] PageQueryInputDto pagination` → `new PageQuery(pagination.PageIndex, pagination.PageSize)` → `PagedResult<T>` → `{ data: { items: [...], totalCount: N }, statusCode: 200 }`. `pageIndex` base 0, `pageSize` por defecto 20 y máximo 100. Orden estable garantizado por D7.

### 6.3 Validaciones — todos los campos de entrada, sin excepción

Reglas estructurales en validadores de FluentValidation (`IStructuralValidator<T>`, ejecutados por `[ValidateRequest]` antes del caso de uso); reglas de negocio en el dominio.

**`GET /business-statuses` — `GetBusinessStatusesInputDto` + `PageQueryInputDto`**

| Campo | Tipo | Obligatorio | Regla estructural | Semántica |
|---|---|---|---|---|
| `name` | `string?` | No | `MaximumLength(200)` | Coincidencia parcial, equivalente a `LIKE '%texto%'`. Omitido = sin filtro |
| `isActive` | `bool?` | No | — | Omitido = **sin filtro** (semántica del SP legado, no del formulario) |
| `kind` | `BusinessStatusKind?` | No | `IsInEnum()` | `All` (defecto), `Intermediate`, `Terminal`. Cubre el filtro de etapas intermedias repetido en 7 sitios del legado |
| `pageIndex` | `int` | No (defecto 0) | `GreaterThanOrEqualTo(0)` | Ya validado por `PageQueryInputValidator` |
| `pageSize` | `int` | No (defecto 20) | `InclusiveBetween(1, 100)` | Ya validado por `PageQueryInputValidator` |

**`GET /business-statuses/{id}` y `DELETE /business-statuses/{id}`**

| Campo | Tipo | Obligatorio | Regla estructural |
|---|---|---|---|
| `id` | `int` (ruta) | Sí | `GreaterThan(0)` |

**`POST /business-statuses` — `CreateBusinessStatusInputDto`**

| Campo | Tipo | Obligatorio | Regla estructural | Regla de dominio |
|---|---|---|---|---|
| `name` | `string?` | Sí | `NotEmpty()`, `MaximumLength(200)` | `NameRequired`, `NameTooLong` |
| `percentage` | `decimal` | Sí | `InclusiveBetween(0, 100)` | `PercentageMustBeInteger`, `TerminalPercentageNotAllowed` (INV-1) |
| `color` | `string?` | No | `Matches("^[0-9A-Fa-f]{6}$")` cuando viene | `InvalidColorFormat`. Nulo o vacío se persiste como `NULL` (D14 c) |
| `isActive` | `bool` | No (defecto `true`) | — | — |

**`PUT /business-statuses/{id}` — `UpdateBusinessStatusInputDto`**

Mismos campos que el alta, con `id` en la ruta. Semántica de reemplazo completo: **todos los campos se envían**, y el caso de uso escribe el agregado entero (así se evita el `SET` destructivo de D-22). Reglas adicionales de dominio: `TerminalPercentageIsImmutable` (INV-2) cuando el estado almacenado es terminal, y `TerminalPercentageNotAllowed` (INV-1) cuando el nuevo porcentaje es 0 o 100.

`name` es nullable en los DTO de entrada a propósito: así el validador reporta el error con su `Property` en vez de que falle el deserializador.

### 6.4 Errores de dominio → HTTP

| Error | `ErrorType` | HTTP | Cuándo |
|---|---|---|---|
| Validación estructural del DTO | `Validation` | 400 | Falla `[ValidateRequest]` |
| `NameRequired`, `NameTooLong`, `PercentageOutOfRange`, `PercentageMustBeInteger`, `InvalidColorFormat` | `Validation` | 400 | Falla el agregado o un VO |
| Acumulado de validaciones del agregado | `DomainError` | 400 | `FromValidationDomainErrors` |
| `TerminalPercentageNotAllowed` (INV-1), `TerminalPercentageIsImmutable` (INV-2) | `Validation` | 400 | Alta o edición con porcentaje terminal |
| `NotFound(id)` | `NotFound` | 404 | Detalle, edición o borrado de un id inexistente (D-15) |
| `TerminalCannotBeDeleted` (INV-3) | `Conflict` | 409 | Borrado de un estado al 0 % o al 100 % (D-06) |
| `StatusInUse(id)` | `Conflict` | 409 | El motor rechaza el `DELETE` por la FK de negocios: error 547 clasificado (D-29) |
| `AmbiguousTerminalStatus` (INV-4) | `Conflict` | 409 | Dos terminales activos del mismo tipo (D-28) |
| `TerminalStatusNotFound` (INV-4) | `NotFound` | 404 | Ningún terminal activo del tipo pedido (D-07) |
| Fallo de persistencia | `Internal` | 500 | `PersistenceErrors.Failure(Origin)` |

Ningún mensaje del motor de base de datos llega al cliente (corrige D-29). Todo error se sella con `Context = "BusinessStatus"` y el `Origin` de quien lo produjo; el caso de uso propaga sin reescribir los errores del repositorio ni del Unit of Work.

---

## §7 Operación

### 7.1 Variables de entorno

**Este contexto no introduce ninguna variable de entorno nueva.** Todo lo que necesita ya está resuelto por la plantilla (`docs/plantilla/variables-entorno.md`), por lo que no se abre GAP.

| Variable | Capa | Por qué la necesita este contexto |
|---|---|---|
| `Cache__Enabled` | ConfigMap | Habilita L1 sobre el detalle (D12) |
| `Cache__L2Enabled` | ConfigMap | Habilita el cache-aside del listado (D12) |
| `Cache__DefaultTtlSeconds` | ConfigMap | TTL de L1 cuando el endpoint no declara `Duration` |
| `Cache__ConnectionString` | Secret compartido `platform-shared` | Redis para L1 y L2. Vacío ⇒ memoria para L1 y `NoOpCacheStore` para L2 |
| `TenantResolverService__Enabled`, `__TimeoutSeconds`, `__CacheTtlMinutes` | ConfigMap | Resolución del tenant, sin la cual no hay conexión a la base (D11) |
| `TENANT_RESOLVER_SERVICE_URL`, `CONNSTRING_ENCRYPTION_KEY` | Secret compartido `platform-shared` | Idem |
| `Sentry__*`, `Cors__AllowedOrigins__*`, `ServiceInfo__Name` | ConfigMap / secreto compartido | Transversales de la plantilla |

El connection string de la base **no** se configura: lo entrega el tenant-resolver cifrado por petición. El TTL de L2 (10 minutos) es una constante del contexto, no una variable: `Cache:DefaultTtlSeconds` aplica solo a L1.

### 7.2 Caché y rendimiento

| Nivel | Qué cachea | Llave / tag | TTL | Invalidación |
|---|---|---|---|---|
| L1 OutputCache | `GET /business-statuses/{id}` | tag `business-statuses`, varía por ruta `id` + `X-Entity-Code` + `Accept-Language` | 300 s | `[OutputCacheInvalidate("business-statuses")]` en POST, PUT y DELETE |
| L1 OutputCache | `GET /business-statuses` | — | — | `NoStore`: la política base no varía por los filtros |
| L2 cache-aside | Snapshot del listado por combinación de filtro y página | `CacheKey.For("businessstatus").Tenant(tenantCode).Resource("list", hashDeFiltroYPagina)` | 10 min | `RemoveByPrefixAsync(CacheKey.For("businessstatus").Tenant(tenantCode).Prefix("list"))`, **post-commit** |

- Se cachea un **record plano serializable**, nunca el agregado: su constructor es privado y `System.Text.Json` no lo reconstruye (`cache.md`).
- Solo se cachean éxitos. Un fallo de Redis degrada a consulta directa con `Warning`.
- La invalidación ocurre en el caso de uso, después de que el commit haya tenido éxito.

Sin telemetría (GAP-11 omitido), estos son los hallazgos cualitativos que justifican la caché: catálogo de 6 a 12 filas, casi inmutable, leído desde 20 sitios en el monolito, incluido un job de validación masiva.

### 7.3 Qué reemplaza cada ruta actual del monolito

| Ruta actual (Discovery §3.3) | Reemplazo en el servicio | Estado tras este plan |
|---|---|---|
| `FlujoNegocio/inicio`, `FlujoNegocio/Lista` | `GET /business-statuses` | Construido, **sin conmutar** (D13) |
| `FlujoNegocio/Crear` (GET) | — (pantalla, no API) | No aplica |
| `FlujoNegocio/ActualizarOportunidad` (POST — nombre erróneo, D-19) | `POST /business-statuses` y `PUT /business-statuses/{id}` | Construido, sin conmutar |
| `FlujoNegocio/{id}/Editar` | `GET /business-statuses/{id}` | Construido, sin conmutar |
| `FlujoNegocio/{id}/Eliminar` (GET y POST) | `DELETE /business-statuses/{id}` | Construido, sin conmutar |
| `api/flujonegocios` (anónimo) | **Ninguno** | Sigue en Jack (GAP-1) |
| Los 25 sitios de lectura interna del monolito | Ninguno en esta iteración | Siguen leyendo la tabla directamente (D1) |

### 7.4 Consumidores y convivencia

Mientras el corte no se ejecute (D13), la tabla tiene **dos escritores potenciales** y muchos lectores fuera del servicio: 25 sitios de lectura en 6 áreas del monolito, 31 objetos de base de datos ajenos al módulo (11 generados dinámicamente, fuera de alcance), informes, backup y los consumidores externos de datos (GAP-13, documentado sin acción). Ninguno se rompe: la tabla no cambia. El riesgo es de coherencia de caché, no de contrato — ver R-2.

---

## §8 Fases y pasos

`estado` es `pending` en todos los pasos al generar el plan; el agente ejecutor lo cambia a `done` tras correr `Verificar`. **Ningún paso nace `blocked`:** no quedan decisiones en `propuesta` ni GAPs bloqueantes abiertos.

Estimación en puntos Fibonacci. `tarea:` indica la rama; cuando no hay clave de Jira se registra `(sin asignar)` y se nombra la rama sugerida.

Organización de la Fase 3 en adelante: **vertical slice**. Cada endpoint HTTP (salvo la resolución de terminales, que no expone ninguno) es una fase propia con su caso de uso, DTOs, mapping, validador, acción de controller, registro DI y caché — de punta a punta, en una sola tarea/rama — en vez de repartir "todos los casos de uso" en una fase y "todos los endpoints" en otra.

| Fase | Slice / alcance | Tareas / ramas | Puntos |
|---|---|---|---|
| F0 Preparación | — (compartido) | `chore/business-status-scaffold` | 3 |
| F1 Dominio | — (compartido) | `feat/business-status-domain-errors-vos`, `feat/business-status-aggregate` | 13 |
| F2 Persistencia | — (compartido) | `feat/business-status-persistence-mapping`, `feat/business-status-repository` | 13 |
| F3 Listar estados | `GET /business-statuses` | `feat/business-status-list` | 8 |
| F4 Detalle por id | `GET /business-statuses/{id}` | `feat/business-status-get-by-id` | 5 |
| F5 Crear estado | `POST /business-statuses` | `feat/business-status-create` | 5 |
| F6 Editar estado | `PUT /business-statuses/{id}` | `feat/business-status-update` | 5 |
| F7 Eliminar estado | `DELETE /business-statuses/{id}` | `feat/business-status-delete` | 5 |
| F8 Resolución de terminales | — (interno, sin endpoint) | `feat/business-status-terminal-resolution` | 3 |
| F9 Pruebas de integración | transversal a las 5 slices | `test/business-status-integration` | 8 |
| **Total** | | | **68** |

---

### Fase 0 — Preparación

**Estrategia de pruebas:** ninguna propia. La fase se valida con la compilación en verde de la solución.

#### [F0.1] Read template documentation and confirm conventions
`id: F0.1 · depende_de: — · tarea: (sin asignar) rama chore/business-status-scaffold · estado: done`
- **Objetivo:** leer la documentación de la plantilla antes de escribir una línea, y confirmar que el plan no contradice lo que la plantilla ya resuelve.
- **Fuente:** template (regla 7 del estándar).
- **Archivos:** lectura de `docs/plantilla/arquitectura.md`, `contextos.md`, `repositorio.md`, `casos-de-uso.md`, `controllers.md`, `patron-result.md`, `errores-dominio.md`, `entidades-y-agregados.md`, `value-objects.md`, `validaciones.md`, `cache.md`, `contrato-api.md`, `testing.md`, `variables-entorno.md`. **No hay servicio hermano de referencia** (GAP-12 omitido).
- **Detalle:** verificar en particular: el agregado no es la entidad de EF Core; el repositorio vive en `Persistence/EntityFramework/{Contexto}/` sin sufijo `Adapter`; `RepositoryBaseEF` no aplica; los `OrderBy` paginados exigen desempate; la caché nunca guarda agregados.
- **Hecho cuando:** el ejecutor deja en el PR de F0.2 una nota de conformidad de dos líneas, o levanta un `⚠️ GAP` si algo del plan contradice la plantilla.
- **Verificar:** `dotnet build Service.slnx`

#### [F0.2] Scaffold BusinessStatus context projects
`id: F0.2 · depende_de: F0.1 · tarea: (sin asignar) rama chore/business-status-scaffold · estado: done`
- **Objetivo:** crear los dos proyectos del contexto y engancharlos a la solución y a Infrastructure, sin código de dominio todavía.
- **Fuente:** template (`ServiceInfo.Domain.csproj` / `ServiceInfo.Application.csproj`) · D2.
- **Archivos:** `src/Contexts/BusinessStatus/Domain/BusinessStatus.Domain.csproj`, `src/Contexts/BusinessStatus/Application/BusinessStatus.Application.csproj`, `Service.slnx`, `src/Infrastructure/Infrastructure.csproj`.
- **Detalle:** `BusinessStatus.Domain` referencia `Shared.Domain`. `BusinessStatus.Application` referencia `BusinessStatus.Domain` y `Shared.Application`. En `Service.slnx` se agrega la carpeta `/src/Contexts/BusinessStatus/` con ambos proyectos, siguiendo la entrada existente de `ServiceInfo`. `Infrastructure.csproj` agrega `ProjectReference` a `BusinessStatus.Application`.
- **Hecho cuando:** los dos proyectos existen, están en la solución, y la solución compila sin advertencias nuevas.
- **Verificar:** `dotnet build Service.slnx`

---

### Fase 1 — Dominio

**Estrategia de pruebas:** unitarias puras, sin dobles ni base de datos. El VO `StatusColor` prueba su frontera (hex válido en mayúsculas/minúsculas, con `#`, longitud ≠ 6, nulo/vacío). El agregado prueba acumulación de errores, las cuatro invariantes, la validación de nombre y porcentaje (rango `0`/`100`/`-1`/`101`, no entero como `50,5` o `99,9`, e igualdad exacta contra el límite — ya sin frontera de tolerancia, porque `Percentage` es `int?`), y que `Reconstruct` no valida. Proyecto: `tests/UnitTests`.

#### [F1.1] Create BusinessStatusErrors
`id: F1.1 · depende_de: F0.2 · tarea: (sin asignar) rama feat/business-status-domain-errors-vos · estado: done`
- **Objetivo:** centralizar todos los errores del contexto antes de escribir los tipos que los referencian.
- **Fuente:** §5.4 · D16 · `docs/plantilla/errores-dominio.md`.
- **Archivos:** `src/Contexts/BusinessStatus/Domain/Errors/BusinessStatusErrors.cs`.
- **Detalle:** clase estática con `public const string Context = "BusinessStatus"` y los 12 errores de la tabla de §5.4. Cada `ValidationError` fija `Property`; `NameTooLong`, `PercentageOutOfRange` y `InvalidColorFormat` fijan además `Attributes` con su límite (`maxLength = 200`, `min = 0`, `max = 100`, `length = 6`).
- **Hecho cuando:** los 12 errores existen con el `ErrorType` de la tabla de §6.4 y el proyecto compila.
- **Verificar:** `dotnet build src/Contexts/BusinessStatus/Domain/BusinessStatus.Domain.csproj`

#### [F1.3] Create StatusColor value object
`id: F1.3 · depende_de: F1.1 · tarea: (sin asignar) rama feat/business-status-domain-errors-vos · estado: done`
- **Objetivo:** validar el formato del color una sola vez y eliminar el default duplicado del legado.
- **Fuente:** D4 · D14 (c) · Discovery §7 D-20, D-30.
- **Archivos:** `src/Contexts/BusinessStatus/Domain/ValueObjects/StatusColor.cs`, `tests/UnitTests/Contexts/BusinessStatus/Domain/StatusColorTests.cs`.
- **Detalle:** `public const int Length = 6`. `Create(string? value)` retorna `InvalidColorFormat` cuando el valor no cumple `^[0-9A-Fa-f]{6}$`. El VO **no** aplica ningún default: la ausencia se representa con `null` en el agregado, nunca con `CCCCCC`. Se conserva el valor tal como lo escribió el usuario, sin normalizar el casing (paridad: el legado guarda minúsculas y mayúsculas indistintamente).
- **Hecho cuando:** los tests cubren `"49ff7c"`, `"49FF7C"`, `"#49ff7c"` (inválido), `"49ff7"` (inválido), `"zzzzzz"` (inválido) y `null`/`""` (no producen VO ni error: los resuelve el agregado como ausencia).
- **Verificar:** `dotnet test tests/UnitTests/UnitTests.csproj --filter FullyQualifiedName~StatusColorTests`

#### [F1.4] Create BusinessStatusAggregate with its args records
`id: F1.4 · depende_de: F1.3 · tarea: (sin asignar) rama feat/business-status-aggregate · estado: done`
- **Objetivo:** modelar el estado individual, con la validación de nombre y porcentaje **dentro del propio agregado** (no solo en el validador estructural de cada slice, Fases 3/5/6), y encerrar en él las cuatro invariantes de escritura.
- **Fuente:** D3 · D4 · D5 · D14 · §5.1 · §5.4 · `docs/plantilla/entidades-y-agregados.md`.
- **Archivos:** `src/Contexts/BusinessStatus/Domain/Aggregates/BusinessStatusAggregate.cs`, `.../Aggregates/CreateBusinessStatusArgs.cs`, `.../Aggregates/UpdateBusinessStatusArgs.cs`, `tests/UnitTests/Contexts/BusinessStatus/Domain/BusinessStatusAggregateTests.cs`.
- **Detalle:** firma y constantes en §5.1. `Create` valida, en este orden, y **acumula** todos los errores que fallen (no se detiene en el primero) para retornarlos juntos vía `DomainError.FromValidationDomainErrors`:
  1. `Name`: `NameRequired` si es nulo/vacío/solo espacios; si no, `NameTooLong` si `Name.Trim().Length > MaxNameLength` (200) — **este chequeo es nuevo respecto a la primera versión del plan**, que solo lo dejaba en el validador estructural.
  2. `Percentage`: `ValidatePercentageForCreate(value)` → `Result<int, ValidationError>`. Falla con `PercentageOutOfRange` fuera de `[MinPercentage, MaxPercentage]`, `PercentageMustBeInteger` si `decimal.Truncate(value) != value`, `TerminalPercentageNotAllowed` si el valor (ya confirmado entero) es exactamente 0 o 100 (INV-1). Si tiene éxito, devuelve el `int` que el agregado guarda en `Percentage`.
  3. `Color`: si `args.Color` no es nulo/vacío, `StatusColor.Create(args.Color)` → `InvalidColorFormat` si falla — **wiring que también faltaba** en la primera versión (el VO existía, pero nada en `Create`/`Update` lo invocaba). Nulo/vacío no genera VO ni error.

  `Update` repite 1 y 3 igual que `Create`. Para 2 aplica INV-1 e INV-2: si el estado almacenado es terminal (`IsTerminal` antes de aplicar el cambio), el porcentaje entrante debe ser **igual** al almacenado (`TerminalPercentageIsImmutable`, igualdad exacta de `int`, sin tolerancia); si no lo es, el porcentaje entrante pasa por `ValidatePercentageForCreate` (misma regla que el alta). `EnsureCanBeDeleted` retorna `TerminalCannotBeDeleted` cuando `IsTerminal == true` (INV-3). `Reconstruct` acepta `int?` y `string?` y no valida nada (D5): ni longitud de nombre, ni formato de color, ni rango de porcentaje — los datos persistidos ya existen, y la conversión desde el `decimal?` real de la columna ya ocurrió en el mapper antes de llegar aquí (F2.3). `Created()` fija `CreatedAt`/`UpdatedAt` en UTC; `Update` refresca `UpdatedAt`.
- **Hecho cuando:** los tests cubren: alta válida; alta con nombre vacío **y** porcentaje 100 devuelve **los dos** errores; alta con nombre de 201 caracteres devuelve `NameTooLong` **sin** llegar a persistencia; alta con color `"zzzzzz"` devuelve `InvalidColorFormat`; alta con `50,5` devuelve `PercentageMustBeInteger`; alta con 0 y con 100 rechazadas; edición de un terminal que cambia el porcentaje rechazada; edición de un terminal que cambia solo nombre y color aceptada (el porcentaje se reenvía igual y no dispara `TerminalPercentageIsImmutable`); edición con nombre de 201 caracteres rechazada igual que en el alta; `EnsureCanBeDeleted` sobre 0, 100 y 50; `Reconstruct` con nombre nulo, porcentaje nulo y color nulo no lanza, y **tampoco valida** un nombre reconstruido de más de 200 caracteres ni un color reconstruido con formato inválido (dato legado tolerado, D5).
- **Verificar:** `dotnet test tests/UnitTests/UnitTests.csproj --filter FullyQualifiedName~BusinessStatusAggregateTests`

#### [F1.5] Create domain query filter and enums
`id: F1.5 · depende_de: F1.4 · tarea: (sin asignar) rama feat/business-status-aggregate · estado: done`
- **Objetivo:** declarar el objeto de filtro y los dos enums del dominio.
- **Fuente:** D8 · §5.3 · Discovery §9 (filtro de etapas intermedias, en alcance).
- **Archivos:** `src/Contexts/BusinessStatus/Domain/Queries/BusinessStatusFilter.cs`, `.../Domain/Enums/BusinessStatusKind.cs`, `.../Domain/Enums/TerminalKind.cs`.
- **Detalle:** `BusinessStatusFilter(string? Name, bool? IsActive, BusinessStatusKind Kind)` como `sealed record`. `BusinessStatusKind { All = 0, Intermediate = 1, Terminal = 2 }`. `TerminalKind { Won = 1, Lost = 2 }`.
- **Hecho cuando:** los tres tipos existen y el proyecto de dominio compila.
- **Verificar:** `dotnet build src/Contexts/BusinessStatus/Domain/BusinessStatus.Domain.csproj`

#### [F1.6] Create IBusinessStatusRepository
`id: F1.6 · depende_de: F1.5 · tarea: (sin asignar) rama feat/business-status-aggregate · estado: done`
- **Objetivo:** que el dominio declare su contrato de persistencia, sin conocer EF Core.
- **Fuente:** §5.3 · D6 · D7 · `docs/plantilla/repositorio.md`.
- **Archivos:** `src/Contexts/BusinessStatus/Domain/Repositories/IBusinessStatusRepository.cs`.
- **Detalle:** firma completa en §5.3. Extiende `IRootRepository<BusinessStatusAggregate, int>`. No lleva sufijo `Port` y no vive en `Ports/`.
- **Hecho cuando:** la interfaz compila y declara `GetAsync`, `CreateAsync` y `GetActiveTerminalsAsync` además de los seis miembros heredados.
- **Verificar:** `dotnet build Service.slnx`

---

### Fase 2 — Persistencia

**Estrategia de pruebas:** el mapper se prueba unitariamente en ambos sentidos, incluida la tolerancia a nulos (nombre, porcentaje, color, actividad). El repositorio **no** se prueba unitariamente contra EF In-Memory: sus reglas reales (orden con desempate, `OFFSET/FETCH`, 547) solo se verifican en la fase 6 con SQL Server real.

> **Ajuste de la estrategia — desarrollador, 2026-08-21.** Se **sí** prueba el repositorio unitariamente contra EF In-Memory (33 casos, `BusinessStatusRepositoryTests`), para sostener el umbral de cobertura de línea del 90 % que exige el gate de CI (`docs/plantilla/testing.md`; solo los unit tests cuentan). Lo que ese proveedor **no** reproduce sigue siendo exclusivo de la Fase 9: el SQL real de `OFFSET/FETCH`, la columna `decimal(20,5)`, el ordenamiento de `NULL` del motor y el error 547 de las FKs entrantes. Los fallos de escritura se alcanzan con un `SaveChangesInterceptor` que lanza la excepción elegida; `SqlException` no se puede construir en un test (no tiene constructor público, como ya documenta `SqlServerErrorClassifierTests`), así que su único `catch` —el de `RemoveAsync`— queda sin cubrir a propósito.

#### [F2.1] Create the BusinessStatus persistence entity
`id: F2.1 · depende_de: F1.6 · tarea: (sin asignar) rama feat/business-status-persistence-mapping · estado: done`
- **Objetivo:** reflejar la fila real de `tbl_opo_negocios_estados`, con su nulabilidad real y sin reglas.
- **Fuente:** §4.1 · D1 · D14 (d) · `DESVIACIÓN-1` (§5.6).
- **Archivos:** `src/Infrastructure/Persistence/EntityFramework/BusinessStatuses/Entities/BusinessStatus.cs`.
- **Detalle:** clase sellada con propiedades públicas mutables: `int Id`, `string? Name`, `bool? IsActive`, `decimal? Percentage`, `string? Color`. Constructor público sin parámetros. Sin navegaciones hacia `tbl_opo_negocios` ni hacia el historial (§4.1).
- **Hecho cuando:** el tipo existe con las cinco propiedades y la nulabilidad de la tabla, y el proyecto compila. El nombre es `BusinessStatus`, sin sufijo: `DESVIACIÓN-1` quedó **rechazada** y la colisión se resuelve calificando cada uso (§5.6).
- **Verificar:** `dotnet build src/Infrastructure/Infrastructure.csproj`

#### [F2.2] Create BusinessStatusConfiguration
`id: F2.2 · depende_de: F2.1 · tarea: (sin asignar) rama feat/business-status-persistence-mapping · estado: done`
- **Objetivo:** mapear la entidad a la tabla legada con los nombres exactos de columna.
- **Fuente:** §4.1 · D1 · `docs/plantilla/repositorio.md`.
- **Archivos:** `src/Infrastructure/Persistence/EntityFramework/BusinessStatuses/Configurations/BusinessStatusConfiguration.cs`.
- **Detalle:**
  ```csharp
  builder.ToTable("tbl_opo_negocios_estados");
  builder.HasKey(x => x.Id);
  builder.Property(x => x.Id).HasColumnName("negest_consecutivoP").ValueGeneratedOnAdd();
  builder.Property(x => x.Name).HasColumnName("negest_nombre").HasMaxLength(200).IsUnicode(false);
  builder.Property(x => x.IsActive).HasColumnName("negest_estado");
  builder.Property(x => x.Percentage).HasColumnName("negest_porcentaje").HasPrecision(20, 5);
  builder.Property(x => x.Color).HasColumnName("negest_color").HasMaxLength(20).IsUnicode(false);
  ```
  `HasMaxLength` e `IsUnicode` están para que EF genere `varchar` del largo correcto contra el esquema real, **no** como validación (la validación vive en el dominio). Ninguna propiedad se declara `IsRequired()`: la tabla admite nulos en las cuatro columnas no clave.
- **Hecho cuando:** la configuración se descubre por `ApplyConfigurationsFromAssembly` y la solución compila. **No se genera ninguna migración** (D1).
- **Verificar:** `dotnet build Service.slnx`

#### [F2.3] Create BusinessStatusRepositoryMapper
`id: F2.3 · depende_de: F2.2 · tarea: (sin asignar) rama feat/business-status-persistence-mapping · estado: done`
- **Objetivo:** traducir en ambos sentidos entre el agregado y la fila, absorbiendo la nulabilidad real.
- **Fuente:** §4.1 · D5 · D14 (c, d).
- **Archivos:** `src/Infrastructure/Persistence/EntityFramework/BusinessStatuses/Mappers/BusinessStatusRepositoryMapper.cs`, `tests/UnitTests/Infrastructure/Persistence/BusinessStatuses/BusinessStatusRepositoryMapperTests.cs`.
- **Detalle:** `ToDomain(Entities.BusinessStatus row)` convierte el porcentaje **antes** de llamar `Reconstruct`: `int? percentage = row.Percentage.HasValue && decimal.Truncate(row.Percentage.Value) == row.Percentage.Value ? (int)row.Percentage.Value : null` — un valor persistido que no sea un entero exacto (dato legado sucio) se trata igual que un `NULL`, nunca se redondea (D5: evita clasificar un `0,4` sucio como «Perdido»). Luego llama `BusinessStatusAggregate.Reconstruct(row.Id, row.Name ?? string.Empty, percentage, row.Color, row.IsActive ?? false)` — nunca `Create`. `ToDocument(BusinessStatusAggregate aggregate)` escribe `Name`, `Percentage = (decimal?)aggregate.Percentage` (conversión inversa, siempre exacta porque el agregado solo contiene enteros), `Color = aggregate.Color?.Value` (nulo si no hay color: **nunca** `CCCCCC`) e `IsActive`. `Id` no se asigna en el alta (identity).
- **Hecho cuando:** los tests cubren ida y vuelta con todos los campos poblados; lectura de una fila con `Name`, `Percentage`, `Color` e `IsActive` nulos sin lanzar; y lectura de una fila con `Percentage = 0.4m` (dato sucio) que produce `Percentage = null` en el dominio, no `0`.
- **Verificar:** `dotnet test tests/UnitTests/UnitTests.csproj --filter FullyQualifiedName~BusinessStatusRepositoryMapperTests`

#### [F2.4] Register the DbSet
`id: F2.4 · depende_de: F2.3 · tarea: (sin asignar) rama feat/business-status-persistence-mapping · estado: done`
- **Objetivo:** exponer la entidad en el `DbContext`.
- **Fuente:** template (`ApplicationDbContext`).
- **Archivos:** `src/Infrastructure/Persistence/EntityFramework/ApplicationDbContext.cs`.
- **Detalle:** `public DbSet<BusinessStatuses.Entities.BusinessStatus> BusinessStatuses => Set<BusinessStatuses.Entities.BusinessStatus>();`
- **Hecho cuando:** el `DbSet` existe y `ApplicationDbContextTests` sigue en verde.
- **Verificar:** `dotnet test tests/UnitTests/UnitTests.csproj --filter FullyQualifiedName~ApplicationDbContextTests`

#### [F2.5] Implement BusinessStatusRepository read operations
`id: F2.5 · depende_de: F2.4 · tarea: (sin asignar) rama feat/business-status-repository · estado: done`
- **Objetivo:** implementar las lecturas del repositorio con orden total y paginación en la consulta.
- **Fuente:** D7 · D8 · D6 · §5.3 · `docs/plantilla/repositorio.md`.
- **Archivos:** `src/Infrastructure/Persistence/EntityFramework/BusinessStatuses/BusinessStatusRepository.cs`.
- **Detalle:** clase sellada `BusinessStatusRepository(ApplicationDbContext context, ILoggerPort<BusinessStatusRepository> logger) : IBusinessStatusRepository`, con `private const string Origin = nameof(BusinessStatusRepository)`. Implementa `GetByIdAsync` (404 con `BusinessStatusErrors.NotFound(id)`), `ExistsAsync`, `GetAsync(filter, page)`, `GetAllAsync(page)` (delega en `GetAsync` con filtro vacío) y `GetActiveTerminalsAsync(kind)`. Todas con `AsNoTracking()`. Orden **siempre** `OrderBy(x => x.Percentage).ThenBy(x => x.Id)` (D7). Filtros: `Name` → `EF.Functions.Like(x.Name, $"%{name}%")`; `IsActive` → igualdad cuando no es nulo; `Kind` → `Intermediate` excluye `Percentage == 0` y `== 100`, `Terminal` los incluye únicamente. `GetActiveTerminalsAsync` filtra `IsActive == true` y el porcentaje del `TerminalKind` pedido, y devuelve la lista completa sin decidir. Cada método envuelve en `try/catch` con `logger.Error` y `PersistenceErrors.Failure(Origin)`; `OperationCanceledException` se deja propagar.
- **Hecho cuando:** los cinco métodos de lectura están implementados, ninguno lanza al llamador, y la solución compila. Ninguna consulta ordena por un solo criterio.
- **Verificar:** `dotnet build Service.slnx`

#### [F2.6] Implement BusinessStatusRepository write operations
`id: F2.6 · depende_de: F2.5 · tarea: (sin asignar) rama feat/business-status-repository · estado: done`
- **Objetivo:** implementar alta, edición y borrado, devolviendo el identity y clasificando el conflicto de FK.
- **Fuente:** D9 · D10 · Discovery §7 D-22, D-26, D-29 · `docs/plantilla/repositorio.md`.
- **Archivos:** `src/Infrastructure/Persistence/EntityFramework/BusinessStatuses/BusinessStatusRepository.cs`.
- **Detalle:** `CreateAsync` agrega la fila, hace `SaveChangesAsync` dentro del repositorio y asigna al agregado el `Id` que pobló el identity (corrige D-26); por eso el caso de uso de alta **no** inyecta `IUnitOfWorkPort`. `AddAsync` queda implementado como encolado simple para completar el contrato. `Update` escribe **todas** las propiedades del agregado sobre la fila rastreada (evita el `SET` destructivo de D-22). `RemoveAsync(int id)` resuelve la fila y falla con `NotFound` si no existe. En los `catch`: `DbUpdateException` → `SqlServerErrorClassifier.Classify(ex, Origin)`; **también** `SqlException` con su propia sobrecarga, porque un borrado que no pase por el change tracker no envuelve la excepción; el resto → `PersistenceErrors.Failure(Origin)`. El 547 queda clasificado como `Conflict` → 409 (D-29).
- **Ejecución (2026-08-21) — se agrega `AssignId` al agregado.** `Entity<TId>.Id` tiene `protected set` y F1.4 no había declarado ningún mutador, así que la primera implementación devolvía el agregado **re-hidratado** desde la fila insertada (`ToDomain(row)`). El desarrollador la rechazó: divergía del patrón canónico de `repositorio.md` (§ `CreateAsync`) y perdía el `CreatedAt`/`UpdatedAt` que fijó `Create()` — hoy inocuo (D14 b, R-7: no se persisten ni salen en el contrato §6), pero roto en cuanto el contrato exponga esas fechas. Se agrega entonces `public void AssignId(int id)` a `BusinessStatusAggregate` (§5.1) y `CreateAsync` queda como la plantilla: `SaveChangesAsync` → `aggregate.AssignId(row.Id)` → `return aggregate`. Implica tocar un archivo de la Fase 1, decisión explícita del desarrollador. **No** se toca `Entity<TId>`: §5.5 cierra que este plan no abre PR de `Shared`, y el mutador solo lo necesita este agregado. `Update` sigue asignando `row.Id = aggregate.Id` en el repositorio, porque el mapper deja el identificador fuera para el alta.
- **Corrección (2026-08-21) — los tres `catch` no aplican a las cuatro escrituras.** Solo se conservan los `catch` que pueden dispararse; el resto era código inalcanzable que además hundía la cobertura del archivo por debajo del 90 %:

  | Escritura | ¿Toca la base? | `catch` presentes |
  |---|---|---|
  | `CreateAsync` | Sí: `SaveChangesAsync` | `DbUpdateException` → genérico, el patrón canónico de `repositorio.md`. **No** lleva `SqlException`: `SaveChangesAsync` pasa por el change tracker y envuelve siempre el error del driver en `DbUpdateException`; la `SqlException` cruda solo la levantan `ExecuteDelete`/`ExecuteUpdate` (`repositorio.md` 508-512), que este repositorio no usa |
  | `RemoveAsync` | Sí: el `SELECT` que resuelve la fila | `SqlException` → genérico. Acá sí aplica: la consulta puede fallar con una `SqlException` sin envolver. El `DELETE` —y su 547— los levanta el Unit of Work, que ya clasifica `DbUpdateException` |
  | `AddAsync` | No: la clave es IDENTITY, EF no consulta nada antes del `INSERT` | genérico |
  | `Update` | No: solo marca estado en el change tracker | genérico |

  En los cuatro casos el método sigue sin lanzar al llamador y sigue devolviendo `PersistenceErrors.Failure(Origin)` ante lo inesperado; el 409 por FK sigue llegando clasificado (D9), ahora desde `UnitOfWorkAdapter.CommitAsync` en el borrado y desde `CreateAsync` en el alta.
- **Hecho cuando:** `CreateAsync` devuelve el agregado con `Id > 0`, `Update` no deja campos sin escribir, y cada escritura tiene los `catch` que su camino real puede disparar, en el orden `DbUpdateException` → `SqlException` → genérico.
- **Verificar:** `dotnet build Service.slnx`

#### [F2.7] Scaffold BusinessStatus DI registration
`id: F2.7 · depende_de: F2.6 · tarea: (sin asignar) rama feat/business-status-repository · estado: done`
- **Objetivo:** dejar el punto de registro de dependencias del contexto listo para que cada slice de la Fase 3 en adelante agregue **una sola línea propia**, sin que ninguna dependa de que otra slice se implemente primero.
- **Fuente:** `docs/plantilla/contextos.md` §5.5 · `docs/plantilla/repositorio.md` (lifetimes).
- **Archivos:** `src/Api/DependencyInjection/BusinessStatusServiceExtensions.cs`, `src/Api/DependencyInjection/ApplicationServiceExtensions.cs`.
- **Detalle:** `AddBusinessStatusServices()` registra únicamente `IBusinessStatusRepository → BusinessStatusRepository` (`Scoped`) y se invoca desde `AddApplicationServices()`. **No** registra ningún caso de uso ni el provider todavía — eso lo hace cada slice (Fases 3-8), agregando su propia línea a este mismo método.
- **Hecho cuando:** el servicio arranca y responde 200 en `/health/ready` sin ningún caso de uso registrado todavía.
- **Verificar:** `dotnet build Service.slnx`

---

### Fase 3 — Slice: Listar estados (`GET /business-statuses`)

**Por qué es la primera slice:** es la única operación de solo lectura sin parámetro de ruta, y establece la convención de paginación (D8) que las demás heredan. Cada slice de aquí en adelante entrega **de punta a punta** (caso de uso + DTOs + mapping + endpoint + validador + registro DI + caché propia + tests) en una sola tarea/rama, en vez de repartir "todos los casos de uso" y "todos los endpoints" en fases separadas.

**Estrategia de pruebas:** unitarias del caso de uso con doble del repositorio; unitarias del controller con doble del caso de uso; unitarias del validador sobre sus fronteras; unitarias de la caché con un doble de `ICacheStore`. La verificación de extremo a extremo es de la Fase 9.

#### [F3] Implement and expose `GET /business-statuses` (list)
`id: F3 · depende_de: F2.7 · tarea: (sin asignar) rama feat/business-status-list · estado: pending`
- **Objetivo:** entregar el listado de punta a punta como una sola unidad de trabajo: caso de uso, endpoint, validador de entrada y caché L2.
- **Fuente:** D8 · D12 · D15 · §6.1 · §6.3 · §7.2 · `docs/plantilla/casos-de-uso.md` · `docs/plantilla/controllers.md` · `docs/plantilla/validaciones.md` · `docs/plantilla/cache.md`.
- **Archivos:** `src/Contexts/BusinessStatus/Application/UseCases/GetBusinessStatuses/` → `IGetBusinessStatusesUseCase.cs`, `GetBusinessStatusesUseCase.cs`, `GetBusinessStatusesInputDto.cs`, `GetBusinessStatusesOutputDto.cs`, `GetBusinessStatusesMapping.cs`; `src/Api/Controllers/BusinessStatusesController.cs` (se crea acá); `src/Api/Validators/GetBusinessStatusesInputValidator.cs`; `src/Api/DependencyInjection/BusinessStatusServiceExtensions.cs`; `src/Infrastructure/Persistence/EntityFramework/BusinessStatuses/BusinessStatusRepository.cs`; `src/Infrastructure/Persistence/EntityFramework/BusinessStatuses/BusinessStatusListSnapshot.cs`; tests en `tests/UnitTests/Contexts/BusinessStatus/Application/GetBusinessStatusesUseCaseTests.cs`, `tests/UnitTests/Api/Controllers/BusinessStatusesControllerTests.cs`, `tests/UnitTests/Api/Validators/BusinessStatusValidatorsTests.cs`, `tests/UnitTests/Infrastructure/Persistence/BusinessStatuses/BusinessStatusRepositoryCacheTests.cs`.
- **Detalle:**
  1. **Caso de uso:** `ExecuteAsync(GetBusinessStatusesInputDto filter, PageQuery page, CancellationToken)` → `PagedResult<GetBusinessStatusesOutputDto>`. El mapping traduce el DTO a `BusinessStatusFilter` y el agregado a `GetBusinessStatusesOutputDto(int Id, string Name, int? Percentage, string? Color, bool IsActive)` — el porcentaje se copia directo de `aggregate.Percentage` (ya es `int?`, D5; sin conversión ni redondeo en esta capa, eso ya ocurrió en el mapper de persistencia, F2.3) y el color se devuelve crudo, nulo incluido (D14 c). Todas las propiedades llevan `[property: Description(...)]`.
  2. **Endpoint:** el controller nace con `[ApiController]`, `[Route("[controller]")]`, `[Tags("business-statuses")]` a nivel de clase, `private const string CacheTag = "business-statuses"` (lo reutilizan las demás slices sobre este mismo archivo) y **una sola** acción por ahora: `GetBusinessStatuses([FromQuery] GetBusinessStatusesInputDto filter, [FromQuery] PageQueryInputDto pagination)` → `HttpOkPagedResult<GetBusinessStatusesOutputDto>`, con `[ValidateRequest]`, `[EndpointSummary]`, `[EndpointDescription]`, los `[ProducesResponseType]` de §6.1, y `[OutputCache(NoStore = true)]` (la política base no varía por los filtros, D12). **Sin atributos de autorización** (D15). `GetBusinessStatusesInputValidator` hereda `AbstractValidator<T>` e implementa `IStructuralValidator<T>`; reglas de §6.3 (`name` ≤ 200, `kind` en el enum) — no rechaza 0/100, eso es INV-1 del dominio. `AddBusinessStatusServices()` (F2.7) agrega su primera línea: `services.AddScoped<IGetBusinessStatusesUseCase, GetBusinessStatusesUseCase>();`.
  3. **Caché L2** (corrige D-12): el repositorio recibe `ICacheStore` por constructor. En `GetAsync`: se construye la llave `CacheKey.For("businessstatus").Tenant(tenantCode).Resource("list", key)` donde `key` es un hash estable del filtro y de la página; `GetAsync<BusinessStatusListSnapshot>` primero, y en miss se consulta y se pobla con `SetAsync(..., TimeSpan.FromMinutes(10))`. **Se cachea el snapshot** (`sealed record BusinessStatusListSnapshot(IReadOnlyList<BusinessStatusSnapshotItem> Items, int TotalCount)`, plano y con constructor público), **nunca el agregado**: su constructor es privado y `System.Text.Json` no puede reconstruirlo. Solo se cachean éxitos; si no hay tenant resuelto, no se cachea.
- **Hecho cuando:** los tests del caso de uso cubren listado sin filtros, filtro por nombre, filtro `kind = Intermediate`, `TotalCount` propagado y error del repositorio propagado tal cual; el endpoint responde `200` con `HttpOkPagedResult` y el validador cubre cada regla de §6.3; los tests de caché verifican hit (una sola consulta para dos llamadas iguales), miss por filtro distinto, y que un `Result` fallido no invoque `SetAsync`; el documento OpenAPI lista la acción bajo el tag `business-statuses`.
- **Verificar:** `dotnet test tests/UnitTests/UnitTests.csproj --filter "FullyQualifiedName~GetBusinessStatusesUseCaseTests|BusinessStatusesControllerTests|BusinessStatusValidatorsTests|BusinessStatusRepositoryCacheTests"`

---

### Fase 4 — Slice: Detalle por id (`GET /business-statuses/{id}`)

#### [F4] Implement and expose `GET /business-statuses/{id}` with L1 cache
`id: F4 · depende_de: F3 · tarea: (sin asignar) rama Feature/business-status-get-by-id-useCase · estado: done`
- **Objetivo:** entregar el detalle de punta a punta: caso de uso con 404 explícito y endpoint cacheado por `id`, con el tag listo para que las slices de escritura lo invaliden.
- **Fuente:** Discovery §7 D-15 · §6.1 · §6.4 · D12 · D15 · `docs/plantilla/controllers.md` · `docs/plantilla/cache.md`.
- **Archivos:** `src/Contexts/BusinessStatus/Application/UseCases/GetBusinessStatusById/` → interfaz, use case, `GetBusinessStatusByIdOutputDto.cs`, mapping; `src/Api/Controllers/BusinessStatusesController.cs` (se le agrega esta segunda acción); `src/Api/DependencyInjection/BusinessStatusServiceExtensions.cs`; tests en `tests/UnitTests/Contexts/BusinessStatus/Application/GetBusinessStatusByIdUseCaseTests.cs`, `tests/UnitTests/Api/Controllers/BusinessStatusesControllerTests.cs`.
- **Detalle:**
  1. **Caso de uso:** `ExecuteAsync(int id, CancellationToken)` → `Result<GetBusinessStatusByIdOutputDto>`. El `NotFound` lo produce el repositorio con su `Origin`; el caso de uso lo propaga sin reescribirlo. No hay `InputDto`: el único parámetro es el id de la ruta.
  2. **Endpoint:** `GetBusinessStatusById([FromRoute] int id)` → `HttpOkResult<GetBusinessStatusByIdOutputDto>`, con `[ProducesResponseType]` 200/404 y `[OutputCache(Duration = 300, Tags = [CacheTag], VaryByRouteValueNames = ["id"])]` — reutiliza el `CacheTag` declarado en F3, no declara uno nuevo. `AddBusinessStatusServices()` agrega `services.AddScoped<IGetBusinessStatusByIdUseCase, GetBusinessStatusByIdUseCase>();`.
- **Ejecución (2026-08-21):** entregado sobre la rama del PR #17 (slice de creación), **sin la slice de F3 en la base** — la de listado vive en su propia rama sin mergear. No hubo dependencia real: el `CacheTag` que F4 debía reutilizar ya lo declaraba el controller de F5, y el detalle no usa `PagedPayload`. Al mergear ambas ramas habrá conflicto textual —no semántico— en `BusinessStatusesController`, `BusinessStatusServiceExtensions` y `BusinessStatusesControllerTests`, porque las dos slices agregan miembros a los mismos archivos.
  - **Sin validador de `id`.** §6.3 lista `GreaterThan(0)` para el id de ruta, pero F4 no declara archivo de validador y no se agregó ninguno: `ValidateRequestFilter` salta los tipos simples (`IsSimpleType`), así que un validador de un `int` suelto nunca correría. Un id ≤ 0 o inexistente responde 404, y uno no numérico lo rechaza el *model binding* con 400 y `Property = "id"`.
  - **Se agrega `BusinessStatusServiceExtensionsTests`** (fuera de la lista de archivos del paso): verifica que las tres registraciones del contexto existan, apunten a su implementación y sean `Scoped`. Se sumó porque en F3 una registración faltante dejó el endpoint devolviendo 500 sin que ninguna prueba lo notara — todas inyectan dobles y no pasan por el contenedor.
- **Hecho cuando:** los tests cubren id existente e id inexistente (404 con `ErrorType.NotFound`); el endpoint responde 200/404 según corresponda, y el tag de caché es **el mismo objeto de cadena** que usarán las invalidaciones de F5-F7.
- **Verificar:** `dotnet test tests/UnitTests/UnitTests.csproj --filter "FullyQualifiedName~GetBusinessStatusByIdUseCaseTests|BusinessStatusesControllerTests"`

---

### Fase 5 — Slice: Crear estado (`POST /business-statuses`)

#### [F5] Implement and expose `POST /business-statuses`
`id: F5 · depende_de: F4 · tarea: (sin asignar) rama Feature/business-status-create-use-case · estado: done`
- **Objetivo:** entregar el alta de punta a punta: caso de uso con invalidación de caché post-commit, validador y endpoint.
- **Fuente:** D3 · D10 · D12 · D15 · §6.1 · §6.3 · Discovery §7 D-05, D-14, D-26 · `docs/plantilla/controllers.md` · `docs/plantilla/validaciones.md`.
- **Archivos:** `src/Contexts/BusinessStatus/Application/UseCases/CreateBusinessStatus/` → interfaz, use case, `CreateBusinessStatusInputDto.cs`, `CreateBusinessStatusOutputDto.cs`, mapping; `src/Api/Controllers/BusinessStatusesController.cs` (se le agrega esta tercera acción); `src/Api/Validators/CreateBusinessStatusInputValidator.cs`; `src/Api/DependencyInjection/BusinessStatusServiceExtensions.cs`; tests en `tests/UnitTests/Contexts/BusinessStatus/Application/CreateBusinessStatusUseCaseTests.cs`, `tests/UnitTests/Api/Controllers/BusinessStatusesControllerTests.cs`, `tests/UnitTests/Api/Validators/BusinessStatusValidatorsTests.cs`.
- **Detalle:**
  1. **Caso de uso:** patrón `input.ToAggregate()` → si falla, sella con `Context` y `Origin`, 400 → `repository.CreateAsync(...)` → si tiene éxito, `cacheStore.RemoveByPrefixAsync(CacheKey.For("businessstatus").Tenant(tenantCode).Prefix("list"))` (invalidación L2 post-commit, D12 — **nunca** desde el repositorio) → retorna `CreateBusinessStatusOutputDto`. **No** inyecta `IUnitOfWorkPort`: `CreateAsync` ya confirmó (F2.6). No hay chequeo de unicidad de nombre (D14 a). Si la escritura falla, no se invalida nada.
  2. **Endpoint:** `CreateBusinessStatus([FromBody] CreateBusinessStatusInputDto input)` → `HttpCreatedResult<CreateBusinessStatusOutputDto>`, con `[ValidateRequest]` y `[OutputCacheInvalidate(CacheTag)]` (mismo `CacheTag` de F3). `CreateBusinessStatusInputValidator`: reglas de §6.3 (`name` requerido y ≤ 200, `percentage` en `[0, 100]`, `color` con el formato hex cuando viene) — no rechaza 0/100, eso es INV-1 del dominio. `AddBusinessStatusServices()` agrega `services.AddScoped<ICreateBusinessStatusUseCase, CreateBusinessStatusUseCase>();`.
- **Hecho cuando:** alta válida devuelve el `Id` asignado y dispara la invalidación; alta con porcentaje 100 responde error de validación, **no** llama al repositorio y **no** invalida caché; alta con nombre vacío y porcentaje 0 devuelve los dos errores acumulados; el endpoint responde 201/400 según corresponda, y el validador cubre cada regla de §6.3.
- **Verificar:** `dotnet test tests/UnitTests/UnitTests.csproj --filter "FullyQualifiedName~CreateBusinessStatusUseCaseTests|BusinessStatusesControllerTests|BusinessStatusValidatorsTests"`
- **Ejecución (2026-08-21).** Cuatro desviaciones respecto de lo escrito arriba, todas registradas:

  1. **Orden.** Se ejecutó F5 con F3 y F4 en `pending`, por instrucción explícita del desarrollador («no hay dependencias entre sí, ejecuta F5; F3 y F4 los realizará otro dev»). El `depende_de: F4` de este paso queda como dependencia **documental**, no funcional.
  2. **`BusinessStatusesController.cs` nace en esta slice, no en F3.** El archivo no existía, así que esta slice lo crea con `[ApiController]`, `[Route("[controller]")]`, `[Tags("business-statuses")]`, `private const string CacheTag = "business-statuses"` y **una sola** acción (`CreateBusinessStatus`). F3 y F4 agregan las suyas sobre este mismo archivo y **reutilizan** el `CacheTag` ya declarado, sin declarar otro. `KebabCaseParameterTransformer` resuelve `BusinessStatuses` → `business-statuses`.
  3. **`DESVIACIÓN-2` — ubicación del validador.** El validador **no** va en `src/Api/Validators/` como dice la lista de archivos de este paso, sino en `src/Infrastructure/Validation/FluentValidation/BusinessStatuses/CreateBusinessStatusInputValidator.cs`. Razón funcional, no estética: `ValidatorRegistrationExtensions.AddContextValidators()` escanea **únicamente el ensamblado `Infrastructure`**, de modo que un `IStructuralValidator<T>` colocado en `Api` nunca se registraría y `[ValidateRequest]` no validaría nada, en silencio. La ubicación elegida es además la que ya usa la plantilla (`docs/plantilla/validaciones.md`) y la que la propia §5.5 de este plan cita para `PageQueryInputValidator`. Su test va en `tests/UnitTests/Infrastructure/Validation/BusinessStatusValidatorsTests.cs`, junto a `PageQueryInputValidatorTests`, y no en `tests/UnitTests/Api/Validators/`. La lista de archivos de F6 (`src/Api/Validators/UpdateBusinessStatusInputValidator.cs`) arrastra el mismo error y debe corregirse igual.
  4. **La invalidación L2 no se implementó — ver `GAP-B` (§9.2) y R-10 (§9.1).** El caso de uso **no** inyecta `ICacheStore` y **no** llama `RemoveByPrefixAsync`. Motivo: la llave de §7.2 está particionada por tenant (`CacheKey.For("businessstatus").Tenant(tenantCode)`) y **el código de tenant no es accesible desde la capa de aplicación**: `TenantMiddleware` lo lee del header `X-Entity-Code` y solo propaga el *connection string* a través de `ITenantConnectionInitializer`/`TenantContext`; `TenantInfo.EntityCode` muere dentro del middleware. Exponerlo exige tocar `Api/Session/**`, `TenantMiddleware` y `SessionServiceExtensions` — fuera de la lista de archivos de este paso y contra §0.5. Se descartó invalidar con un prefijo sin `.Tenant(...)`: no coincidiría con ninguna llave que escriba F3 y dejaría una invalidación rota en silencio, que es peor que no tenerla. **La invalidación L1 sí quedó implementada** (`[OutputCacheInvalidate(CacheTag)]`), que es lo único que este endpoint puede invalidar hoy: la caché L2 del listado todavía no existe porque la crea F3.
- **Ejecutado:** `dotnet build Service.slnx` en verde; `dotnet test tests/UnitTests/UnitTests.csproj` → **488/488**, de los cuales **32** son nuevos de esta slice (10 del caso de uso, 3 del controller, 19 del validador).

---

### Fase 6 — Slice: Editar estado (`PUT /business-statuses/{id}`)

#### [F6] Implement and expose `PUT /business-statuses/{id}`
`id: F6 · depende_de: F5 · tarea: (sin asignar) rama feat/business-status-update · estado: pending`
- **Objetivo:** entregar la edición de punta a punta: caso de uso con la misma invariante que el alta más la inmutabilidad del terminal, invalidación de caché, validador y endpoint.
- **Fuente:** D3 (INV-1, INV-2) · D12 · D15 · §6.1 · §6.3 · Discovery §7 D-05, D-22 · `docs/plantilla/controllers.md` · `docs/plantilla/validaciones.md`.
- **Archivos:** `src/Contexts/BusinessStatus/Application/UseCases/UpdateBusinessStatus/` → interfaz, use case, `UpdateBusinessStatusInputDto.cs`, `UpdateBusinessStatusOutputDto.cs`, mapping; `src/Api/Controllers/BusinessStatusesController.cs` (se le agrega esta cuarta acción); `src/Api/Validators/UpdateBusinessStatusInputValidator.cs`; `src/Api/DependencyInjection/BusinessStatusServiceExtensions.cs`; tests en `tests/UnitTests/Contexts/BusinessStatus/Application/UpdateBusinessStatusUseCaseTests.cs`, `tests/UnitTests/Api/Controllers/BusinessStatusesControllerTests.cs`, `tests/UnitTests/Api/Validators/BusinessStatusValidatorsTests.cs`.
- **Detalle:**
  1. **Caso de uso:** `GetByIdAsync` → si falla, propaga (404) → `aggregate.Update(args)` → si falla, sella y retorna 400 → `repository.Update(aggregate)` → `unitOfWork.CommitAsync()` → si tiene éxito, invalida el listado igual que F5 → retorna el DTO. Semántica de reemplazo completo: se escriben los cuatro campos siempre.
  2. **Endpoint:** `UpdateBusinessStatus([FromRoute] int id, [FromBody] UpdateBusinessStatusInputDto input)` → `HttpOkResult<UpdateBusinessStatusOutputDto>`, con `[ValidateRequest]` y `[OutputCacheInvalidate(CacheTag)]`. `UpdateBusinessStatusInputValidator`: mismas reglas estructurales que el alta (§6.3). `AddBusinessStatusServices()` agrega `services.AddScoped<IUpdateBusinessStatusUseCase, UpdateBusinessStatusUseCase>();`.
- **Hecho cuando:** edición válida de un intermedio dispara invalidación; intento de mover un intermedio a 100 rechazado sin invalidar; edición de nombre y color de un terminal aceptada sin tocar el porcentaje; intento de cambiar el porcentaje de un terminal rechazado; id inexistente → 404; fallo del commit propagado sin invalidar; el endpoint responde 200/400/404 según corresponda, y el validador cubre cada regla de §6.3.
- **Verificar:** `dotnet test tests/UnitTests/UnitTests.csproj --filter "FullyQualifiedName~UpdateBusinessStatusUseCaseTests|BusinessStatusesControllerTests|BusinessStatusValidatorsTests"`

---

### Fase 7 — Slice: Eliminar estado (`DELETE /business-statuses/{id}`)

#### [F7] Implement and expose `DELETE /business-statuses/{id}`
`id: F7 · depende_de: F6 · tarea: (sin asignar) rama feat/business-status-delete · estado: pending`
- **Objetivo:** entregar el borrado de punta a punta: caso de uso que protege terminales y clasifica el conflicto por uso, invalidación de caché, y endpoint.
- **Fuente:** D9 (INV-3) · D12 · D15 · §6.1 · Discovery §7 D-06, D-29 · `docs/plantilla/controllers.md`.
- **Archivos:** `src/Contexts/BusinessStatus/Application/UseCases/DeleteBusinessStatus/` → `IDeleteBusinessStatusUseCase.cs`, `DeleteBusinessStatusUseCase.cs`; `src/Api/Controllers/BusinessStatusesController.cs` (se le agrega esta quinta y última acción); `src/Api/DependencyInjection/BusinessStatusServiceExtensions.cs`; tests en `tests/UnitTests/Contexts/BusinessStatus/Application/DeleteBusinessStatusUseCaseTests.cs`, `tests/UnitTests/Api/Controllers/BusinessStatusesControllerTests.cs`.
- **Detalle:**
  1. **Caso de uso:** `GetByIdAsync` → 404 si no existe → `aggregate.EnsureCanBeDeleted()` → 409 `TerminalCannotBeDeleted` si es terminal (sin llamar al repositorio, sin invalidar) → `repository.RemoveAsync(id)` → `unitOfWork.CommitAsync()` → si tiene éxito, invalida el listado igual que F5/F6. El 409 por FK **no** se decide acá: llega ya clasificado desde el repositorio o el Unit of Work y se propaga sin reescribir (D9), sin invalidar. El servicio no consulta `tbl_opo_negocios`.
  2. **Endpoint:** `DeleteBusinessStatus([FromRoute] int id)` → `HttpNoContentResult`, con `[ProducesResponseType]` 204/404/409 y `[OutputCacheInvalidate(CacheTag)]`. `AddBusinessStatusServices()` agrega `services.AddScoped<IDeleteBusinessStatusUseCase, DeleteBusinessStatusUseCase>();` — con esta línea el controller queda con sus **cinco** endpoints y `AddBusinessStatusServices()` con sus **cinco** casos de uso, cada uno agregado en la slice que lo introdujo.
- **Hecho cuando:** borrado de un intermedio dispara invalidación; borrado de un estado al 0 % y al 100 % rechazado con `Conflict` **sin** llamar al repositorio ni invalidar; id inexistente → 404; `Conflict` proveniente del commit propagado tal cual, sin invalidar; el endpoint responde 204/404/409 según corresponda.
- **Verificar:** `dotnet test tests/UnitTests/UnitTests.csproj --filter "FullyQualifiedName~DeleteBusinessStatusUseCaseTests|BusinessStatusesControllerTests"`

---

### Fase 8 — Resolución de terminales (capacidad interna, sin endpoint)

No es una slice HTTP: `TerminalBusinessStatusProvider` no se expone como acción de controller (D6) — su único consumidor será la futura migración de Negocios. Se deja como fase propia, después de que las cinco slices HTTP ya estén completas, precisamente porque no compite por el mismo archivo de controller ni bloquea ninguna de ellas.

#### [F8.1] Implement TerminalBusinessStatusProvider
`id: F8.1 · depende_de: F7 · tarea: (sin asignar) rama feat/business-status-terminal-resolution · estado: pending`
- **Objetivo:** dejar **una sola** operación que resuelva el estado Ganado o Perdido, que filtre por actividad y falle ante ambigüedad.
- **Fuente:** D6 (INV-4) · Discovery §7 D-28, D-07, D-09.
- **Archivos:** `src/Contexts/BusinessStatus/Application/Providers/TerminalBusinessStatusProvider.cs`, `src/Api/DependencyInjection/BusinessStatusServiceExtensions.cs`, `tests/UnitTests/Contexts/BusinessStatus/Application/TerminalBusinessStatusProviderTests.cs`.
- **Detalle:** clase concreta sin interfaz (convención de `providers.md`), registrada `Scoped` — última línea de `AddBusinessStatusServices()`. `Task<Result<BusinessStatusAggregate>> ResolveAsync(TerminalKind kind, CancellationToken)`: llama `repository.GetActiveTerminalsAsync(kind)`; 0 resultados → `TerminalStatusNotFound(kind)` (404); más de 1 → `AmbiguousTerminalStatus(kind)` (409) con `logger.Error` que incluye los identificadores en conflicto; exactamente 1 → el agregado. Nunca retorna nulo.
  > **Sin consumidor dentro de este servicio** (D6, R-3): su consumidor es la futura migración de Negocios. No se expone endpoint y no se llama desde ningún caso de uso.
- **Hecho cuando:** los tests cubren los tres desenlaces y verifican que un terminal **inactivo** nunca se devuelve (escenario exacto de D-28 en `udbzq10trabajos`: fila 5 inactiva al 100 %, fila 21 activa al 100 %).
- **Verificar:** `dotnet test tests/UnitTests/UnitTests.csproj --filter FullyQualifiedName~TerminalBusinessStatusProviderTests`

---

### Fase 9 — Verificación de punta a punta

**Por qué no está dividida por slice:** a diferencia de las Fases 3-8, estos casos ejercitan el stack completo contra SQL Server real (`OFFSET/FETCH` con orden total, `decimal(20,5)`, el error 547) y varios de ellos combinan más de una slice (p. ej. el caso de orden estable necesita datos creados por la Fase 5 y leídos por la Fase 3). Partirlos por slice duplicaría el fixture de esquema sin aportar aislamiento real.

**Estrategia de pruebas:** Testcontainers con SQL Server real, siguiendo `tests/IntegrationTests/Infrastructure/SqlServerContainerFixture.cs` y `DatabaseResetter.cs` ya existentes. Es la única fase que puede verificar lo que EF In-Memory no reproduce.

#### [F9.1] Provision the legacy table for integration tests
`id: F9.1 · depende_de: F8.1 · tarea: (sin asignar) rama test/business-status-integration · estado: pending`
- **Objetivo:** crear en el contenedor de pruebas la tabla legada tal cual existe, con sus nulos y sin constraints.
- **Fuente:** Discovery §4.1, §4.2 `[verificado en BD]` · D1.
- **Archivos:** `tests/IntegrationTests/BusinessStatuses/Schema/tbl_opo_negocios_estados.sql`, `tests/IntegrationTests/BusinessStatuses/BusinessStatusFixture.cs`.
- **Detalle:** el script crea `dbo.tbl_opo_negocios_estados` con las cinco columnas de §4.1, PK clustered identity sobre `negest_consecutivoP`, y **sin** índices adicionales, CHECK, UNIQUE, triggers ni defaults — el objetivo es reproducir la base real, no una mejorada. Semilla con los seis estados del tenant de desarrollo (Discovery §4.4) más, en un caso de prueba dedicado, el escenario roto de `udbzq10trabajos` (dos al 100 %, uno inactivo). **Si el fixture existente no admite aplicar un script de esquema propio**, detenerse y reportar `⚠️ GAP` según §0.4; no improvisar otro mecanismo.
- **Hecho cuando:** la suite de integración levanta el contenedor, crea la tabla y la deja vacía entre pruebas mediante el `DatabaseResetter` existente.
- **Verificar:** `dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter FullyQualifiedName~BusinessStatus`

#### [F9.2] End-to-end tests over the five endpoints
`id: F9.2 · depende_de: F9.1 · tarea: (sin asignar) rama test/business-status-integration · estado: pending`
- **Objetivo:** verificar contra SQL Server real lo que los tests unitarios no pueden: orden, paginación, tipos y el 409 por FK.
- **Fuente:** §6 · D7 · D9 · D11 · Discovery §7 D-28, D-29, D-31.
- **Archivos:** `tests/IntegrationTests/BusinessStatuses/BusinessStatusEndpointsTests.cs`, `tests/IntegrationTests/BusinessStatuses/BusinessStatusOrderingTests.cs`.
- **Detalle:** casos mínimos — (1) ABM completo de punta a punta con 201/200/204, ejercitando las cinco slices en secuencia; (2) listado paginado con dos filas del **mismo porcentaje**, verificando que el orden es estable entre ejecuciones y que ninguna fila se repite ni se salta entre páginas (D7, slice de la Fase 3); (3) alta con porcentaje 0 y con 100 → 400 (slice de la Fase 5); (4) edición de un terminal cambiando el porcentaje → 400 (slice de la Fase 6); (5) borrado de un terminal → 409 (slice de la Fase 7); (6) borrado de un estado con una fila hija en `tbl_opo_negocios` → **409 sin texto de SQL Server** (D-29, slice de la Fase 7) — la tabla hija mínima se crea en el mismo script de F9.1; (7) lectura de una fila con `negest_nombre`, `negest_porcentaje` y `negest_color` nulos → 200 sin excepción (D5, slices de las Fases 3-4); (8) petición sin `X-Entity-Code` con multitenencia activa → error de la plantilla, no 500 del contexto (transversal a las cinco slices).
- **Hecho cuando:** los ocho casos pasan y ninguno depende del orden de ejecución.
- **Verificar:** `dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter FullyQualifiedName~BusinessStatus`

---

## §9 Riesgos, GAPs y changelog

### 9.1 Riesgos

| # | Riesgo | Origen | Impacto | Mitigación adoptada |
|---|---|---|---|---|
| R-1 | **Los tenants con la invariante rota siguen rotos.** El servicio impide empeorar, pero no repara. En `udbzq10trabajos` conviven dos «Ganado» (uno inactivo) y dos «Perdido» | GAP-5 `NO APLICA` · Discovery §4.4 | Alto en ese tenant: la resolución de terminales fallará con 409 en cuanto tenga consumidor | Ninguna en este plan. Las guardas de D3 evitan nuevas derivas. La remediación queda pendiente de decisión de negocio |
| R-2 | **Doble escritor y caché no coherente durante la convivencia.** Jack sigue escribiendo por los SPs; esa escritura no invalida la caché L2 del servicio | D1 + D13 + GAP-A `NO APLICA` | Medio: el servicio puede servir hasta 10 minutos de datos obsoletos tras una edición hecha desde Jack | TTL corto (10 min). Se cierra al ejecutar el corte de escritura, que no forma parte de este plan |
| R-3 | **`TerminalBusinessStatusProvider` queda sin consumidor** hasta que migre Negocios | D6 | Bajo: código correcto pero no ejercitado en producción | Cubierto por tests unitarios que reproducen el escenario de D-28 |
| R-4 | **El desempate por identificador puede no ser lo que el desarrollador pidió** al decir «conservar el mismo ordenamiento actual» | D7 | Bajo si se confirma; alto si se revierte, porque `OFFSET/FETCH` dejaría de ser estable | Declarado explícitamente en D7. Revisar antes de ejecutar F2.5 |
| R-5 | **El ABM queda sin autenticación ni autorización** | D15 | Alto si el servicio llegara a exponerse fuera de la red interna | Decisión firmada del desarrollador. Debe revisarse **antes** de cualquier exposición externa |
| R-6 | El servicio **no puede crear estados terminales**. Un tenant sin catálogo semilla no podrá tener «Ganado» ni «Perdido» por esta vía | D3 (paridad con la regla del legado, D-05) | Bajo: los 19 tenants sanos de la muestra conservan el catálogo semilla | Registrado. Si aparece la necesidad, es un cambio de producto, no una corrección |
| R-7 | Las fechas de auditoría del agregado no se persisten: la tabla no tiene columnas para ellas | D14 (b) · D-16 diferido | Bajo | `CreatedAt`/`UpdatedAt` viven solo en memoria; se resolverá con la política de auditoría del servicio |
| R-8 | ~~El nombre `BusinessStatusRow` se aparta de la convención de la plantilla~~ **Cerrado (2026-08-21)** | `DESVIACIÓN-1` (§5.6) | — | La desviación fue rechazada: la entidad se llama `BusinessStatus` y la colisión se resuelve calificando cada uso. Sin riesgo residual |
| R-10 | **El alta no invalida la caché L2 del listado.** `CreateBusinessStatusUseCase` (F5) no llama `RemoveByPrefixAsync`, porque la llave de §7.2 se particiona por tenant y el código de tenant no llega a la capa de aplicación (ver `GAP-B`) | F5 · §7.2 · D12 | Medio **a partir de que F3 exista**: hasta 10 minutos de listado obsoleto tras cada alta hecha por el servicio. Hoy nulo: sin F3 no hay caché L2 que invalidar | Ninguna en F5. Se cierra con `GAP-B`: quien implemente F3 introduce el accesor de código de tenant y agrega la línea de invalidación a F5, F6 y F7. La invalidación L1 (`[OutputCacheInvalidate]`) sí está puesta |
| R-9 | **Un porcentaje persistido fuera del rango de `int` lanzaría `OverflowException` en el mapper.** La conversión de F2.3 (`(int)row.Percentage.Value`) tolera el no-entero y el nulo, pero no un entero que no quepa en `int`: la columna es `decimal(20,5)` y no tiene CHECK que la limite a 0-100 (D1) | F2.3 · D1 · D5 | Bajo: exigiría un dato ≥ 2.147.483.648 en una columna de porcentaje; ningún tenant de la muestra lo tiene (Discovery §4.4) | Detectado al ejecutar la Fase 2 y **no implementado** (§0.5: no se agrega alcance). Si se decide cubrirlo, es una guarda de rango en `ToWholePercentage` que devuelva `null`, igual que hace con el no-entero |

### 9.2 GAPs consolidados

**No queda ningún GAP `BLOQUEANTE` abierto. Ningún paso de §8 nace `blocked`.** (`GAP-B`, abierto el 2026-08-21 al ejecutar F5, es `NO BLOQUEANTE`: bloquea una línea de F5/F6/F7, no un paso completo.)

| GAP | Estado | Resolución del 2026-08-14 |
|---|---|---|
| **GAP-B (código de tenant para la llave L2)** | **Abierto — `NO BLOQUEANTE`** · detectado 2026-08-21 al ejecutar F5 | §7.2 exige `CacheKey.For("businessstatus").Tenant(tenantCode)`, pero **ninguna capa por debajo de HTTP conoce `tenantCode`**: `TenantMiddleware` lee `X-Entity-Code`, resuelve el `TenantInfo` y propaga **solo** el connection string vía `ITenantConnectionInitializer` → `TenantContext`; `TenantInfo.EntityCode` no sale del middleware, y no hay `IHttpContextAccessor` registrado. **Decide quien implemente F3**, que es la slice que construye la llave: lo natural es un accesor de solo lectura análogo a `ITenantConnectionInitializer` (p. ej. `ITenantCodeAccessor` en `Api/Session/`, poblado por el middleware, expuesto como puerto a Application). Al cerrarlo hay que agregar la invalidación L2 a F5 (ya ejecutada sin ella, R-10), F6 y F7 |
| GAP-1 | **Cerrado** | No se expone equivalente del endpoint anónimo. `api/flujonegocios` sigue en Jack (D8, §1.2) |
| GAP-17 | **Cerrado** | Veredictos de Discovery §7 adoptados en bloque (D16, §4.3) |
| GAP-A (corte de escritura) | **Cerrado — `NO APLICA`** | No se construye feature flag. El corte se planifica aparte (D13) |
| GAP-D (authn/authz) | **Cerrado — `NO APLICA`** | Servicio interno no expuesto a internet (D15). Ver R-5 |
| GAP-5 (remediación de la invariante) | **Cerrado — `NO APLICA`** | Sin remediación ni verificación de cobertura del parque. Ver R-1 |
| GAP-16 (SPs generados dinámicamente) | **Cerrado — `NO APLICA`** | La tabla no se mueve (D1), así que siguen funcionando. Además, fuera del alcance acordado |
| GAP-6 (protección real de endpoints anónimos) | **Cerrado — `NO APLICA`** | Afecta a Jack, no al servicio |
| GAP-18 (commit de origen) | **Cerrado — `NO APLICA`** | Se adopta `db555c53…` como origen de este plan |
| GAP-2 (unicidad de nombre) | **Cerrado** | No se agrega. Paridad con `negest_nombre` (D14 a) |
| GAP-7 (catálogo de permisos) | **Cerrado — `NO APLICA`** | Sin autorización en el servicio (D15) |
| GAP-9 (personalizaciones por cliente) | **Documentado, sin acción** | Las dos personalizaciones conocidas (ISER y formulario Q10) leen el catálogo desde Jack; no cambian con este plan |
| GAP-10 (adopción del parámetro 381) | **Documentado, sin acción** | El catálogo no tiene parámetros propios; el interruptor 381 gobierna la funcionalidad que lo usa, en Jack |
| GAP-13 (consumidores externos: Power BI, ETL, replicador) | **Documentado, sin acción** | La tabla permanece (D1): ningún consumidor externo se rompe |
| GAP-11 (telemetría) · GAP-12 (servicio hermano) | **Omitidos** | La caché se decide por evidencia estructural (D12); la única referencia de convenciones es `docs/plantilla/` |

**Vacíos que este plan cierra por decisión y no por GAP:** variables de entorno (§7.1: ninguna nueva), caché (D12), accesos a tablas fuera del dominio (D9: ninguno), campos de entrada obligatorios (§6.3, tabla completa), y mecanismo de corte (D13: fuera de alcance por decisión firmada).

### 9.3 Changelog

| Fecha | Cambio | Origen |
|---|---|---|
| 2026-08-14 | Creación del plan a partir de `01-discovery-flujos-negocio.md` rev. 2. 16 decisiones aprobadas, 8 riesgos, 0 GAPs bloqueantes, 28 pasos en 7 fases, 71 puntos | Ficha de decisiones aprobada por el desarrollador |
| 2026-08-14 | Revisión de diseño: el porcentaje deja de modelarse como VO (`ProgressPercentage` eliminado); pasa a `decimal?` con constantes (`MinPercentage`/`MaxPercentage`) y validación propia en `BusinessStatusAggregate` (D4, D5 reescritas). Se elimina el paso F1.2 (27 pasos en total). Se cierra además el gap de validación en el agregado: `Name` valida longitud máxima (200) y `Color` invoca `StatusColor.Create` dentro de `Create`/`Update`, no solo en el validador estructural de F4.2 — antes ninguno de los dos estaba realmente implementado pese a que sus errores (`NameTooLong`, `InvalidColorFormat`) ya estaban declarados. "Terminal" se mantiene como término técnico, sin renombrar | Revisión con el desarrollador |
| 2026-08-14 | Segunda revisión del mismo día: `Percentage` pasa de `decimal?` a `int?` dentro del agregado (D5 reescrita otra vez). El `decimal` real de la columna (`decimal(20,5)`) queda confinado a la entidad de persistencia (`BusinessStatusRow`) y al borde de entrada (`ValidatePercentageForCreate`/`ForUpdate` siguen recibiendo `decimal` para poder emitir `PercentageMustBeInteger`); el mapper (F2.3) hace la única conversión decimal↔int del servicio, tratando un valor persistido no entero como ausencia de porcentaje (`null`), nunca redondeándolo. Al ser `int`, `IsWon`/`IsLost`/`IsTerminal` comparan por igualdad exacta y se elimina la constante `PercentageTolerance`, que ya no hace falta | Revisión con el desarrollador |
| 2026-08-14 | Se agrega §10 Deuda técnica, documentando el desalineamiento de tipo entre `negest_porcentaje` (`decimal(20,5)` en BD) y `Percentage` (`int?` en la aplicación) | Revisión con el desarrollador |
| 2026-08-14 | Reorganización de §8: las antiguas Fase 3 (Aplicación, todos los casos de uso), Fase 4 (API, todos los endpoints y validadores juntos), Fase 5 (Caché) y Fase 6 (Integración) se reemplazan por **vertical slice**: una fase por endpoint (Listar F3, Detalle F4, Crear F5, Editar F6, Eliminar F7), cada una con su caso de uso, DTOs, validador, acción de controller, registro DI y caché de punta a punta en la misma tarea/rama. `TerminalBusinessStatusProvider` (sin endpoint) queda como Fase 8 propia. Las pruebas de integración de punta a punta (antes Fase 6) pasan a Fase 9, sin dividir por slice porque ejercitan el stack completo y combinan más de una — se agrega el paso F2.7 (scaffold de `AddBusinessStatusServices()` con solo el repositorio) del que cada slice cuelga su propia línea de registro. Total: 68 puntos en 28 pasos, 10 fases | Revisión con el desarrollador |
| 2026-08-21 | **Ejecución de la Fase 2** y tres correcciones pedidas por el desarrollador al revisarla contra `docs/plantilla/`: (a) `CreateAsync` queda con los dos `catch` del patrón canónico de `repositorio.md` —`SaveChangesAsync` envuelve siempre el error del driver en `DbUpdateException`, así que el `catch (SqlException)` era inalcanzable; el de `RemoveAsync` se mantiene porque el `SELECT` sí puede lanzarla sin envolver—; (b) se agrega `AssignId(int id)` a `BusinessStatusAggregate` (§5.1) para que `CreateAsync` complete el mismo agregado en vez de reconstruirlo, conservando el `CreatedAt`/`UpdatedAt` de `Create()`; (c) `DESVIACIÓN-1` se **rechaza**: la entidad de persistencia se llama `BusinessStatus` y la configuración `BusinessStatusConfiguration`, con la colisión de namespace resuelta por calificación (§5.6), lo que cierra R-8. Además se ajusta la estrategia de pruebas de la Fase 2: el repositorio **sí** se prueba unitariamente contra EF In-Memory para sostener el umbral de cobertura del 90 % del CI | Revisión con el desarrollador |
| 2026-08-21 | Ejecución de **F5** (`POST /business-statuses`) fuera de orden, con F3 y F4 en `pending`, por instrucción del desarrollador. Se crean el caso de uso, sus DTOs y mapping, `BusinessStatusesController` (con su `CacheTag`, que F3/F4/F6/F7 reutilizan), el validador estructural y el registro DI; 32 tests unitarios nuevos, suite completa en 488/488. Cuatro desviaciones registradas en el paso: orden, autoría del controller, `DESVIACIÓN-2` (el validador vive en `Infrastructure/Validation/FluentValidation/`, no en `Api/Validators/`, porque el escáner de registro solo mira el ensamblado `Infrastructure`) y la **invalidación L2 no implementada** → `GAP-B` + R-10 | Ejecución del plan |
| 2026-08-14 | Cada slice de las Fases 3-7 se colapsa en **un único paso** (`F3`, `F4`, `F5`, `F6`, `F7`, sin sub-numeración): el caso de uso, el endpoint, el validador y la caché de cada slice quedan documentados como una sola unidad de trabajo, en vez de repartidos en F*.1/F*.2/F*.3. Todas las referencias cruzadas del documento (D3, D5, D8, D9, D12, §4.1, §4.3) se actualizan al nuevo esquema de ids. Los puntos por fase no cambian (mismo trabajo, agrupado distinto); el total de pasos baja de 28 a 22 | Revisión con el desarrollador |

---

## §10 Deuda técnica

| # | Deuda | Origen | Impacto | Plan de resolución |
|---|---|---|---|---|
| DT-1 | La columna `negest_porcentaje` está creada en base de datos como `decimal(20,5)` (§4.1, `[verificado en BD]`), mientras que la aplicación —dominio, DTOs de entrada/salida y contrato HTTP— maneja el porcentaje siempre como entero (`int?` en `BusinessStatusAggregate`, `int`/`int?` en los DTOs; D5). El desalineamiento de tipo entre el esquema real y el modelo de aplicación no se corrige: se **absorbe** en un único punto de conversión, el mapper de persistencia (`BusinessStatusRepositoryMapper`, F2.3) | D1 (se reutiliza la tabla legada tal cual, sin DDL ni migraciones) · D5 (decisión de modelar `int` en la aplicación pese al tipo real de la columna) | Bajo mientras la conversión permanezca centralizada en un solo lugar (F2.3): cualquier ajuste futuro de tipo pasa por ahí, no por sitios dispersos. El riesgo real es que alguien agregue un segundo punto de conversión (otro repositorio, un reporte, un script) que no replique la misma regla de tolerancia a datos sucios (un valor no entero se trata como `null`, nunca se redondea) | No se resuelve en este plan — D1 impide tocar el esquema de la tabla legada mientras la convivencia con Jack esté vigente. Si en el futuro el catálogo migra a una tabla propia del servicio (fuera del alcance actual de este plan, §1.2), ese es el momento natural de normalizar la columna a un tipo entero (`int`/`smallint`) y eliminar la conversión por completo |
