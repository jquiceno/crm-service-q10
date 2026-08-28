# Working plan

---

service: crm-service

context: Actividades (CRM Jack — versión nueva)

doc: plan

status: draft

source: `../discovery-actividades-final.md` (discovery consolidado: consenso + arbitraje + verificación multi-tenant, 2026-08-14)

updated: 2026-08-14

# Plan de trabajo — Actividades (`crm-service`)

> **Convención de rótulos.** Las decisiones de este plan se numeran `DEC-n` (no `D-n` como sugiere la plantilla) para no colisionar con los defectos `D1–D33` del Discovery, que se citan constantemente. Los GAPs del plan se numeran `GAP-Pn`; cuando heredan un GAP del Discovery lo declaran (`← GAP-x`).

## 0. Cómo ejecutar este plan

> Dirigido al agente ejecutor. Copiar tal cual.

1. **Antes de ejecutar nada, verifica el plan.** Recorre todos los pasos y confirma que cada uno tiene `id`, `depende_de` existente, `estado`, `Fuente:`, `Hecho cuando:` y `Verificar:`. Confirma que ninguna decisión de §2 que afecte tu fase está en `estado: propuesta`, y que no quedan GAPs `BLOQUEANTE` abiertos que la afecten. Si algo falta, **detente y repórtalo**: no ejecutes un plan incompleto ni completes tú lo que falte.
2. Ejecuta los pasos en orden de `id`, respetando `depende_de`. No inicies pasos con `estado: blocked`.
3. Al terminar un paso, corre su comando de `Verificar` y solo entonces cambia `estado: pending` → `done` en este mismo archivo.
4. **Si la realidad del repositorio contradice el plan** (el archivo ya existe, la interfaz tiene otra firma, la tabla tiene otras columnas): detente, no improvises. Reporta con el formato `⚠️ GAP` y espera instrucción.
5. No agregues alcance. Si detectas una mejora, anótala como riesgo; no la implementes.

> **Advertencia específica de este dominio (regla 4 reforzada):** el Discovery §4.1-bis midió **drift de esquema entre instituciones** (columnas ausentes, tipos distintos, firmas de SP distintas). "La tabla tiene otras columnas" no es un caso hipotético aquí: es un hecho medido. Ante cualquier discrepancia de esquema, detente y reporta — jamás asumas que la BD que tienes delante es "la" canónica.

## 1. Contexto y alcance

**Qué se construye.** El contexto `Activities` dentro de `crm-service` (repo local `C:\Users\Cristobal Vasquez\Documents\crm-service`, `main`, HEAD `9f24956`): el dominio de la bitácora comercial del CRM nuevo de Jack, con su primer frente de exposición — la paridad del API público v1 (`GET/POST api/actividades`) — bajo estrategia *strangler*. Es la **fase API-first** que el Discovery §1 recomienda: 80 llamadas reales/30 d, todas GET, riesgo de corte mínimo.

**De dónde parte el repositorio.** `crm-service` es un scaffold del template .NET de la plataforma (commit `d00b533`): Clean Architecture por contextos (`src/Contexts/{Contexto}/{Domain,Application}`), `Shared` con patrón Result + taxonomía de errores + primitivas DDD, **tenancy por BD ya resuelto** (`TenantResolverServiceClient` + `AesConnectionStringDecryptor` + `TenantContext`), EF Core (`ApplicationDbContext`, `RepositoryBaseEF`), y 22 documentos de convenciones en `docs/plantilla/` que este plan cita como "template". Solo existe el contexto de ejemplo `ServiceInfo`.

**Dentro del alcance (esta iteración):**

1. Contexto `Activities`: aggregate, VOs, errores, casos de uso `GetActivities` y `CreateActivity`.
2. Persistencia **sobre la BD legada del tenant** (sin migración de datos — DEC-2), con acceso drift-safe.
3. Endpoints `GET /activities` y `POST /activities` con el contrato del template (envelope `{data, statusCode}`).
4. Adaptador en el monolito: `api/actividades` (contrato español intacto) delega en `crm-service` tras feature flag por institución.
5. Paridad verificada contra el comportamiento legado documentado en Discovery Anexo B.1, **incluidos** sus side-effects (`opo_fecha_ultimo_registro`, auditoría — DEC-3).
6. Fase 0 de prerrequisitos: inventario de drift (Discovery GAP-5), identificación del consumidor actual del API, y seguimiento de las remediaciones del monolito (fuga partners, superficie sin auth) que corren como tickets paralelos.

**Fuera del alcance de esta iteración** (Discovery §9 + decisiones DEC-4/DEC-2; ver "Diferido" en §8):

- Frente MVC completo (`Negocios/Actividades/*`, ≈1,17 M req/30 d), bandeja de próximas, badge, export Excel.
- Reunión virtual (tipo '6'), adjuntos (solo máster), reporte 504, cierre masivo desde `api/negocios`.
- Migración de datos a almacenamiento propio (bloqueada por Discovery GAP-4/GAP-5).
- Correcciones dentro del monolito (D2/D3 fuga partners, D9/D10 AllowAnonymous): tickets paralelos, se rastrean en §9 pero no son pasos de este repo.
- CRM v1 (`tbl_mer_*`) y homónimos académicos: fuera permanente.

**Ajuste de alcance posterior al Discovery:** ninguno. El Discovery GAP-6 (tenancy) queda **cerrado por evidencia** en este plan: la base del repo ya implementa aislamiento por BD por tenant (§7.1), que es exactamente el modelo del legado.

## 2. Decisiones cerradas (ADR)

> Ninguna decisión está firmada aún: **todas nacen `propuesta`** y la Fase 1 queda `blocked` hasta que el tech lead firme las que la afectan (regla dura de la plantilla). Las recomendaciones provienen del Discovery consolidado; donde el Discovery dejó GAP, la decisión lo referencia.

### DEC-1 — El padre canónico de una Activity es el Deal (Negocio)

`estado: propuesta · firmó: — · fecha: — · origen: Discovery GAP-1 + §4.1 + D1`

* **Decisión:** `DealId` es obligatorio en el aggregate; `OpportunityId` se deriva del Deal y no se acepta como entrada.
* **Alternativas descartadas:** Oportunidad como padre (el 99,95 % de las filas tiene `negact_opo_consecutivo` NULL y los 5 SPs de lectura hacen INNER JOIN a negocios); doble padre opcional (replica D1: una rama estructuralmente inalcanzable y dos definiciones de "oportunidad de la actividad").
* **Consecuencias:** elimina D1 por diseño; el API rechaza actividades sin deal; la lectura deriva `opportunityId` vía join al deal (como ya hace `pa_opo_negocios_actividades_detalle_retornar` con `ISNULL`).
* **Afecta:** §4, §5.2, §6.2 · pasos F1.3, F2.3, F2.5.

### DEC-2 — Fase 1 opera sobre la BD legada del tenant, sin migración de datos y con acceso drift-safe

`estado: propuesta · firmó: — · fecha: — · origen: Discovery §4.1-bis + GAP-5 + §1 (estrategia)`

* **Decisión:** el repositorio EF mapea `tbl_opo_negocios_actividades` directamente en la BD de la institución (resuelta por `TenantContext`), proyectando **solo el subconjunto de columnas común a todas las variantes conocidas** (las 15 de los tenants universitarios; `ConsecutivoActMiG` jamás se referencia), con longitudes validadas en dominio (no confiar en el esquema: `negact_descripcion` es 2000 en una base y MAX en otra).
* **Alternativas descartadas:** almacenamiento propio + migración inmediata (bloqueado por GAP-4/GAP-5: no hay esquema canónico ni discriminador universal de filas migradas); dual-write (complejidad y riesgo de divergencia sin beneficio en fase de paridad); reusar los SPs legados como capa de acceso (heredan errores tragados D3, firmas con drift C3 y parámetros desalineados D19).
* **Consecuencias:** cero migración de datos en fase 1; el corte es reversible por feature flag; el modelo EF **no** puede usar migrations sobre la BD legada (solo mapeo); las pruebas de integración deben correr contra **al menos dos variantes de esquema** (F2.6); los índices faltantes del legado (D15) no se corrigen en esta fase (riesgo R3).
* **Afecta:** §4, §5.5, §7.1, §7.3 · pasos F2.1–F2.6.

### DEC-3 — La escritura es una transacción explícita que replica los side-effects del legado y emite un evento de dominio

`estado: propuesta · firmó: — · fecha: — · origen: Discovery GAP-2 + §4.2 (SPs ingresar) + D3/D5/D26/D31`

