---
service: crm-service-q10
context: loss-reasons (Causas de pérdida)
doc: plan
status: draft
source: discovery_causas.md
updated: 2026-09-04
---

# Plan de trabajo — Causas de pérdida (`crm-service-q10`)

> Generado con el prompt **v2.0** de `Services WorkFlow › Promps › Plan de trabajo para migración de microservicios .NET` y la plantilla `Services WorkFlow › Templates › Working plan`. Diez secciones, en este orden.
>
> Fuente funcional: [`discovery_causas.md`](discovery_causas.md) (Discovery del contexto, `jack@e9bbcb03f14`).
> Fuente técnica: `docs/plantilla/*.md` del propio repositorio + inventario real del código.

## 0. Cómo ejecutar este plan

> Dirigido al agente ejecutor. Copiar tal cual.

1. **Antes de ejecutar nada, verifica el plan.** Recorre todos los pasos y confirma que cada uno tiene `id`, `depende_de` existente, `estado`, `Fuente:`, `Hecho cuando:` y `Verificar:`. Confirma que ninguna decisión de §2 que afecte tu fase está en `estado: propuesta`, y que no quedan GAPs `BLOQUEANTE` abiertos que la afecten. Si algo falta, **detente y repórtalo**: no ejecutes un plan incompleto ni completes tú lo que falte.
2. Ejecuta los pasos en orden de `id`, respetando `depende_de`. No inicies pasos con `estado: blocked`.
3. Al terminar un paso, corre su comando de `Verificar` y solo entonces cambia `estado: pending` → `done` en este mismo archivo.
4. **Si la realidad del repositorio contradice el plan** (el archivo ya existe, la interfaz tiene otra firma, la tabla tiene otras columnas): detente, no improvises. Reporta con el formato `⚠️ GAP` y espera instrucción.
5. No agregues alcance. Si detectas una mejora, anótala como riesgo; no la implementes.

## 1. Contexto y alcance

Se construye el contexto **`LossReason`** en `crm-service-q10`: el catálogo de razones por las cuales se pierde un negocio, hoy en el monolito Jack bajo `Areas/GestionComercial` (`tbl_opo_causas`, 8 filas en el tenant analizado, 6 SPs propios).

Estado del repositorio destino: **fork limpio de `service-template-dotnet`**, un solo commit (`d00b533 chore: scaffold crm-service from dotnet service template`), rama base `main`, un único contexto de referencia (`ServiceInfo`) y **cero** entidades EF, repositorios o migraciones. El repositorio **no compila hoy** (`GAP-1`).

**Dentro del alcance**

* Contexto `LossReason` completo: `Domain` + `Application`, persistencia EF Core sobre `tbl_opo_causas`, y los 5 endpoints REST que reemplazan el CRUD y el endpoint de API del legado.
* Lectura del uso de una causa en `tbl_opo_negocios`, **solo** para poder responder 409 al borrar (vía Reader, sin repositorio propio).
* Tests unitarios y de integración, con la puerta de cobertura del repositorio.

**Fuera del alcance de esta iteración** (heredado de Discovery §9, sin ajustes posteriores)

* **La escritura de `tbl_opo_negocios.neg_cau_consecutivo`** — la asignación de la causa a un negocio pertenece al agregado *Negocio*, tiene 4 escritores (dos fuera de `GestionComercial`) y la invariante *ganado ⇒ sin causa*. Ver `GAP-7`.
* Las 4 vistas Razor, el dropdown de `Negocios/_Estados.cshtml` y el exportable de oportunidades.
* Los 8 SPs ajenos que leen `tbl_opo_causas` por `LEFT JOIN`: siguen en el monolito.
* **La integración y el corte** (client en el monolito, feature flag, cutover): es materia de `03-flujos.md`, no de este plan. §7.4 deja la tabla de reemplazo de rutas para que ese documento la consuma.

**Fuera de alcance de forma permanente**

* `pa_inf_opo_excel_oportunidades_dinamico_VERSION_ANTERIOR` y `…_brayan`: copias muertas, 0 referencias.
* `tbl_aca_causas` y `causeradi_*`: otros dominios homónimos (Discovery §2.1).

## 2. Decisiones cerradas (ADR)

> **Las catorce decisiones están `aprobada` desde el 2026-08-14** (resolución de `GAP-6`). Ninguna fase queda condicionada por una decisión sin firmar, y §8 pasa entero a `pending`.
>
> D1–D11 son de diseño del servicio; **D12, D13 y D14 registran las resoluciones de `GAP-2`, `GAP-3` y `GAP-5`**, que no cambian ningún paso pero sí las condiciones de operación bajo las que el plan es válido.

### D1 — Persistir con EF Core Database First contra `tbl_opo_causas`, sin usar los stored procedures del legado

`estado: aprobada · firmó: tech lead · fecha: 2026-08-14 · origen: [template] + [Discovery §4.2]`

* **Decisión:** el contexto accede a `tbl_opo_causas` con EF Core sobre `ApplicationDbContext`, mapeando la tabla como entidad de persistencia. Los 6 SPs (`pa_opo_causas_*`, `pa_apis_opo_causas_retornar`) **no se invocan** desde el servicio.
* **Alternativas descartadas:** llamar a los SPs con `FromSqlRaw`, porque arrastraría el parámetro muerto `@aplent_codigoP` (Discovery D5), el contrato `@NmbError`/`@MsgError` y la ausencia de paginación del SP web, y porque la plantilla no documenta ni usa SPs ni Dapper en ningún punto · reescribir los SPs, porque es trabajo en el monolito y este plan no lo toca.
* **Consecuencias:** los SPs quedan vivos sirviendo al monolito hasta el decomiso; el servicio y el monolito escriben la **misma tabla física** durante la convivencia; toda la semántica de error pasa a `SqlServerErrorClassifier`.
* **Afecta:** §4 · §5.5 · pasos F2.1–F2.7.

### D2 — `LossReason` es un Aggregate con Repository, no un catálogo leído por Reader

`estado: aprobada · firmó: tech lead · fecha: 2026-08-14 · origen: [template]`

* **Decisión:** modelar `LossReasonAggregate` con `ILossReasonRepository : IRootRepository<LossReasonAggregate, int>`.
* **Alternativas descartadas:** tratarlo como catálogo servido por un Reader, porque el árbol de decisión de `conceptos-reader-provider-repository.md` corta en la primera pregunta —*"¿Escribe, o es la fuente de verdad de un Aggregate del contexto?"*— y este servicio es dueño del CRUD. La regla *"los catálogos no llevan repositorio propio"* aplica a catálogos **ajenos** al contexto, no al que el contexto posee.
* **Consecuencias:** el contrato del repositorio vive en `Domain/Repositories/`; hay entidad de persistencia + mapper separados del agregado; el Reader queda reservado para `tbl_opo_negocios` (D7).
* **Afecta:** §5 · §5.5 · pasos F1.3, F1.5, F2.4.

### D3 — Identificador `int` heredado, poblado por `IDENTITY`, con `CreateAsync`

`estado: aprobada · firmó: tech lead · fecha: 2026-08-14 · origen: [Discovery §4.1]`

* **Decisión:** `LossReasonAggregate : AggregateRoot<int>`; la creación usa `ILossReasonRepository.CreateAsync`, no `AddAsync` + `CommitAsync`.
* **Alternativas descartadas:** `Guid` como en el ejemplo `Product` de la plantilla, porque la columna es `int IDENTITY` y la FK entrante desde `tbl_opo_negocios` ya la referencia — cambiar el tipo obligaría a migrar 299.937 filas · `AddAsync` + Unit of Work, porque `repositorio.md` reserva `CreateAsync` exactamente para el caso `IDENTITY` (devuelve el agregado con su PK).
* **Consecuencias:** `CreateLossReasonUseCase` **no inyecta `IUnitOfWorkPort` ni llama a `CommitAsync`** (regla explícita de `repositorio.md`); el resto de los use cases de escritura sí.
* **Afecta:** §4 · §5.2 · §5.6 · pasos F1.3, F1.5, F2.4, F3.3.

### D4 — `Name` es un primitivo, no un Value Object

`estado: aprobada · firmó: tech lead · fecha: 2026-08-14 · origen: [template]`

* **Decisión:** `Name` se modela como `string` en el agregado. **Obligatoriedad y longitud se validan en dos lugares, a propósito:** en FluentValidation sobre el DTO de entrada (rechazo temprano, con `Property` y 400) **y como invariante dentro de `LossReasonAggregate.Create` y `.Update`**, que acumulan `NameRequired` y `NameTooLong` y los devuelven juntos.
* **Alternativas descartadas:** un VO `LossReasonName`, porque `value-objects.md` lo prohíbe explícitamente para el caso *Required + MaxLength* sin más lógica de negocio (`❌ NO crear → SummaryValueObject con esas mismas dos reglas`), y `cau_nombre` no tiene ninguna otra regla en el legado · dejar la validación **solo** en FluentValidation, que es lo que la documentación considera suficiente para este caso: se descarta porque deja el agregado construible en estado inválido desde cualquier llamador que no pase por HTTP (un job, un test, un caso de uso futuro), y la invariante de negocio deja de estar en el dominio.
* **Consecuencias:** el contexto **no tiene carpeta `Domain/ValueObjects/` poblada**. La regla vive duplicada: **cambiar el límite obliga a tocar dos archivos** —el validador y el agregado— y el test de F1.6 existe precisamente para que la divergencia se note. Una regla futura sobre el nombre (unicidad, formato) obliga a revisitar esta decisión.
* **Afecta:** §5.2 · §5.3 · §5.4 · §6 · pasos F1.1, F1.3, F1.6, F4.1.

### D5 — Límite único de 50 caracteres para `Name`

`estado: aprobada · firmó: tech lead · fecha: 2026-08-14 · origen: [equipo] + [Discovery §7 D4, veredicto «se corrige»]`

* **Decisión:** el límite del servicio es **50**. Se aplica en FluentValidation (`MaximumLength(50)`) **y** en la invariante del agregado (D4), y se publica en `[property: Description(...)]` y en `LossReasonErrors.NameTooLong.Attributes["max"]`.
* **Alternativas descartadas:** 200, el de la columna `varchar(200)`, porque sería un límite más laxo que el que la UI viene aplicando desde 2019 y consolidaría por API nombres que la pantalla nunca podría mostrar completos · 51, el del `maxlength` del input, que no es un límite sino un artificio para disparar la validación de cliente.
* **Consecuencias:** **es paridad exacta con el frente web del legado** (`CausasViewModel.MaxLength(50)`), que es el consumidor que se corta primero. Elimina las tres longitudes en conflicto del Discovery D4 dejando una sola en el servicio. Cierra el escenario de Discovery D6 —el fallo de validación que convertía una edición en creación— porque crear y actualizar pasan a ser verbos distintos (§6.3), no porque el input deje de ser inválido. **Riesgo asumido:** la columna admite 200 y el endpoint `GET api/causas` del legado nunca validó longitud, así que **pueden existir filas con más de 50 caracteres**; el servicio las **lee** sin problema (`Reconstruct` no valida, D6) pero **rechaza el `PUT`** hasta que se acorte el nombre. Queda como riesgo R7, con la consulta de verificación en §9.1.
* **Afecta:** §5.2 · §5.4 · §6.1 · §6.2 · §6.3 · §9.1 R4, R7 · pasos F1.1, F1.3, F1.6, F4.1, F4.4, F5.1.

### D6 — El servicio trata `cau_nombre` y `cau_estado` como `NOT NULL` aunque la BD no lo exija

`estado: aprobada · firmó: tech lead · fecha: 2026-08-14 · revisada y refirmada: 2026-08-21 · origen: [Discovery §4.1] + [template] + [decisión de equipo, 2026-08-21]`

* **El hecho, primero:** **la BD no tiene la restricción.** `cau_nombre varchar(200)` y `cau_estado bit` **aceptan NULL** hoy, en todos los tenants. Está verificado por tres vías independientes: el dump `02-columnas.tsv` leído con la trampa del script (`Dump-DbSchema.ps1` serializa `True` como cadena vacía, así que `acepta_null` vacío significa *sí acepta*, calibrado contra `cau_consecutivoP`, que es `IDENTITY` y también aparece vacío en su columna); el cuerpo de `pa_opo_causas_modificar`, que declara `@cau_nombre VARCHAR(200) = NULL` y lo asigna sin guarda; y Discovery §7 D2/D3, que lo registraron con dos fuentes cada uno. **El Discovery tiene razón y no se corrige.**
* **Decisión:** aun así, **el servicio trata las dos columnas como no anulables**. `LossReason.Name` se declara `string` y `LossReason.IsActive` como `bool` en la entidad EF; `LossReasonRepositoryMapper.ToDomain` pasa los dos valores tal cual a `Reconstruct`, sin normalizar; la configuración lo hace explícito con `.IsRequired()` sobre `Name` (`bool` no anulable ya lo implica para `IsActive`). Es una decisión **de integridad de la información**: el servicio no acepta como válido un dato que el dominio considera inválido, y no lo disfraza.
* **Por qué se elige esto y no normalizar:** normalizar `null → string.Empty` / `null → false` en el mapper —como estuvo escrita esta decisión hasta el 2026-08-21— **convierte un dato corrupto en un dato plausible**: una causa sin nombre se sirve como causa de nombre vacío, y una sin estado se sirve como inactiva. El consumidor no puede distinguir «no tiene nombre» de «se llama ""», y el defecto se propaga silencioso en vez de salir a la luz. Tratarlas como `NOT NULL` hace que una fila corrupta **falle de forma ruidosa**, que es el comportamiento que el equipo quiere: se arregla el dato, no se maquilla.
* **Consecuencias, sin adornos:** el mapper es una traducción directa, sin ramas, y el filtro por nombre no necesita guarda de nulo. **Pero la BD sí puede devolver NULL**, y cuando lo haga SqlClient lanzará `SqlNullValueException` **por la consulta entera, no por la fila**: el `GET /loss-reasons` de ese tenant responde 500 hasta que el dato se corrija. Eso es deliberado —es el «fallo ruidoso» que la decisión busca—, pero **no es gratis**, y por eso queda como riesgo **R10** en §9.1 con su consulta de detección. La condición que sostiene esta decisión es que el dato esté limpio en los tenants objetivo **antes del corte**.
* **Lo que esta decisión NO hace:** no altera la BD. No hay `ALTER TABLE` ni migración en el alcance de este plan (`tasks_causas.md` R5), así que el veredicto «se corrige → `NOT NULL`» de Discovery D2/D3 **sigue pendiente del lado del esquema** y es trabajo de otro. Mientras no se aplique, el monolito puede seguir escribiendo NULL por `pa_opo_causas_modificar` a espaldas del servicio.
* **Afecta:** §4 · §9.1 R10 · pasos F2.1, F2.2, F2.3, F2.8, F2.9, F1.3, F5.1.

### D7 — El borrado valida el uso con un Reader antes de borrar, y deja el 547 como red de seguridad

`estado: aprobada · firmó: tech lead · fecha: 2026-08-14 · origen: [Discovery §7 D7, veredicto «se corrige»] + [template]`

* **Decisión:** `DeleteLossReasonUseCase` consulta `ILossReasonUsageReader.IsUsedAsync(id)` sobre `tbl_opo_negocios` y devuelve `LossReasonErrors.InUse(id)` (`ErrorType.Conflict` → 409) si la causa está asignada. Si la carrera se pierde, la FK `FK_tbl_opo_causas_tbl_opo_negocios` (`NO_ACTION`) produce el error 547 que `SqlServerErrorClassifier` ya traduce a `Conflict`.
* **Alternativas descartadas:** apoyarse **solo** en el 547, porque `repositorio.md` establece que el clasificador deliberadamente **no** expone un predicado para el 547 y que nombrar el valor culpable se valida en el caso de uso con una consulta de existencia previa; sin el pre-chequeo el 409 llega sin mensaje útil · borrado lógico (`IsActive = false`), porque cambiaría el contrato observable del legado, donde borrar y desactivar son dos operaciones distintas y ambas existen.
* **Consecuencias:** el contexto necesita **leer una tabla ajena** → un Reader (`Application/Ports/`), no un repositorio (regla explícita: no crear un repositorio sobre algo que no es agregado). `neg_cau_consecutivo` **no está indexado** (verificado: 6 índices en `tbl_opo_negocios`, ninguno lo cubre), así que el chequeo es un scan sobre ~300.000 filas — aceptable porque borrar una causa es una acción administrativa rara, y queda como riesgo R2.
* **Afecta:** §5.5 · §6.5 · §9.1 R2 · pasos F2.5, F2.6, F3.5.

### D8 — Paginación siempre, en los dos frentes

`estado: aprobada · firmó: tech lead · fecha: 2026-08-14 · origen: [Discovery §5.3] + [template]`

* **Decisión:** `GET /loss-reasons` es siempre paginado con `PageQueryInputDto` (`PageIndex` base 0, `PageSize` 20, máximo 100) y responde `{ items, totalCount }`.
* **Alternativas descartadas:** replicar el frente web, que no pagina y devuelve la lista completa, porque no tiene techo y la plantilla no ofrece un contrato de lista sin paginar.
* **Consecuencias:** **ruptura de paridad con el frente web** del legado. El consumidor del monolito debe enviar un `pageSize` que preserve el comportamiento actual, o paginar. Va a `03-flujos.md` §3 como ruptura declarada, no como bug.
* **Afecta:** §6.1 · §7.4 · §9.1 R4 · pasos F2.4, F3.1, F4.2.

### D9 — Filtro de estado opcional y catálogo vacío con 200

`estado: aprobada · firmó: tech lead · fecha: 2026-08-14 · origen: [Discovery §5.3]`

* **Decisión:** `isActive` es un filtro **opcional** (sin él se devuelven todas); un resultado vacío responde **200 con `items: []` y `totalCount: 0`**.
* **Alternativas descartadas:** replicar `GET api/causas`, que exige `Estado` y responde **400** si falta, y **404** si no hay resultados. Ambos comportamientos contradicen el contrato uniforme de la plantilla: un filtro sin resultados no es un recurso ausente.
* **Consecuencias:** **dos rupturas de paridad** con el frente API del legado. Cualquier consumidor actual de `GET api/causas` que trate el 404 como "no hay causas" debe actualizarse antes del corte. Va a `03-flujos.md` §3.
* **Afecta:** §6.1 · §6.5 · §9.1 R4 · pasos F3.1, F4.1, F4.2.

### D10 — Caché solo en L1 (Output Caching), sin L2

`estado: aprobada · firmó: tech lead · fecha: 2026-08-14 · origen: [equipo] + [template]`

* **Decisión:** los dos endpoints de lectura se cachean en **L1** con `[OutputCache]` bajo el tag `loss-reasons`, y las tres escrituras lo invalidan con `[OutputCacheInvalidate]`. **No se usa L2** (`ICacheStore`): el contexto no inyecta caché en el repositorio ni en los casos de uso.
* **Alternativas descartadas:** L1 + L2, porque duplicaría la invalidación (L1 por tag en el borde, L2 post-commit en el caso de uso) para un catálogo de 8 filas · sin caché, la versión anterior de esta decisión, revisada por el equipo · cachear el listado **con la política base**, porque `cache.md` es explícito: la política base varía por tenant y headers, **no** por los parámetros de filtro, así que serviría el resultado de un filtro para otro.
* **Consecuencias:** el listado es filtrado, así que **necesita su propia política** que varíe además por `name`, `isActive`, `pageIndex` y `pageSize` — sin eso, cachearlo es un bug de correctitud, no una optimización. Esa política es un archivo compartido del arranque y se registra en F4.3. La variación por `X-Entity-Code` la aporta la política base y es lo que mantiene el aislamiento entre tenants: **si un request llega sin el header, su respuesta se guarda como «sin tenant» y se comparte** — otra razón por la que §7.1 exige el header. La invalidación solo dispara si el handler no lanzó y el status es `< 400`.
* **Afecta:** §5.5 · §7.2 · §7.3 · pasos F4.2, F4.3, F5.1.

### D11 — Ante la contradicción entre los documentos de la plantilla y el scaffold, manda el documento

`estado: aprobada · firmó: tech lead · fecha: 2026-08-14 · origen: [template]`

