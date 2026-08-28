# Tareas de código

service: crm-service

context: Actividades (CRM nuevo de Jack)

doc: tareas de código

status: draft

source: [02-plan-gestioncomercial-actividades.md](02-plan-gestioncomercial-actividades%201.md)

updated: 2026-08-21

> Solo tareas de código, desde Fase 1 (F0.1/F0.2/GAP-P5 son investigación, no código — quedan fuera de este
> documento). Cada tarea trae su detalle completo para poder entregarse tal cual. La recomendación de
> agrupamiento va al final, separada de las tareas.

> **Nota de convenciones (wiki dev de `service-template-dotnet`).** La wiki se contradice en dos puntos de
> nomenclatura: los documentos con evidencia de código real e historial (`Puertos y Adaptadores.md`,
> `Patrón de Repositorio.md`, y `Reader, Provider y Repository.md` de `audits-service` — este último cita un
> `git log` real que deshizo la variante `...RepositoryPort`) usan una convención; los documentos tipo tutorial
> (`Casos de uso.md`, `Controllers.md`, `Contextos.md`) usan otra, de punta a punta. Las tareas de abajo
> resuelven la contradicción a favor de los primeros:
> - Repositorio del aggregate → `I{Contexto}Repository` en `Domain/Repositories/` (nunca `Port`, nunca
>   `Domain/Ports/`).
> - Interfaz de entrada de un caso de uso → `I{CasoDeUso}UseCase`, co-ubicada en
>   `Application/UseCases/{CasoDeUso}/` (nunca `I{Acción}{Contexto}Port` en `Application/Ports/`).
>
> Si el `main` real de `crm-service` ya sigue la otra convención, avisa y se ajustan las tareas 7, 8 y 10.

## Fase 1 — Dominio `Activities`

### Tarea 1 — Scaffold del contexto Activities

- **Objetivo:** crear `Activities.Domain` y `Activities.Application` siguiendo el layout del contexto de
  ejemplo `ServiceInfo`.
- **Depende de:** nada.
- **Fuente:** §5.1 del Plan · convenciones de `docs/plantilla/`.
- **Archivos:** `src/Contexts/Activities/**` (csproj + carpetas), referencias agregadas a la solución.
- **Hecho cuando:** la solución compila con los dos proyectos vacíos ya referenciados.
- **Verificar:** `dotnet build`.

### Tarea 2 — Value Objects, enums y errores de dominio

- **Objetivo:** los 7 Value Objects (`ActivityType`, `ActivityStatus`, `CallOutcome`, `MeetingOutcome`,
  `Description`, `Outcome`, `AdvisorId`) y el catálogo `ActivityErrors`.
- **Depende de:** Tarea 1 · requiere `DEC-13` aprobada.
- **Fuente:** §5.3, §5.4 del Plan · DEC-5, DEC-6, DEC-7, DEC-13, DEC-15 · Discovery §4.3.
- **Archivos:** `src/Contexts/Activities/Domain/{ValueObjects,Errors}/*.cs`.
- **Detalle:** `ActivityType`, `CallOutcome` y `MeetingOutcome` son **enums** — ningún char legado entra al
  dominio (DEC-15). Los enums de resultado van separados por tipo de actividad, con `DealClosed` como valor
  escribible en ambos (DEC-7). `VirtualMeeting` (tipo `'6'`) y `LegacyMeeting` (tipo `'3'`) quedan solo
  lectura — la primera por DEC-5, la segunda por paridad con el legado (nunca fue seleccionable ni en el
  formulario ni en la API, no es parte de DEC-5).
- **Hecho cuando:** cada VO rechaza entradas inválidas devolviendo `Result` (nunca una excepción), con sus
  tests en verde.
- **Verificar:** `dotnet test tests/UnitTests --filter Activities.Domain`.

### Tarea 3 — Aggregate Activity con invariantes

- **Objetivo:** el aggregate, con factorías que hacen imposibles los estados inválidos.
- **Depende de:** Tarea 2.
- **Fuente:** §5.2 del Plan · DEC-1 · Discovery Anexo B.1.
- **Archivos:** `src/Contexts/Activities/Domain/Aggregates/Activity.cs`.
- **Detalle:** dos factorías —
  `Schedule(DealId, ActivityType, Description, DueAt, AdvisorId, CreatedById, DateTime now)` y
  `RegisterCompleted(DealId, ActivityType, Outcome, OutcomeType?, AdvisorId, CreatedById, DateTime now)`.
  Invariantes: `DealId` obligatorio y > 0; `Type` escribible limitado a {Call, WhatsApp, Email, Note,
  Meeting}; `Scheduled` exige `Description`+`DueAt` y prohíbe `Outcome`/`OutcomeType`; `Note` no puede ser
  `Scheduled`; `Completed` exige `Outcome`, y `OutcomeType` solo si el tipo es Call o Meeting;
  `AdvisorId`/`CreatedById` no vacíos, ≤20.