* **Decisión:** `CreateActivity` ejecuta en una sola transacción: INSERT de la actividad + UPDATE condicional de `tbl_opo_oportunidades.opo_fecha_ultimo_registro` (misma regla del SP legado: solo si la fecha es más reciente) + registro de auditoría (`EXEC pa_seg_auditoria_ingresar`, SP transversal que se conserva) — con errores **explícitos** (Result/throw, nunca tragados) y aislamiento por defecto (no READ UNCOMMITTED). Además publica `ActivityRecorded` (evento de dominio en memoria, sin bus en fase 1) para que la fase 2 pueda mover el side-effect a Oportunidades sin tocar el caso de uso.
* **Alternativas descartadas:** omitir el side-effect (rompe paridad con el frente MVC y reproduce D5, el defecto del API legado); evento asíncrono con bus desde fase 1 (infraestructura que la fase no necesita; el UPDATE síncrono es el comportamiento legado a preservar); invocar el SP legado `pa_opo_negocios_actividades_ingresar` (D3: CATCH sin THROW).
* **Consecuencias:** el servicio nuevo **corrige D5 desde el día uno** (el API pasa a actualizar `opo_fecha_ultimo_registro`, cosa que el SP del API legado no hacía) — es un cambio de comportamiento deliberado y documentado, no un bug de paridad; la auditoría conserva su formato (con la trampa de datos personales del legado documentada como riesgo R6).
* **Afecta:** §5.5, §5.6, §6.2 · pasos F2.4, F3.5.

### DEC-4 — Reunión virtual (tipo '6') fuera del contrato de fase 1

`estado: propuesta · firmó: — · fecha: — · origen: Discovery GAP-3 + A16/A17 + D16`

* **Decisión:** `VirtualMeeting` no es un tipo escribible: el POST lo rechaza (como ya hace el API legado, Anexo B.1); en lectura, las filas históricas tipo '6' se devuelven con su tipo real. Toda la cadena aulas-virtuales/correos permanece en el monolito.
* **Alternativas descartadas:** soportarlo completo (arrastra MasterDB, mailing con zonas horarias quemadas y dos personas hardcodeadas — solo aplica al tenant máster); ocultar las filas históricas (rompe lecturas de paridad).
* **Consecuencias:** el máster Q10 **no** entra al feature flag del strangler en fase 1 (su flujo de tipo 6 es MVC y sigue en el monolito); el enum del dominio declara el valor como no-escribible.
* **Afecta:** §5.3, §6.2 · pasos F1.2, F3.4.

### DEC-5 — El contrato español legado no se rompe: adaptador en el monolito, contrato nuevo en inglés en el servicio

`estado: propuesta · firmó: — · fecha: — · origen: Discovery Anexo B.1 + plantilla (regla de contratos consumidos) + docs/plantilla/contrato-api.md`

* **Decisión:** `crm-service` expone `GET/POST /activities` (inglés, kebab-case, envelope `{data, statusCode}`). El endpoint legado `api/actividades` (campos `Consecutivo_negocio`, `Estado_actividad`…) se conserva en el monolito como **adaptador** que traduce y delega vía HTTP cuando el feature flag de la institución está activo.
* **Alternativas descartadas:** exponer el contrato español desde el servicio nuevo (viola la regla de idioma del template §3.1); romper el contrato legado (hay un consumidor activo con 80 GET/30 d — GAP-P8 lo identifica antes del corte).
* **Consecuencias:** doble contrato durante la convivencia; el adaptador es el punto de comparación para las pruebas de paridad (golden tests F3.5); los mensajes de validación en español del legado se mantienen en el adaptador, no en el servicio.
* **Afecta:** §6 completo, §7.4 · pasos F3.1–F3.5.

### DEC-6 — Estado del aggregate: `Scheduled | Completed | Cancelled`; el NULL legado se lee como `Scheduled`

`estado: propuesta · firmó: — · fecha: — · origen: Discovery D11 + GAP-4 + §4.1`

* **Decisión:** `negact_completada`/`negact_anulada` (`bit NULL` en BD) se colapsan en un VO `ActivityStatus`; en lectura, NULL se interpreta como `Scheduled` (el comportamiento efectivo de Dapper en el legado — paridad). La normalización física (`NOT NULL DEFAULT 0`) y el discriminador de filas migradas quedan para la fase de migración (GAP-P3).
* **Alternativas descartadas:** exponer los dos booleanos crudos (permite estados imposibles como completada+anulada); tratar NULL como estado propio "Migrada" (sin discriminador universal — `ConsecutivoActMiG` no existe en todos los tenants, Discovery C2).
* **Consecuencias:** el badge/lista del futuro frente MVC contará igual que la lectura del servicio (mata el eje 3 de D4 cuando ese frente migre); las filas históricas sin fecha de vencimiento nunca aparecen "vencidas".
* **Afecta:** §4, §5.3 · pasos F1.2, F2.3.

### DEC-7 — "Finalización del negocio" es un resultado de origen SYSTEM, no escribible por API

`estado: propuesta · firmó: — · fecha: — · origen: Discovery GAP-8 + §4.3`

* **Decisión:** los códigos reservados ('7' llamadas / '3' reuniones) existen en el enum `OutcomeType` como valores de solo-lectura/sistema; el POST los rechaza. Solo el futuro caso de uso de cierre masivo (diferido) podrá escribirlos.
* **Alternativas descartadas:** excluirlos del enum (rompe la lectura del histórico); permitirlos en el POST (hoy el legado los oculta filtrando combos en 8 sitios distintos — regla implícita que se perdería).
* **Consecuencias:** la regla vive una sola vez, en el dominio, no en 8 filtros de UI.
* **Afecta:** §5.3, §6.2 · pasos F1.2, F1.3.

### DEC-8 — Un solo reloj: hora de la institución con DST

`estado: propuesta · firmó: — · fecha: — · origen: Discovery D12 (Media-Alta) + GAP-5 del Doc-1 original`

* **Decisión:** puerto `IClock` (a crear en `Shared` — §5.5) que entrega `now` en la zona horaria IANA/Windows del tenant **con DST**. Sustituye a los tres relojes del legado (`DateTime.Now`, `Institucion.FechaHoraActual`, `FNZ_Q10_fecha_retornar` con offset fijo sin DST).
* **Alternativas descartadas:** UTC puro en dominio + conversión en el borde (válido, pero las fechas legadas están grabadas en hora local del tenant — comparar UTC contra ellas rompería "vencida" y el UPDATE condicional de `opo_fecha_ultimo_registro`); replicar el offset fijo (estructuralmente incapaz de DST, causa raíz de D12).
* **Consecuencias:** de dónde sale la TZ del tenant es **GAP-P11** (el legado la deriva de parámetro de cultura + tabla de diferencias; `TenantInfo` del resolver podría traerla); mientras se firma, el paso F2.4 queda con esa entrada bloqueada.
* **Afecta:** §5.5, §7.3 · pasos F1.0, F2.4, F2.5.

### DEC-9 — Autenticación y autorización obligatorias en todos los endpoints

`estado: propuesta · firmó: — · fecha: — · origen: Discovery D9/D10 + GAP-11 + Anexo B.1 (autenticación del API legado)`

* **Decisión:** ningún endpoint del contexto es anónimo. El servicio exige identidad de plataforma; el modo legado "solo header `aplentId` sin usuario" **no se replica** — el adaptador del monolito es quien traduce la autenticación legada hacia credenciales de servicio (mecánica exacta: GAP-P10).
* **Alternativas descartadas:** replicar `[AllowAnonymous]`+aplentId (el Discovery probó que deja la superficie sin control de acceso: 656 excepciones/14 d de peticiones sin sesión, 0×403 en 30 d); autorización por controlador sin área (reproduce D10).
* **Consecuencias:** el corte de tráfico requiere resolver GAP-P10 antes de F3.4; los golden tests de paridad se ejecutan autenticados.
* **Afecta:** §6, §7.2 · pasos F3.1, F3.4.

### DEC-10 — Errores explícitos con la taxonomía de `Shared`; nunca tragados, nunca truncados

`estado: propuesta · firmó: — · fecha: — · origen: Discovery D3/D26/D31 + template (patron-result.md, errores-dominio.md)`