* **Decisión:** el contexto sigue `docs/plantilla/*.md`, no el código de `ServiceInfo`, en los tres puntos donde divergen: (a) la interfaz del caso de uso se llama `I{CasoDeUso}UseCase` y se coubica en `Application/UseCases/{CasoDeUso}/`, no `I…Port` en `Application/Ports/`; (b) los casos de uso se inyectan por **constructor primario del controller**, no como parámetro de la action; (c) los validadores van en `src/Infrastructure/Validation/FluentValidation/{Contexto}/`, siguiendo `validaciones.md` **y** el único precedente real del repo (`…/FluentValidation/Shared/PageQueryInputValidator.cs`), no en `Api/Validators/` como dice `contextos.md` §5.4.
* **Alternativas descartadas:** copiar `ServiceInfo`, porque es el andamiaje mínimo de la plantilla y ya se desvía de su propia documentación · abrir una `DESVIACIÓN` por cada punto, porque la desviación la tiene el scaffold, no este plan.
* **Consecuencias:** el contexto nuevo no se parece al de referencia en esos tres detalles. Quien compare por analogía va a notarlo: queda registrado aquí para que no se lea como error.
* **Afecta:** §5.6 · §6 · pasos F3.1–F3.5, F4.1, F4.2.

### D12 — El servicio no implementa autenticación: el control es de infraestructura

`estado: aprobada · firmó: tech lead · fecha: 2026-08-14 · origen: [equipo] — resolución de GAP-2`

* **Decisión:** el servicio **no** incorpora ningún mecanismo de autenticación. Solo es alcanzable a través de los pipelines de la plataforma, y esa restricción se valida en infraestructura, no en el código del servicio.
* **Alternativas descartadas:** JWT o API key en el servicio, porque duplicaría un control que la plataforma ya ejerce y agregaría una superficie de configuración (claves, rotación) sin dueño en este contexto.
* **Consecuencias:** **el aislamiento de red pasa a ser el único control de acceso.** Si el servicio se expusiera fuera de ese perímetro —un ingress mal configurado, un port-forward, un ambiente de pruebas abierto— quedaría sin ninguna barrera: cualquiera podría crear, editar y borrar causas. Eso no es una objeción a la decisión, es la condición que la sostiene, y por eso el riesgo R5 se reescribe en lugar de cerrarse. No hay pasos de §8 que construir ni deshacer.
* **Afecta:** §7 · §9.1 R5.

### D13 — El servicio no valida permisos: la autorización la ejerce Jack

`estado: aprobada · firmó: tech lead · fecha: 2026-08-14 · origen: [equipo] — resolución de GAP-3`

* **Decisión:** el servicio **no** modela funciones, roles ni permisos. Los servicios construidos con esta plantilla se consumen desde Jack —el mismo patrón que `comunicados/announcements`— y es Jack quien decide si el usuario puede ejecutar la operación antes de llamar.
* **Alternativas descartadas:** replicar el modelo de `tbl_seg_funciones` en el servicio, porque duplicaría la fuente de verdad de los permisos y obligaría a sincronizar dos catálogos · exigir un token con claims de rol, descartado junto con D12.
* **Consecuencias:** ya no hace falta consultar `tbl_seg_funciones` / `tbl_seg_roles_funciones` / `tbl_seg_menu`, así que el `GAP-3` heredado del Discovery se cierra sin trabajo de investigación. **Pero el defecto D1 del Discovery no se corrige con esta migración, solo cambia de dueño:** las 7 acciones `[AllowAnonymous]` de `EstructuracionComercialController` siguen abiertas en Jack, y el controller que llame al servicio necesitará sus filas de función para que la autorización exista de verdad. Eso es trabajo del lado de Jack, en `03-flujos.md`, y está anotado en §9.1 R9 para que no se pierda al cerrar el GAP.
* **Afecta:** §3.4 de Discovery · §7 · §9.1 R9 · §4 del backlog de tareas.

### D14 — El tenant lo determina y lo envía Jack

`estado: aprobada · firmó: tech lead · fecha: 2026-08-14 · origen: [equipo] — resolución de GAP-5`

* **Decisión:** Jack resuelve a qué institución pertenece la operación y transmite esa identidad al servicio; el servicio la recibe por el mecanismo estándar de la plantilla (`X-Entity-Code`) y resuelve la conexión con `TenantMiddleware` + `ITenantResolverServiceClient`, sin lógica propia.
* **Alternativas descartadas:** que el servicio deduzca el tenant del payload o de un parámetro de negocio, porque lo acoplaría al modelo de sesión del monolito · que Jack envíe la cadena de conexión, porque expondría una credencial en el borde y contradice el diseño del tenant-resolver, que la entrega cifrada.
* **Consecuencias:** **toda llamada desde Jack debe traer el `X-Entity-Code`.** Una llamada sin él no solo consulta la base equivocada: además el caché L1 la guarda como «sin tenant» y comparte esa respuesta con las demás (D10). Es la misma condición por dos motivos distintos, y es lo que verifica el escenario de caché de F5.1. No cambia §7.1: el mecanismo ya estaba en la plantilla.
* **Afecta:** §7.1 · §7.2 · §9.1 R8 · paso F5.1.

## 3. Glosario y trazabilidad

### 3.1 Término de negocio (ES) → nombre técnico (EN)

| Negocio (ES) | Técnico (EN) | Dónde vive |
|---|---|---|
| Causa de pérdida | `LossReason` | contexto, agregado, endpoints |
| Nombre de la causa | `Name` | `LossReasonAggregate.Name` ← `cau_nombre` |
| Estado (activa/inactiva) | `IsActive` | `LossReasonAggregate.IsActive` ← `cau_estado` |
| Consecutivo | `Id` | `LossReasonAggregate.Id` ← `cau_consecutivoP` |
| Negocio | *(sin tipo propio)* | solo como tabla legada `tbl_opo_negocios`, leída por el Reader |
| Causa en uso | `InUse` | `LossReasonErrors.InUse(id)` |

**Regla de idioma:** contexto, clases, DTOs, endpoints y campos JSON en inglés. Tablas, columnas y SPs legados se citan tal cual (`tbl_opo_causas`, `cau_nombre`, `pa_opo_causas_retornar`). Los `[property: Description(...)]` de los DTOs van **en inglés** (revisión de QA sobre el PR de T6, 2026-08-21). ~~en español, como en la plantilla~~ — es una **desviación deliberada del ejemplo de `casos-de-uso.md` §5.2**, que los escribe en español, y deja el servicio con **una sola lengua en todo el artefacto técnico**, incluida la documentación OpenAPI que estos atributos alimentan. **Aplica a los DTOs de las cinco carpetas de casos de uso**, no solo a `GetLossReasons`.

### 3.2 Trazabilidad Discovery → Plan

| Discovery | Se usa en |
|---|---|
| §3.1 resolución de tenant | §7.1 |
| §3.3 rutas y acciones | §6 · §7.4 |
| §3.4 habilitación (`[AllowAnonymous]`, deny-by-default) | `GAP-2` · `GAP-3` · §9.1 R5 |
| §4.1 tablas y nulabilidad | §4 · D6 · pasos F2.1–F2.3 |
| §4.2 SPs y sus contratos | D1 · §4 |
| §5.1 escrituras (4 escritores de la asignación) | §1 fuera de alcance · `GAP-7` |
| §5.2 lecturas y conteos | §7.4 |
| §5.3 diferencias entre frentes | D8 · D9 · §9.1 R4 |
| §6 parámetros y personalizaciones (ninguno) | §7.2 |
| §7 D1 `se corrige` | `GAP-2` |
| §7 D2/D3 `se corrige` | D6 · §5.4 |
| §7 D4 `se corrige` | D5 · §9.1 R7 |
| §7 D6 `se corrige` | D5 (queda sin efecto) · §6.2 |
| §7 D7 `se corrige` | D7 |
| §7 D9 `se corrige` | D3 · §6.3 |
| §7 D11/D12 `se corrige` | fuera de alcance (asignación) · `GAP-7` |
| §8 sin telemetría | D10 · §7.3 |
| §9 alcance | §1 |
| §10 GAP-1 | §9.1 R1 |
| §10 GAP-2 | `GAP-3` |
| §10 GAP-5 | `GAP-6` |
| §10 GAP-6 | `GAP-7` |

## 4. Mapeo legado → modelo

> El esquema vive en Discovery §4. Acá se **mapea**, no se re-transcribe.

| Columna / SP legado | Propiedad de dominio | Tipo | Persistencia | Trampa |
|---|---|---|---|---|
| `cau_consecutivoP` | `Id` | `int` | PK, `ValueGeneratedOnAdd()` | Es `IDENTITY`: obliga a `CreateAsync` y prohíbe el `AddAsync`+`CommitAsync` (D3) |
| `cau_nombre` | `Name` | `string` | `varchar(200) **NULL** en la BD`, entidad `string` + `.IsRequired()` | La columna **sí acepta NULL** (Discovery §4.1, confirmado el 2026-08-21), pero **el servicio la trata como obligatoria por decisión** (D6): la entidad la declara no anulable y el mapper no normaliza. Una fila con NULL hace fallar la consulta → R10 |
| `cau_estado` | `IsActive` | `bool` | `bit **NULL** en la BD`, entidad `bool` | Idem: **sí acepta NULL**, y el servicio la trata como obligatoria (D6). Discovery D3 —el NULL que tumbaba el listado del legado— **no está cerrado**: el esquema no cambió, cambió quién falla y cómo → R10 |
| `@aplent_codigoP` (los 6 SPs) | — | — | — | Parámetro declarado y **nunca usado** en ningún cuerpo (Discovery D5). No cruza al modelo: el aislamiento es por base de datos, no por columna |
| `total_count` (`pa_apis_opo_causas_retornar`) | — | — | — | Lo reemplaza `PagedResult<T>.TotalCount`; no es una columna del dominio |
| `RETURN SCOPE_IDENTITY()` (`pa_opo_causas_ingresar`) | valor de retorno de `CreateAsync` | `int` | — | El legado lo descarta (`IngresarCausa` es `void`, Discovery D9); acá el 201 devuelve el recurso creado con su `id` |
| `neg_cau_consecutivo` (`tbl_opo_negocios`) | — | `int?` | entidad **keyless** de solo lectura | No cruza al dominio. Se lee únicamente para el chequeo de uso del borrado (D7). Columna **sin índice** → R2 |
| `neg_consecutivoP`, `neg_negest_consecutivo`, resto de `tbl_opo_negocios` | — | — | — | No se mapean: fuera del contexto |

## 5. Dominio

### 5.1 Estructura de carpetas

```
src/Contexts/LossReason/
├── Domain/
│   ├── LossReason.Domain.csproj              → ref. Shared.Domain
│   ├── Aggregates/
│   │   ├── LossReasonAggregate.cs
│   │   └── LossReasonArgs.cs
│   ├── Queries/LossReasonFilter.cs
│   ├── Repositories/ILossReasonRepository.cs
│   └── Errors/LossReasonErrors.cs
└── Application/
    ├── LossReason.Application.csproj         → ref. LossReason.Domain + Shared.Application
    ├── Ports/ILossReasonUsageReader.cs
    └── UseCases/
        ├── GetLossReasons/
        ├── GetLossReasonById/
        ├── CreateLossReason/
        ├── UpdateLossReason/
        └── DeleteLossReason/

src/Infrastructure/Persistence/EntityFramework/LossReasons/
├── LossReasonRepository.cs
├── LossReasonUsageReader.cs
├── Entities/{LossReason.cs, DealLossReasonUsage.cs}
├── Configurations/{LossReasonConfiguration.cs, DealLossReasonUsageConfiguration.cs}
└── Mappers/LossReasonRepositoryMapper.cs

src/Infrastructure/Validation/FluentValidation/LossReasons/
├── CreateLossReasonInputValidator.cs
├── UpdateLossReasonInputValidator.cs
└── GetLossReasonsInputValidator.cs

src/Api/Controllers/LossReasonsController.cs
src/Api/DependencyInjection/LossReasonServiceExtensions.cs
```

Las carpetas `Domain/ValueObjects/`, `Domain/Enums/`, `Domain/Entities/` y `Domain/Models/` **no se crean**: el contexto no tiene VOs (D4), ni enums, ni entidades hijas, ni modelos de lectura propios.

### 5.2 Aggregate

`LossReasonAggregate : AggregateRoot<int>` (`Shared.Domain.Aggregates`), `sealed`, constructor privado.

| Miembro | Firma | Regla |
|---|---|---|
| `Name` | `public string Name { get; private set; } = string.Empty;` | **invariantes del agregado: requerido y ≤ 50** (D4, D5) |
| `IsActive` | `public bool IsActive { get; private set; }` | sin invariante |
| Creación | `public static Result<LossReasonAggregate> Create(CreateLossReasonArgs input)` | valida **las dos** invariantes de `Name`, **acumula** en `List<ValidationError>` y cierra con `DomainError.FromValidationDomainErrors(errors)` |
| Reconstrucción | `public static LossReasonAggregate Reconstruct(int id, string name, bool isActive)` | **no valida, no llama `Created()`** — por eso una fila legada de más de 50 caracteres se lee sin error (R7) |
| Mutación | `public Result Update(UpdateLossReasonArgs input)` | **las mismas dos invariantes**, con la misma acumulación; llama `SetUpdatedAt(DateTime.UtcNow)` |

Las invariantes de `Name` se validan **también** aquí, no solo en FluentValidation (D4): el validador protege la entrada HTTP, el agregado protege el dominio de cualquier otro llamador. La constante del límite vive en el agregado (`public const int NameMaxLength = 50;`) y el validador la referencia, para que las dos capas no puedan divergir en silencio.
| Auditoría | `protected override void Created()` | **solo `SetCreatedAt`** en UTC. `UpdatedAt` queda `null` hasta la primera mutación real, que es lo que lo hace legible: «nunca se ha actualizado» ≠ «se actualizó al crearse». Es una **desviación consciente del ejemplo de `entidades-y-agregados.md`**, que fija ambos (revisión de QA, 2026-08-21) |

**`CreatedAt`/`UpdatedAt` no tienen columna en `tbl_opo_causas`.** El agregado los mantiene en memoria porque `AggregateRoot<TId>` los declara, pero **el mapper no los persiste ni los lee**: `ToDocument` los ignora y `Reconstruct` los deja en `null`. No se agregan columnas a una tabla del monolito.

### 5.3 Value Objects

Ninguno (D4). `Name` es `string`; su validación estructural vive en FluentValidation y su invariante en el agregado.

### 5.4 Errores de dominio

`Contexts/LossReason/Domain/Errors/LossReasonErrors.cs`, `public static class`, `public const string Context = "LossReason";`

| Miembro | Tipo | `ErrorType` | HTTP | Origen |
|---|---|---|---|---|
| `NameRequired` | `static readonly ValidationError` | `Validation` | 400 | invariante del agregado (D4) |
| `NameTooLong` | `static readonly ValidationError` | `Validation` | 400 | invariante del agregado (D4); lleva `Attributes["max"] = 50` (D5) |
| `NotFound(int id)` | método de fábrica → `DomainError` | `NotFound` | 404 | `GetById`, `Update`, `Delete` |
| `InUse(int id)` | método de fábrica → `DomainError` | `Conflict` | 409 | D7 |

Ambos `ValidationError` llevan `Property = nameof(LossReasonAggregate.Name)`. Los errores se declaran **sin `Context` ni `Origin`**; los sella quien los origina.

### 5.5 Contratos de repositorio y puertos

**Auditoría de `Shared` — obligatoria antes de diseñar cualquier abstracción (regla 3).** Inventario real del repositorio, no de la documentación:

| Capacidad | ¿Existe? | Ruta | Reutilizar / extender / crear |
|---|---|---|---|
| `Result` / `Result<T>` / `Result<TValue,TError>` | Sí | `src/Shared/Results/Result.cs` | **Reutilizar** |
| `PagedResult<T>` (`Items` + `TotalCount`) | Sí | `src/Shared/Results/PagedResult.cs` | **Reutilizar** |
| `DomainError`, `ErrorDetail`, `ErrorType` | Sí | `src/Shared/Results/Errors/` | **Reutilizar** |
| `ValidationError`, `ConflictError`, `NotFoundError` | Sí | `src/Shared/Results/Errors/` | **Reutilizar** |
| `SharedErrors.NotFound(entityName, id)` | Sí | `src/Shared/Results/Errors/SharedErrors.cs` | **No usar** — el mensaje del contexto es más específico; `LossReasonErrors.NotFound(id)` |
| `AggregateRoot<TId>` / `Entity<TId>` | Sí | `src/Shared/Domain/Aggregates/`, `…/Entities/` | **Reutilizar** |
| `IRootRepository<TAggregate,TId>` | Sí | `src/Shared/Domain/Interfaces/IRootRepository.cs` | **Reutilizar** (heredar) |
| `RepositoryBaseEF<TAggregate,TId>` | Sí | `src/Infrastructure/Persistence/EntityFramework/Common/RepositoryBaseEF.cs` | **No heredar** — asume que el agregado es la entidad mapeada; `contextos.md` §5.3 lo declara no aplicable y no tiene subclases en el repo |
| `PageQuery` / `PageQueryInputDto` | Sí | `src/Shared/Domain/Pagination/`, `src/Shared/Application/Dtos/` | **Reutilizar** |
| `PageQueryInputValidator` | Sí | `src/Infrastructure/Validation/FluentValidation/Shared/` | **Reutilizar** |
| `IUnitOfWorkPort` / `UnitOfWorkAdapter` | Sí | `src/Shared/Application/Ports/`, `src/Infrastructure/Adapters/Persistence/` | **Reutilizar** (no en `Create`, D3) |
| `SqlServerErrorClassifier` (547 → Conflict) | Sí | `src/Infrastructure/Adapters/Persistence/SqlServer/` | **Reutilizar** — cubre la red de seguridad de D7 |
| `PersistenceErrors.Failure(origin)` | Sí | `…/EntityFramework/Common/PersistenceErrors.cs` | **Reutilizar** |
| `ILoggerPort<T>` | Sí | `src/Shared/Application/Ports/ILoggerPort.cs` | **Reutilizar** |
| `IStructuralValidator<T>` + `ValidateRequestAttribute` | Sí | `src/Infrastructure/Validation/`, `src/Shared/Infrastructure/Presentation/Attributes/` | **Reutilizar** |
| `HttpOkResult<T>`, `HttpCreatedResult<T>`, `HttpNoContentResult`, `HttpOkPagedResult<T>` | Sí | `src/Shared/Infrastructure/Presentation/Results/` | **Reutilizar** |
| `ApiSuccessResponse<T>` / `ApiErrorResponse` / `ErrorHttpMapper` | Sí | `src/Shared/Infrastructure/Presentation/` | **Reutilizar** |
| Resolución de tenant (`TenantMiddleware`, `TenantContext`, `IDbConnectionProvider`) | Sí | `src/Api/Middleware/`, `src/Shared/Infrastructure/MasterAccess/` | **Reutilizar** — nada que construir (§7.1) |
| `ICacheStore` / `CacheKey` (L2) | Sí | `src/Shared/Application/` | **No usar** — D10 descarta L2 |
| `OutputCacheInvalidateAttribute` (L1) | Sí | `src/Shared/Infrastructure/Presentation/Filters/` | **Reutilizar** (D10) |
| Output cache configurado (`ConfigureCache` / `UseCacheMiddleware`, política base con `SetVaryByHeader("X-Entity-Code", "Accept-Language")`) | Sí | `src/Api/DependencyInjection/OutputCacheExtensions.cs` | **Reutilizar sin tocar.** La revisión del 2026-08-28 descartó registrar una política propia: el listado complementa la base desde el atributo (enmienda de `F4.3`) |
| Entidad EF, `IEntityTypeConfiguration`, mapper, repositorio concreto | **No** | — | **Crear** en el contexto (F2) |
| `DbSet` alguno en `ApplicationDbContext` | **No** | `…/ApplicationDbContext.cs` | **Extender** con el `DbSet` del contexto (F2.7) |

**Nada se crea en `Shared`.** El contexto no necesita ninguna capacidad transversal nueva, así que no hay paso de extensión de `Shared` ni PR aparte.

Contratos propios:

```csharp
// Contexts/LossReason/Domain/Repositories/ILossReasonRepository.cs
public interface ILossReasonRepository : IRootRepository<LossReasonAggregate, int>
{
    Task<PagedResult<LossReasonAggregate>> GetAsync(
        LossReasonFilter filter, PageQuery page, CancellationToken cancellationToken = default);

    Task<Result<LossReasonAggregate>> CreateAsync(
        LossReasonAggregate aggregate, CancellationToken cancellationToken = default);
}

// Contexts/LossReason/Application/Ports/ILossReasonUsageReader.cs
public interface ILossReasonUsageReader
{
    Task<Result<bool>> IsUsedAsync(int lossReasonId, CancellationToken cancellationToken = default);
}

// Contexts/LossReason/Domain/Queries/LossReasonFilter.cs
public sealed record LossReasonFilter(string? Name, bool? IsActive);

// Contexts/LossReason/Domain/Aggregates/LossReasonArgs.cs
public sealed record CreateLossReasonArgs(string? Name, bool IsActive);
public sealed record UpdateLossReasonArgs(string? Name, bool IsActive);
```

`GetAllAsync` (heredado de `IRootRepository`) se implementa delegando en `GetAsync` con un filtro vacío; no se deja sin implementar.

### 5.6 Application — un caso de uso por operación

| Caso de uso | Endpoint | Firma | Nota |
|---|---|---|---|
| `GetLossReasonsUseCase` | `GET /loss-reasons` | `Task<PagedResult<GetLossReasonsOutputDto>> ExecuteAsync(GetLossReasonsInputDto input, PageQuery page, CancellationToken)` | Sin `Result` envolvente: `PagedResult` extiende `Result` |
| `GetLossReasonByIdUseCase` | `GET /loss-reasons/{id}` | `Task<Result<GetLossReasonByIdOutputDto>> ExecuteAsync(int id, CancellationToken)` | 404 sale del `ErrorType` |
| `CreateLossReasonUseCase` | `POST /loss-reasons` | `Task<Result<CreateLossReasonOutputDto>> ExecuteAsync(CreateLossReasonInputDto input, CancellationToken)` | **Sin `IUnitOfWorkPort`** (D3) |
| `UpdateLossReasonUseCase` | `PUT /loss-reasons/{id}` | `Task<Result<UpdateLossReasonOutputDto>> ExecuteAsync(int id, UpdateLossReasonInputDto input, CancellationToken)` | Cargar → `Update()` → `Update(aggregate)` → `CommitAsync` |
| `DeleteLossReasonUseCase` | `DELETE /loss-reasons/{id}` | `Task<Result> ExecuteAsync(int id, CancellationToken)` | `ExistsAsync` → `IsUsedAsync` → `RemoveAsync` → `CommitAsync` |

Cada carpeta lleva sus cinco archivos coubicados: `I{X}UseCase.cs`, `{X}UseCase.cs`, `{X}InputDto.cs`, `{X}OutputDto.cs`, `{X}Mapping.cs` (D11).

**Qué va y qué no va en `{X}Mapping.cs`** (precisado por la revisión de QA del 2026-08-21, tras un ida y vuelta):

| Va en el Mapping | Va inline en el caso de uso |
|---|---|
| `ToOutputDto()` — agregado → DTO de salida | La construcción del **objeto de filtro** (`new LossReasonFilter(input.Name, input.IsActive)`) |
| `ToAggregate()` / `ToUpdateArgs()` — DTO de entrada → dominio | |

El corte es el de `casos-de-uso.md` §5.5, que arma el filtro con un `new` dentro del `ExecuteAsync` y mapea los items con `ToOutputDto()`. El Mapping traduce **entre DTO y dominio**; un `ToFilter()` no traduce, solo mueve tres campos a un record de consulta, y esconder eso en otro archivo aleja la lectura del caso de uso sin ganar nada. **Aplica a las cinco carpetas** — T7, T8, T9 y T10 van igual. `private const string Origin = nameof({X}UseCase);` solo en los que originan errores propios (`Update`, `Delete`; `Create` lo lleva por los errores del agregado). **`GetLossReasonById` no lo lleva** —corregido el 2026-08-28 por la revisión del PR de T7—: el `NotFound` lo origina y lo sella el **repositorio**, y el caso de uso solo propaga, que es lo que dice el propio §5.6 en la fila de `GetLossReasonByIdUseCase` («404 sale del `ErrorType`») y el Detalle de `F3.2`. Un `Origin` ahí sería código muerto.

**Orden en `DeleteLossReasonUseCase`: primero el dominio/existencia, después el Reader** — validar el uso antes gastaría un scan de 300.000 filas en un request que iba a responder 404.

## 6. Contratos de API

Ruta base `/loss-reasons`, derivada de `[Route("[controller]")]` sobre `LossReasonsController` por el `KebabCaseParameterTransformer` ya registrado en `Program.cs`. **Sin prefijo de versión** (la plantilla no versiona rutas). Envelope uniforme `{ data, statusCode }` / `{ error, statusCode }`.

### 6.1 `GET /loss-reasons`

| Param | Tipo | Obligatorio | Default | Validación | Capa |
|---|---|---|---|---|---|
| `search` | `string?` | No | `null` | `MaximumLength(50)` | FluentValidation (`GetLossReasonsInputValidator`) |
| `isActive` | `bool?` | No | `null` (todas) | tipo | Model binding |
| `pageIndex` | `int` | No | `0` | `>= 0` | `PageQueryInputValidator` (existente) |
| `pageSize` | `int` | No | `20` | `1..100` | `PageQueryInputValidator` (existente) |

El parámetro se llama **`search`**, no `name`: es el texto que se busca, y filtra por `Name`. Su validador **no usa el catálogo de errores de dominio** —un texto de búsqueda demasiado largo es un request mal formado, no una invariante rota del catálogo— y responde con su propio mensaje, `Search text must not exceed 50 characters.`. El número sí sigue siendo `LossReasonAggregate.NameMaxLength`: buscar un texto más largo que el nombre más largo posible no puede casar con ninguna fila.

Éxito 200:

```json
{ "data": { "items": [ { "id": 1, "name": "Precio", "isActive": true } ], "totalCount": 8 }, "statusCode": 200 }
```

Catálogo vacío → **200 con `items: []`** (D9), no 404.

### 6.2 `POST /loss-reasons`

| Param | Tipo | Obligatorio | Default | Validación | Capa |
|---|---|---|---|---|---|
| `name` | `string?` | **Sí** | — | `NotEmpty()`, `MaximumLength(50)` | FluentValidation **y** invariante de `LossReasonAggregate.Create` (D4) |
| `isActive` | `bool?` | **Sí** | — | `NotNull()` | FluentValidation **y** invariante de `LossReasonAggregate.Create` (D4) |

`name` se declara **anulable** en el DTO a propósito, para que el validador reporte el error con su `Property` en vez de que el deserializador falle con un 400 genérico. Éxito **201** con el recurso creado, incluido su `id` (D3). Sin header `Location` (la plantilla no lo usa).

### 6.3 `PUT /loss-reasons/{id}`

| Param | Tipo | Obligatorio | Default | Validación | Capa |
|---|---|---|---|---|---|
| `id` | `int` | **Sí** | — | `> 0` | FluentValidation (`ConsecutiveIdInputValidator`, vía `ConsecutiveIdInputDto`) |
| `name` | `string?` | **Sí** | — | `NotEmpty()`, `MaximumLength(50)` | FluentValidation **y** invariante de `LossReasonAggregate.Update` (D4) |
| `isActive` | `bool?` | **Sí** | — | `NotNull()` | FluentValidation **y** invariante de `LossReasonAggregate.Update` (D4) |

`isActive` es **anulable y obligatorio**, simétrico con el `POST`: si fuera `bool` no anulable, omitirlo lo haría llegar como `false` por el default del CLR y **desactivaría la causa sin que nadie lo pidiera**. Éxito 200 con el recurso actualizado. **No existe el discriminador `tipo`** del legado: crear y actualizar son verbos y rutas distintos, lo que elimina de raíz el escenario de Discovery D6.

Una fila legada cuyo `cau_nombre` supere los 50 caracteres se lee por `GET` pero **falla el `PUT` con 400** hasta que se acorte el nombre (D5, R7).

### 6.4 `GET /loss-reasons/{id}` y `DELETE /loss-reasons/{id}`

| Param | Tipo | Obligatorio | Default | Validación | Capa |
|---|---|---|---|---|---|
| `id` | `int` | **Sí** | — | `> 0` | FluentValidation (`ConsecutiveIdInputValidator`, vía `ConsecutiveIdInputDto`) |

`GET` → 200 / **400** / 404. `DELETE` → **204 sin cuerpo** / **400** / 404 / 409 (D7). Las dos actions **sí** llevan `[ValidateRequest]`: el `id` viaja envuelto en `ConsecutiveIdInputDto` precisamente para que la validación estructural lo alcance.

### 6.5 Errores de dominio → HTTP

| Error | `ErrorType` | HTTP | Cuándo |
|---|---|---|---|
| `LossReasonErrors.NameRequired` | `Validation` | 400 | nombre vacío |
| `LossReasonErrors.NameTooLong` | `Validation` | 400 | nombre > 50 (D5) |
| `LossReasonErrors.IsActiveRequired` | `Validation` | 400 | `isActive` ausente o nulo en `POST`/`PUT` |
| Fallo de FluentValidation | `Validation` | 400 | estructura del request, incluidos `search` > 50 y un `id` de ruta `<= 0` |
| `LossReasonErrors.NotFound(id)` | `NotFound` | 404 | `GET`/`PUT`/`DELETE` sobre un id inexistente |
| `LossReasonErrors.InUse(id)` | `Conflict` | 409 | `DELETE` de una causa asignada a un negocio (D7) |
| 547 vía `SqlServerErrorClassifier` | `Conflict` | 409 | carrera perdida en el `DELETE` |
| `PersistenceErrors.Failure(origin)` | `Internal` | 500 | fallo de BD |

Cada action declara `[ProducesResponseType]` para **todos** sus códigos, con `ApiSuccessResponse<T>` / `ApiErrorResponse`, más `[EndpointSummary]`, `[EndpointDescription]` y `[Tags("LossReasons")]` una sola vez a nivel de controller. **Toda** propiedad de DTO, de entrada y de salida, lleva `[property: Description(...)]`.

## 7. Operación

### 7.1 Resolución de tenant

Nada que construir. `TenantMiddleware` lee `X-Entity-Code` (header) o `EntityCode` (query), resuelve con `ITenantResolverServiceClient`, descifra el connection string y lo publica en `TenantContext`, que `ApplicationDbContext` consume vía `IDbConnectionProvider`. Excluye `/health`, `/openapi` e `/info`.

**Quién lo determina: Jack** (D14). El monolito ya sabe en qué institución está la sesión y transmite esa identidad; el servicio no la deduce.

Consecuencia para este contexto: **cada request debe traer el tenant**, porque `tbl_opo_causas` vive en la base de cada institución. Sin `X-Entity-Code` el servicio no sabe contra qué base consultar **y además el caché L1 guarda la respuesta como «sin tenant» y la comparte** (D10). Es un cambio frente al legado, donde el tenant venía de la sesión.

### 7.1.1 Seguridad de acceso

| Control | Dónde vive | Decisión |
|---|---|---|
| Autenticación | Infraestructura: el servicio solo es alcanzable por los pipelines de la plataforma | D12 |
| Autorización (quién puede administrar el catálogo) | **Jack**, antes de invocar al servicio — mismo patrón que `comunicados/announcements` | D13 |
| Aislamiento entre instituciones | `X-Entity-Code` enviado por Jack + `TenantMiddleware` | D14 |

El servicio **no** implementa ninguno de los tres: los recibe resueltos. Eso es lo que hace que no haya pasos de §8 dedicados a seguridad, y también lo que concentra todo el riesgo en el perímetro (R5) y en el lado de Jack (R9).

### 7.2 Variables de entorno

**El contexto no agrega ninguna variable nueva.** Las que deben estar configuradas para que funcione ya existen en la plantilla:

| Variable | Valor requerido | Origen |
|---|---|---|
| `TenantResolverService__Enabled` | `true` | ConfigMap |
| `TENANT_RESOLVER_SERVICE_URL` | URL del resolver | secreto `platform-shared` |
| `CONNSTRING_ENCRYPTION_KEY` | clave compartida | secreto `platform-shared` |
| `Cache__ConnectionString` | host de Redis | `platform-shared` |
| `Cache__DefaultTtlSeconds` | TTL por defecto de L1 | ConfigMap |

`Cache__L2Enabled` **no aplica a este contexto** en cuanto a uso: D10 descarta L2 para las consultas de causas. Sí es obligatorio para arrancar, junto con `Cache__ConnectionString`, desde que la multitenencia es requisito.

**Enmienda del 2026-09-04 — ya no existe el modo de desarrollo sin tenant.** Esta sección decía que con `TenantResolverService:Enabled = false` el servicio arrancaba contra EF **InMemory**. El merge de `main` (`feat(startup)!: require multitenancy and drop the in-memory database`) **elimina ese proveedor y pone el flag en `true` por defecto**, también en `appsettings.Development.json`. Hoy `dotnet run` aborta con `Critical Error: multitenancy … is on but TenantResolverService:BaseUrl is missing`. Consecuencias para este plan:

* **Levantar el servicio en local exige la pila completa**: resolver de tenants alcanzable, `CONNSTRING_ENCRYPTION_KEY`, SQL Server y Redis. Es lo que `docker-compose.yml` tiene que cubrir, y convierte en necesaria la tarea de contenedores que estaba aparcada.
* **La verificación en caliente de `F4.2`/`F4.3` ya no se reproduce tal cual**: se hizo con multitenencia apagada, un modo que no existe. Lo verificado sigue siendo válido —el comportamiento comprobado es de la capa HTTP, no del proveedor de datos—, pero **repetirlo exige la pila**, y eso es lo que hará `F5.1`.
* Los unitarios **no se ven afectados**: no arrancan la app, y los de `F2.9` usan el paquete InMemory desde `UnitTests.csproj`, no el proveedor que se quitó del runtime. 490/490 en verde tras el merge.

→ `GAP-5` deja de ser «el modo de desarrollo no sirve datos reales» y pasa a ser «no hay modo de desarrollo sin la pila».

### 7.3 Caché y rendimiento

**L1 (Output Caching), sin L2** (D10). Tag único `loss-reasons`, declarado una sola vez como `private const string CacheTag` en el controller para que lectura e invalidación no puedan desalinearse.

| Endpoint | Caché | Motivo |
|---|---|---|
| `GET /loss-reasons` | `[OutputCache(Duration = CacheDurationSeconds, Tags = [CacheTag])]` | Listado **filtrado**, y aun así **no declara `VaryByQueryKeys`**: el atributo reaplica `DefaultPolicy` después de la política base y restaura `QueryKeys = "*"`, así que ya varía por toda la query. Declarar la lista *restringe* la clave y es lo que rompe el filtrado (`cache.md`, «Cómo se arma la clave de caché») |
| `GET /loss-reasons/{id}` | `[OutputCache(Duration = CacheDurationSeconds, Tags = [CacheTag])]` | El `id` va en la ruta, así que la política base basta |
| `POST` / `PUT` / `DELETE` | `[OutputCacheInvalidate(CacheTag)]` | Invalida ambas lecturas; solo dispara si el status es `< 400` |

`CacheDurationSeconds` son **3 días** (`3 * 24 * 60 * 60`), por indicación de la revisión del 2026-09-04, compartidos por las dos lecturas. La duración de cada endpoint **la decide el supervisor del servicio**, según `cache.md`. Es un catálogo administrativo y toda escritura invalida el tag, así que una lectura rancia vive hasta la siguiente mutación **de ese tenant**.

El aislamiento entre tenants lo da el `SetVaryByHeader("X-Entity-Code", …)` de la política base, que el atributo del listado complementa en vez de reemplazar. **Un request sin ese header cachea como «sin tenant» y la respuesta se comparte** — es la razón operativa por la que §7.1 lo exige, no solo la resolución de la base.

Rendimiento: el catálogo tiene 8 filas en el tenant medido y el `ORDER BY` debe **desempatar con la clave** (`ORDER BY Name, Id`) porque `OFFSET/FETCH` puede repetir o saltar filas si el orden no es único — y `cau_nombre` no tiene índice ni restricción de unicidad. El único punto caro es el chequeo de uso del borrado (R2), que no se cachea porque no es un endpoint.

### 7.4 Rutas del monolito y qué las reemplaza

| Ruta actual | Reemplazo | Estado |
|---|---|---|
| `GET Causas/Lista` | `GET /loss-reasons` | pendiente — ruptura de paridad por paginación (D8) |
| `GET Causas/{id}/Editar` (carga del modelo) | `GET /loss-reasons/{id}` | pendiente |
| `POST Causas/Actualizar` con `tipo=creacion` | `POST /loss-reasons` | pendiente |
| `POST Causas/Actualizar` con `tipo=edicion` | `PUT /loss-reasons/{id}` | pendiente |
| `POST Causas/{id}/Eliminar` | `DELETE /loss-reasons/{id}` | pendiente — ahora 409 explícito (D7) |
| `GET api/causas` (API v1 del monolito) | `GET /loss-reasons` | pendiente — rupturas por filtro opcional y 200 en vacío (D9) |
| `GET Causas` (vista `Inicio`) | — | no migra — vista Razor |
| `GET Causas/Crear` (formulario vacío) | — | no migra — vista Razor |
| `GET Causas/{id}/Eliminar` (confirmación) | — | no migra — vista Razor |
| Los 8 SPs que leen `tbl_opo_causas` por `LEFT JOIN` | — | no migran — siguen en el monolito |

El client en el monolito, el feature flag y el orden de corte son de `03-flujos.md`.

## 8. Fases y pasos

> **Reparto:** cada paso declara en `tarea:` su tarea y su responsable — **Juan Camilo, Brayan o Juan Esteban**. El reparto completo, con las olas de ejecución y qué espera a qué merge, vive en `tasks_causas.md` §2.2–§2.4; este plan sigue siendo el dueño de los pasos, no del reparto.
>
> `estado` es `pending` al generar el plan, o `blocked` si depende de un GAP bloqueante. **Ninguna fase está condicionada**: los siete GAPs se resolvieron el 2026-08-14 (§9.2) y las catorce decisiones están firmadas. Las decisiones que afectan a cada fase se declaran igual en su encabezado, para que se sepa qué hay que rehacer si alguna se revisa.

### Fase 0 — Preparación · `pending`

> Decisiones que la afectan: ninguna.

#### [F0.1] Read template documentation and reference context
`id: F0.1 · depende_de: — · tarea: — (lectura, las tres personas) · estado: pending`
- Objetivo: conocer el contrato de la plantilla antes de escribir una línea.
- Fuente: template (regla 7 del prompt)
- Archivos: `docs/plantilla/*.md` (lectura), `src/Contexts/ServiceInfo/**` (lectura)
- Detalle: leer `arquitectura.md`, `contextos.md`, `casos-de-uso.md`, `controllers.md`, `repositorio.md`, `conceptos-reader-provider-repository.md`, `patron-result.md`, `errores-dominio.md`, `entidades-y-agregados.md`, `validaciones.md`, `contrato-api.md`, `openapi.md`, `testing.md`, `estandares-codigo.md`. Contrastar con `ServiceInfo` y con D11.
- Hecho cuando: el ejecutor puede enunciar sin consultar dónde va el contrato del repositorio, dónde el del Reader, y por qué `CreateAsync` no lleva `CommitAsync`.
- Verificar: `dotnet --version` (no hay build todavía; este paso no produce cambios)

#### [F0.2] Restore the missing GetServiceInfoOutputDto
`id: F0.2 · depende_de: F0.1 · tarea: — · estado: done`
- Objetivo: dejar el repositorio compilable.
- Fuente: `GAP-1`
- Archivos: `src/Contexts/ServiceInfo/Application/UseCases/GetServiceInfo/GetServiceInfoOutputDto.cs`
- Detalle: **resuelto fuera de este plan por el dueño del repositorio** e incorporado con un pull — commit `9f24956 fix: restore missing GetServiceInfo DTO/test and lowercase github_repo` (2026-08-14). No hay trabajo que ejecutar; el paso se conserva para que la traza del `GAP-1` no se pierda.
- Hecho cuando: `dotnet build Service.slnx -c Release` termina con exit code 0.
- Verificar: `dotnet build Service.slnx -c Release` — **ejecutado el 2026-08-14: exit code 0**