- **Hecho cuando:** cada invariante tiene su test rojo→verde.
- **Verificar:** `dotnet test tests/UnitTests --filter Activities.Domain`.

## Fase 2 — Aplicación y persistencia

### Tarea 4 — Extender Shared: puerto `IClockPort` (PR propio)

- **Objetivo:** un reloj por institución con zona horaria y horario de verano, reutilizable por cualquier
  contexto del repo (no solo Actividades).
- **Depende de:** nada dentro de este contexto (requiere `DEC-12` aprobada; usa hallazgos de la investigación
  de zona horaria hecha en Fase 0, fuera de este documento).
- **Fuente:** DEC-12 · regla del template: extender `Shared` siempre va en PR propio, separado · Puertos y
  Adaptadores.md (todo `Port` compartido lleva el sufijo `Port` — `ILoggerPort`, `IUnitOfWorkPort`,
  `IRequestValidatorPort`; no existe un Port compartido sin ese sufijo).
- **Archivos:** `src/Shared/Application/Ports/IClockPort.cs`, su adaptador en
  `src/Infrastructure/Adapters/` (patrón `{Tecnología}ClockAdapter`, p. ej. `TimeZoneClockAdapter`), tests.
- **Detalle:** reemplaza los tres relojes distintos que usa hoy el legado (`DateTime.Now`,
  `Institucion.FechaHoraActual`, `FNZ_Q10_fecha_retornar` con offset fijo sin horario de verano). Se registra
  `Scoped` en el contenedor de DI (no `Singleton` como `ILoggerPort<T>`), porque resuelve la zona horaria del
  tenant activo en cada request.
- **Hecho cuando:** puerto + adaptador + tests en verde, con el PR mergeado de forma independiente.
- **Verificar:** `dotnet test tests/UnitTests --filter Clock`.

### Tarea 5 — Mapeo EF drift-safe de tbl_opo_negocios_actividades

- **Objetivo:** `ActivityConfiguration` con las 15 columnas universales explícitas (las que existen en las
  378 instituciones), value converters char↔enum, y la regla `(completada, anulada)` NULL⇒`Scheduled`.
- **Depende de:** Tarea 3 · requiere `DEC-16` aprobada.
- **Fuente:** §4 del Plan · DEC-3, DEC-6, DEC-15 · Discovery §4.1/§4.1-bis.
- **Archivos:** `src/Infrastructure/Persistence/EntityFramework/Activities/Configurations/ActivityConfiguration.cs`
  (el template siempre anida las configuraciones EF en un subdirectorio `Configurations/`, no sueltas junto al
  `DbContext`), `ApplicationDbContext` (agrega `DbSet<Activity> Activities => Set<Activity>();`).
- **Detalle:** los converters son el único lugar donde viven los chars legados — un char desconocido leído de
  BD debe rechazarse con un error explícito, nunca un `KeyNotFoundException` (corrige D20). Sin navigation
  properties ni relaciones configuradas — todo por ID (DEC-16). **Nunca** mapear `ConsecutivoActMiG`. Sin
  migrations de EF sobre la base legada — es solo mapeo, no se toca el esquema.
- **Hecho cuando:** el mismo mapeo materializa filas correctamente en **las dos** bases de prueba (16 y 15
  columnas — el drift real, no una hipótesis).
- **Verificar:** `dotnet test tests/IntegrationTests --filter Activities.Mapping`.

### Tarea 6 — Readers de Deal y Advisor

- **Objetivo:** `IDealReader` e `IAdvisorReader` con implementación EF sobre los read models de solo lectura.
- **Depende de:** Tarea 1 (no depende de la 2 ni de la 3).
- **Fuente:** §4.1, §5.5 del Plan · DEC-16, DEC-17 · Discovery Anexo B.1.
- **Archivos:** `src/Contexts/Activities/Application/Ports/{IDealReader,IAdvisorReader}.cs` (interfaz de
  Reader — **sin** sufijo `Port`, aunque viva en la carpeta `Ports/`, per "Reader, Provider y Repository.md"),
  `src/Infrastructure/Adapters/Persistence/Activities/{DealReader,AdvisorReader}.cs` (el adaptador EF de un
  Reader vive junto a los demás adaptadores de persistencia del contexto, no dentro de
  `Persistence/EntityFramework/`) + configuraciones mínimas read-only de `Deal`, `Opportunity` y `Person` en
  `Persistence/EntityFramework/Activities/Configurations/`.