* **Decisión:** todo flujo devuelve `Result`/`DomainError` de `Shared.Results` y el mapeo HTTP lo hace `ErrorHttpMapper`; ninguna operación captura-y-silencia; ningún mensaje se trunca; aislamiento de lectura por defecto (no READ UNCOMMITTED).
* **Alternativas descartadas:** replicar el patrón legado `@NmbError/@MsgError VARCHAR(100)` (D31: diagnóstico degradado; D3: fallos silenciosos como el rollback por FK de A2).
* **Consecuencias:** los errores del servicio y del legado difieren en forma; el adaptador (DEC-5) traduce a los mensajes españoles del contrato viejo.
* **Afecta:** §5.4, §6.x · pasos F1.4, F3.2.

## 3. Glosario y trazabilidad

### 3.1 Término de negocio (ES) → nombre técnico (EN)

| Negocio (ES) | Técnico (EN) | Dónde vive |
|--------------|--------------|------------|
| Actividad | `Activity` | aggregate, `Contexts/Activities/Domain/Aggregates` |
| Negocio | `Deal` (`DealId`) | referencia externa (no es aggregate de este contexto) |
| Oportunidad | `Opportunity` (`OpportunityId`, derivado) | referencia externa |
| Actividad programada | `ActivityStatus.Scheduled` | VO `ActivityStatus` |
| Actividad completada | `ActivityStatus.Completed` | VO `ActivityStatus` |
| Actividad anulada | `ActivityStatus.Cancelled` | VO `ActivityStatus` |
| Actividad vencida | `IsOverdue` (calculado con `IClock`) | aggregate |
| Tipo de actividad (Llamada/WhatsApp/Correo/Nota/Reunión/Reunión virtual) | `ActivityType` (`Call`, `WhatsApp`, `Email`, `Note`, `Meeting`, `VirtualMeeting`†, `LegacyMeeting`†) | VO/enum; † = solo lectura (DEC-4, legado '3') |
| Descripción para actividad (UI) / `negact_titulo` | `Description` | VO, máx. 500 |
| Resultado (texto) (UI) / `negact_descripcion` | `Outcome` | VO, máx. 2000 |
| Tipo de resultado / `negact_resultado` | `OutcomeType` (incluye valores SYSTEM — DEC-7) | VO/enum |
| Asesor responsable | `AdvisorId` | VO (código persona, ≤20) |
| Quien registró | `CreatedById` | VO (código persona, ≤20) |
| Fecha de actividad | `ActivityDate` / `CreatedAt` | aggregate |
| Fecha de vencimiento | `DueAt` | aggregate |
| Fecha de resuelto | `CompletedAt` | aggregate |
| Oportunidad archivada | `OpportunityArchived` (error de dominio) | §5.4 |
| Institución | `Tenant` | `TenantContext` (Shared) |
| Última gestión de la oportunidad | `Opportunity.LastActivityAt` (`opo_fecha_ultimo_registro`) | side-effect DEC-3 |

**Regla de idioma:** bounded context, clases, DTOs, endpoints y contratos JSON siempre en inglés. Única excepción: tablas, columnas y SPs del esquema legado, que se citan tal cual existen. Un concepto = un nombre en todo el documento.

### 3.2 Trazabilidad Discovery → Plan

| Discovery (final consolidado) | Se usa en |
|-----------|-----------|
| §1 estrategia strangler API-first; §8.0 muestreo ×10 | §1 alcance; R4 (métricas de paridad) |
| §4.1 tabla de columnas + trampa semántica titulo/descripcion | §4 mapeo (columna Trampa) |
| §4.1-bis drift C1–C6 | DEC-2; F0.1; F2.6; R1; GAP-P2 |
| §4.2 side-effects de los SPs; D3 errores tragados; D31 MsgError | DEC-3; DEC-10 |
| §4.3 catálogos, valores reservados, GAP-8 | DEC-7; §5.3 |
| §5.3 diferencias MVC vs API (D5 `opo_fecha_ultimo_registro`) | DEC-3 (corrección deliberada) |
| Anexo B.1 contrato y validaciones del API legado | §6 (tablas de validación completas) |
| Anexo B.4 declaraciones de ausencia (sin PUT/PATCH/DELETE; sin APIs en v0/Móviles) | §6 (superficie mínima); §8 Diferido |
| D1 rama inalcanzable / GAP-1 padre canónico | DEC-1 |
| D4 (3 ejes badge/lista) + D12 relojes / GAP-5 Doc-1 | DEC-6; DEC-8 |
| D9/D10/GAP-11 superficie sin auth | DEC-9; GAP-P7 (monolito) |
| D2/D3 fuga partners / GAP-7 | GAP-P7 (ticket monolito, paralelo) |
| D22 toggle no idempotente | §8 Diferido (endpoint de estado será PUT idempotente cuando toque) |
| GAP-2 side-effects | DEC-3; GAP-P5 |
| GAP-3 reunión virtual | DEC-4; GAP-P4 |
| GAP-4 discriminador migradas | GAP-P3; DEC-6 |
| GAP-5 inventario drift | GAP-P2; F0.1 |
| GAP-6 tenancy | **CERRADO**: la base del repo lo resuelve (§7.1) |
| GAP-8 resultado reservado | DEC-7 |
| GAP-9 cierre masivo canónico | §8 Diferido (regla API con guarda) |
| GAP-10 validar sobre mayor consumidor | GAP-P9; F2.6/F3.5 |
| A2 borrado con rollback por FK; A25 matrícula→completar | §8 Diferido (deuda del dominio Deal/Opportunity, no de esta fase) |
| D6 cuello de botella asesores (`pa_mer_…personas`) | fuera de alcance §1 (dominio Oportunidades/Seguridad); R5 |

## 4. Mapeo legado → modelo

> El esquema vive en Discovery §4.1/§4.1-bis. Aquí no se re-transcribe: se mapea. Persistencia = BD legada del tenant (DEC-2), tabla `tbl_opo_negocios_actividades`.

| Columna / SP legado | Propiedad de dominio | Tipo | Persistencia | Trampa |
|---------------------|----------------------|------|--------------|--------|
| `negact_consecutivoP` | `Activity.Id` | `int` | PK identity (la genera la BD) | único índice de la tabla (D15) |
| `negact_neg_consecutivo` | `Activity.DealId` | `int` | NOT NULL **en dominio** (DEC-1) | columna nullable en BD; 0 filas NULL en datos |
| `negact_opo_consecutivo` | — (no cruza como entrada) | — | se escribe derivado del Deal, como hace el SP del API legado | 99,95 % NULL; dos definiciones de "oportunidad" en el legado (D1) |
| `negact_tipo` | `Activity.Type` | `ActivityType` (char map) | char(1) | '3' y '5' son ambos "Reunión"; '6' no escribible (DEC-4); diccionario legado sin TryGetValue (D20) |
| `negact_titulo` | `Activity.Description` | `Description` (≤500) | varchar(500) | ⚠️ **semántica invertida**: la UI lo llama "Descripción" |
| `negact_descripcion` | `Activity.Outcome` | `Outcome` (≤2000) | varchar(2000) **o MAX según tenant** (C1) | ⚠️ invertida: la UI lo llama "Resultado"; límite se valida en dominio, no se confía al esquema |
| `negact_resultado` | `Activity.OutcomeType` | `OutcomeType` (char map) | char(1) | SPs legados lo declaraban VARCHAR(500) (D19); valores '7'/'3' reservados SYSTEM (DEC-7) |
| `negact_fecha` | `Activity.CreatedAt` | `DateTime` (TZ tenant) | datetime | tres relojes en el legado (D12) → `IClock` (DEC-8) |
| `negact_fecha_vencimiento` | `Activity.DueAt` | `DateTime?` | datetime NULL | obligatoria si `Scheduled` |
| `negact_completada` | `Activity.Status` (con `negact_anulada`) | `ActivityStatus` | bit NULL → lectura NULL⇒Scheduled (DEC-6) | 380.717 NULL en la BD de muestra (D11) |
| `negact_anulada` | ↑ (`Cancelled`) | ↑ | bit NULL | solo aplica a tipo 6 en el legado; en dominio es estado general |
| `negact_fecha_resuelto` | `Activity.CompletedAt` | `DateTime?` | datetime NULL | se fija al completar |
| `negact_asesor` | `Activity.AdvisorId` | `AdvisorId` (≤20) | varchar(20), FK personas | el API legado valida existencia + rol (se replica en use case) |
| `negact_per_codigo` | `Activity.CreatedById` | `AdvisorId` | varchar(20), FK personas | 0 nulos en datos |
| `negact_descripcion_virtual` | — (no cruza) | — | varchar(500) | tipo 6 fuera de fase 1 (DEC-4); columna muerta en exports legados (D29) |
| `ConsecutivoActMiG` | — (no cruza) | — | **no referenciar jamás** | no existe en todos los tenants (C2); solo trazabilidad de migración futura (GAP-P3) |
| SP `pa_apis_opo_negocios_actividades_retornar` | reemplazado por query del repositorio (F2.3) | — | — | READ UNCOMMITTED en legado — no replicar (DEC-10) |
| SP `pa_apis_opo_negocios_actividades_ingresar` | reemplazado por `CreateActivity` (F2.4/F2.5) | — | — | no actualizaba `opo_fecha_ultimo_registro` (D5) — el servicio SÍ lo hace (DEC-3) |
| SP `pa_seg_auditoria_ingresar` | **se conserva** (puerto `IAuditLogger` lo invoca) | — | EXEC en la transacción | texto de auditoría con datos personales (R6) |
| UPDATE `tbl_opo_oportunidades.opo_fecha_ultimo_registro` | side-effect de `CreateActivity` | — | UPDATE condicional en transacción | regla exacta del SP MVC (solo si más reciente) |
| SPs `pa_opo_negocios_actividades_{retornar,detalle,modificar,eliminar}` + `tareas_proximas*` + adjuntos | — (no cruzan en fase 1) | — | siguen siendo del monolito (frente MVC) | ver §8 Diferido |