#### [F0.3] Scaffold the LossReason context projects
`id: F0.3 · depende_de: F0.2 · tarea: T2 (Juan Esteban) · estado: done`
- Objetivo: crear los dos proyectos del contexto y registrarlos en la solución.
- Fuente: template · `ServiceInfo` como referencia de estructura
- Archivos: `src/Contexts/LossReason/Domain/LossReason.Domain.csproj`, `src/Contexts/LossReason/Application/LossReason.Application.csproj`, `Service.slnx`
- Detalle: `LossReason.Domain` referencia `Shared.Domain`; `LossReason.Application` referencia `LossReason.Domain` y `Shared.Application`. Namespaces raíz `LossReason.Domain` y `LossReason.Application`, sin prefijo `Contexts`. Agregar ambos a `Service.slnx` bajo `/src/Contexts/LossReason/`.
- Hecho cuando: los dos proyectos compilan vacíos y aparecen en la solución.
- Verificar: `dotnet build Service.slnx -c Release` — **ejecutado el 2026-08-21: exit code 0, 13 proyectos, 0 advertencias**. Commit `96915cb` en `feat/loss-reasons-scaffold`.

### Fase 1 — Dominio · `done`

> Decisiones que la afectan: **D2, D3, D4, D5, D6** — todas firmadas.
> Estrategia de pruebas: unitarias puras sobre el agregado (xUnit + Shouldly), sin mocks. Cubrir las invariantes de `Name` en `Create` **y** en `Update` (válido, vacío, solo espacios, 50 exactos, 51), la acumulación de varios errores en una sola respuesta, y que `Reconstruct` **no** valida ni asigna auditoría.

#### [F1.1] Create LossReasonErrors
`id: F1.1 · depende_de: F0.3 · tarea: T3 (Juan Esteban) · estado: done`
- Objetivo: declarar el catálogo de errores del contexto; el agregado los referencia al compilar.
- Fuente: D4 · D5 · `errores-dominio.md`
- Archivos: `src/Contexts/LossReason/Domain/Errors/LossReasonErrors.cs`
- Detalle: `public static class LossReasonErrors` con `public const string Context = "LossReason";`, `NameRequired` y `NameTooLong` como `static readonly ValidationError` con `Property = nameof(LossReasonAggregate.Name)` y `Attributes["max"] = LossReasonAggregate.NameMaxLength` en el segundo, más `NotFound(int id)` (`ErrorType.NotFound`) e `InUse(int id)` (`ErrorType.Conflict`) como métodos de fábrica. Sin `Context` ni `Origin` en las definiciones.
- Hecho cuando: el archivo compila y ningún error declara `Origin`.
- Verificar: `dotnet build Service.slnx -c Release`

#### [F1.2] Create LossReasonArgs
`id: F1.2 · depende_de: F1.1 · tarea: T3 (Juan Esteban) · estado: done`
- Objetivo: declarar los records de argumentos de los factories.
- Fuente: `entidades-y-agregados.md`
- Archivos: `src/Contexts/LossReason/Domain/Aggregates/LossReasonArgs.cs`
- Detalle: `public sealed record CreateLossReasonArgs(string? Name, bool IsActive);` y `public sealed record UpdateLossReasonArgs(string? Name, bool IsActive);` — **solo primitivos**, nunca Value Objects.
- Hecho cuando: ambos records existen en un único archivo y no referencian ningún tipo de dominio.
- Verificar: `dotnet build Service.slnx -c Release`

#### [F1.3] Create LossReasonAggregate
`id: F1.3 · depende_de: F1.2 · tarea: T3 (Juan Esteban) · estado: done`
- Objetivo: el agregado con sus invariantes y su auditoría.
- Fuente: D3 · D4 · D5 · D6 · `entidades-y-agregados.md`
- Archivos: `src/Contexts/LossReason/Domain/Aggregates/LossReasonAggregate.cs`
- Detalle: `public sealed class LossReasonAggregate : AggregateRoot<int>` con constructor privado y `public const int NameMaxLength = 50;` (D5) — es la **única** fuente del límite; el validador de F4.1 la referencia.
  `Create(CreateLossReasonArgs)` valida **las dos invariantes de `Name` dentro del dominio** (D4): añade `LossReasonErrors.NameRequired` si es nulo, vacío o solo espacios, y `LossReasonErrors.NameTooLong` si excede `NameMaxLength`. **Acumula, no cortocircuita**: recorre ambas y cierra con `DomainError.FromValidationDomainErrors(errors)` si hay alguna.
  `Update(UpdateLossReasonArgs)` aplica **exactamente las mismas dos validaciones** con la misma acumulación antes de mutar, y luego llama `SetUpdatedAt(DateTime.UtcNow)`.
  `Reconstruct(int id, string name, bool isActive)` devuelve el tipo desnudo **sin validar** ni llamar `Created()` — es lo que permite leer filas legadas de más de 50 caracteres (R7). `Created()` fija **solo** `SetCreatedAt` en UTC.
  **El agregado tiene dos constructores privados: uno sin `Id` que usa `Create`, y otro con `Id` que delega en el primero y solo usa `Reconstruct`.** `Create` no menciona el `Id` — ni lo asigna ni explica por qué: que lo genere la BD por `IDENTITY` (D3) es un hecho de infraestructura y el dominio no lo narra (revisión de QA, 2026-08-21).
- Hecho cuando: `Create` con nombre vacío devuelve `NameRequired`; con 51 caracteres devuelve `NameTooLong`; con 50 exactos tiene éxito; y `Update` se comporta igual en los tres casos. Un `Create` que viole ambas devuelve **un solo** `Result` con **dos** errores en `Details`.
- Verificar: `dotnet build Service.slnx -c Release`

#### [F1.4] Create LossReasonFilter
`id: F1.4 · depende_de: F1.3 · tarea: T3 (Juan Esteban) · estado: done`
- Objetivo: el objeto de filtro del listado.
- Fuente: `contextos.md` (`Domain/Queries/`)
- Archivos: `src/Contexts/LossReason/Domain/Queries/LossReasonFilter.cs`
- Detalle: `public sealed record LossReasonFilter(string? Name, bool? IsActive);`
- Hecho cuando: el record existe y no depende de nada fuera de `Domain`.
- Verificar: `dotnet build Service.slnx -c Release`

#### [F1.5] Declare ILossReasonRepository
`id: F1.5 · depende_de: F1.4 · tarea: T3 (Juan Esteban) · estado: done`
- Objetivo: el contrato de persistencia, en el dominio.
- Fuente: D2 · D3 · `repositorio.md`
- Archivos: `src/Contexts/LossReason/Domain/Repositories/ILossReasonRepository.cs`
- Detalle: `public interface ILossReasonRepository : IRootRepository<LossReasonAggregate, int>` más `GetAsync(LossReasonFilter, PageQuery, CancellationToken)` → `Task<PagedResult<LossReasonAggregate>>` y `CreateAsync(LossReasonAggregate, CancellationToken)` → `Task<Result<LossReasonAggregate>>`. Sin sufijo `Port`.
- Hecho cuando: la interfaz compila y hereda de `IRootRepository<LossReasonAggregate, int>`.
- Verificar: `dotnet build Service.slnx -c Release`

#### [F1.6] Unit tests for the domain layer
`id: F1.6 · depende_de: F1.5 · tarea: T3 (Juan Esteban) · estado: done`
- Objetivo: fijar las invariantes antes de que exista infraestructura.
- Fuente: `testing.md`
- Archivos: `tests/UnitTests/Contexts/LossReason/Domain/LossReasonAggregateTests.cs`
- Detalle: xUnit + Shouldly, nombres `MethodUnderTest_Scenario_ExpectedOutcome`. Casos: `Create_WithValidArgs_ReturnsAggregateWithAuditDates`, `Create_WithEmptyName_ReturnsNameRequired`, `Create_WithWhitespaceName_ReturnsNameRequired`, `Create_WithNameOf51Characters_ReturnsNameTooLong`, `Create_WithNameOf50Characters_Succeeds`, `Create_WithEmptyAndTooLongName_AccumulatesBothErrors`, `Update_WithEmptyName_ReturnsNameRequired`, `Update_WithNameOf51Characters_ReturnsNameTooLong`, `Update_WithValidArgs_SetsUpdatedAt`, `Reconstruct_WithNameLongerThan50_DoesNotValidate`, `Reconstruct_Always_DoesNotSetAuditDates`. **Assertar `IsFailure`, nunca `ShouldThrow`.**
- Hecho cuando: los 11 tests pasan, ninguno usa `Assert.*` de xUnit, y los límites se escriben contra `LossReasonAggregate.NameMaxLength`, no contra un `50` literal.
- Verificar: `dotnet test tests/UnitTests -c Release` — **ejecutado el 2026-08-21: los 11 pasan** (355 en total en la suite). Commit `3500688` en `feat/loss-reasons-domain`.
- Nota de ejecución: el caso `Create_WithEmptyAndTooLongName_AccumulatesBothErrors` se construye con **51 espacios**, que es la única forma de violar las dos invariantes a la vez. `DomainError.BuildDetails` agrupa por `Property`, así que el resultado trae **un `ErrorDetail` de `Name` con los dos mensajes**, no dos `ErrorDetail`.
- **Archivo compartido no previsto:** `tests/UnitTests/UnitTests.csproj` necesitó la `ProjectReference` a `LossReason.Domain` para que el test compile. Lo añadió T3; **T6–T10 tendrán que añadir la de `LossReason.Application`**. Registrado en `tasks_causas.md` §3.

### Fase 2 — Persistencia · `pending`

> Decisiones que la afectan: **D1, D2, D3, D6, D7**.
> Estrategia de pruebas: unitarias sobre el mapper (función pura; **sin casos NULL** desde la revisión de D6 del 2026-08-21 — no porque la BD no los admita, sino porque el tipo de la entidad ya no los representa). **Enmendada y firmada el 2026-08-21 por el tech lead:** el repositorio se prueba **también** con unitarios sobre `ApplicationDbContext` + EF InMemory (paso F2.9), porque la puerta de cobertura de CI mide **solo unit tests** y 77 renglones sin cubrir dejaban el pipeline en 89,6 %, por debajo del piso de 90. Lo que InMemory no puede honrar —constraints, el 547 de D7, la `IDENTITY`, el `varchar`— **sigue siendo materia de la Fase 5 contra SQL Server real**, igual que el Reader. Precedente en el propio repositorio: `RepositoryBaseEFTests` ya prueba así el repositorio genérico de la plantilla.

#### [F2.1] Create the LossReason persistence entity
`id: F2.1 · depende_de: F1.5 · tarea: T4 (Brayan) · estado: done`
- Objetivo: la fila de `tbl_opo_causas` como entidad EF, con la nulabilidad que **el servicio decide exigir** (D6), que es más estricta que la de la columna.
- Fuente: D6 · Discovery §4.1
- Archivos: `src/Infrastructure/Persistence/EntityFramework/LossReasons/Entities/LossReason.cs`
- Detalle: `public sealed class LossReason { public int CauConsecutivoP { get; set; } public string? CauNombre { get; set; } public bool? CauEstado { get; set; } }`. ~~**`string?` y `bool?` son obligatorios**: leer un NULL en una propiedad no anulable hace que SqlClient falle la query entera.~~ **Superado por la enmienda de nulabilidad del 2026-08-21, abajo.**
- Hecho cuando: las tres propiedades declaran el tipo de la columna y la nulabilidad que exige D6.
- Verificar: `dotnet build Service.slnx -c Release` — **ejecutado el 2026-08-21: exit code 0, 0 errores**
- **Enmienda de nulabilidad del 2026-08-21 (decisión de equipo, no del esquema):** las columnas **admiten NULL en la BD**, pero el servicio las exige obligatorias (D6), así que la entidad queda `public string Name { get; set; } = string.Empty;` y `public bool IsActive { get; set; } = true;`. Los inicializadores **no son valores de negocio**: `Nullable` está en `enable` y `TreatWarningsAsErrors` en `true` (`Directory.Build.props`), así que un `string` no anulable sin inicializar es `CS8618` → error de compilación. El mapper asigna los dos antes de persistir, y en lectura EF los sobrescribe con la fila —**salvo que la fila traiga NULL, y entonces la consulta entera falla**, que es el precio aceptado de D6 (R10). Ver D6 revisada.
- **Enmienda del 2026-08-21 (revisión del PR de T4, confirmada por el tech lead):** las propiedades se nombran en **inglés y sin abreviar** —`Id`, `Name`, `IsActive`— y los nombres legados se citan en la configuración con `HasColumnName` (F2.2). **Es la convención del equipo**, no una desviación. El snippet de arriba las nombraba como las columnas (`CauConsecutivoP`, `CauNombre`, `CauEstado`), que son español abreviado y contradicen la regla de idioma de §3.1 y el ejemplo de `contextos.md` §5.3, donde la entidad usa nombres propios y el mapeo al esquema vive en el `IEntityTypeConfiguration`. **El tipo y la nulabilidad no cambian**, que es lo que D6 exige.

#### [F2.2] Create the LossReason EF configuration
`id: F2.2 · depende_de: F2.1 · tarea: T4 (Brayan) · estado: done`
- Objetivo: mapear la entidad a la tabla legada.
- Fuente: D1 · D3 · `repositorio.md`
- Archivos: `src/Infrastructure/Persistence/EntityFramework/LossReasons/Configurations/LossReasonConfiguration.cs`
- Detalle: `IEntityTypeConfiguration<LossReason>`; `ToTable("tbl_opo_causas")`, `HasKey(x => x.Id)`, `Property(x => x.Id).HasColumnName("cau_consecutivoP").ValueGeneratedOnAdd()`, `Property(x => x.Name).HasColumnName("cau_nombre").HasMaxLength(200).IsUnicode(false)`, `Property(x => x.IsActive).HasColumnName("cau_estado")` — **aquí es donde los nombres legados se citan tal cual** (enmienda de F2.1). `HasMaxLength`/`IsRequired` solo para que EF genere el tipo de parámetro correcto, **no como validación** (proyecto Database First). Sin `DeleteBehavior` en cascada.
  **El `200` es el ancho de la columna, no el límite del servicio.** D5 fija 50 y ese número vive en `LossReasonAggregate.NameMaxLength`, en el dominio y en el validador; acá se declara el esquema real, así que **no se reemplaza por la constante** — son dos números distintos a propósito (R7 existe justamente porque no coinciden).
  `IsUnicode(false)` **no es opcional**: sin él `HasMaxLength(200)` produce `nvarchar(200)` y cada consulta mandaría un parámetro `nvarchar` contra una columna `varchar`, con conversión implícita en el servidor.
- Nota de ejecución: se implementó primero con `HasColumnType("varchar(200)")`, que da el mismo tipo pero acopla la configuración a la sintaxis de SQL Server. **Corregido el 2026-08-21 por la revisión del PR** a `HasMaxLength(200).IsUnicode(false)`, que es la forma del ejemplo de `contextos.md` §5.3. ~~**Sin `IsRequired`**: la columna admite NULL y D6 exige que la entidad lo refleje.~~ **Corregido el 2026-08-21:** la columna es `NOT NULL`, así que `Property(x => x.Name)` lleva `.IsRequired()`. Sobre `IsActive` no se declara: `bool` no anulable ya lo hace requerido por convención.
- Hecho cuando: la configuración se descubre por `ApplyConfigurationsFromAssembly` sin registro manual.
- Verificar: `dotnet build Service.slnx -c Release` — **ejecutado el 2026-08-21: exit code 0, 0 errores**

#### [F2.3] Create LossReasonRepositoryMapper
`id: F2.3 · depende_de: F2.2 · tarea: T4 (Brayan) · estado: done`
- Objetivo: traducir entidad ↔ agregado. ~~normalizando los NULL~~ — **sin normalización desde el 2026-08-21**: la BD admite NULL, pero el servicio no lo acepta como dato válido y prefiere fallar a maquillarlo (D6).
- Fuente: D6 · `repositorio.md`
- Archivos: `src/Infrastructure/Persistence/EntityFramework/LossReasons/Mappers/LossReasonRepositoryMapper.cs`
- Detalle: `ToDomain(LossReason)` llama `LossReasonAggregate.Reconstruct(d.Id, d.Name, d.IsActive)` — **`Reconstruct`, nunca `Create`**. Los `?? string.Empty` / `?? false` que este paso especificaba originalmente **se eliminaron el 2026-08-21** con la revisión de D6. `ToDocument(LossReasonAggregate)` escribe `CauNombre`/`CauEstado` y **no** toca `CauConsecutivoP` en creación (lo asigna `IDENTITY`). `CreatedAt`/`UpdatedAt` no se persisten: no existen como columnas.
- Hecho cuando: `ToDomain` de una fila completa devuelve el agregado con los tres campos idénticos, sin ramas de normalización. ~~El caso «fila con `CauNombre = null` y `CauEstado = null`»~~ **no se prueba en el mapper**: no es que la BD no lo admita —sí lo admite—, es que el tipo de la entidad ya no lo representa, así que el NULL no llega nunca hasta acá. Falla antes, al materializar la consulta (D6 · R10).
- Verificar: `dotnet build Service.slnx -c Release` — **ejecutado el 2026-08-21: exit code 0, 0 errores**
- **Archivo compartido no previsto:** `src/Infrastructure/Infrastructure.csproj` no referenciaba el contexto, así que el mapper no compilaba (`CS0246`). Se añadió `ProjectReference` a `LossReason.Application.csproj` —que arrastra `LossReason.Domain`— por instrucción del 2026-08-21 tras reportarlo como GAP. **Esa única línea cubre también a T5** (`F2.6` necesita `ILossReasonUsageReader`, de `Application`), así que Juan Camilo no tiene que tocar este archivo. Registrado en `tasks_causas.md` §3.

#### [F2.4] Implement LossReasonRepository
`id: F2.4 · depende_de: F2.3 · tarea: T4 (Brayan) · estado: done`
- Objetivo: la implementación del contrato de dominio.
- Fuente: D1 · D2 · D3 · D8 · `repositorio.md`
- Archivos: `src/Infrastructure/Persistence/EntityFramework/LossReasons/LossReasonRepository.cs`
- Detalle: `public sealed class LossReasonRepository(ApplicationDbContext context, ILoggerPort<LossReasonRepository> logger) : ILossReasonRepository`, `private const string Origin = nameof(LossReasonRepository);`. Lecturas con `.AsNoTracking()`. `GetAsync` filtra por `Name` (`Contains`) e `IsActive` cuando vienen, ordena `OrderBy(CauNombre).ThenBy(CauConsecutivoP)` — **el desempate por la clave es obligatorio** — y pagina con `Skip(page.Skip).Take(page.PageSize)` + `COUNT`. `GetAllAsync` delega en `GetAsync` con filtro vacío. `CreateAsync` hace `SaveChangesAsync` interno y devuelve el agregado con su `Id`. Cada método en `try/catch (Exception ex) when (ex is not OperationCanceledException)` → `logger.Error(...)` + `PersistenceErrors.Failure(Origin)`. **Ninguna excepción escapa.** No hereda de `RepositoryBaseEF`.
- Hecho cuando: los 8 miembros del contrato están implementados y ninguno deja escapar una excepción.
- Verificar: `dotnet build Service.slnx -c Release` — **ejecutado el 2026-08-21: exit code 0, 0 errores**
- Nota de ejecución: **`CreateAsync` no puede devolver el mismo agregado con su `Id`.** `Entity<TId>.Id` tiene setter `protected` y `Shared` no expone un `AssignId` como el que ilustra `repositorio.md`; §5.5 prohíbe crear nada en `Shared`. Se devuelve `LossReasonRepositoryMapper.ToDomain(document)` después del `SaveChangesAsync`, que es el mismo estado observable (`CreatedAt`/`UpdatedAt` vuelven en `null`, y D6/§5.2 ya establecen que no se persisten). El `Update` sí asigna `CauConsecutivoP` sobre el documento del mapper: el mapper no lo escribe porque en creación lo asigna `IDENTITY`, pero un `UPDATE` necesita direccionar la fila.