- **Detalle:** `IDealReader.GetDealContextAsync(dealId)` → `{DealExists, OpportunityId, OpportunityArchived}`.
  `IAdvisorReader.ResolveByIdentificationAsync(identification)` → código de persona o no encontrado (puede
  devolver `Task<string?>` sin envolver en `Result`, igual que `IPersonNameReader.GetFullNameAsync` en la
  wiki — un Reader no encuentra nada no es una falla, es un resultado válido) — **la validación de rol NO va
  acá** (es responsabilidad del llamador, DEC-17). Mapeo mínimo, siempre `AsNoTracking()`, joins explícitos,
  sin navigation properties (DEC-16). Confirmar en este paso el nombre real de la columna de estado del
  negocio (para el filtro `deal-state-id` de la Tarea 8).
- **Hecho cuando:** un integration test resuelve un deal existente/archivado y un asesor existente/inexistente
  contra la BD de pruebas.
- **Verificar:** `dotnet test tests/IntegrationTests --filter Activities.Readers`.

### Tarea 7 — `IActivityRepository` + `ActivityRepositoryAdapter`

- **Objetivo:** el contrato de persistencia del aggregate y su implementación EF. `ActivityRepositoryAdapter`
  hereda `RepositoryBaseEF<Activity, int>` — que ya trae `GetByIdAsync`, `GetAllAsync`, `AddAsync`, `Update` y
  `Remove` — así que esta tarea solo agrega lo que el genérico no cubre: el override de `GetNotFoundError()`
  y un método propio `SearchAsync(ActivityFilter, PageQuery)` (patrón de listado filtrado de Casos de uso.md
  §5.5). No se reescribe `AddAsync` ni se construye un `GetPagedAsync` desde cero.
- **Depende de:** Tarea 5.
- **Fuente:** DEC-1, DEC-3, DEC-4 del Plan · Discovery §4.2 (semántica del SP que se reemplaza) · Patrón de
  Repositorio.md (`RepositoryBaseEF`) · Casos de uso.md §5.5 (`{Contexto}Filter` + `SearchAsync`).
- **Archivos:** `src/Contexts/Activities/Domain/Repositories/IActivityRepository.cs` (extiende
  `IRootRepository<Activity, int>` + `SearchAsync`), `src/Contexts/Activities/Domain/Filters/ActivityFilter.cs`
  (record `ActivityFilter(int? DealId, int? OpportunityId, string? DealStateId)`),
  `src/Infrastructure/Adapters/Persistence/Activities/ActivityRepositoryAdapter.cs` (el adaptador de
  repositorio vive en `Infrastructure/Adapters/Persistence/{Contexto}/`, no dentro de
  `Persistence/EntityFramework/{Contexto}/` — esa carpeta es solo para `Configurations/` y el `DbContext`).
- **Detalle:** `SearchAsync` replica el doble `INNER JOIN` del SP legado (Activity→Deal→Opportunity, más
  Person para el nombre del asesor) como joins explícitos de LINQ, no navigations (DEC-16). La derivación de
  `OpportunityId` a partir del deal (DEC-1) **no va en esta tarea**: es responsabilidad de la Tarea 8, que la
  resuelve vía `IDealReader` antes de construir el aggregate — el repositorio solo persiste el aggregate ya
  armado, vía el `AddAsync` heredado sin lógica propia.
- **Hecho cuando:** un integration test demuestra que (a) el `AddAsync` heredado persiste el aggregate y
  devuelve su id; (b) `SearchAsync` pagina y filtra igual que el SP legado (orden `negact_consecutivoP ASC`);
  (c) no se toca ninguna tabla ajena al dominio de Actividades.
- **Verificar:** `dotnet test tests/IntegrationTests --filter Activities.Repository`.

### Tarea 8 — Use cases `GetActivities` y `CreateActivity`

- **Objetivo:** los dos casos de uso completos, cada uno con su interfaz `I{CasoDeUso}UseCase` co-ubicada
  (ver nota de convenciones al inicio del documento — no `Application/Ports/`), DTOs, Mapping y validación de
  request.