## 5. Dominio

### 5.1 Estructura de carpetas

Sigue el patrón del contexto de ejemplo `ServiceInfo` y `docs/plantilla/contextos.md`:

```
src/Contexts/Activities/
├── Domain/                        (Activities.Domain.csproj)
│   ├── Aggregates/Activity.cs
│   ├── ValueObjects/{ActivityType,ActivityStatus,OutcomeType,Description,Outcome,AdvisorId}.cs
│   ├── Errors/ActivityErrors.cs
│   └── Repositories/IActivityRepository.cs
├── Application/                   (Activities.Application.csproj)
│   ├── Ports/{IGetActivitiesPort,ICreateActivityPort,IDealReader,IAdvisorReader,IAuditLogger}.cs
│   └── UseCases/
│       ├── GetActivities/{GetActivitiesUseCase,GetActivitiesInputDto,ActivityOutputDto}.cs
│       └── CreateActivity/{CreateActivityUseCase,CreateActivityInputDto,CreateActivityOutputDto}.cs
src/Infrastructure/Persistence/EntityFramework/Activities/
│   ├── ActivityConfiguration.cs   (mapeo explícito de columnas — DEC-2)
│   ├── ActivityRepository.cs
│   └── {DealReader,AdvisorReader}.cs
src/Api/Controllers/ActivitiesController.cs
tests/UnitTests/Contexts/Activities/…
tests/IntegrationTests/Activities/…
```

### 5.2 Aggregates y sub-entidades

**`Activity` (aggregate root, sin sub-entidades en esta fase).** Patrón según `docs/plantilla/entidades-y-agregados.md`: hereda `AggregateRoot` de `Shared.Domain`; toda mutación pasa por métodos con invariantes; los adjuntos (relación master-only, fuera de alcance) no se modelan.

Invariantes (fuente: Discovery Anexo B.1 + §4.3 + DEC-1/4/6/7):

1. `DealId` obligatorio y > 0 (DEC-1).
2. `Type` ∈ {Call, WhatsApp, Email, Note, Meeting} para escritura; `VirtualMeeting`/`LegacyMeeting` solo lectura (DEC-4).
3. `Scheduled` ⇒ `Description` obligatoria, `DueAt` obligatoria, `Outcome`/`OutcomeType` prohibidos; `Note` no puede ser `Scheduled`.
4. `Completed` ⇒ `Outcome` obligatorio, `CompletedAt` fijado por reloj de tenant; `OutcomeType` obligatorio solo si `Type` ∈ {Call, Meeting}, prohibido en el resto.
5. `OutcomeType` de origen SYSTEM ('7' llamada / '3' reunión) no aceptado desde API (DEC-7).
6. `AdvisorId`/`CreatedById` no vacíos, ≤ 20.

### 5.3 Value Objects

Patrón según `docs/plantilla/value-objects.md` (hereda `Shared.Domain.ValueObjects.ValueObject`).

| VO | Regla | Se valida en |
|-----|-------|--------------|
| `ActivityType` | mapea char legado ('1','7','2','4','5' escribibles; '6','3' solo lectura); rechaza códigos desconocidos con error, no excepción (corrige D20) | Dominio (creación) |
| `ActivityStatus` | {Scheduled, Completed, Cancelled}; desde BD: (completada, anulada) con NULL⇒false (DEC-6) | Dominio |
| `OutcomeType` | enum por tipo (Call: SinRespuesta…Contactado; Meeting: Realizada/Cancelada); valores SYSTEM no escribibles (DEC-7) | Dominio |
| `Description` | no vacía cuando aplica; longitud ≤ 500 | Dominio (+ pre-validación de request en API) |
| `Outcome` | no vacío cuando aplica; longitud ≤ 2000 | Dominio (+ API) |
| `AdvisorId` | no vacío, ≤ 20 chars | Dominio |

### 5.4 Errores de dominio

En `ActivityErrors` (patrón `docs/plantilla/errores-dominio.md`, sobre `Shared.Results.Errors`): `DealNotFound`, `OpportunityArchived`, `AdvisorNotFound`, `AdvisorNotAllowed` (sin rol admin/superadmin — regla del API legado), `InvalidActivityType`, `TypeNotWritable` (VirtualMeeting/Legacy), `NoteCannotBeScheduled`, `DescriptionRequired`, `OutcomeRequired`, `OutcomeNotAllowedWhenScheduled`, `OutcomeTypeRequired`, `OutcomeTypeNotAllowed`, `SystemOutcomeTypeNotWritable`, `DueDateRequired`. Mapeo a HTTP en §6.x.

### 5.5 Contratos de repositorio y puertos

**Auditoría de `Shared`** — ejecutada sobre el repo real (`main` @ `9f24956`), obligatoria antes de diseñar:

| Capacidad | ¿Existe? | Ruta | Reutilizar / extender / crear |
|-----------|----------|------|-------------------------------|
| Result + taxonomía de errores (`Result`, `PagedResult`, `DomainError`, `ErrorType`, `ValidationError`…) | Sí | `src/Shared/Results/` | **Reutilizar** |
| Primitivas DDD (`AggregateRoot`, `Entity`, `ValueObject`, `IRootRepository`) | Sí | `src/Shared/Domain/` | **Reutilizar** |
| Paginación (`PageQuery`, `PageQueryInputDto`, `PagedResult`) | Sí | `src/Shared/{Domain/Pagination,Application/Dtos,Results}/` | **Reutilizar** |
| Tenancy por BD (resolver HTTP + descifrado AES + `TenantContext` como `IDbConnectionProvider`) | Sí | `src/Shared/Infrastructure/MasterAccess/`, `src/Api/Session/` | **Reutilizar** — cierra Discovery GAP-6 |
| Persistencia EF por tenant (`ApplicationDbContext`, `RepositoryBaseEF`) | Sí | `src/Infrastructure/Persistence/EntityFramework/` | **Extender** (agregar `DbSet`/configuración de Activities) |
| UnitOfWork (`IUnitOfWorkPort`) | Sí | `src/Shared/Application/Ports/` | **Reutilizar** (transacción de DEC-3) |
| Manejo HTTP de errores (`ErrorHttpMapper`, `GlobalExceptionMiddleware`, `ApiResponses`, `Http*Result`) | Sí | `src/Shared/Infrastructure/Presentation/` | **Reutilizar** |
| Validación de request (`IRequestValidatorPort`, `ValidateRequestAttribute`) | Sí | `src/Shared/{Application/Ports,Infrastructure/Presentation}/` | **Reutilizar** |
| Logging estructurado (`ILoggerPort`) / Observabilidad (Sentry) | Sí | `src/Shared/Application/Ports/`, `src/Infrastructure/Observability/` | **Reutilizar** |
| Caché (`ICacheStore`, L2 Redis) | Sí | `src/Shared/Application/Ports/`, `src/Infrastructure/Caching/` | **No usar en fase 1** (§7.3) |
| **Reloj por tenant (`IClock` con TZ + DST)** | **No** | — | **Crear** (extender `Shared` — paso propio y PR propio, F1.0; DEC-8) |
| **Eventos de dominio (publicación)** | **No** (solo `AggregateRoot` sin dispatcher visible — confirmar en F1.0) | — | **Crear mínimo** (dispatcher en memoria) o confirmar existencia; F1.0 |
| Auditoría de negocio (puerto hacia `pa_seg_auditoria_ingresar`) | No | — | **Crear** en el contexto (puerto `IAuditLogger`, adaptador en Infrastructure — DEC-3) |