#### [F2.5] Declare ILossReasonUsageReader
`id: F2.5 · depende_de: F1.5 · tarea: T3 (Juan Esteban) · estado: done`
- Objetivo: el puerto de lectura de la tabla ajena, en Application.
- Fuente: D7 · `conceptos-reader-provider-repository.md`
- Archivos: `src/Contexts/LossReason/Application/Ports/ILossReasonUsageReader.cs`
- Detalle: `public interface ILossReasonUsageReader { Task<Result<bool>> IsUsedAsync(int lossReasonId, CancellationToken cancellationToken = default); }`. **Sin sufijo `Port` ni `Adapter`**: es un Reader.
- Hecho cuando: la interfaz vive en `Application/Ports/` y no referencia ningún tipo de Infrastructure.
- Verificar: `dotnet build Service.slnx -c Release`

#### [F2.6] Implement LossReasonUsageReader
`id: F2.6 · depende_de: F2.5 · tarea: T5 (Juan Camilo) · estado: done`
- Objetivo: leer `tbl_opo_negocios` sin crearle un repositorio.
- Fuente: D7 · Discovery §4.1
- Archivos: `src/Infrastructure/Persistence/EntityFramework/LossReasons/Entities/DealLossReasonUsage.cs`, `…/Configurations/DealLossReasonUsageConfiguration.cs`, `…/LossReasonUsageReader.cs`
- Detalle: entidad **keyless** con una sola propiedad `int? LossReasonId` mapeada a la columna legada `neg_cau_consecutivo`, configurada `ToTable("tbl_opo_negocios").HasNoKey()` — es solo lectura y no se le crea repositorio (regla explícita). El reader hace `AnyAsync(x => x.LossReasonId == lossReasonId)` con `.AsNoTracking()`, `private const string Origin = nameof(LossReasonUsageReader);`, y el mismo `try/catch` → `PersistenceErrors.Failure(Origin)`. La implementación vive en `Persistence/EntityFramework/`, **no** en `Adapters/`. **No se registra `DbSet` en `ApplicationDbContext`**: la revisión del PR lo retiró por no tener consumidor —el reader consulta con `context.Set<DealLossReasonUsage>()` y `ApplyConfigurationsFromAssembly` descubre la configuración sola—, así que este paso **no toca el archivo compartido con F2.7**.
- Hecho cuando: la entidad keyless no expone escritura y el reader devuelve `Result<bool>` en las tres ramas (usada, libre, fallo).
- Verificar: `dotnet build Service.slnx -c Release`

#### [F2.7] Register the LossReason DbSet in ApplicationDbContext
`id: F2.7 · depende_de: F2.4 · tarea: T4 (Brayan) · estado: done`
- Objetivo: exponer la entidad del agregado al contexto de EF.
- Fuente: `contextos.md` §5.3
- Archivos: `src/Infrastructure/Persistence/EntityFramework/ApplicationDbContext.cs`
- Detalle: agregar `public DbSet<LossReasons.Entities.LossReason> LossReasons => Set<LossReasons.Entities.LossReason>();`. Es el **primer** `DbSet` del servicio. El `DbSet` keyless del Reader lo añade F2.6 sobre este mismo archivo compartido.
- Hecho cuando: `ApplicationDbContext` lo expone y `ApplyConfigurationsFromAssembly` descubre `LossReasonConfiguration`.
- Verificar: `dotnet build Service.slnx -c Release` — **ejecutado el 2026-08-21: exit code 0, 0 errores**. Es el primer `DbSet` del servicio; que el mapeo apunte de verdad a `tbl_opo_causas` lo verifica F5.1 contra SQL real, como declara la estrategia de pruebas de esta fase.

#### [F2.8] Unit tests for the mapper
`id: F2.8 · depende_de: F2.7 · tarea: T4 (Brayan) · estado: done`
- Objetivo: fijar la traducción del mapper. ~~la normalización de NULL, que es la corrección de Discovery D3~~ — reencuadrado el 2026-08-21: no hay NULL que normalizar (D6 revisada).
- Fuente: D6 · `testing.md`
- Archivos: `tests/UnitTests/Infrastructure/Persistence/LossReasons/LossReasonRepositoryMapperTests.cs`
- Detalle: ~~`ToDomain_WithNullName_MapsToEmptyString`, `ToDomain_WithNullState_MapsToInactive`~~, `ToDomain_WithCompleteRow_MapsAllFields`, `ToDocument_OnCreate_DoesNotSetIdentityColumn`. **Revisión del 2026-08-21:** los dos tests de NULL se **borran** —probaban un caso que el esquema no admite— y en su lugar queda `ToDomain_WithInactiveRow_MapsTheState`, que cubre el `false` sin fingir un NULL. Quedan 3.
- Hecho cuando: los 3 tests pasan.
- Verificar: `dotnet test tests/UnitTests -c Release` — **ejecutado el 2026-08-21: los 4 pasan** (359 en total en la suite). Commit `fbafbda` en `feat/loss-reasons-persistence`. **Reejecutado el 2026-08-21 tras la revisión de D6: los 3 pasan** (381 en total en la suite).
- Nota de ejecución: `tests/UnitTests/UnitTests.csproj` **no** necesitó cambios: ya referenciaba `Infrastructure.csproj`. El tipo de la entidad se importa con alias (`LossReasonDocument`) porque `LossReason` es a la vez el nombre de la entidad y el namespace raíz del contexto.

#### [F2.9] Unit tests for the repository
`id: F2.9 · depende_de: F2.8 · tarea: T4 (Brayan) · estado: done`
- Objetivo: dar cobertura de unit test a las 77 líneas del repositorio, que la puerta de CI exige y la Fase 5 no puede aportar.
- Fuente: **enmienda del 2026-08-21** (pendiente de firma) · `testing.md` (puerta de cobertura) · precedente `RepositoryBaseEFTests`
- Archivos: `tests/UnitTests/Infrastructure/Persistence/LossReasons/LossReasonRepositoryTests.cs`
- Detalle: `ApplicationDbContext` real sobre `UseInMemoryDatabase`, una base por test, `ChangeTracker.Clear()` después del seed para partir de un contexto como el de un request. `ILoggerPort` con NSubstitute. Se cubren las tres cosas que se rompen en silencio y no necesitan constraints: que `GetAsync` **ordene con el desempate por la clave** (dos filas de igual nombre salen por `Id`), que filtre por `Name` y por `IsActive` —incluida la fila con `cau_estado` NULL, que no es `false` para el filtro—, y que `GetByIdAsync`/`RemoveAsync` devuelvan `NotFound` **con su `Origin`**. Además: `GetAllAsync` sin filtro, la página que no es la primera con su `TotalCount` sin paginar, `CreateAsync` que confirma y devuelve el `Id`, `AddAsync`/`Update`/`RemoveAsync` que **solo dejan el cambio encolado** para el Unit of Work, y la rama de fallo de cada método —alcanzada disponiendo el contexto bajo el repositorio— que vuelve como `Internal` con el `Origin` del repositorio y deja un `logger.Error`.
- **Fuera de este paso, sigue en F5.1:** el 547 de una causa en uso, la `IDENTITY` real, el `varchar(200)`, y que el filtro por nombre sea insensible a mayúsculas (la colación del servidor). Los asserts de este paso usan la misma capitalización a propósito, para significar lo mismo en los dos proveedores.
- Hecho cuando: los 24 tests pasan y `LossReasonRepository` queda sin líneas descubiertas en el reporte de cobertura.
- Verificar: `dotnet test tests/UnitTests -c Release --collect:"XPlat Code Coverage" --settings coverlet.runsettings` — **ejecutado el 2026-08-21: 383 pasan, cobertura de línea 97,1 %** (1025/1055; era 89,6 % con el repositorio descubierto, por debajo del piso de 90).

### Fase 3 — Aplicación · `pending`

> Decisiones que la afectan: **D3, D4, D5, D7, D8, D9, D11**.
> Estrategia de pruebas: unitarias con NSubstitute sobre repositorio y Reader, un paso de test por caso de uso (F3.6–F3.10). Además de la ruta feliz, **assertar `Error.Origin`**: cuando falla el repositorio el use case debe propagar el `Origin` del repositorio, no reescribirlo.
> **Los cinco casos de uso son independientes entre sí**: todos dependen solo de F2.7 (y F3.5 además de F2.6). No hay dependencia de código entre ellos, así que pueden ejecutarse en paralelo.

#### [F3.1] Create GetLossReasons use case
`id: F3.1 · depende_de: F2.7 · tarea: T6 (Juan Esteban) · estado: done`
- Objetivo: el listado paginado y filtrado.
- Fuente: D8 · D9 · D11 · `casos-de-uso.md`
- Archivos: `src/Contexts/LossReason/Application/UseCases/GetLossReasons/{IGetLossReasonsUseCase,GetLossReasonsUseCase,GetLossReasonsInputDto,GetLossReasonsOutputDto,GetLossReasonsMapping}.cs`
- Detalle: `Task<PagedResult<GetLossReasonsOutputDto>> ExecuteAsync(GetLossReasonsInputDto input, PageQuery page, CancellationToken cancellationToken = default)`. `GetLossReasonsInputDto(string? Name, bool? IsActive)` → `LossReasonFilter`. Salida `(int Id, string Name, bool IsActive)`. Un catálogo vacío devuelve `PagedResult` exitoso con `items: []` (D9), **no** un error. Todas las propiedades con `[property: Description(...)]`.
- Hecho cuando: un repositorio que devuelve 0 filas produce un `PagedResult` con `IsSuccess = true` y `TotalCount = 0`.
- Verificar: `dotnet build Service.slnx -c Release` — **ejecutado el 2026-08-21: 0 errores, 0 advertencias**
- Nota de ejecución: el use case **no declara `Origin`** — no origina ningún error, solo propaga el del repositorio.
- **Revisión de QA sobre el PR (2026-08-21), aplicada:** `GetLossReasonsMapping.cs` **se mantiene, con `ToOutputDto()` únicamente**; **el `ToFilter()` era el que sobraba** y el filtro se arma inline con `new LossReasonFilter(...)`, como en `casos-de-uso.md` §5.5. Los `[property: Description(...)]` de los dos DTOs pasan **a inglés** (§3.1). Se quita el comentario del catálogo vacío del use case, porque D9 ya lo explica y el test `ExecuteAsync_WithNoRows_ReturnsSuccessfulEmptyPage` lo fija. **Las dos primeras son reglas de contexto: T7–T10 van igual** (§3.1 y §5.6).

#### [F3.2] Create GetLossReasonById use case
`id: F3.2 · depende_de: F2.7 · tarea: T7 (Juan Camilo) · estado: done`
- Objetivo: la consulta por id.
- Fuente: D11 · `casos-de-uso.md`
- Archivos: `src/Contexts/LossReason/Application/UseCases/GetLossReasonById/{…}.cs` (5 archivos)
- Detalle: `Task<Result<GetLossReasonByIdOutputDto>> ExecuteAsync(int id, CancellationToken)`. Propaga tal cual el error del repositorio; el 404 sale del `ErrorType`, no de un `if` en el controller.
- Hecho cuando: un id inexistente devuelve `ErrorType.NotFound`.
- Verificar: `dotnet build Service.slnx -c Release` — **ejecutado el 2026-08-28: exit code 0, 0 errores**
- Nota de ejecución: **son 4 archivos, no 5.** No hay `GetLossReasonByIdInputDto`: la entrada es un `int id` y envolverlo en un DTO no aporta nada. El caso de uso inyecta **solo** `ILossReasonRepository` —sin `IUnitOfWorkPort`, porque es una lectura— y **no declara `Origin`**: no origina errores, propaga tal cual el del repositorio, que es lo que exige el Detalle.

#### [F3.3] Create CreateLossReason use case
`id: F3.3 · depende_de: F2.7 · tarea: T8 (Brayan) · estado: done`
- Objetivo: la creación, con el PK que devuelve la BD.
- Fuente: D3 · D5 · `repositorio.md`
- Archivos: `src/Contexts/LossReason/Application/UseCases/CreateLossReason/{…}.cs` (5 archivos)
- Detalle: `input.ToAggregate()` → `LossReasonAggregate.Create(args)`; si falla, `return error with { Context = LossReasonErrors.Context, Origin = Origin };`. Persiste con `repository.CreateAsync(...)` y devuelve `aggregate.ToOutputDto()` con el `Id` asignado. **No inyecta `IUnitOfWorkPort` ni llama `CommitAsync`** (D3). `CreateLossReasonInputDto(string? Name, bool IsActive = true)` con `Name` anulable a propósito.
- Hecho cuando: el use case no tiene ninguna referencia a `IUnitOfWorkPort` y el DTO de salida incluye el `Id`.
- Verificar: `dotnet build Service.slnx -c Release` — **ejecutado el 2026-08-21: exit code 0, 0 errores**

#### [F3.4] Create UpdateLossReason use case
`id: F3.4 · depende_de: F2.7 · tarea: T9 (Brayan) · estado: done`
- Objetivo: la actualización.
- Fuente: D5 · `casos-de-uso.md`
- Archivos: `src/Contexts/LossReason/Application/UseCases/UpdateLossReason/{…}.cs` (5 archivos)
- Detalle: cargar con `GetByIdAsync` → `aggregate.Update(input.ToUpdateArgs())` → `repository.Update(aggregate)` → `unitOfWork.CommitAsync(...)`. **El agregado se modifica, no se reemplaza.** Los errores del agregado se sellan con `Context`/`Origin`; los del repositorio y el Unit of Work se propagan tal cual.
- Hecho cuando: el error de un repositorio que falla llega al llamador con el `Origin` del repositorio intacto.
- Verificar: `dotnet build Service.slnx -c Release` — **ejecutado el 2026-08-21: exit code 0, 0 errores**

#### [F3.5] Create DeleteLossReason use case
`id: F3.5 · depende_de: F2.7, F2.6 · tarea: T10 (Juan Camilo) · estado: done`
- Objetivo: el borrado con el 409 por uso.
- Fuente: D7 · `casos-de-uso.md`
- Archivos: `src/Contexts/LossReason/Application/UseCases/DeleteLossReason/{IDeleteLossReasonUseCase,DeleteLossReasonUseCase}.cs`
- Detalle: `Task<Result> ExecuteAsync(int id, CancellationToken)`. Orden obligatorio: `repository.ExistsAsync(id)` → si no existe, `LossReasonErrors.NotFound(id)`; luego `usageReader.IsUsedAsync(id)` → si está en uso, `LossReasonErrors.InUse(id)`; luego `RemoveAsync` + `CommitAsync`. **Primero la existencia, después el Reader**: así un 404 no paga el scan de 300.000 filas. Sin DTOs (204 sin cuerpo).
- Hecho cuando: borrar una causa en uso devuelve `ErrorType.Conflict` sin llegar a `RemoveAsync`.
- Verificar: `dotnet build Service.slnx -c Release` — **ejecutado el 2026-08-28: exit code 0, 0 errores**
- Nota de ejecución: **sí declara `Origin`**, a diferencia de `F3.2`, porque este caso de uso **sí origina errores propios** (`NotFound` cuando no existe, `InUse` cuando está asignada) y los sella con `with { Origin = Origin }`. Los ajenos —del repositorio y del Reader— los propaga sin tocar. **No sella `Context`**: en todo el servicio, `Context` solo se pone sobre errores de validación del agregado (`F3.3`), mientras que cada `NotFound` se sella únicamente con `Origin`, igual que en `LossReasonRepository`.

> Los cinco pasos de test que siguen son deliberadamente uno por caso de uso: agrupados en un solo paso hacían que la tarea de escrituras superara el techo de R2 y no pudiera moverse de estado por partes.

#### [F3.6] Unit tests for GetLossReasons
`id: F3.6 · depende_de: F3.1 · tarea: T6 (Juan Esteban) · estado: done`
- Objetivo: cubrir el listado, incluido el catálogo vacío.
- Fuente: D9 · `testing.md`
- Archivos: `tests/UnitTests/Contexts/LossReason/Application/GetLossReasonsUseCaseTests.cs`
- Detalle: NSubstitute para `ILossReasonRepository`; Shouldly para asserts. Casos: filtro aplicado y propagado al repositorio, `TotalCount` reflejado, **repositorio con 0 filas → `IsSuccess` con `items` vacío** (D9), y fallo del repositorio → el `Origin` del repositorio llega intacto.
- Hecho cuando: los 4 casos pasan.
- Verificar: `dotnet test tests/UnitTests -c Release` — **ejecutado el 2026-08-21: los 4 pasan**
- Nota de ejecución: **T6 añadió a `tests/UnitTests/UnitTests.csproj` la `ProjectReference` a `LossReason.Application`**, que F1.6 había dejado anotada como pendiente para la primera de T6–T10. **T7–T10 ya no tienen que tocar ese archivo**, solo rebasar sobre la base.

#### [F3.7] Unit tests for GetLossReasonById
`id: F3.7 · depende_de: F3.2 · tarea: T7 (Juan Camilo) · estado: done`
- Objetivo: cubrir la consulta por id.
- Fuente: `testing.md`
- Archivos: `tests/UnitTests/Contexts/LossReason/Application/GetLossReasonByIdUseCaseTests.cs`
- Detalle: casos: id existente → DTO mapeado; id inexistente → `ErrorType.NotFound`; fallo del repositorio → `Origin` propagado sin reescribir.
- Hecho cuando: los 3 casos pasan.
- Verificar: `dotnet test tests/UnitTests -c Release` — **ejecutado el 2026-08-28: 3/3 del caso de uso y 388/388 en la suite**
- Nota de ejecución: el caso del fallo del repositorio **arma el error con `DomainError` de `Shared`**, no con `PersistenceErrors` de Infrastructure. Es un test de la capa de aplicación: importar Infrastructure para fabricar el error invertía la dependencia sin necesidad. Es el mismo patrón del test de T6.

#### [F3.8] Unit tests for CreateLossReason
`id: F3.8 · depende_de: F3.3 · tarea: T8 (Brayan) · estado: done`
- Objetivo: cubrir la creación y su contrato de persistencia.
- Fuente: D3 · D4 · `testing.md`
- Archivos: `tests/UnitTests/Contexts/LossReason/Application/CreateLossReasonUseCaseTests.cs`
- Detalle: casos: input válido → `CreateAsync` recibido una vez y el DTO de salida trae el `Id`; **nombre inválido → el use case falla en el agregado y `CreateAsync` no se llama nunca** (D4); fallo del repositorio → `Origin` propagado. **Assertar explícitamente que `IUnitOfWorkPort.CommitAsync` no se invoca** (D3) — **enmendado el 2026-08-21:** el assert lee los parámetros del constructor en vez de un sustituto sin inyectar, que no podía fallar. Ver §9.3.
- Hecho cuando: los 3 casos pasan y existe el assert de que no hay commit.
- Verificar: `dotnet test tests/UnitTests -c Release` — **ejecutado el 2026-08-21: los 3 pasan** (384 en total en la suite)

#### [F3.9] Unit tests for UpdateLossReason
`id: F3.9 · depende_de: F3.4 · tarea: T9 (Brayan) · estado: done`
- Objetivo: cubrir la actualización.
- Fuente: D4 · `testing.md`
- Archivos: `tests/UnitTests/Contexts/LossReason/Application/UpdateLossReasonUseCaseTests.cs`
- Detalle: casos: input válido → `Update` + `CommitAsync` recibidos una vez; id inexistente → `NotFound` sin llegar a `CommitAsync`; **nombre inválido → falla en el agregado y no se persiste** (D4); fallo del commit → `Origin` del Unit of Work propagado.
- Hecho cuando: los 4 casos pasan.
- Verificar: `dotnet test tests/UnitTests -c Release` — **ejecutado el 2026-08-21: los 4 pasan** (385 en total en la suite)

#### [F3.10] Unit tests for DeleteLossReason
`id: F3.10 · depende_de: F3.5 · tarea: T10 (Juan Camilo) · estado: done`
- Objetivo: cubrir el borrado y el 409 por uso.
- Fuente: D7 · `testing.md`
- Archivos: `tests/UnitTests/Contexts/LossReason/Application/DeleteLossReasonUseCaseTests.cs`
- Detalle: NSubstitute para repositorio, `ILossReasonUsageReader` e `IUnitOfWorkPort`. **Del Reader hay que cubrir las tres ramas: en uso, libre, y el Reader falla.** Además: id inexistente → `NotFound` **sin llamar al Reader** (es lo que evita el scan de 300.000 filas en un 404, D7); en uso → `Conflict` sin llamar a `RemoveAsync`.
- Hecho cuando: los 5 casos pasan y existe el assert de que un 404 no consulta el Reader.
- Verificar: `dotnet test tests/UnitTests -c Release` — **ejecutado el 2026-08-28: 6/6 del caso de uso y 417/417 en la suite**
- Nota de ejecución: **son 6 tests, no 5.** Con los cinco del paso, la rama de `RemoveAsync` fallando quedaba sin cubrir (93,3 % de líneas), así que se añade `ExecuteAsync_WhenRemoveFails_DoesNotCommit`, que además fija la regla de que un borrado que falló **no llega al commit**. Con él, el caso de uso queda en **100 % de líneas y de ramas**. Los errores del repositorio y del Reader se arman con `DomainError` de `Shared`, no con `PersistenceErrors` de Infrastructure: es un test de la capa de aplicación, igual que en `F3.6` y `F3.7`.