- **Depende de:** Tarea 4, Tarea 6, Tarea 7 · requiere `DEC-12` aprobada.
- **Fuente:** §5.6, §6 del Plan · DEC-7, DEC-11, DEC-12 · Casos de uso.md §5.1 (Create) y §5.5 (GetAll con
  filtro).
- **Archivos:** `src/Contexts/Activities/Application/UseCases/GetActivities/` (`IGetActivitiesUseCase`,
  `GetActivitiesUseCase`, `GetActivitiesInputDto`, `GetActivitiesOutputDto`, `GetActivitiesMapping`) y
  `.../CreateActivity/` (`ICreateActivityUseCase`, `CreateActivityUseCase`, `CreateActivityInputDto`,
  `CreateActivityOutputDto`, `CreateActivityMapping`).
- **Detalle:** `GetActivitiesUseCase` — construye un `ActivityFilter` (Tarea 7) desde el input (al menos uno
  de `deal-id`/`opportunity-id`/`deal-state-id` obligatorio, validado en el `IStructuralValidator` de la
  Tarea 10, no aquí), llama `repository.SearchAsync(filter, page)`, paginación con tope 5000 (el del legado),
  NULL⇒`Scheduled` en la proyección; es de solo lectura — no inyecta `IUnitOfWorkPort`. `CreateActivityUseCase`
  — valida vía `IDealReader`/`IAdvisorReader`, construye el aggregate, `repository.AddAsync(...)` y luego,
  en un paso propio y explícito, `unitOfWork.CommitAsync(...)` (el commit nunca es implícito en `AddAsync` —
  ver Patrón de Repositorio.md), sin actualizar tablas de otros dominios (DEC-4/DEC-11) y sin validar rol del
  asesor (DEC-17, es del llamador).
- **Hecho cuando:** los tests unitarios (con NSubstitute sobre los puertos) cubren cada regla condicional de
  la tabla de validaciones §6.2 del Plan, el flujo feliz de §6.1, la aceptación de `deal-closed` como
  resultado válido (DEC-7), y que `CreateActivityUseCase` llama `unitOfWork.CommitAsync` tras un `AddAsync`
  exitoso.
- **Verificar:** `dotnet test tests/UnitTests --filter Activities.Application`.

### Tarea 9 — Integración multi-variante de esquema

- **Objetivo:** la misma suite de integración corriendo en verde contra `udbzq10trabajos` (16 columnas) y un
  tenant universitario (15 columnas, `varchar(MAX)`).
- **Depende de:** Tarea 8.
- **Fuente:** DEC-3 del Plan · Discovery §4.1-bis · riesgo R1 (se aceptó una muestra de 4 de 378 instituciones
  como evidencia suficiente — esta tarea es la prueba concreta de que la regla aguanta el drift real medido)
  · Testing.md (stack obligatorio: xUnit + Shouldly + Testcontainers.MsSql + Respawn; hereda
  `IntegrationTestBase`/`SqlServerContainerFixture`; nombres `Endpoint_Scenario_ExpectedOutcome` — nunca EF
  InMemory, que ignora constraints).
- **Hecho cuando:** la suite queda verde en ambas variantes; cualquier divergencia de comportamiento se
  documenta como riesgo nuevo en §9.1 del Plan, no se ignora.
- **Verificar:** `dotnet test tests/IntegrationTests --filter Activities` (con la matriz de conexiones de
  ambos tenants).

## Fase 3 — API, adaptador y corte

### Tarea 10 — `ActivitiesController` + mapeo de errores

- **Objetivo:** `GetAll` (`GET /activities`, recibe `IGetActivitiesUseCase` **por parámetro**, nunca por
  constructor) y `Create` (`POST /activities`, recibe `ICreateActivityUseCase` por parámetro) según §6 del
  Plan, autenticados sin excepción (DEC-9). Cada action lleva `[Tags("activities")]`, `[ValidateRequest]`,
  `[EndpointSummary]`/`[EndpointDescription]`, y un `[ProducesResponseType]` por cada código posible —
  `typeof(ApiSuccessResponse<T>)` en éxito, `typeof(ApiErrorResponse)` en error —, con la tabla de
  errores→HTTP (§6.x) cerrada contra los códigos reales del mapper de `Shared`.