**Repositorio y puertos del contexto** (patrón `docs/plantilla/conceptos-reader-provider-repository.md`):

* `IActivityRepository` (Domain/Repositories — **Repository**, único que toca el aggregate): `AddAsync`, `GetPagedAsync(filter, PageQuery)`.
* `IDealReader` (Application/Ports — **Reader**, tabla foránea `tbl_opo_negocios` + `tbl_opo_oportunidades`): `GetDealContextAsync(dealId)` → `{DealExists, OpportunityId, OpportunityArchived}`. Sustituye las 2 lecturas del API legado.
* `IAdvisorReader` (Application/Ports — **Reader**, `tbl_per_personas` + roles): `ResolveByIdentificationAsync(identification)` → `{PersonCode, IsAdminOrSuperAdmin}` (regla del API legado, Anexo B.1).
* `IAuditLogger` (Application/Ports): `LogActivityCreatedAsync(…)` → adaptador que ejecuta `pa_seg_auditoria_ingresar` dentro de la transacción (DEC-3).
* `IClock` (Shared, nuevo): `Now(tenant)` (DEC-8).

### 5.6 Application — un Use Case por endpoint

| Use Case | Endpoint | Nota |
|----------|----------|------|
| `GetActivitiesUseCase` | `GET /activities` | filtros del contrato legado (deal, opportunity, deal-state); paginación `PageQuery` (límite 5000 como el legado); NULL⇒Scheduled en la proyección (DEC-6) |
| `CreateActivityUseCase` | `POST /activities` | valida vía `IDealReader`/`IAdvisorReader`; transacción DEC-3 (insert + `opo_fecha_ultimo_registro` + auditoría); emite `ActivityRecorded` |

## 6. Contratos de API

Convenciones del template (`docs/plantilla/contrato-api.md`): envelope `{data, statusCode}`; paginado `{data: {items, totalCount}, statusCode}`; rutas kebab-case; errores uniformes vía `ErrorHttpMapper`. Una sola convención de paginación y de errores en todo el servicio.

### 6.1 `GET /activities`

| Param | Tipo | Obligatorio | Default | Validación | Capa |
|-------|------|-------------|---------|------------|------|
| `deal-id` | int | condicional* | — | > 0 | API (request validator) |
| `opportunity-id` | int | condicional* | — | > 0 | API |
| `deal-state-id` | int | condicional* | — | > 0 | API |
| `page` | int | no | 1 | ≥ 1 | API (`PageQueryInputDto`) |
| `page-size` | int | no | 30 | 1–5000 (tope del API legado, `MAXIMUM_LIMIT_CUSTOM`) | API |

\* Al menos **uno** de `deal-id` / `opportunity-id` / `deal-state-id` es obligatorio (regla del legado: "al menos un parámetro de consulta"). Se valida en API; sin ninguno ⇒ `Validation` 400.

**Éxito 200:**

```json
{
  "data": {
    "items": [
      {
        "id": 380995, "dealId": 1200, "dealName": "…",
        "opportunityId": 845, "opportunityName": "…",
        "type": "call", "status": "completed",
        "description": null, "outcome": "Se contactó al cliente…", "outcomeType": "contacted",
        "advisorId": "1017…", "advisorName": "…", "advisorIdentification": "…",
        "createdAt": "2026-08-01T10:15:00", "dueAt": null, "completedAt": "2026-08-01T10:20:00"
      }
    ],
    "totalCount": 128
  },
  "statusCode": 200
}
```

### 6.2 `POST /activities`

Todos los campos de entrada, sin excepción (fuente: validaciones del API legado, Discovery Anexo B.1, trasladadas a dominio):

| Param | Tipo | Obligatorio | Default | Validación | Capa |
|-------|------|-------------|---------|------------|------|
| `dealId` | int | sí | — | > 0; el deal existe (`DealNotFound`); su oportunidad no archivada (`OpportunityArchived`) | API (forma) + Application (existencia) |
| `status` | string | sí | — | `scheduled` \| `completed` | API + Dominio |
| `type` | string | sí | — | `call`\|`whatsapp`\|`email`\|`note`\|`meeting`; `virtual-meeting` rechazado (`TypeNotWritable`, DEC-4); `note` no puede ser `scheduled` (`NoteCannotBeScheduled`) | Dominio |
| `advisorIdentification` | string | sí | — | ≤ 20; existe (`AdvisorNotFound`); rol Superadmin/Administrativo (`AdvisorNotAllowed`) | API (longitud) + Application (lookup) |
| `activityDate` | datetime | sí | — | fecha válida; `CreatedAt` real la fija `IClock` (paridad con SP legado que ignoraba el parámetro para creación) | API + Application |
| `description` | string | condicional | — | obligatoria si `scheduled` (≤ 500, `DescriptionRequired`); **prohibida** si `completed` | Dominio |
| `outcome` | string | condicional | — | obligatorio si `completed` (≤ 2000, `OutcomeRequired`); **prohibido** si `scheduled` (`OutcomeNotAllowedWhenScheduled`) | Dominio |
| `outcomeType` | string | condicional | — | obligatorio si `completed` y `type` ∈ {call, meeting} (`OutcomeTypeRequired`); prohibido si `scheduled`; ignorado con otros tipos (paridad legado); valores SYSTEM rechazados (`SystemOutcomeTypeNotWritable`, DEC-7) | Dominio |
| `dueAt` | datetime | condicional | — | obligatoria si `scheduled` (`DueDateRequired`) — el legado la tomaba de `Fecha_actividad`; el adaptador la mapea | Dominio |

**Éxito 201:** `{ "data": { "id": 380996 }, "statusCode": 201 }` (el POST legado solo devolvía el consecutivo — se preserva la parquedad).

**Error (ejemplo 400):** `{ "errors": [ { "code": "activity.description_required", "message": "…" } ], "statusCode": 400 }` (forma exacta según `ApiResponses` de Shared; el adaptador del monolito traduce a los mensajes españoles del contrato legado — DEC-5).

### 6.x Errores de dominio → HTTP

| Error | ErrorType | HTTP |
|-------|-----------|------|
| `DealNotFound`, `AdvisorNotFound` | NotFound | 404 |
| `OpportunityArchived` | DomainError | según `ErrorHttpMapper` de Shared (verificar en F3.2; el legado devolvía 400) |
| `AdvisorNotAllowed` | DomainError | ídem (legado: 404 — divergencia deliberada a resolver en F3.2 con el adaptador) |
| `InvalidActivityType`, `TypeNotWritable`, `NoteCannotBeScheduled`, `DescriptionRequired`, `OutcomeRequired`, `OutcomeNotAllowedWhenScheduled`, `OutcomeTypeRequired`, `SystemOutcomeTypeNotWritable`, `DueDateRequired` | Validation / DomainError | 400 / según mapper |
| Falta de al menos un filtro en GET | Validation | 400 |
| Sin identidad | Unauthorized | 401 |

## 7. Operación

### 7.1 Resolución de tenant

**Ya resuelta por la base del repo** (cierra Discovery GAP-6): middleware de tenant → `TenantResolverServiceClient` (HTTP al servicio de tenants, `TENANT_RESOLVER_SERVICE_URL`) → cadena de conexión descifrada con `AesConnectionStringDecryptor` (`CONNSTRING_ENCRYPTION_KEY`) → `TenantContext` (scoped) que implementa `IDbConnectionProvider` para el `ApplicationDbContext` por request. El aislamiento por BD del legado **se conserva tal cual** (coherente con Discovery D27: `aplent_codigoP` era decorativo). Pendiente: cómo viaja el código de tenant desde el adaptador del monolito (GAP-P10).

### 7.2 Variables de entorno

Base del template (`docs/plantilla/variables-entorno.md`): `ASPNETCORE_*`, `TenantResolverService__*`, `Cache__*`, `Sentry__*`, `Cors__*`; secretos de plataforma `CONNSTRING_ENCRYPTION_KEY`, `TENANT_RESOLVER_SERVICE_URL`, `SENTRY_DSN`, `Cache__ConnectionString`. **Propias de este contexto:** ninguna nueva identificada para fase 1, **salvo** la fuente de la zona horaria por tenant (DEC-8): si no viene en `TenantInfo`, se define aquí — `GAP-P11`, no una omisión.