### Fase 4 — API · `done`

> Decisiones que la afectan: **D5, D8, D9, D10, D11**.
> Estrategia de pruebas: unitarias de los validadores con `FluentValidation.TestHelper` y del controller con NSubstitute de los casos de uso.

#### [F4.1] Create the input validators
`id: F4.1 · depende_de: F3.1, F3.3, F3.4 · tarea: T11 (Juan Esteban) · estado: done`
- Objetivo: la validación estructural del request.
- Fuente: D5 · D9 · D11 · `validaciones.md`
- Archivos: `src/Infrastructure/Validation/FluentValidation/LossReasons/{CreateLossReasonInputValidator,UpdateLossReasonInputValidator,GetLossReasonsInputValidator}.cs`
- Detalle: `sealed`, heredan `AbstractValidator<T>` **e** implementan `IStructuralValidator<T>` (eso los registra por reflection, sin registro manual). `Name`: `NotEmpty().MaximumLength(LossReasonAggregate.NameMaxLength)` en crear y actualizar; solo `MaximumLength(...)` en el filtro. **El límite se referencia desde la constante del agregado, nunca se escribe `50` literal** (D4: la regla está duplicada a propósito, pero el número no). **No hay DataAnnotations.** La paginación la cubre el `PageQueryInputValidator` existente.
- Hecho cuando: los tres validadores se resuelven por DI sin ninguna línea de registro añadida, y ninguno contiene un literal numérico de longitud.
- Verificar: `dotnet build Service.slnx -c Release` — **ejecutado el 2026-08-28: 0 errores, 0 advertencias**
- Nota de revisión (2026-08-28): la ronda de PR cambió tres cosas del paso. **(a)** El filtro del listado pasa de `Name` a **`Search`**, con la descripción de OpenAPI diciendo que filtra por `Name`; el parámetro no es un nombre, es el texto que se busca. **(b)** `GetLossReasonsInputValidator` **deja de usar el catálogo de errores de dominio** y lleva su propio mensaje: un texto de búsqueda demasiado largo es un request mal formado, no una invariante rota. Se conserva la constante para el número, no el error. **(c)** `UpdateLossReasonInputValidator` gana la regla `NotNull()` sobre `IsActive`, y el DTO, los `UpdateLossReasonArgs` y `LossReasonAggregate.Update` pasan a `bool?` — **esto último toca archivos de T3 y T9**, fuera de la lista de `Archivos:` de este paso, y se hizo por indicación explícita de la revisión: es el mismo arreglo que Brayan ya había aplicado al `POST`. Sin él, un `PUT` sin `isActive` respondía **200 y desactivaba la causa** con el `false` del default del CLR.
- Nota de ejecución: **el paso entregó dos validadores, no tres.** `CreateLossReasonInputValidator` ya existía: lo escribió T8 junto a su caso de uso, porque su test de `F3.8` lo necesitaba. Se conserva tal cual y este paso solo añade `UpdateLossReasonInputValidator` y `GetLossReasonsInputValidator`, calcados de él en el uso de `WithState(...)` sobre el catálogo de errores de dominio. **`GetLossReasonsInputValidator` no lleva `NotEmpty`** y el paso tampoco lo pedía: en el filtro un nombre vacío significa «no filtrar por nombre», así que exigirlo convertiría el listado sin filtros en un 400. Los tres leen el límite de `LossReasonAggregate.NameMaxLength`; no hay ningún `50` literal en `src/`. Verificado además en caliente: `GET /loss-reasons?name=<51 chars>` responde 400 con `attributes.max = 50`, lo que prueba que el registro por reflexión de `AddContextValidators` los alcanza sin una sola línea de registro manual.

#### [F4.2] Create LossReasonsController
`id: F4.2 · depende_de: F4.1, F3.2, F3.5 · tarea: T11 (Juan Esteban) · estado: done`
- Objetivo: los 5 endpoints.
- Fuente: D8 · D9 · D10 · D11 · §6 · `controllers.md` · `openapi.md`
- Archivos: `src/Api/Controllers/LossReasonsController.cs`
- Detalle: `[ApiController]`, `[Route("[controller]")]`, `[Tags("LossReasons")]` (una sola vez), `sealed`, **constructor primario** con los 5 casos de uso (D11). Tipos de retorno: `HttpOkPagedResult<T>` (GET lista), `HttpOkResult<T>` (GET por id, PUT), `HttpCreatedResult<T>` (POST), `HttpNoContentResult` (DELETE) — el tipo decide el status de éxito, no se traduce a mano. `[ValidateRequest]` solo en las actions con DTO.
  **Caché L1 (D10):** `private const string CacheTag = "loss-reasons";` y `private const int CacheDurationSeconds` declarados una sola vez; `[OutputCache(Duration = CacheDurationSeconds, Tags = [CacheTag])]` en el listado —sin política nombrada y **sin `VaryByQueryKeys`**, ver la enmienda de F4.3—, `[OutputCache(Duration = CacheDurationSeconds, Tags = [CacheTag])]` en el GET por id, y `[OutputCacheInvalidate(CacheTag)]` en POST, PUT y DELETE.
  `[ProducesResponseType]` por cada código de §6.5, con `ApiSuccessResponse<T>`/`ApiErrorResponse`; para el 204, sin tipo. `[EndpointSummary]`/`[EndpointDescription]` en inglés en cada action. `.ConfigureAwait(false)` en todas las llamadas; `CancellationToken cancellationToken = default` al final.
- Hecho cuando: `/loss-reasons` responde en los 5 verbos, ninguna action declara `[Tags]`, y el tag de caché aparece una sola vez como constante.
- Verificar: `dotnet build Service.slnx -c Release` — **ejecutado el 2026-08-28: 0 errores, 0 advertencias**
- Nota de revisión (2026-08-28): **los `id` de ruta se validan, y no con el mecanismo obvio.** `ValidateRequestFilter` descarta los tipos simples, así que un validador sobre un `int` nunca correría; una restricción de ruta (`{id:int:min(1)}`) respondería **404**, escondiendo un id mal formado como recurso inexistente. El `id` viaja envuelto en **`ConsecutiveIdInputDto`** (`src/Shared/Application/Dtos/`, junto a `PageQueryInputDto`) con su **`ConsecutiveIdInputValidator`** (`src/Infrastructure/Validation/FluentValidation/Shared/`), y las tres actions con id ganan `[ValidateRequest]`. El nombre lo cerró la revisión el 2026-09-04 en `Consecutive…`, tras pasar por `Sequence…`; la referencia es el `cau_consecutivoP` del legado. El `Id` lleva su `[Description]` para OpenAPI, también por indicación de la revisión. Los dos archivos son **adiciones transversales, no del contexto**, y su mensaje es genérico (`Id must be greater than 0.`) porque el validador compartido no sabe de qué contexto viene. Verificado que el enlace de un tipo complejo desde la ruta entrega el valor real y que **OpenAPI sigue documentando `Id[path]`**, no un `route.Id` ni un body.
- Nota de ejecución: los 5 verbos verificados en caliente contra la app corriendo (`dotnet run`, multitenencia apagada, EF InMemory): `GET` lista 200 con `{items, totalCount}`, `GET/{id}` 200 y 404, `POST` 201 con el `Id` asignado, `PUT` 200 y 404, `DELETE` 204 sin cuerpo y 404 en el segundo intento. El listado devuelve cuerpos distintos antes y después de un `POST`, lo que prueba que `[OutputCacheInvalidate]` evita servir el listado rancio. Rutas verificadas sin prefijo: `LossReasonsController` → `/loss-reasons` por el `KebabCaseParameterTransformer`, sin escribir la ruta a mano.
  **Corrección del 2026-09-04:** el merge de `main` sustituye `ASPNETCORE_PATHBASE` por un prefijo aplicado en proceso (`GlobalRoutePrefixConvention` + `RoutePrefixConfig`, clave de configuración `RoutePrefix`, hoy `/crm-service`). La ruta efectiva pasa a ser **`/crm-service/loss-reasons`**, en local y en despliegue. El controller no cambia —sigue declarando `[Route("[controller]")]` y **nunca debe escribir el prefijo**—, pero toda §6 y §7.4 se leen ahora con ese prefijo delante.
  **⚠️ GAP-8, resuelto dentro del paso.** El `[ProducesResponseType]` del 200 del listado no compilaba: `controllers.md` §5.5 y `casos-de-uso.md` §5.2 prescriben `ApiSuccessResponse<PagedPayload<T>>`, pero **`PagedPayload<T>` está declarado `internal`** en `Shared/Infrastructure/Presentation/Results/HttpOkPagedResult.cs` y no hay `InternalsVisibleTo` hacia `Api`, así que ningún controller puede nombrarlo. Es un drift de la plantilla que nadie había tocado porque **este es el primer endpoint paginado del servicio**. Aplicando D11 (ante la contradicción entre documento y scaffold, manda el documento) se cambió la visibilidad del record a `public`, un cambio de una palabra sin efecto en runtime. **Toca `src/Shared`, fuera de la lista de `Archivos:` de este paso y de lo que R5 daba por auditado.** ✅ **Firmado por el tech lead el 2026-08-28: pasarlo a `public` es la solución aceptada.** El arreglo sigue perteneciendo aguas arriba, en `service-template-dotnet`, donde hoy ningún servicio puede documentar un endpoint paginado.

#### [F4.3] Wire up dependency injection and the output cache policy
`id: F4.3 · depende_de: F4.2 · tarea: T11 (Juan Esteban) · estado: done`
- Objetivo: registrar el contexto en el arranque y la política de caché que el listado necesita.
- Fuente: D10 · `contextos.md` §5.5 · `puertos-y-adaptadores.md` · `cache.md`
- Archivos: `src/Api/DependencyInjection/LossReasonServiceExtensions.cs`, `src/Api/DependencyInjection/ApplicationServiceExtensions.cs`, `src/Api/DependencyInjection/OutputCacheExtensions.cs`
- Detalle: `public static IServiceCollection AddLossReasonServices(this IServiceCollection services)`, todo `Scoped`, en el orden normativo: **primero** `ILossReasonRepository` y `ILossReasonUsageReader`, **después** los 5 casos de uso. Agregar `services.AddLossReasonServices();` en `AddApplicationServices()`.
  Registrar en `ConfigureCache` la política `"loss-reasons-list"`, que **parte de la política base** (conserva `SetVaryByHeader("X-Entity-Code", "Accept-Language")`, que es lo que aísla los tenants) y **añade** `SetVaryByQuery("name", "isActive", "pageIndex", "pageSize")`. Sin esa variación por query, el listado filtrado serviría el resultado de un filtro para otro (D10).
  Antes de editar, confirmar la ruta real del archivo de configuración de caché: si `ConfigureCache` no está donde dice este paso, **detenerse y reportar** (regla 4 de §0), no buscarlo y decidir por cuenta propia.
- Hecho cuando: la app arranca, `/loss-reasons` no da error de resolución de dependencias, y dos requests con distinto `name` devuelven cuerpos distintos.
- Verificar: `dotnet build Service.slnx -c Release` — **ejecutado el 2026-08-28: 0 errores, 0 advertencias**
- **Enmienda de la revisión (2026-08-28): no hay política nombrada. `OutputCacheExtensions.cs` no se toca.** La primera entrega registró `"loss-reasons-list"` como el paso pedía; la revisión lo rechazó y mandó guiarse de la documentación. `cache.md` distingue dos mecanismos en su tabla de `[OutputCache]`: `PolicyName` «selecciona una política nombrada **en lugar de** la base», mientras que las demás propiedades del atributo **complementan** la base. Con la segunda vía, la política base sigue viva y con ella el `SetVaryByHeader("X-Entity-Code", "Accept-Language")` que aísla los tenants, sin repetirlo. Lo que queda es un atributo en el controller:
  `[OutputCache(Duration = CacheDurationSeconds, Tags = [CacheTag])]`.
  La revisión pidió además **`Duration` explícito**, declarado como constante y compartido con el `GET /{id}` para que las dos lecturas no se desalineen. La primera entrega puso `60` segundos, el valor de los dos ejemplos de lectura de `controllers.md`; **la segunda ronda (2026-09-04) lo subió a 3 días**, escrito como `3 * 24 * 60 * 60` para que el número se lea solo, ya que la misma ronda mandó quitar el comentario que lo explicaba. Encaja con el caso: catálogo administrativo que cambia poco, y toda escritura invalida el tag, así que una lectura rancia solo dura hasta la siguiente mutación. **Segunda enmienda (2026-09-04): fuera `VaryByQueryKeys`.** Las dos primeras entregas lo declararon —`("EntityCode", "search", "isActive", "pageIndex", "pageSize")`— sobre la premisa de que la política base, con su `SetVaryByQuery("EntityCode")`, dejaba la clave restringida al tenant y había que reabrirla. **La premisa era falsa** y la versión al día de `cache.md` lo dice explícito: el atributo `[OutputCache]` **reaplica `DefaultPolicy` después** de la política base y con ello restaura `QueryKeys = "*"`, así que todo endpoint anotado ya varía por la query completa, `EntityCode` incluido. Declarar la lista no añadía nada: *restringía*. De hecho **el fallo de la ronda anterior lo causó precisamente declararla** —`search` no estaba en la lista—, no su ausencia. Verificado en caliente tras quitarla: `?search=Pri` y `?search=Comp` devuelven cuerpos distintos, `?isActive=true`/`false` también, `?pageSize=1`/`50` también, `?search=<51 caracteres>` responde **400** y no un 200 cacheado, la segunda llamada al mismo filtro sí viene de la caché, y un `POST` invalida el tag.
  Con esta enmienda **la lista de `Archivos:` de este paso queda en dos**, no tres, y el `Detalle` sobre `ConfigureCache` queda sin efecto.
- Nota de ejecución: `ConfigureCache` **sí estaba** donde el paso decía, así que la regla 4 no se activó. Las tres condiciones del `Hecho cuando` se verificaron en caliente, y **se repitieron enteras tras la enmienda**, porque el mecanismo de caché cambió: la app arranca, `GET /loss-reasons` responde 200 sin error de resolución, y `?search=Pri`, `?isActive=false` y `?pageSize=1` devuelven cuerpos distintos entre sí con la caché activa.
  **Ahí apareció un fallo que los unitarios no veían.** Al renombrar el filtro a `search` (revisión de `F4.1`) quedó `"name"` en `VaryByQueryKeys`: el listado dejó de variar por el filtro y **servía la respuesta cacheada de otra búsqueda** — `?search=<51 caracteres>` respondía 200 con el resultado anterior en vez de 400. Es R8 exactamente. Corregido, y cubierto con un test que lee el atributo por reflexión y lo compara contra las propiedades de `GetLossReasonsInputDto` + `PageQueryInputDto` + `EntityCode`, de modo que renombrar una propiedad sin tocar el atributo ahora rompe la suite. El test se verificó **rompiéndolo a propósito** antes de darlo por bueno.
- **Cierre del punto (2026-09-04): `VaryByQueryKeys` sale, y la revisión tenía razón.** El primer rechazo a quitarlo se argumentó sobre el `SetVaryByQuery("EntityCode")` de la política base, sin saber que el atributo reaplica `DefaultPolicy` encima. Al traer `docs/plantilla/cache.md` desde `main` —los commits `JK-11790`, que reescribieron justo esa sección— quedó claro lo contrario, y la verificación en caliente lo confirmó. El test de reflexión cambia de sentido: en vez de fijar qué claves se declaran, ahora fija que **no se declara ninguna** (`GetLossReasons_DoesNotRestrictTheCacheKeyToASubsetOfTheQuery`), que es la forma real de que R8 no vuelva.
- **Deuda detectada de paso, fuera de T11:** `src/Api/DependencyInjection/OutputCacheExtensions.cs` de este fork está atrasado respecto de la plantilla en dos puntos que `cache.md` ya documenta — le falta el `excludeDefaultPolicy: true` de la política base (sin él la caché **no es opt-in** y se cachean endpoints sin `[OutputCache]`, health checks incluidos) y el `SetVaryByHeader([])` de la política `Global`. → `GAP-9`.

#### [F4.4] Unit tests for validators and controller
`id: F4.4 · depende_de: F4.3 · tarea: T11 (Juan Esteban) · estado: done`
- Objetivo: cubrir la capa de presentación.
- Fuente: `testing.md`
- Archivos: `tests/UnitTests/Api/Controllers/LossReasonsControllerTests.cs`, `tests/UnitTests/Infrastructure/Validation/LossReasons/{Create,Update,Get}LossReasonInputValidatorTests.cs`
- Detalle: validadores con `TestValidate(input)` + `ShouldHaveValidationErrorFor`; casos límite de D5: **50 caracteres pasa, 51 falla, vacío falla**. Controller con NSubstitute de los casos de uso, verificando que delega y no decide.
- Hecho cuando: todos pasan y existe un test que fija el límite exacto en 50 **por ambos caminos** — el validador (F4.1) y el agregado (F1.6) —, que es lo que hace visible cualquier divergencia entre las dos capas de D4.
- Verificar: `dotnet test tests/UnitTests -c Release` — **ejecutado el 2026-08-28: 466/466 en verde**, cobertura 97,5 % líneas / 92,5 % ramas. Son **45 tests nuevos** tras la ronda de revisión: 11 del controller (incluido el de la clave de caché), 8 del validador de actualizar, 10 del de listar, 7 del `ConsecutiveIdInputValidator`, 5 del contrato de longitud y 4 en `LossReasonAggregateTests` y el validador de crear por el `IsActive` obligatorio del `PUT`.
- Nota de ejecución: **son cuatro archivos de test, no tres, y no cuelgan de una carpeta `LossReasons/`.** Dos precisiones sobre la lista de `Archivos:` del paso:
  1. **`{Create,Update,Get}LossReasonInputValidatorTests.cs` van en `tests/UnitTests/Infrastructure/Validation/`, plano**, no en un subdirectorio `LossReasons/`: es donde T8 ya había dejado el de crear, y abrir la carpeta obligaba a mover su archivo o a partir los tres hermanos en dos sitios.
  2. **El `Hecho cuando` pedía un test que no existía en ninguna parte.** Los tests de F1.6 y los de los validadores leen el límite de `LossReasonAggregate.NameMaxLength`, así que **todos seguirían en verde si alguien cambiara la constante**: fijan la coherencia, no el número. Nace `LossReasonNameLengthContractTests.cs`, el único sitio con el literal `50` escrito, que recorre los dos caminos de D4 con las mismas cadenas — 50 pasa y 51 falla en los tres validadores **y** en `LossReasonAggregate.Create`/`Update`. Es lo que el paso pedía; el archivo extra es la forma de cumplirlo.
- Cobertura: **97,5 % de líneas / 92,4 % de ramas** en la suite, sobre el piso de 90 de la puerta de CI. `LossReasonsController` y los tres validadores quedan en 100 %.

### Fase 5 — Verificación de extremo a extremo · `pending`

> Decisiones que la afectan: todas.
> Estrategia de pruebas: integración con Testcontainers (SQL Server real) — requiere **Docker corriendo**. La multitenencia queda apagada; `ApiFactory` re-apunta el `DbContext` al contenedor.