- **Depende de:** Tarea 8 · requiere `DEC-10` y `DEC-13` aprobadas.
- **Fuente:** §6 del Plan · DEC-9, DEC-10, DEC-13 · Controllers.md · Contrato de respuesta API.md.
- **Archivos:** `src/Api/Controllers/ActivitiesController.cs`; `docs/servicio/activities.md` (procedencia
  legada + este plan).
- **Detalle:** el contrato de error del template es un **objeto singular**, no un arreglo:
  `{ "error": { "type": "...", "code": "HTTP.<TYPE>", "message": "...", "details": [...] }, "statusCode": 4xx }`.
  El §6.1 del Plan trae un ejemplo con la forma vieja (`{"errors":[{code,message}], ...}`), que no es la de
  este template — se corrige también ahí. Cada fila de la tabla §6.x debe mapear al `type`/`code` reales del
  contrato singular, no al formato viejo.
- **Hecho cuando:** el OpenAPI expone ambos endpoints; hay un test por cada fila de §6.x; ninguna celda de
  §6.x quedó en "confirmar"; el JSON de error de cada endpoint sigue exactamente `{error:{...}, statusCode}`.
- **Verificar:**
  `dotnet test tests/UnitTests --filter Api.Activities && curl -fs localhost:8080/openapi/v1.json | grep -q '"/activities"'`.

### Tarea 11 — Adaptador con feature flag en el monolito

- **Objetivo:** que `Areas/API/v1/GestionComercial/Controllers/ActividadesController.cs` de Jack delegue en
  `crm-service` (contrato en español intacto para el consumidor externo) cuando la institución tiene el
  feature flag activo, con reversa al camino legado si está apagado.
- **Depende de:** Tarea 10 (además, necesita saber quién es el consumidor real y cómo autentica el adaptador
  contra el servicio — investigación de Fase 0, fuera de este documento) · requiere `DEC-11` aprobada.
- **Fuente:** DEC-10, DEC-11, DEC-17 · §7.4 del Plan.
- **Archivos:** repo del monolito `jack` (rama propia); traductor español↔inglés según §3.1 + Anexo B.1.
- **Detalle:** el adaptador **conserva** la validación de rol del asesor que ya existe hoy en el legado
  (`:168-174`, DEC-17) antes de delegar, y **después** de un POST exitoso registra la auditoría con el
  mecanismo existente de Jack (DEC-11) — no actualiza `opo_fecha_ultimo_registro` (eso es paridad exacta con
  el API legado, D5, y no es responsabilidad de este servicio).
- **Hecho cuando:** con el flag apagado, el comportamiento es idéntico al actual; con el flag encendido, el
  POST crea la actividad vía el servicio **y** deja la fila de auditoría en Jack.
- **Verificar:** `suite de golden tests de la Tarea 12, corrida en ambos estados del flag`.

### Tarea 12 — Golden tests de paridad

- **Objetivo:** una suite que ejecuta las validaciones y flujos del Anexo B.1 del Discovery contra el camino
  legado y contra el adaptador+servicio, comparando los payloads normalizados.
- **Depende de:** Tarea 11, Tarea 9.
- **Fuente:** Anexo B.1 · DEC-10 · §6 del Plan.
- **Detalle:** cubrir tanto éxitos como errores, incluidos los códigos HTTP heredados (por ejemplo, el 404
  indistinguible cuando el asesor no existe vs. cuando existe pero no tiene el rol correcto — DEC-17).
- **Hecho cuando:** 0 divergencias funcionales, o cada divergencia encontrada queda documentada como
  deliberada, con la decisión (`DEC-n`) que la respalda.
- **Verificar:** ejecución de la suite completa, con el reporte adjunto a la tarea.

### Tarea 13 — Corte del consumidor real

- **Objetivo:** activar el feature flag para la institución del consumidor real, observar una ventana de
  tiempo acordada con telemetría real, y declarar cerrado el corte del frente API.
- **Depende de:** Tarea 12.
- **Fuente:** §1 del Plan (estrategia) · Discovery §8.0.
- **Detalle:** usar siempre `sum(itemCount)` en las consultas de comparación, nunca `count()` (muestreo
  10:1). No hay canario de dos semanas clásico — con 80 GET/30 días una sola institución produce ~0 datos en
  ese lapso; el control real es el flag reversible + la comparación offline contra la Tarea 12.
- **Hecho cuando:** el consumidor identificado opera sobre el servicio nuevo sin divergencias reportadas en
  la ventana observada, con el flag documentado como vía de reversa.
- **Verificar:** consulta KQL de comparación legado-vs-servicio, documentada con su resultado en §9.3 del
  Plan.

---