### 7.3 Caché y rendimiento

- **Sin caché en fase 1.** El volumen del frente API es mínimo (80 GET/30 d reales) y los SPs equivalentes del legado corren en ≤ 40 ms p95; introducir `ICacheStore` sería alcance no pedido. Las claves Redis legadas (`ESTADO_OPORTUNIDAD_NEW_*`) son del monolito y no se tocan.
- **Consultas con lista explícita de columnas** (DEC-2) y paginación obligatoria (tope 5000).
- La tabla legada solo tiene el índice de la PK (D15): las consultas por deal/opportunity escanean. Riesgo aceptado en fase 1 (mismo perfil que el legado, volumen bajo) — R3; crear índices en BDs legadas queda explícitamente fuera (drift GAP-P2).
- El cuello de botella real del módulo (D6, 3 s del combo de asesores) es del frente MVC y **no entra** a este servicio (R5 vigila que nadie lo "migre" por accidente).

### 7.4 Rutas del monolito y qué las reemplaza

| Ruta actual | Reemplazo | Estado |
|-------------|-----------|--------|
| `GET api/actividades` (`Areas/API/v1/GestionComercial/Controllers/ActividadesController.cs:26`) | Adaptador → `GET /activities` de crm-service (feature flag por institución) | fase 1 (F3.4) |
| `POST api/actividades` (`:50`) | Adaptador → `POST /activities` | fase 1 (F3.4) |
| `Negocios/Actividades/*` (frente MVC, 18 acciones) | — | sin reemplazo en esta iteración (Diferido) |
| `PUT api/negocios/estado/{ganar,perder,ganar/colegio}` (cierre masivo) | — | sin reemplazo (Diferido; regla canónica = variante con guarda, Discovery GAP-9) |
| Reporte 504, export Excel, bandeja próximas | — | sin reemplazo (Diferido) |

## 8. Fases y pasos

> `tarea:` queda `[sin asignar]` en todos los pasos hasta que se creen las claves Jira (GAP-P6). Cadena: Discovery §X → DEC-n → paso Fn.m → Tarea → PR.

### Fase 0 — Prerrequisitos y evidencia · `pending`

No depende de decisiones de diseño; puede ejecutarse ya. **Estrategia de pruebas:** los entregables son scripts/reportes verificables por inspección + re-ejecución.

#### [F0.1] Inventario de drift de esquema por institución
`id: F0.1 · depende_de: — · tarea: [sin asignar] · estado: pending`
- Objetivo: medir la extensión real del drift (variantes de esquema/SP) sobre todas las BDs activas, agrupando por huella.
- Fuente: Discovery GAP-5 (BLOQUEANTE) + §4.1-bis.
- Archivos: script T-SQL/PowerShell de solo lectura (repo de tooling interno o `docs/servicio/` de crm-service), reporte de variantes.
- Detalle: iterate `sys.columns` (nombre+tipo+orden) de `tbl_opo_negocios_actividades`, `sys.parameters` de los 13 SPs, `sys.foreign_keys`, y hash de `OBJECT_DEFINITION` por SP; agrupar por hash; salida: tabla institución→variante.
- Hecho cuando: existe el reporte con el 100 % de las BDs activas clasificadas por variante y está enlazado desde el Discovery (GAP-5) y desde este plan.
- Verificar: `re-ejecutar el script sobre 2 BDs ya medidas (udbzq10trabajos, udbzunilimachiolecca_Primary) y confirmar que reproduce las divergencias C1-C6 del Discovery §4.1-bis`

#### [F0.2] Identificar el consumidor real del API legado
`id: F0.2 · depende_de: — · tarea: [sin asignar] · estado: pending`
- Objetivo: saber quién emite los 80 GET/30 d a `api/actividades` (todos en Azure) antes de ponerles un adaptador delante.
- Fuente: Discovery Anexo B.1 (uso real) + GAP-P8.
- Archivos: consulta KQL documentada + hallazgo en §9.
- Detalle: `requests | where url contains '/api/actividades' | summarize by client_IP, user_Agent, appName` en Insights-TempPeru (30-90 d, `sum(itemCount)`); correlacionar con la institución (tenant) emisora.
- Hecho cuando: el consumidor (institución + sistema cliente) está identificado y anotado en GAP-P8, o se declaró inidentificable con la evidencia intentada.
- Verificar: `la KQL y su resultado quedan pegados en GAP-P8 de este archivo`

#### [F0.3] Registrar y enlazar los tickets de remediación del monolito
`id: F0.3 · depende_de: — · tarea: [sin asignar] · estado: pending`
- Objetivo: que la fuga de partners (D2/D3) y la superficie sin auth (D9/D10) tengan tickets propios en el monolito, fuera de este repo, antes del corte.
- Fuente: Discovery GAP-7 + GAP-11.
- Archivos: solo este documento (§9.2, claves de ticket).
- Hecho cuando: GAP-P7 tiene las dos claves Jira anotadas.
- Verificar: `GAP-P7 actualizado con claves`

#### [F0.4] Confirmar dispatcher de eventos de dominio y fuente de TZ en la base del template
`id: F0.4 · depende_de: — · tarea: [sin asignar] · estado: pending`
- Objetivo: cerrar las dos incógnitas de la auditoría de Shared: si `AggregateRoot` ya trae dispatch de domain events, y si `TenantInfo` del resolver trae zona horaria.
- Fuente: §5.5 (filas "Crear/confirmar") + DEC-8 + GAP-P11.
- Archivos: lectura de `src/Shared/Domain/Aggregates/AggregateRoot.cs`, `src/Shared/Infrastructure/MasterAccess/Http/Tenants/TenantInfo.cs`, `docs/plantilla/{entidades-y-agregados,providers}.md`.
- Hecho cuando: §5.5 queda sin filas en estado "confirmar" y GAP-P11 tiene respuesta o dueño.
- Verificar: `§5.5 y GAP-P11 actualizados con cita ruta:línea`

### Fase 1 — Dominio `Activities` · `blocked` (por GAP-P1: DEC-1/4/6/7/8 en `propuesta`)

**Estrategia de pruebas:** unit tests puros de dominio (xUnit, sin infraestructura), un test por invariante de §5.2 y por regla de VO de §5.3, según `docs/plantilla/testing.md`.

#### [F1.0] Extender Shared: puerto IClock (PR propio)
`id: F1.0 · depende_de: F0.4 · tarea: [sin asignar] · estado: blocked`
- Objetivo: reloj por tenant con TZ + DST, reutilizable por cualquier contexto.
- Fuente: DEC-8 + regla del template (§5.5: extender Shared = paso y PR propios).
- Archivos: `src/Shared/Application/Ports/IClock.cs`, adaptador en `src/Infrastructure/`, tests.
- Detalle: `DateTime Now(TimeZoneInfo tenantTz)` o equivalente según lo que F0.4 confirme sobre TenantInfo.
- Hecho cuando: puerto + adaptador + tests en verde, PR independiente mergeado.
- Verificar: `dotnet test tests/UnitTests --filter Clock`

#### [F1.1] Scaffold del contexto Activities
`id: F1.1 · depende_de: — · tarea: [sin asignar] · estado: blocked`
- Objetivo: crear `Activities.Domain` y `Activities.Application` siguiendo el layout de ServiceInfo.
- Fuente: §5.1 + `docs/plantilla/contextos.md`.
- Archivos: `src/Contexts/Activities/**` (csproj + carpetas), referencias en la solución.
- Hecho cuando: la solución compila con los dos proyectos vacíos referenciados.
- Verificar: `dotnet build`

#### [F1.2] Value Objects y enums
`id: F1.2 · depende_de: F1.1 · tarea: [sin asignar] · estado: blocked`
- Objetivo: los 6 VOs de §5.3 con sus reglas, incluidos los valores solo-lectura (DEC-4) y SYSTEM (DEC-7).
- Fuente: §5.3 · DEC-4, DEC-6, DEC-7 · Discovery §4.3.
- Archivos: `src/Contexts/Activities/Domain/ValueObjects/*.cs`.
- Hecho cuando: todos los VOs rechazan entradas inválidas devolviendo Result (no excepciones) y sus tests pasan.
- Verificar: `dotnet test tests/UnitTests --filter Activities.Domain.ValueObjects`