#### [F5.1] Integration tests for the endpoints
`id: F5.1 · depende_de: F4.4 · tarea: T12 (Juan Esteban) · estado: pending`
- Objetivo: verificar el contrato real sobre una base real.
- Fuente: `testing.md` · §6
- Archivos: `tests/IntegrationTests/LossReasons/LossReasonEndpointsTests.cs`
- Detalle: `[Collection(IntegrationTestCollection.Name)]`, hereda `IntegrationTestBase`. **Seed con la entidad de persistencia, no con el agregado.** Leer el cuerpo con `ApiResponse<T>` y, en el listado, con `ApiResponse<ApiPagedData<T>>` (doblemente envuelto) — **no assertar solo el `StatusCode`**. Casos: listado paginado con `totalCount`; catálogo vacío → **200 con `items: []`** (D9); creación → 201 con `id`; actualización → 200; borrado libre → 204; borrado de una causa referenciada desde `tbl_opo_negocios` → **409** (D7); id inexistente → 404; nombre de 51 caracteres → 400 (D5); fila sembrada con `CauNombre` de más de 50 caracteres → se **lee** por `GET` pero su `PUT` responde 400 (R7); fila sembrada con `cau_nombre = NULL` o `cau_estado = NULL` → **el listado falla con 500** y el cuerpo es el envelope de error, no datos maquillados: es el comportamiento que D6 elige a propósito, y el test existe para **fijarlo como contrato conocido** en vez de descubrirlo en producción (R10). **Sembrar con SQL crudo**, no con la entidad de persistencia: su tipo ya no permite construir la fila corrupta; y una creación **invalida el listado cacheado** (D10), verificando que el `GET` posterior incluye la fila nueva.
- Hecho cuando: los 10 escenarios pasan contra el contenedor.
- Verificar: `dotnet test tests/IntegrationTests -c Release`

#### [F5.2] Full build, test and coverage gate
`id: F5.2 · depende_de: F5.1 · tarea: T12 (Juan Esteban) · estado: pending`
- Objetivo: dejar el contexto en verde con la puerta de CI.
- Fuente: `testing.md` · `.github/workflows/ci.yml`
- Archivos: — (sin cambios de código; si la cobertura no llega, se añaden tests en los archivos ya creados)
- Detalle: correr el flujo idéntico al de CI. Solo los **unit tests** cuentan para el porcentaje; el umbral es `COVERAGE_THRESHOLD` (default **90** de cobertura de línea) y CI falla por debajo. `Program.cs`, `*DependencyInjection*.cs` y `Extensions/*Extensions.cs` están excluidos por `coverlet.runsettings`.
- Hecho cuando: `summary.linecoverage` ≥ 90 y los tres comandos terminan en 0.
- Verificar: `dotnet build Service.slnx -c Release; if ($?) { dotnet test tests/UnitTests -c Release --collect:"XPlat Code Coverage" --settings coverlet.runsettings }; if ($?) { dotnet test tests/IntegrationTests -c Release }`

### Diferido, sin fecha

| Qué | Decisión que lo respalda |
|---|---|
| Migrar la escritura de `neg_cau_consecutivo` | §1 fuera de alcance · `GAP-7` |
| Caché L2 (`ICacheStore`) del catálogo | D10 — L1 sí entra en esta iteración |
| Autenticación dentro del servicio | D12 — el control es de infraestructura, no se difiere: **no se hace** |
| Modelo de permisos dentro del servicio | D13 — lo ejerce Jack, **no se hace** |
| Autorizar las rutas `[AllowAnonymous]` del lado de Jack | R9 — necesario antes del corte, pero es trabajo en el monolito |
| Client, feature flag y cutover en el monolito | §1 · `03-flujos.md` |
| Eliminar las dos copias muertas de `pa_inf_opo_excel_oportunidades_dinamico` | §1 fuera de alcance permanente |

## 9. Riesgos, GAPs y changelog

### 9.1 Riesgos

| # | Riesgo | Estado |
|---|---|---|
| R1 | **El esquema verificado proviene del dump de una sola institución** y es anterior al commit analizado (Discovery §0, GAP-1). Si otro tenant tiene `tbl_opo_causas` con otra forma, F2.1–F2.3 fallan en runtime, no en compilación | Abierto — mitiga F5.1 solo para el esquema del contenedor |
| R2 | **`neg_cau_consecutivo` no está indexado** (verificado: 6 índices en `tbl_opo_negocios`, ninguno lo cubre) sobre ~300.000 filas. El chequeo de uso del borrado es un scan completo | Aceptado — borrar una causa es una acción administrativa rara; el orden de F3.5 evita pagarlo en los 404 |
| R3 | ~~El repositorio no compila~~ | ✅ **Cerrado** el 2026-08-14 — commit `9f24956`; `dotnet build Service.slnx -c Release` ejecutado con exit code 0 |
| R4 | **Tres rupturas de paridad declaradas** (paginación D8; filtro opcional y 200 en vacío D9). QA las reportará como bugs si no las conoce | Abierto — se transfieren a `03-flujos.md` §3 antes del corte |
| R5 | **El aislamiento de red es el único control de acceso** (D12). El servicio no autentica: cualquiera que alcance el puerto puede crear, editar y borrar causas de cualquier tenant cuyo `X-Entity-Code` conozca. La decisión es deliberada y descansa por completo en que el perímetro se sostenga | Aceptado — condición de validez de D12. Revisar en cualquier cambio de ingress, network policy o ambiente de pruebas |
| R9 | **La migración no corrige el defecto D1 del Discovery, solo lo traslada** (D13). Las 7 acciones `[AllowAnonymous]` de `EstructuracionComercialController` siguen abiertas en Jack, y el controller que llame al servicio necesita sus filas en `tbl_seg_funciones` para que exista autorización real. Cerrar `GAP-3` cerró la investigación, no el defecto | Abierto — trabajo del lado de Jack, en `03-flujos.md`. Sin esto, el corte deja el catálogo tan expuesto como hoy |
| R6 | Durante la convivencia, **monolito y servicio escriben la misma tabla física** sin coordinación. Un borrado desde el servicio puede sorprender a una sesión del monolito | Aceptado — es inherente al patrón de corte progresivo |
| R7 | **El límite de 50 (D5) es más estricto que la columna `varchar(200)`.** El endpoint `GET api/causas` del legado nunca validó longitud, así que puede haber filas de más de 50 caracteres: el servicio las lee pero **no las deja actualizar** (400 en el `PUT`) hasta acortar el nombre | Abierto — cuantificar antes del corte con `SELECT cau_consecutivoP, LEN(cau_nombre) FROM tbl_opo_causas WHERE LEN(cau_nombre) > 50;` en los tenants objetivo. Si el conteo es 0, el riesgo se cierra; si no, decidir entre acortar los datos o revisar D5 |
| R8 | **El caché L1 del listado depende de que la política varíe por los filtros** (D10, paso F4.3). Si alguien la registra mal o el endpoint cae en la política base, se sirve el resultado de un filtro para otro — un fallo de correctitud que se ve como datos equivocados, no como error | **Mitigado y con red automatizada** desde el 2026-08-28, **no cerrado.** El riesgo **se materializó de verdad** durante T11: al renombrar el filtro a `search`, el `VaryByQueryKeys` quedó en `"name"` y el listado sirvió la respuesta cacheada de otra búsqueda; lo detectó la verificación en caliente, no los unitarios. Ahora un test lee el atributo por reflexión y lo compara contra las propiedades de los DTOs que la action enlaza, así que un renombrado sin actualizar la clave rompe la suite. **Reformulado el 2026-09-04, y el riesgo era el contrario del que decía esta fila.** No hacía falta «que la política varíe por los filtros»: un endpoint anotado ya varía por toda la query porque el atributo reaplica `DefaultPolicy` (`cache.md`). El peligro real es **declarar `VaryByQueryKeys`**, que *restringe* la clave — y es exactamente lo que causó el fallo reproducido en T11. Con el atributo sin lista de claves, el riesgo queda **cerrado en su parte de filtrado**, verificado en caliente (filtros, `isActive`, paginación, el 400 no cacheado, acierto de caché e invalidación por tag) y protegido por `GetLossReasons_DoesNotRestrictTheCacheKeyToASubsetOfTheQuery`, que rompe la suite si alguien vuelve a declarar una lista. Sigue abierto solo lo que ningún unitario puede cubrir —que el middleware honre las reglas en despliegue real, con Redis y multitenencia—, que cierra F5.1. **Nota de impacto:** la duración pasó a 3 días, así que cualquier error de clave que se cuele dura tres días, no un minuto |
| R10 | **El servicio exige `NOT NULL` donde la BD no lo exige** (D6). `cau_nombre` y `cau_estado` aceptan NULL y `pa_opo_causas_modificar` puede seguir escribiéndolo desde el monolito durante la convivencia. Una sola fila con NULL hace que SqlClient lance `SqlNullValueException` **por la consulta entera**: el `GET /loss-reasons` de ese tenant responde 500 hasta que el dato se corrija, y no solo esa fila. **Es el fallo ruidoso que la decisión busca** —mejor que servir un nombre vacío como si fuera un nombre—, pero convierte un dato sucio en una caída del listado | **Aceptado, con condición.** La decisión es válida mientras el dato esté limpio: **cuantificar antes del corte** con `SELECT COUNT(*) FROM tbl_opo_causas WHERE cau_nombre IS NULL OR cau_estado IS NULL;` en los tenants objetivo (tarea `EXT-9`). Si el conteo es 0 el riesgo queda latente; si no, hay que limpiar el dato **antes** de exponer el servicio. Cerrarlo de raíz exige el `ALTER TABLE … NOT NULL` de Discovery D2/D3, que **no está en el alcance de este plan** |

### 9.2 GAPs

**No quedan GAPs bloqueantes.** Los siete originales se resolvieron el 2026-08-14; el octavo apareció y se resolvió dentro de F4.2 el 2026-08-28, y solo espera firma. Se conservan con su resolución porque cerrarlos borrando el enunciado haría ilegible por qué el plan es como es.

| id | Qué faltaba | Resolución | Dónde quedó |
|---|---|---|---|
| `GAP-1` | El repositorio no compilaba: faltaba `GetServiceInfoOutputDto` (`CS0246`) | ✅ **Resuelto por el dueño del repositorio** e incorporado con un pull — commit `9f24956`. Verificado con `dotnet build Service.slnx -c Release`: exit code 0 | F0.2 `done` · R3 cerrado |
| `GAP-2` | Mecanismo de autenticación del servicio | ✅ **No se implementa autenticación en el servicio.** Solo es accesible mediante pipelines; el control lo ejerce infraestructura | **D12** · R5 reescrito |
| `GAP-3` | Qué roles administran hoy el catálogo (`tbl_seg_*`) | ✅ **Los servicios de esta plantilla no validan permisos**: lo hace Jack, que es quien los invoca, igual que con `comunicados/announcements`. No hace falta consultar las tablas | **D13** · R9 abierto |
| `GAP-4` | Mecanismo de feature-flag del corte | ✅ **Fuera del alcance de este plan.** La integración y el cutover son de `03-flujos.md` | §1 · §4 del backlog |
| `GAP-5` | Si el tenant-resolver sirve la base del CRM y con qué `X-Entity-Code` | ✅ **Jack resuelve el tenant** y lo transmite al servicio, que lo consume con el mecanismo estándar de la plantilla | **D14** · §7.1 |
| `GAP-6` | Los veredictos del Discovery §7 sin firmar | ✅ **Todas las propuestas quedan firmadas.** Las catorce decisiones de §2 pasan a `aprobada` | §2 completo |
| `GAP-7` | Destino de la escritura de `neg_cau_consecutivo` | ✅ **El agregado de negocio se queda en el monolito**, fuera del alcance de este plan | §1 fuera de alcance |
| `GAP-8` | **`PagedPayload<T>` es `internal`**, así que ningún controller puede declarar el `[ProducesResponseType]` del 200 de un listado paginado que `controllers.md` §5.5 y `casos-de-uso.md` §5.2 prescriben. Drift de la plantilla, invisible hasta hoy porque este es el primer endpoint paginado del servicio | ✅ **Resuelto en F4.2 y firmado el 2026-08-28.** El record pasa a `public` por D11 (manda el documento); el tech lead acepta esa solución. Cambio de una palabra, sin efecto en runtime, pero **toca `src/Shared`**, que R5 daba por no tocado. El arreglo definitivo va aguas arriba, en `service-template-dotnet` | F4.2 · nota de ejecución |
| `GAP-9` | **`OutputCacheExtensions.cs` del fork está atrasado respecto de la plantilla.** Al traer `docs/plantilla/cache.md` desde `main` (commits `JK-11790`) aparecen dos diferencias con el código que tenemos: falta el **`excludeDefaultPolicy: true`** en `AddBasePolicy` —sin él la caché **no es opt-in** y se cachea toda respuesta 200 que llegue al middleware, incluidos `/health/live`, `/health/ready` e `/info`, que quedan reportando salud vieja durante `DefaultTtlSeconds`— y falta el `SetVaryByHeader([])` de la política nombrada `Global`, sin el cual esa política no quita la variación por tenant que promete | 🟡 **Resuelto a medias el 2026-09-04.** La mitad que importaba —el `excludeDefaultPolicy: true`— **llega con el merge de `main`**: la caché vuelve a ser opt-in y los health checks dejan de almacenarse. La otra mitad, la política nombrada `Global`, **queda abierta aguas arriba a propósito**. Se corrigió aquí con el `SetVaryByHeader([])` que prescribe `cache.md` y **se revirtió por indicación de la revisión** (`ed53dde`): ningún endpoint la selecciona hoy, así que el arreglo no cambia nada en producción y sí aparta el fork de `main`. **El defecto es real y está verificado contra el runtime**, no solo leído: ejecutando la política base y la nombrada en el orden del middleware sobre un `OutputCacheContext`, con `p => { }` quedan `X-Entity-Code, Accept-Language` en la clave y con `SetVaryByHeader([])` no queda ninguna — es decir, un endpoint `Global` genera **una entrada por tenant y por locale**, justo lo contrario de lo que promete su nombre. **Lo reporta el tech lead a `service-template-dotnet`.** Mientras no se arregle allá, **nadie debe usar `PolicyName = "Global"`** en este servicio | fuera de las fases · descubierto en la revisión de T11 |

Dos consecuencias de estas resoluciones **no se cierran con ellas** y siguen vivas como riesgos, no como GAPs:

* **R5** — con D12, el perímetro de red pasa a ser el único control de acceso. Es la condición bajo la cual el plan es válido.
* **R9** — con D13, el defecto D1 del Discovery (7 acciones `[AllowAnonymous]` en Jack) **cambia de dueño en vez de corregirse**. Cerrar `GAP-3` cerró la investigación de permisos, no el agujero: si nadie lo toma del lado de Jack, el corte deja el catálogo tan expuesto como está hoy.

### 9.3 Changelog de enmiendas

| Fecha | Qué cambió | Decisión afectada | Pasos afectados | Tareas invalidadas |
|---|---|---|---|---|
| 2026-08-14 | Versión inicial | — | — | — |
| 2026-08-14 | Las invariantes de `Name` (requerido y longitud) se validan **también** en el agregado, además de en FluentValidation; el límite pasa a `LossReasonAggregate.NameMaxLength` como fuente única del número | D4 | F1.1, F1.3, F1.6, F4.1, F4.4 | ninguna — el plan no se había ejecutado |
| 2026-08-14 | Límite de `Name`: **200 → 50** | D5 | F1.1, F1.3, F1.6, F4.1, F4.4, F5.1 | ninguna — el plan no se había ejecutado. Abre R7 |
| 2026-08-14 | Caché: de «sin caché» a **L1 sí, L2 no**, con política propia para el listado filtrado e invalidación por tag | D10 | F4.2, F4.3, F5.1 | ninguna — el plan no se había ejecutado. Abre R8 |
| 2026-08-14 | **Se eliminan las dependencias artificiales entre los cinco casos de uso** (R7 de las reglas de tareas): F3.1–F3.4 pasan a depender solo de F2.7 y F3.5 de F2.7 + F2.6. Ninguno consumía código del anterior; la cadena solo reflejaba el orden de redacción y serializaba cinco tareas que pueden ir en paralelo | — | F3.2, F3.3, F3.4, F3.5 | ninguna — el plan no se había ejecutado |
| 2026-08-14 | **F3.6 se divide en F3.6–F3.10**, un paso de test por caso de uso. Agrupados hacían que la tarea de escrituras llegara a 12 archivos de `src/`, por encima del techo de R2, y no pudiera moverse de estado por partes | — | F3.6 → F3.6–F3.10 | ninguna — el plan no se había ejecutado |
| 2026-08-14 | F4.1 y F4.2 declaran sus dependencias reales sobre los casos de uso que consumen, en vez de colgar de F3.5 por posición | — | F4.1, F4.2 | ninguna — el plan no se había ejecutado |
| 2026-08-14 | El anexo con la tabla de tareas se reemplaza por un puntero a `tasks_causas.md`, para no sostener dos fuentes de verdad del reparto en PRs | — | — | ninguna |
| 2026-08-21 | **Revisión de QA sobre el PR de T6**, con tres reglas que valen para **todo el contexto**, no solo para el listado: (a) los `[property: Description(...)]` de los DTOs pasan de español **a inglés** (§3.1), desviándose del ejemplo de `casos-de-uso.md` §5.2; (b) **`{X}Mapping.cs` se queda**, pero **solo con la traducción DTO ↔ dominio**: `ToOutputDto()` sí, `ToFilter()` no — el objeto de filtro se construye inline en el `ExecuteAsync` (§5.6, con la tabla de qué va dónde); (c) se quita del use case el comentario del catálogo vacío. **T7, T8, T9 y T10 quedan afectadas antes de escribirse** | — | §3.1 · §5.6 · F3.1 · F3.2 · F3.3 · F3.4 · F3.5 | ninguna — T7–T10 aún no arrancan |
| 2026-08-21 | **D6 se reescribe con su motivo real, sin cambiar el código.** La enmienda de T4 afirmaba que las columnas son `NOT NULL` en la BD; **son NULLABLE** —verificado por el dump leído con la trampa del script, por `pa_opo_causas_modificar` (`@cau_nombre VARCHAR(200) = NULL`) y por Discovery D2/D3, que **queda confirmado, no desactualizado**—. La entidad no anulable **se mantiene**, pero como **decisión técnica de integridad**, no como reflejo del esquema: el servicio prefiere fallar ruidosamente ante un dato corrupto antes que normalizarlo a `""`/`false` y propagarlo. Se retira la «contradicción registrada» contra el Discovery, se corrige §4, F2.1, F2.3 y F2.8, y **se abre R10** con su consulta de detección y la tarea `EXT-9`. **F5.1 invierte su escenario de NULL**: ahora fija que el listado responde 500, y siembra con SQL crudo porque el tipo de la entidad ya no permite construir la fila | **D6** | §4 · F2.1 · F2.2 · F2.3 · F2.8 · F5.1 · §9.1 R10 | ninguna — el código de T4 no cambia |
| 2026-08-21 | **Firmadas las dos enmiendas abiertas de T4** por el tech lead: los nombres de la entidad en inglés con `HasColumnName` son **la convención**, no una desviación; y los unitarios del repositorio con EF InMemory (F2.9) **se quedan**, porque la puerta de cobertura de GitHub exige >90 % y solo cuenta unit tests | — | Fase 2 (estrategia) · F2.1 · F2.9 | ninguna |
| 2026-08-21 | **Revisión de QA sobre la rama de T3.** Tres ajustes en `LossReasonAggregate`, ninguno de comportamiento observable por el consumidor: (a) se elimina el comentario que explicaba el `IDENTITY` — el dominio no narra infraestructura; (b) `Create` deja de pasar el `Id`: se parte el constructor privado en dos, uno sin `Id` para `Create` y otro con `Id` que delega, solo para `Reconstruct`; (c) `Created()` deja de llamar `SetUpdatedAt`, así que `UpdatedAt` es `null` hasta la primera mutación. **(c) es una desviación del ejemplo de `entidades-y-agregados.md`** y se declara como tal | — | F1.3 (detalle) · F1.6 (un assert) | ninguna — T3 se corrige en su propia rama |
| 2026-08-21 | **La entidad de persistencia usa nombres propios en inglés, no los de las columnas.** La revisión del PR de T4 objetó los identificadores en español abreviado (`CauNombre`, `CauEstado`, `CauConsecutivoP`) que el snippet de F2.1 prescribía: contradicen la regla de idioma de §3.1 y el ejemplo de `contextos.md` §5.3. Pasan a `Id`, `Name`, `IsActive`, y el esquema legado se cita en `HasColumnName`. Tipo y nulabilidad intactos, así que **D6 no se toca**. Se aprovechó para podar los comentarios que repetían decisiones del plan: el código no documenta la migración, solo lo que no se ve en él | — | `F2.1`, `F2.2` (detalle) · `F2.3`, `F2.4`, `F2.8`, `F2.9` (mismo rename) | ninguna |
| 2026-08-21 | **El repositorio se prueba también con unitarios (EF InMemory); se agrega el paso F2.9.** La puerta de cobertura de CI mide solo unit tests, así que los 77 renglones de `LossReasonRepository` dejaron el pipeline de T4 en **89,6 %**, bajo el piso de 90 — y F5.1 no lo puede arreglar porque los tests de integración no cuentan para el porcentaje. La estrategia de la Fase 2 decía que el repositorio se probaba solo en la Fase 5; se enmienda para admitir unitarios sobre `ApplicationDbContext` + InMemory, con el precedente de `RepositoryBaseEFTests`. **Es una desviación de `testing.md` («No usar EF InMemory») y queda pendiente de la firma del tech lead.** Todo lo que depende de constraints sigue en F5.1. Cobertura resultante: **97,1 %** | — | **F2.9 nuevo** · encabezado de estrategia de la Fase 2 | ninguna |
| 2026-08-21 | **`Infrastructure.csproj` se declara archivo compartido del contexto.** `F2.3` no podía compilar: el proyecto de infraestructura no referenciaba `LossReason`. Se añadió la `ProjectReference` a `LossReason.Application` (arrastra `Domain`), reportado como GAP y autorizado antes de aplicarlo. No cambia ninguna decisión ni dependencia; la referencia cubre a T4 y a T5, así que `F2.6` no toca ese archivo | — | `F2.3`, `F2.4` (lista de `Archivos:`) · `F2.6` no lo necesita | ninguna |
| 2026-08-21 | **`cau_nombre` y `cau_estado` son `NOT NULL`: D6 se invierte.** La verificación contra la BD contradice a `discovery_causas.md` §4.1, que las daba como NULLABLE con un `[verificado en BD]`. La entidad EF pasa de `string?`/`bool?` a `string`/`bool`, `LossReasonConfiguration` declara `.IsRequired()` sobre `Name`, el mapper pierde los `?? string.Empty` / `?? false` y el filtro de `GetAsync` pierde la guarda `x.Name != null`, que quedó como código muerto. Se borran los dos tests de NULL del mapper (`ToDomain_WithNullName_MapsToEmptyString`, `ToDomain_WithNullState_MapsToInactive`) y `GetByIdAsync_WithNullColumns_NormalizesThroughTheMapper` del repositorio, más las filas NULL que sembraban los tests de filtro; entra `ToDomain_WithInactiveRow_MapsTheState` para no perder el caso `false`. **La discrepancia con el Discovery queda abierta** y se corrige en su propia revisión: este plan no lo reescribe | **D6 (invertida)** | `F2.1`, `F2.2`, `F2.3`, `F2.8`, `F2.9` · §4 · encabezado de estrategia de la Fase 2 | ninguna — T4 se corrige en su propia rama |
| 2026-08-21 | **El assert de "no hay commit" de F3.8 pasa a leer el constructor, no un sustituto.** Tal como el paso lo pedía —`DidNotReceive()` sobre un `IUnitOfWorkPort`— el assert **no podía fallar nunca**: el use case no recibe el puerto, así que el sustituto quedaba sin inyectar y el test seguiría verde aunque alguien agregara el `CommitAsync`. Cumplía la letra de F3.8 y no su intención. Se reemplaza por una aserción de reflexión sobre los parámetros del constructor, que **sí falla** ante la regresión: verificado simulándola (agregar el puerto y llamar `CommitAsync` deja el test en rojo; sin usarlo ni siquiera compila, `CS9113`). **Es una desviación de la letra del paso F3.8** y se declara como tal. La mitad conductual de D3 —que el insert se confirma dentro del repositorio— ya la cubre `F2.9`. El caso equivalente de T9 no tiene el problema: allí el puerto sí se inyecta | — | `F3.8` (detalle) | ninguna — T8 se corrige en su propia rama |
| 2026-08-21 | **Asignación del plan a un equipo de tres.** Los 33 pasos de §8 pasan de `tarea: (sin asignar)` a declarar tarea y responsable (Juan Camilo, Brayan, Juan Esteban); `F0.1` queda como lectura de las tres personas. Ninguna decisión, paso, dependencia ni estimación cambia: el reparto vive en `tasks_causas.md` | — | ninguno en su contenido | ninguna |
| 2026-08-14 | **Resolución de los siete GAPs.** D1–D11 pasan a `aprobada`; se añaden **D12** (sin autenticación en el servicio), **D13** (sin validación de permisos, la ejerce Jack) y **D14** (Jack determina y envía el tenant). Las seis fases pasan de `blocked` a `pending` | D1–D11 firmadas · D12, D13, D14 nuevas | Fase 0 a Fase 5 desbloqueadas · F0.2 → `done` | ninguna — el plan no se había ejecutado. Cierra R3, reescribe R5, **abre R9** |
| 2026-08-21 | **F2.6 ejecutado** (T5 — Juan Camilo): entidad keyless `DealLossReasonUsage` (`tbl_opo_negocios`, `HasNoKey()`), su configuración EF y `LossReasonUsageReader` (implementa `ILossReasonUsageReader` con `AnyAsync` + `AsNoTracking` + guard `OperationCanceledException` → `PersistenceErrors.Failure`). **Descubrimiento:** `Infrastructure.csproj` no referenciaba `LossReason.Application.csproj`; se añadió la referencia. Verificado: `dotnet build Service.slnx -c Release` (0 errores, 0 advertencias) y `dotnet test tests/UnitTests -c Release` (357/357 en verde). F2.6 → `done` | — | F2.6 | ninguna |
| 2026-08-28 | **Revisión de QA sobre el PR de T11.** Seis cambios, tres de ellos con efecto en el contrato de §6: **(a) fuera la política nombrada** —`OutputCacheExtensions.cs` vuelve a quedar intacto y el listado se cachea con `[OutputCache(Duration, Tags, VaryByQueryKeys)]`, que *complementa* la política base en vez de reemplazarla, con `Duration = 60` explícito y compartido con el `GET /{id}`—; **(b)** el filtro del listado pasa de `name` a **`search`**, y su validador deja el catálogo de errores de dominio por un mensaje propio (sigue tomando el número de `NameMaxLength`); **(c)** `isActive` del `PUT` pasa a **`bool?` obligatorio** en DTO, Args y agregado, igual que ya estaba el `POST` — sin eso un `PUT` sin `isActive` respondía 200 y **desactivaba la causa** con el `false` del default; **(d)** el `id` de ruta se valida con **`IdInputDto` + `IdInputValidator` en `Shared`**, porque `ValidateRequestFilter` descarta los tipos simples y una restricción de ruta daría 404 en vez de 400; **(e)** fuera el comentario XML de `PagedPayload<T>`; **(f)** datos de prueba a inglés, la regla que salió de la revisión de T9. **(c) toca archivos de T3 y T9**, fuera de la lista de `Archivos:` de la Fase 4, por indicación explícita de la revisión | **D10** (mecanismo de caché) · **D4** (segunda capa del `PUT`) | §6.1 · §6.2 · §6.3 · §6.4 · §6.5 · `F4.1` · `F4.2` · `F4.3` (enmienda) · `F4.4` · §9.1 R8 | ninguna — T11 se corrige en su propia rama |
| 2026-08-28 | **R8 se materializó y ahora tiene red.** Al renombrar el filtro a `search` quedó `"name"` en `VaryByQueryKeys` y el listado sirvió la respuesta cacheada de otra búsqueda: `?search=<51 caracteres>` respondía 200 con el resultado anterior en vez de 400. **No lo vio ningún unitario; lo vio la verificación en caliente.** Se corrige y se cubre con un test que compara las claves declaradas contra las propiedades de los DTOs que la action enlaza, verificado rompiéndolo a propósito. El riesgo baja de «solo lo cubre la revisión humana» a «cubierto para el conjunto de claves», y sigue esperando F5.1 para lo demás | — | §9.1 R8 · `F4.3` | ninguna |
| 2026-08-28 | **Fase 4 ejecutada completa** (`F4.1`–`F4.4`, T11 — Juan Esteban): los dos validadores que faltaban, `LossReasonsController` con sus 5 endpoints, `AddLossReasonServices` con la política de caché `"loss-reasons-list"`, y 34 tests nuevos. Build en verde (0 advertencias), **455/455** unitarios, cobertura **97,5 %**. Los 5 verbos verificados en caliente contra la app corriendo. La Fase 4 pasa a `done`; **F5.1 queda desbloqueado** | — | `F4.1`, `F4.2`, `F4.3`, `F4.4` → `done` · encabezado de la Fase 4 | ninguna |
| 2026-08-28 | **`PagedPayload<T>` pasa de `internal` a `public` (`GAP-8`).** `controllers.md` §5.5 y `casos-de-uso.md` §5.2 prescriben `[ProducesResponseType(typeof(ApiSuccessResponse<PagedPayload<T>>), 200)]`, pero el tipo era `internal` en `Shared.Presentation` y sin `InternalsVisibleTo`: **el paso F4.2 no compilaba tal como estaba escrito**. Se aplicó D11 (ante la contradicción documento/scaffold, manda el documento). Cambio de una palabra, sin efecto en runtime, pero **`src/Shared` deja de estar intacto**, contra lo que R5 daba por auditado. ✅ **Firmado por el tech lead el mismo día**; el arreglo pertenece aguas arriba, en `service-template-dotnet` | **D11** | `F4.2` · §9.2 `GAP-8` · §9.1 R5 (auditoría de `Shared`) | ninguna |
| 2026-08-28 | **La política de caché nombrada también repite `SetVaryByQuery("EntityCode")` de la base.** F4.3 enumeraba solo el header a conservar, pero la política base varía además por el `EntityCode` de query, que es el segundo canal de tenant de la plantilla; como una política nombrada **reemplaza** a la base y no se apila sobre ella (`cache.md`), omitirlo dejaría a dos tenants que usan ese canal compartiendo entrada de caché. Lista final: `("EntityCode", "name", "isActive", "pageIndex", "pageSize")`. Precisión del paso, no cambio de decisión: D10 pide partir de la base | — | `F4.3` (detalle) · §9.1 R8 | ninguna |
| 2026-08-28 | **F4.4 gana un cuarto archivo de test y sus rutas se aplanan.** El `Hecho cuando` exigía fijar el límite «exacto en 50 por ambos caminos», pero **ningún test existente escribía el número**: todos lo leen de `LossReasonAggregate.NameMaxLength` y seguirían verdes si la constante cambiara. Nace `LossReasonNameLengthContractTests.cs`, único sitio con el literal `50`, que recorre validadores y agregado con las mismas cadenas. Además, los tres tests de validador van en `tests/UnitTests/Infrastructure/Validation/` plano, no bajo `LossReasons/`, porque es donde T8 ya había dejado el de crear | — | `F4.4` (lista de `Archivos:` y detalle) | ninguna |
| 2026-09-04 | **Segunda ronda de revisión sobre el PR de T11**, la de los cinco comentarios del 28-ago 21:27–21:44 que no se habían visto. Cuatro aplicados: **(a)** fuera el comentario que justificaba la duración del caché; **(b)** la duración sube de 60 segundos a **3 días**, escrita `3 * 24 * 60 * 60` para que el número siga siendo legible sin comentario; **(c)** y **(d)** `IdInputDto` → `ConsecutiveIdInputDto` y `IdInputValidator` → `ConsecutiveIdInputValidator`, con sus archivos y su test renombrados. La revisión escribió `Sequence…`; el tech lead lo fijó en `Consecutive…`, por el `cau_consecutivoP` del legado — **revertido el mismo día, ver la última fila**. El quinto —quitar `VaryByQueryKeys`— se rechazó primero por error; ver la fila siguiente | **D10** (duración) | §6.2 · §6.3 · §6.4 · §7.3 · §5 (inventario) · `F4.2` · `F4.3` · `F4.4` · §9.1 R8 | ninguna — se corrige en la rama de T11 |
| 2026-09-04 | **`VaryByQueryKeys` sale del listado: la revisión tenía razón y el rechazo previo estaba mal fundado.** Se argumentó que sin la lista la clave quedaba restringida a `EntityCode` por el `SetVaryByQuery` de la política base. Al traer `docs/plantilla/cache.md` desde `main` —commits `JK-11790`, que reescribieron la sección «Cómo se arma la clave de caché»— se ve lo contrario: **el atributo `[OutputCache]` reaplica `DefaultPolicy` después de la política base y restaura `QueryKeys = "*"`**, así que un endpoint anotado ya varía por toda la query. Declarar la lista *restringe* la clave, y **es lo que causó el fallo de la ronda anterior**, no su ausencia. Verificado en caliente tras quitarla: filtros distintos dan cuerpos distintos, `isActive` y la paginación varían, `?search=<51 caracteres>` responde 400 y no un 200 cacheado, el mismo filtro repetido sí acierta en caché, y un `POST` invalida el tag. El test de reflexión cambia de sentido: fija que **no** se declara ninguna clave. Se corrigió también la respuesta publicada en el hilo del PR | **D10** | `F4.2` · `F4.3` (segunda enmienda) · `F4.4` · §7.3 · §9.1 R8 · §9.2 `GAP-9` | ninguna |
| 2026-09-04 | **La corrección de la política `Global` se revierte; el defecto pasa a seguimiento aguas arriba.** La revisión pidió mantener el registro alineado con la plantilla. Antes de revertir se comprobó el mecanismo **contra el runtime**, no contra el documento: armando las mismas políticas que `ConfigureCache` y ejecutándolas en el orden del middleware sobre un `OutputCacheContext`, con `p => { }` la clave conserva `X-Entity-Code, Accept-Language` y con `p.SetVaryByHeader([])` no conserva ninguna. O sea que el defecto existe —un endpoint `Global` cachea por tenant— y `cache.md` lo documenta como comprobado, prescribiendo la línea que se quitó. Se revierte porque **hoy nadie selecciona `PolicyName = "Global"`**: el arreglo no cambia comportamiento y sí separa el fork de `main`. **Regla operativa mientras siga abierto: no usar `PolicyName = "Global"` en este servicio.** Lo reporta el tech lead a `service-template-dotnet` | — | §9.2 `GAP-9` | ninguna |
| 2026-09-04 | **Tercera ronda del PR de T11: cuatro hilos nuevos (T14–T17), tres cambios.** **(a)** El nombre se cierra en **`ConsecutiveIdInputDto`/`ConsecutiveIdInputValidator`** — la revisión rectifica sus propios hilos T12/T13 («lo íbamos a dejar al final como Consecutive…»), así que el ir y venir termina donde había empezado. **(b)** El `Id` del DTO gana su `[Description]` para OpenAPI, que era la única propiedad enlazada del contexto sin documentar. **(c)** `CacheDurationSeconds` pasa de `3 * 24 * 60 * 60` a **`259200`** literal. Sobre (c) queda una observación, no una objeción: junto con el hilo que quitó el comentario explicativo, el valor queda sin nada que diga que son 3 días — si algún día hay que ajustarlo, el número no se explica solo | — | §6.2 · §6.3 · §6.4 · §3 de `tasks` · `F4.2` · `F4.3` · `F4.4` | ninguna |
| 2026-09-04 | **`Consecutive…` vuelve a `Sequence…`.** Los hilos de la revisión sobre `IdInputDto` e `IdInputValidator` pedían literalmente `SequenceIdInputDto` y `SequenceIdInputValidator`; el desvío a `Consecutive…` —motivado por el `cau_consecutivoP` del legado— se revierte por indicación del tech lead. Quedan **`SequenceIdInputDto`** y **`SequenceIdInputValidator`**, con sus tres archivos renombrados por `git mv`. Es solo nomenclatura: ni el contrato HTTP ni el mensaje (`Id must be greater than 0.`) cambian | — | §6.2 · §6.3 · §6.4 · §3 de `tasks` · `F4.2` (nota de revisión) · `F4.4` | ninguna |
| 2026-09-04 | **La rama base se pone al día con `main`** (merge `5140cce`), 62 archivos y tres cambios que tocan este plan. **(a) La multitenencia pasa a ser requisito y desaparece EF InMemory del runtime** (`feat(startup)!`): `dotnet run` aborta sin resolver de tenants, clave de cifrado y Redis, así que el «modo de desarrollo» de §7.2 **ya no existe** y la tarea de contenedores deja de ser opcional. **(b) `ASPNETCORE_PATHBASE` se sustituye por un prefijo en proceso** (`RoutePrefix = /crm-service`, `GlobalRoutePrefixConvention`): las rutas de §6 y §7.4 se leen con `/crm-service` delante, sin tocar el controller. **(c) La caché pasa a ser opt-in** con `excludeDefaultPolicy: true`, que cierra `GAP-9`. De paso, **`main` ya trae `PagedPayload<T>` público**, el mismo arreglo que `GAP-8` hizo aquí: coinciden y el merge no da conflicto. Build limpio y **445/445** en la base, **490/490** al traerla a la rama de T11 | — | §7.2 · §7.4 · §6 (prefijo) · §9.2 `GAP-5`, `GAP-8`, `GAP-9` · `F4.2` (nota) · `F5.1` | ninguna en código — **sí operativa: nadie puede levantar el servicio en local hasta tener la pila** |
| 2026-09-04 | **`GAP-9` abierto: el `OutputCacheExtensions.cs` del fork está atrasado respecto de la plantilla.** La versión al día de `cache.md` documenta un `excludeDefaultPolicy: true` en la política base que este repositorio no tiene; sin él la caché **no es opt-in** y se almacena toda respuesta 200 que pase por el middleware, incluidos los health checks y `/info`, que siguen reportando estado viejo durante `DefaultTtlSeconds`. Falta además el `SetVaryByHeader([])` de la política `Global`. No afecta a `loss-reasons` —declara `[OutputCache]` explícito— pero sí al servicio. **Fuera del alcance de T11**, que por decisión de la revisión no toca ese archivo | — | §9.2 `GAP-9` | ninguna — necesita tarea propia |
| 2026-08-21 | **Revisión del PR de T5.** La propiedad de la entidad keyless pasa de `NegCauConsecutivo` a **`LossReasonId`** (nombres en inglés y sin abreviar; el nombre de columna legado se queda solo en la configuración EF), el Reader deja de nombrar la tabla en su comentario, y **se retira el `DbSet<DealLossReasonUsage>` de `ApplicationDbContext`** por no tener consumidor. Con eso **`F2.6` deja de tocar el archivo compartido** y el choque declarado entre T4 y T5 desaparece. Sin cambios de comportamiento | — | `F2.6` (lista de `Archivos:` y detalle) | ninguna |

---

## Anexo — Tareas

Las tareas viven en **`tasks_causas.md`**, su propio documento (`doc: tasks`), con la tabla maestra, los bloqueos previos, las tareas externas y los archivos compartidos.

No se duplican aquí a propósito: son dos granularidades distintas y mantenerlas en dos lugares las hace divergir. El **paso** (§8 de este plan) es la unidad de trabajo del agente ejecutor; la **tarea** es la unidad de revisión, PR y estimación. Este plan es el dueño de las decisiones y de los pasos; `tasks_causas.md` es el dueño del reparto en PRs.

## Criterio de cierre

El plan pasa a `approved` cuando:

- [x] Las diez secciones están escritas o justificadas.
- [x] Cada decisión de §2 tiene alternativas descartadas, consecuencias y `Afecta:`.
- [x] Toda decisión que afecte a la Fase 1 está en `estado: aprobada` → **las catorce lo están** (2026-08-14).
- [x] Todo campo de entrada de §6 tiene su fila en la tabla de validaciones.
- [x] `Shared` está auditado en §5.5.
- [x] Cada paso de §8 tiene `id`, `depende_de`, `Fuente`, `Hecho cuando` y `Verificar`.
- [x] El tech lead firmó §2 — resolución de `GAP-6`, 2026-08-14.

**El plan pasa a `approved`.** Los siete GAPs están resueltos (§9.2) y la Fase 0 puede arrancar; `F0.2` ya está `done` y verificado.

Queda un pendiente que **no** bloquea la ejecución pero sí el corte: **R9** — la autorización de las rutas que hoy son `[AllowAnonymous]` en Jack no la resuelve este plan (D13), y sin dueño el cutover deja el catálogo tan expuesto como está hoy.