#### [F1.3] Aggregate Activity con invariantes
`id: F1.3 · depende_de: F1.2 · tarea: [sin asignar] · estado: blocked`
- Objetivo: aggregate con factorías `Schedule(...)` y `RegisterCompleted(...)` que hacen imposibles los estados inválidos de §5.2.
- Fuente: §5.2 · DEC-1 · Discovery Anexo B.1.
- Archivos: `src/Contexts/Activities/Domain/Aggregates/Activity.cs`.
- Detalle: `static Result<Activity> Schedule(DealId, ActivityType, Description, DueAt, AdvisorId, CreatedById, IClock)` / `static Result<Activity> RegisterCompleted(DealId, ActivityType, Outcome, OutcomeType?, AdvisorId, CreatedById, IClock)`.
- Hecho cuando: cada invariante de §5.2 tiene su test rojo→verde.
- Verificar: `dotnet test tests/UnitTests --filter Activities.Domain`

#### [F1.4] Errores de dominio
`id: F1.4 · depende_de: F1.1 · tarea: [sin asignar] · estado: blocked`
- Objetivo: catálogo `ActivityErrors` de §5.4 sobre la taxonomía de Shared.
- Fuente: §5.4 · DEC-10 · `docs/plantilla/errores-dominio.md`.
- Archivos: `src/Contexts/Activities/Domain/Errors/ActivityErrors.cs`.
- Hecho cuando: cada error tiene código estable (`activity.*`) y los VOs/aggregate los usan.
- Verificar: `dotnet test tests/UnitTests --filter Activities`

### Fase 2 — Aplicación y persistencia · `blocked` (por GAP-P1 y resultado de F0.1)

**Estrategia de pruebas:** unit tests de use cases con puertos falsos + **integration tests contra DOS variantes reales de esquema** (`udbzq10trabajos` y un tenant universitario) — el drift es requisito de prueba, no un imprevisto (DEC-2).

#### [F2.1] Contrato IActivityRepository + filtros
`id: F2.1 · depende_de: F1.3 · tarea: [sin asignar] · estado: blocked`
- Objetivo: contrato del repositorio en dominio (patrón `repositorio.md`).
- Fuente: §5.5 · DEC-1.
- Archivos: `src/Contexts/Activities/Domain/Repositories/IActivityRepository.cs`.
- Detalle: `Task<Result<int>> AddAsync(Activity, CancellationToken)`; `Task<PagedResult<Activity>> GetPagedAsync(ActivityFilter, PageQuery, CancellationToken)` con `ActivityFilter {DealId?, OpportunityId?, DealStateId?}`.
- Hecho cuando: compila y está consumido por los use cases (F2.5).
- Verificar: `dotnet build`

#### [F2.2] Readers de Deal y Advisor
`id: F2.2 · depende_de: F1.1 · tarea: [sin asignar] · estado: blocked`
- Objetivo: `IDealReader` y `IAdvisorReader` (§5.5) con implementación EF sobre tablas foráneas.
- Fuente: §5.5 · Discovery Anexo B.1 (validaciones de existencia/rol/archivada).
- Archivos: `src/Contexts/Activities/Application/Ports/*.cs`, `src/Infrastructure/Persistence/EntityFramework/Activities/{DealReader,AdvisorReader}.cs`.
- Hecho cuando: integration test devuelve deal existente/archivado y asesor con/sin rol sobre la BD de pruebas.
- Verificar: `dotnet test tests/IntegrationTests --filter Activities.Readers`

#### [F2.3] Mapeo EF drift-safe de tbl_opo_negocios_actividades
`id: F2.3 · depende_de: F2.1, F0.1 · tarea: [sin asignar] · estado: blocked`
- Objetivo: `ActivityConfiguration` con columnas explícitas (las 15 comunes), conversión char↔enum, (completada,anulada) NULL⇒Scheduled.
- Fuente: §4 (mapeo) · DEC-2, DEC-6 · Discovery §4.1/§4.1-bis.
- Archivos: `src/Infrastructure/Persistence/EntityFramework/Activities/ActivityConfiguration.cs`, `ApplicationDbContext`.
- Detalle: nunca mapear `ConsecutivoActMiG` ni `negact_descripcion_virtual` como propiedades de dominio (la segunda se mapea shadow para no perderla en updates futuros — decisión menor documentada en el paso).
- Hecho cuando: el mismo mapeo materializa filas correctamente en **ambas** BDs de prueba (16 y 15 columnas).
- Verificar: `dotnet test tests/IntegrationTests --filter Activities.Mapping`

#### [F2.4] ActivityRepository + transacción de escritura con side-effects
`id: F2.4 · depende_de: F2.3, F1.0 · tarea: [sin asignar] · estado: blocked`
- Objetivo: implementación del repositorio y de la transacción DEC-3 (insert + `opo_fecha_ultimo_registro` condicional + `IAuditLogger`→`pa_seg_auditoria_ingresar`).
- Fuente: DEC-3 · Discovery §4.2 (regla exacta del UPDATE condicional).
- Archivos: `src/Infrastructure/Persistence/EntityFramework/Activities/ActivityRepository.cs`, adaptador de auditoría.
- Hecho cuando: integration test demuestra: (a) inserción devuelve id; (b) `opo_fecha_ultimo_registro` solo avanza, nunca retrocede; (c) fila de auditoría creada; (d) fallo en cualquiera revierte todo.
- Verificar: `dotnet test tests/IntegrationTests --filter Activities.Repository`

#### [F2.5] Use cases GetActivities y CreateActivity
`id: F2.5 · depende_de: F2.2, F2.4 · tarea: [sin asignar] · estado: blocked`
- Objetivo: los dos casos de uso de §5.6 con sus DTOs, validación de request y publicación de `ActivityRecorded`.
- Fuente: §5.6 · §6 · DEC-3.
- Archivos: `src/Contexts/Activities/Application/UseCases/**`.
- Hecho cuando: unit tests (puertos falsos) cubren cada regla condicional de la tabla §6.2 y el flujo feliz de §6.1.
- Verificar: `dotnet test tests/UnitTests --filter Activities.Application`

#### [F2.6] Integration tests de paridad multi-esquema
`id: F2.6 · depende_de: F2.5 · tarea: [sin asignar] · estado: blocked`
- Objetivo: suite que corre los dos use cases contra `udbzq10trabajos` y `udbzunilimachiolecca_Primary` (o la variante que F0.1 designe como representativa).
- Fuente: DEC-2 · Discovery §4.1-bis · GAP-P9.
- Hecho cuando: misma suite verde en ambas variantes; cualquier divergencia de comportamiento queda documentada como riesgo.
- Verificar: `dotnet test tests/IntegrationTests --filter Activities -e TENANT_MATRIX=trabajos,lima`

### Fase 3 — API, adaptador y corte · `blocked` (por DEC-5/DEC-9 en `propuesta` + GAP-P8/P10)

**Estrategia de pruebas:** golden tests de paridad (misma petición → API legado vs adaptador+servicio, comparación de payloads normalizados), sobre las validaciones de Anexo B.1; canario con flag en una institución de bajo tráfico antes del consumidor real.

#### [F3.1] ActivitiesController
`id: F3.1 · depende_de: F2.5 · tarea: [sin asignar] · estado: blocked`
- Objetivo: `GET /activities` y `POST /activities` según §6, patrón `ServiceInfoController`.
- Fuente: §6 · DEC-5, DEC-9 · `docs/plantilla/controllers.md`.
- Archivos: `src/Api/Controllers/ActivitiesController.cs`.
- Hecho cuando: OpenAPI expone ambos endpoints con sus contratos y los tests de API pasan.
- Verificar: `dotnet test tests/UnitTests --filter Api.Activities && curl -fs localhost:8080/openapi/v1.json | grep -q '"/activities"'`

#### [F3.2] Mapeo de errores dominio→HTTP y mensajes
`id: F3.2 · depende_de: F3.1, F1.4 · tarea: [sin asignar] · estado: blocked`
- Objetivo: cerrar la tabla §6.x con los códigos reales de `ErrorHttpMapper` (incluida la divergencia `AdvisorNotAllowed` 404-legado vs mapper).
- Fuente: §6.x · DEC-10.
- Hecho cuando: §6.x no tiene ninguna celda "según ErrorHttpMapper (verificar)" y hay un test por fila.
- Verificar: `dotnet test tests/UnitTests --filter Activities.ErrorMapping`

#### [F3.3] Documentación del servicio
`id: F3.3 · depende_de: F3.1 · tarea: [sin asignar] · estado: blocked`
- Objetivo: `docs/servicio/` con el contexto Activities (decisiones, contrato, procedencia legada).
- Fuente: template `docs/plantilla/README.md`.
- Hecho cuando: el doc referencia este plan y el Discovery, y pasa revisión de PR.
- Verificar: `existencia de docs/servicio/activities.md enlazado desde docs/servicio/README.md`

#### [F3.4] Adaptador con feature flag en el monolito
`id: F3.4 · depende_de: F3.2, F0.2 · tarea: [sin asignar] · estado: blocked`
- Objetivo: `Areas/API/v1/GestionComercial/Controllers/ActividadesController.cs` del monolito delega en crm-service (contrato español intacto) cuando la institución tiene el flag; fallback al camino legado si el flag está apagado.
- Fuente: DEC-5, DEC-9 · §7.4 · GAP-P10.
- Archivos: monolito `jack` (rama/tarea propia), traductor ES↔EN de campos según §3.1 + Anexo B.1.
- Hecho cuando: con flag OFF el comportamiento es byte-idéntico al actual; con flag ON los golden tests de F3.5 pasan.
- Verificar: `suite de golden tests en ambos estados del flag`

#### [F3.5] Paridad, canario y corte
`id: F3.5 · depende_de: F3.4, F2.6 · tarea: [sin asignar] · estado: blocked`
- Objetivo: activar el flag en una institución de bajo tráfico, comparar 2 semanas de telemetría, luego cortar al consumidor real (GAP-P8).
- Fuente: §1 estrategia · Discovery §8.0 (⚠️ todas las métricas de App Insights están muestreadas 10:1 — comparar con `sum(itemCount)`, jamás `count()`).
- Hecho cuando: 0 divergencias funcionales en el periodo canario y el consumidor real migrado con el flag.
- Verificar: `KQL de comparación legado-vs-servicio documentada y con resultado en §9.3`

### Diferido, sin fecha

| Qué | Decisión que lo respalda |
|-----|--------------------------|
| Frente MVC (18 acciones), bandeja próximas, badge, export | Estrategia §1 (fase 2 del strangler); requiere resolver primero D4/D29/D17 en diseño |
| Cierre masivo (`api/negocios` + MVC duplicado) como use case único con guarda | Discovery GAP-9 (regla canónica: la variante API); D8 |
| Endpoint de cambio de estado idempotente (sustituye el toggle D22) | DEC-6; se diseña con el frente MVC |
| Migración de datos a almacenamiento propio + normalización `NOT NULL` + índices | DEC-2; bloqueada por GAP-P2/GAP-P3 |
| Reunión virtual, adjuntos, reporte 504 | DEC-4; Discovery GAP-3 y §9 |
| Mover el side-effect `opo_fecha_ultimo_registro` a evento consumido por Oportunidades | DEC-3 (el evento ya queda emitido); GAP-P5 |

## 9. Riesgos, GAPs y changelog

### 9.1 Riesgos

| # | Riesgo | Estado |
|-----|--------|--------|
| R1 | El drift real (378 instituciones) revela variantes no contempladas por el subconjunto de 15 columnas → F0.1 puede invalidar F2.3 | abierto (mitigación: F0.1 antes de F2) |
| R2 | El consumidor del API legado (GAP-P8) depende de semántica no documentada (orden, campos extra del payload español) | abierto (mitigación: golden tests F3.5) |
| R3 | Consultas sin índices sobre la tabla legada (D15): aceptable al volumen actual, degrada si el frente MVC migra sobre el mismo acceso | aceptado fase 1, revisar en fase 2 |
| R4 | Métricas de paridad falseadas por el muestreo 10:1 de App Insights (Discovery §8.0) si alguien compara `count()` | abierto (regla escrita en F3.5) |
| R5 | Presión por "aprovechar" y migrar el combo de asesores (D6, 3 s) dentro de este contexto — pertenece a Oportunidades/Seguridad | abierto (guardarraíl §1 fuera de alcance) |
| R6 | La auditoría legada concatena datos personales (identificación, correo, celular) — el servicio la conserva por paridad (DEC-3) y hereda esa exposición | aceptado, señalado a seguridad junto con GAP-P7 |
| R7 | Doble contrato (ES en monolito, EN en servicio) desincronizado si el legado recibe cambios durante la convivencia | abierto (mitigación: adaptador único punto de traducción + golden tests) |

### 9.2 GAPs vivos

```
⚠️ GAP-P1 (BLOQUEANTE): Ninguna decisión de §2 está firmada; DEC-1/2/3/4/6/7/8 afectan la Fase 1-2 y DEC-5/9/10 la Fase 3 · Afecta: F1.*–F3.* · Confirmar con: tech lead de crm-service (+ PO para DEC-1/DEC-4/DEC-7)
⚠️ GAP-P2 (BLOQUEANTE ← GAP-5 Discovery): extensión del drift de esquema en las 378 instituciones · Afecta: F2.3, F2.6, toda migración futura · Confirmar con: DBA/plataforma · Resuelve: F0.1
⚠️ GAP-P3 (← GAP-4 Discovery): discriminador de filas migradas en tenants sin ConsecutivoActMiG · Afecta: fase de migración (diferida) · Confirmar con: tech lead + QA, con datos de F0.1
⚠️ GAP-P4 (← GAP-3 Discovery): confirmación de PO de que reunión virtual queda fuera (DEC-4) · Afecta: F1.2, F3.4 (exclusión del máster) · Confirmar con: Product Owner
⚠️ GAP-P5 (← GAP-2 Discovery): dueño final del side-effect opo_fecha_ultimo_registro (evento vs escritura directa permanente) · Afecta: evolución post-fase 1 de F2.4 · Confirmar con: dueño del dominio Oportunidades
⚠️ GAP-P6: claves Jira sin crear para los pasos F0.1–F3.5 · Afecta: trazabilidad paso→tarea→PR · Confirmar con: tech lead
⚠️ GAP-P7 (← GAP-7 + GAP-11 Discovery): tickets del monolito para la fuga de partners (D2/D3) y la superficie sin auth (D9/D10) — paralelos, no bloquean este plan pero sí el riesgo de la convivencia · Afecta: seguridad durante el strangler · Confirmar con: tech lead + seguridad · Se registra en: F0.3
⚠️ GAP-P8: identidad del consumidor de los 80 GET/30 d de api/actividades (Azure) · Afecta: F3.4/F3.5 (a quién se corta) · Confirmar con: telemetría (F0.2) + comercial
⚠️ GAP-P9 (← GAP-10 Discovery): acceso de lectura a la BD del mayor consumidor (udbztrabogotalonjadecolombia) para la matriz de pruebas F2.6 · Afecta: representatividad de la paridad · Confirmar con: DBA
⚠️ GAP-P10: mecánica de autenticación servicio-a-servicio del adaptador (el API legado acepta header aplentId sin usuario; el servicio exige identidad — DEC-9) · Afecta: F3.4 · Confirmar con: plataforma/seguridad
⚠️ GAP-P11: fuente de la zona horaria por tenant para IClock (¿TenantInfo del resolver? ¿parámetro de cultura legado?) · Afecta: F1.0, F2.4 · Confirmar con: plataforma · Se investiga en: F0.4
```

### 9.3 Changelog de enmiendas

| Fecha | Qué cambió | Decisión afectada | Pasos afectados | Tareas invalidadas |
|-------|------------|-------------------|-----------------|--------------------|
| 2026-08-14 | Versión inicial del plan (a partir del Discovery consolidado + auditoría real del repo crm-service `main@9f24956`) | — | — | — |

---

## Criterio de cierre

El plan pasa a `approved` cuando:

- [x] Las diez secciones están escritas o justificadas.
- [x] Cada decisión de §2 tiene alternativas descartadas, consecuencias y `Afecta:`.
- [ ] Toda decisión que afecte a la Fase 1 está en `estado: aprobada`. ← **pendiente: todas en `propuesta` (GAP-P1)**
- [x] Todo campo de entrada de §6 tiene su fila en la tabla de validaciones.
- [x] `Shared` está auditado en §5.5 (sobre el repo real; 2 filas se confirman en F0.4).
- [x] Cada paso de §8 tiene `id`, `depende_de`, `Fuente`, `Hecho cuando` y `Verificar` (falta `tarea:` — GAP-P6).
- [ ] El tech lead escribió `APROBADO` sobre §2.
