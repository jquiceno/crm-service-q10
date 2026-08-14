---
service: crm-service
context: AdsChannel
doc: tareas
status: draft
source: 02-plan-ads-channels.md
updated: 2026-08-14
---

# Tareas — AdsChannel

10 tareas listas para crear como tickets en Jira, una por cada paso de `02-plan-ads-channels.md` §8. Cada una es autosuficiente: no hace falta abrir el plan para saber qué hacer. Al crearlas, copiá el título como Summary y el resto tal cual en la Description; completá **Depende de** como enlace "is blocked by" y **Épica/Componente** según la convención del tablero.

Épica/Componente sugerido para las 10: `AdsChannel`. Ninguna nace bloqueada (sin GAP bloqueante abierto — ver el cierre al final de este documento).

---

## F0.1 — Verificar documentación del template y el inventario de Shared

**Tipo:** Tarea técnica (spike/verificación) · **Depende de:** ninguna · **Épica:** AdsChannel

**Descripción:** Antes de escribir cualquier código del contexto `AdsChannel`, confirmar que el repositorio `crm-service` sigue coincidiendo con los supuestos sobre los que está armado el plan.

**Qué hacer:**
- Leer todos los archivos bajo `docs/plantilla/`.
- Confirmar que siguen existiendo, con las firmas que el plan asume, estos tipos: `AggregateRoot<TId>`, `IRootRepository<TAggregate, TId>`, `IUnitOfWorkPort`, `SqlServerErrorClassifier`, `ICacheStore`/`[OutputCache]`, `TenantMiddleware`, `IStructuralValidator<T>`.
- Si algo no coincide (falta un tipo, cambió una firma, etc.), reportarlo como `⚠️ GAP`/`DESVIACIÓN` y esperar instrucción antes de seguir con F1.1.

**Criterios de aceptación:**
- [ ] Se dejó constancia por escrito (comentario del PR o log) de que la plantilla y `Shared` siguen coincidiendo con el plan, **o**
- [ ] Se reportó una `DESVIACIÓN` puntual y se detuvo la ejecución.

**Verificación:** `Test-Path docs/plantilla/arquitectura.md` + revisión manual del resto del checklist de arriba.

**Archivos que toca:** ninguno (paso de solo lectura).

---

## F1.1 — Crear los contratos de valor de AdsChannel (Args y Filter)

**Tipo:** Tarea técnica · **Depende de:** F0.1 · **Épica:** AdsChannel

**Descripción:** Crear el proyecto de dominio del contexto y las formas de datos de entrada/consulta (sin reglas de negocio todavía).

**Qué hacer:**
- Crear `src/Contexts/AdsChannel/Domain/AdsChannel.Domain.csproj`, reflejando el SDK/target framework de `Shared.Domain.csproj` y referenciando `Shared.Domain` y `Shared.Results`.
- Registrar el proyecto en `Service.slnx`, bajo una carpeta nueva `/src/Contexts/AdsChannel/`.
- Crear `Domain/Aggregates/AdsChannelArgs.cs` con:
  ```
  public sealed record CreateAdsChannelArgs(string? Name, bool IsActive = true);
  public sealed record UpdateAdsChannelArgs(string Name, bool IsActive);
  ```
- Crear `Domain/Queries/AdsChannelFilter.cs` con:
  ```
  public sealed record AdsChannelFilter(string? NameContains, bool? IsActive);
  ```

**Criterios de aceptación:**
- [ ] El proyecto nuevo compila.
- [ ] Los tres tipos existen exactamente con esas firmas — no dependen de ningún otro tipo del contexto todavía.

**Verificación:** `dotnet build src/Contexts/AdsChannel/Domain/AdsChannel.Domain.csproj`

**Archivos que toca:** `AdsChannel.Domain.csproj`, `AdsChannelArgs.cs`, `AdsChannelFilter.cs`.

---

## F1.2 — Crear el agregado AdsChannel, sus errores y el contrato de repositorio

**Tipo:** Tarea técnica · **Depende de:** F1.1 · **Épica:** AdsChannel

**Descripción:** Modelar el comportamiento y las invariantes del contexto: el agregado, sus errores de dominio y el contrato de persistencia que el dominio le exige a la infraestructura.

**Qué hacer:**
- Crear `Domain/Errors/AdsChannelErrors.cs`:
  ```
  public static class AdsChannelErrors
  {
      public const string Context = "AdsChannel";
      public static DomainError NotFound(int id);
      public static readonly ValidationError NameRequired;   // Property = nameof(AdsChannelAggregate.Name)
      public static readonly ValidationError NameTooLong;    // Property = nameof(AdsChannelAggregate.Name), Attributes["maxLength"] = 100
      public static DomainError NameAlreadyExists(string name); // ErrorType.Conflict
  }
  ```
- Crear `Domain/Aggregates/AdsChannelAggregate.cs`:
  ```
  public sealed class AdsChannelAggregate : AggregateRoot<int>
  {
      public string Name     { get; private set; }
      public bool   IsActive { get; private set; }

      public static Result<AdsChannelAggregate> Create(CreateAdsChannelArgs input);
      public static AdsChannelAggregate Reconstruct(int id, string? name, bool? isActive);
      public Result Update(UpdateAdsChannelArgs input);
      protected override void Created();
  }
  ```
  **Importante:** `Create()` **y** `Update()` deben validar `Name` con las mismas dos reglas — requerido (`NameRequired` si viene vacío/en blanco) y longitud máxima 100 (`NameTooLong` si la excede) —, acumulando ambos errores si los dos fallan. No es solo responsabilidad de `Create()`.
- Crear `Domain/Repositories/IAdsChannelRepository.cs`:
  ```
  public interface IAdsChannelRepository : IRootRepository<AdsChannelAggregate, int>
  {
      Task<Result<bool>> ExistsByNameAsync(string name, int? excludingId = null, CancellationToken cancellationToken = default);
      Task<PagedResult<AdsChannelAggregate>> GetAsync(AdsChannelFilter filter, PageQuery page, CancellationToken cancellationToken = default);
      Task<Result<AdsChannelAggregate>> CreateAsync(AdsChannelAggregate aggregate, CancellationToken cancellationToken = default);
  }
  ```

**Criterios de aceptación:**
- [ ] El proyecto de dominio compila completo.
- [ ] `Create()` retorna error si `Name` es nulo/vacío/blanco (`NameRequired`) o supera 100 caracteres (`NameTooLong`).
- [ ] `Update()` valida exactamente lo mismo que `Create()`.
- [ ] No se creó un Value Object para `Name` (la regla es solo requerido + longitud, cubierta por el agregado y por FluentValidation más adelante — no se justifica un VO).

**Verificación:** `dotnet build src/Contexts/AdsChannel/Domain/AdsChannel.Domain.csproj`

**Archivos que toca:** `AdsChannelErrors.cs`, `AdsChannelAggregate.cs`, `IAdsChannelRepository.cs`.

---

## F2.1 — Crear la entidad de persistencia, la configuración de EF y el mapper

**Tipo:** Tarea técnica · **Depende de:** F1.2 · **Épica:** AdsChannel

**Descripción:** Mapear `AdsChannelAggregate` sobre la tabla legada `tbl_opo_medios_publicitarios`, **sin alterarla** — es Database First, la tabla la sigue escribiendo también el monolito legado.

**Qué hacer:**
- Crear `src/Infrastructure/Persistence/EntityFramework/AdsChannel/Entities/AdsChannel.cs`:
  ```
  public int    Id       { get; set; }
  public string? Name    { get; set; }
  public bool?  IsActive { get; set; }
  ```
  (nullable en `Name`/`IsActive` porque así es la columna real en la base — no se agrega `NOT NULL` en el esquema, solo en el dominio, ya resuelto en F1.2).
- Crear `Configurations/AdsChannelConfiguration.cs`: `ToTable("tbl_opo_medios_publicitarios")`, `HasKey(x => x.Id)`, `Property(x => x.Id).HasColumnName("medpub_consecutivoP").ValueGeneratedOnAdd()`, `Property(x => x.Name).HasColumnName("medpub_nombre").HasMaxLength(100).IsUnicode(false)`, `Property(x => x.IsActive).HasColumnName("medpub_estado")`.
- **No** declarar ninguna relación/navegación hacia `tbl_opo_oportunidades` — ese contexto no existe en este servicio y no se necesita para nada de este plan.
- Crear `Mappers/AdsChannelRepositoryMapper.cs`: `ToDomain(document)` llama a `AdsChannelAggregate.Reconstruct(document.Id, document.Name, document.IsActive)`; `ToDocument(aggregate)` escribe `Id`, `Name`, `IsActive`.
- Agregar `DbSet<AdsChannel> AdsChannels` a `src/Infrastructure/Persistence/EntityFramework/ApplicationDbContext.cs`.

**Criterios de aceptación:**
- [ ] `Infrastructure.csproj` compila.
- [ ] `DbSet<AdsChannel> AdsChannels` es consultable (`dotnet ef dbcontext info` no tira error).
- [ ] La tabla física `tbl_opo_medios_publicitarios` no se tocó (sin migraciones que la alteren).

**Verificación:** `dotnet build src/Infrastructure/Infrastructure.csproj`

**Archivos que toca:** `Entities/AdsChannel.cs`, `Configurations/AdsChannelConfiguration.cs`, `Mappers/AdsChannelRepositoryMapper.cs`, `ApplicationDbContext.cs` (editado).

---

## F2.2 — Implementar AdsChannelRepository

**Tipo:** Tarea técnica · **Depende de:** F2.1 · **Épica:** AdsChannel

**Descripción:** Implementar el repositorio completo — todos los métodos, no solo los que el primer caso de uso vaya a necesitar — porque no depende de ningún caso de uso para existir.

**Qué hacer:**
- Crear `src/Infrastructure/Persistence/EntityFramework/AdsChannel/AdsChannelRepository.cs` implementando `IAdsChannelRepository`:
  - `GetByIdAsync`/`ExistsAsync`/`GetAsync(filter, page)`: `AsNoTracking()`, ordenar por `Name` y desempatar por `Id`.
  - `ExistsByNameAsync(name, excludingId)`: equivalente a `SELECT 1 ... WHERE medpub_nombre = @name AND (@excludingId IS NULL OR medpub_consecutivoP <> @excludingId)`.
  - `CreateAsync`: `AddAsync` + `SaveChangesAsync` dentro del repositorio (para recuperar el `Id` generado); capturar `DbUpdateException`, revisar primero `SqlServerErrorClassifier.IsUniqueViolation` antes de clasificar genérico.
  - `Update(aggregate)`: marcar la entidad mapeada como `Modified` vía `context.Entry(...)`.
  - `RemoveAsync(id)`: cargar la entidad con tracking, `_set.Remove(entity)`, retornar `AdsChannelErrors.NotFound(id)` si no existe. **No** capturar acá la excepción de FK (eso sube desde `CommitAsync` en el caso de uso de borrado, F3.3).
  - Todo error que produzca esta clase lleva `Origin = nameof(AdsChannelRepository)`.
- Crear `src/Api/DependencyInjection/AdsChannelServiceExtensions.cs` con `AddAdsChannelServices()`, registrando `IAdsChannelRepository` (los casos de uso se agregan a este mismo archivo en F3.1–F3.5).
- Invocar `AddAdsChannelServices()` desde `Api/DependencyInjection/ApplicationServiceExtensions.cs`.

**Criterios de aceptación:**
- [ ] La solución completa compila.
- [ ] Los 8 miembros de `IAdsChannelRepository` (heredados + propios) están implementados de verdad — ninguno con `NotImplementedException`.
- [ ] `IAdsChannelRepository` queda registrado en el contenedor de DI.

**Verificación:** `dotnet build` (de toda la solución)

**Archivos que toca:** `AdsChannelRepository.cs`, `AdsChannelServiceExtensions.cs` (nuevo), `ApplicationServiceExtensions.cs` (editado).

---

## F3.1 — Vertical slice: CreateAdsChannel (`POST /ads-channels`)

**Tipo:** Historia técnica (vertical slice) · **Depende de:** F2.2 · **Épica:** AdsChannel

**Descripción:** Entregar el endpoint de creación completo y probado de punta a punta — caso de uso, validador, controller, DI y tests — en una sola tarea.

**Qué hacer:**
- Crear `src/Contexts/AdsChannel/Application/AdsChannel.Application.csproj` y, dentro de `Application/UseCases/CreateAdsChannel/`: `ICreateAdsChannelUseCase`, `CreateAdsChannelInputDto`, `CreateAdsChannelOutputDto`, `CreateAdsChannelMapping`, `CreateAdsChannelUseCase`.
- `ExecuteAsync`: `repository.ExistsByNameAsync(name)` → si existe, `AdsChannelErrors.NameAlreadyExists` sellado con `Context`/`Origin` → `input.ToAggregate()` (dispara la validación de `Create()`, ya sellada por el dominio) → `repository.CreateAsync(aggregate)` (sin `IUnitOfWorkPort`: ya confirma internamente) → `ToOutputDto()`.
- Crear `src/Infrastructure/Validation/FluentValidation/AdsChannel/CreateAdsChannelInputValidator.cs`, implementando `IStructuralValidator<T>`: `RuleFor(x => x.Name).NotEmpty().MaximumLength(100);`.
- Crear `src/Api/Controllers/AdsChannelsController.cs` (es la primera acción, el archivo no existe todavía): `[ApiController] [Route("[controller]")] [Tags("AdsChannels")]`, constructor `AdsChannelsController(ICreateAdsChannelUseCase createAdsChannelUseCase)`, acción `[HttpPost] [ValidateRequest] [OutputCacheInvalidate("ads-channels")]` que retorna `HttpCreatedResult<AdsChannelOutputDto>`.
- Editar `AdsChannelServiceExtensions.cs` para registrar `ICreateAdsChannelUseCase`.
- Tests:
  - `tests/UnitTests/Contexts/AdsChannel/Domain/AdsChannelAggregateTests.cs` (nuevo): crear válido setea `CreatedAt`/`UpdatedAt`; `Name` vacío/en blanco → `NameRequired`; `Name` > 100 caracteres → `NameTooLong`.
  - `tests/UnitTests/Contexts/AdsChannel/Application/CreateAdsChannelUseCaseTests.cs`: camino feliz, `NameAlreadyExists`, propagación de error del repositorio con su `Origin` original.
  - `tests/IntegrationTests/Contexts/AdsChannel/CreateAdsChannelEndpointTests.cs`: `POST /ads-channels` → 201; nombre duplicado → 409; `Name` vacío o > 100 caracteres → 400.

**Criterios de aceptación:**
- [ ] `POST /ads-channels` con `X-Entity-Code` válido y un `Name` nuevo responde `201` con el recurso creado.
- [ ] Repetir el mismo `Name` responde `409`.
- [ ] `Name` vacío o de más de 100 caracteres responde `400`.
- [ ] Todos los tests unitarios y de integración de esta tarea pasan.

**Verificación:** `dotnet test tests/UnitTests/UnitTests.csproj --filter FullyQualifiedName~AdsChannel && dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter FullyQualifiedName~CreateAdsChannel`

**Archivos que toca:** `AdsChannel.Application.csproj`, 5 archivos de `CreateAdsChannel/`, `CreateAdsChannelInputValidator.cs`, `AdsChannelsController.cs` (nuevo), `AdsChannelServiceExtensions.cs` (editado), `AdsChannelAggregateTests.cs` (nuevo), `CreateAdsChannelUseCaseTests.cs`, `CreateAdsChannelEndpointTests.cs`.

---

## F3.2 — Vertical slice: UpdateAdsChannel (`PUT /ads-channels/{id}`)

**Tipo:** Historia técnica (vertical slice) · **Depende de:** F3.1 · **Épica:** AdsChannel

**Descripción:** Entregar el endpoint de edición completo y probado, reutilizando el controller y la validación ya creados en F3.1.

**Qué hacer:**
- Dentro de `Application/UseCases/UpdateAdsChannel/`: `IUpdateAdsChannelUseCase`, `UpdateAdsChannelInputDto`, `UpdateAdsChannelOutputDto`, `UpdateAdsChannelMapping`, `UpdateAdsChannelUseCase`.
- `ExecuteAsync(int id, UpdateAdsChannelInputDto input, ...)`: `repository.GetByIdAsync(id)` (propaga `NotFound` tal cual) → `repository.ExistsByNameAsync(input.Name, excludingId: id)` (sella conflicto) → `aggregate.Update(input.ToUpdateArgs())` (misma validación requerido+longitud que `Create`; sella si falla) → `repository.Update(aggregate)` → `unitOfWork.CommitAsync()` → `ToOutputDto()`.
- Crear `UpdateAdsChannelInputValidator.cs` con las mismas reglas (`NotEmpty`, `MaximumLength(100)`).
- **Editar** `AdsChannelsController.cs`: agregar `IUpdateAdsChannelUseCase` al constructor y la acción `[HttpPut("{id}")] [ValidateRequest] [OutputCacheInvalidate("ads-channels")]`, retorna `HttpOkResult<AdsChannelOutputDto>`.
- Editar `AdsChannelServiceExtensions.cs` para registrar `IUpdateAdsChannelUseCase`.
- Tests:
  - Editar `AdsChannelAggregateTests.cs`: agregar casos de `Update` — válido setea `UpdatedAt` y no toca `CreatedAt`; `Name` vacío → `NameRequired`; `Name` > 100 → `NameTooLong`.
  - `UpdateAdsChannelUseCaseTests.cs`, `UpdateAdsChannelEndpointTests.cs`: `PUT /ads-channels/{id}` → 200; `{id}` inexistente → 404; nombre duplicado → 409; `Name` inválido → 400.

**Criterios de aceptación:**
- [ ] `PUT /ads-channels/{id}` con datos válidos responde `200` con el recurso actualizado.
- [ ] `{id}` inexistente responde `404`.
- [ ] Nombre duplicado responde `409`.
- [ ] `Name` inválido responde `400`.
- [ ] Todos los tests de esta tarea pasan.

**Verificación:** `dotnet test tests/UnitTests/UnitTests.csproj --filter FullyQualifiedName~AdsChannel && dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter FullyQualifiedName~UpdateAdsChannel`

**Archivos que toca:** 5 archivos de `UpdateAdsChannel/`, `UpdateAdsChannelInputValidator.cs`, `AdsChannelsController.cs` (editado), `AdsChannelServiceExtensions.cs` (editado), `AdsChannelAggregateTests.cs` (editado), `UpdateAdsChannelUseCaseTests.cs`, `UpdateAdsChannelEndpointTests.cs`.

---

## F3.3 — Vertical slice: DeleteAdsChannel (`DELETE /ads-channels/{id}`)

**Tipo:** Historia técnica (vertical slice) · **Depende de:** F3.2 · **Épica:** AdsChannel

**Descripción:** Entregar el endpoint de baja, sin validar contra `tbl_opo_oportunidades` (decisión D4 del plan: esa tabla queda fuera del dominio de este contexto).

**Qué hacer:**
- Dentro de `Application/UseCases/DeleteAdsChannel/`: `IDeleteAdsChannelUseCase`, `DeleteAdsChannelUseCase`. Sin DTOs, sin Mapping.
- `ExecuteAsync(int id, ct)`: `repository.RemoveAsync(id)` (retorna `NotFound` si ya no existe) → `unitOfWork.CommitAsync()`.
- **No** agregar ninguna consulta a `tbl_opo_oportunidades` para nombrar el conflicto — el `409` genérico que produce `SqlServerErrorClassifier` al chocar con la FK es la respuesta esperada (D4).
- **Editar** `AdsChannelsController.cs`: agregar `IDeleteAdsChannelUseCase` al constructor y la acción `[HttpDelete("{id}")] [OutputCacheInvalidate("ads-channels")]`, retorna `HttpNoContentResult`.
- Editar `AdsChannelServiceExtensions.cs`.
- Tests:
  - `DeleteAdsChannelUseCaseTests.cs`.
  - `DeleteAdsChannelEndpointTests.cs`: sembrar una fila de `tbl_opo_oportunidades` que referencie al `AdsChannel` a borrar (para el caso de conflicto); cubrir eliminar sin referencias → 204, `{id}` inexistente → 404, eliminar con referencias → 409.

**Criterios de aceptación:**
- [ ] `DELETE /ads-channels/{id}` sobre un registro sin referencias responde `204`.
- [ ] `{id}` inexistente responde `404`.
- [ ] `DELETE` sobre un registro referenciado por una Oportunidad responde `409` (sin nombrar la Oportunidad — es el comportamiento esperado, D4).
- [ ] Todos los tests de esta tarea pasan.

**Verificación:** `dotnet test tests/UnitTests/UnitTests.csproj --filter FullyQualifiedName~AdsChannel && dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter FullyQualifiedName~DeleteAdsChannel`

**Archivos que toca:** 2 archivos de `DeleteAdsChannel/`, `AdsChannelsController.cs` (editado), `AdsChannelServiceExtensions.cs` (editado), `DeleteAdsChannelUseCaseTests.cs`, `DeleteAdsChannelEndpointTests.cs`.

---

## F3.4 — Vertical slice: GetAdsChannelById (`GET /ads-channels/{id}`)

**Tipo:** Historia técnica (vertical slice) · **Depende de:** F3.3 · **Épica:** AdsChannel

**Descripción:** Entregar la lectura de un solo elemento, con caché de salida.

**Qué hacer:**
- Dentro de `Application/UseCases/GetAdsChannelById/`: `IGetAdsChannelByIdUseCase`, `GetAdsChannelByIdOutputDto`, `GetAdsChannelByIdMapping`, `GetAdsChannelByIdUseCase`.
- `ExecuteAsync(int id, ct)`: `repository.GetByIdAsync(id)` → `ToOutputDto()`. Lectura pura, sin necesidad de constante `Origin`.
- **Editar** `AdsChannelsController.cs`: agregar `IGetAdsChannelByIdUseCase` al constructor y la acción `[HttpGet("{id}")] [OutputCache(Duration = 120, Tags = ["ads-channels"], VaryByRouteValueNames = ["id"])]`, retorna `HttpOkResult<AdsChannelOutputDto>`.
- Editar `AdsChannelServiceExtensions.cs`.
- Tests:
  - `GetAdsChannelByIdUseCaseTests.cs`.
  - `GetAdsChannelByIdEndpointTests.cs`: `{id}` existente → 200; `{id}` inexistente → 404; segunda llamada al mismo `{id}` sirve desde caché (no vuelve a tocar la base — patrón de test de `cache.md`).

**Criterios de aceptación:**
- [ ] `GET /ads-channels/{id}` de un registro existente responde `200` con sus datos.
- [ ] `{id}` inexistente responde `404`.
- [ ] Una segunda llamada al mismo `{id}` dentro del TTL de caché no vuelve a consultar la base de datos.
- [ ] Todos los tests de esta tarea pasan.

**Verificación:** `dotnet test tests/UnitTests/UnitTests.csproj --filter FullyQualifiedName~AdsChannel && dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter FullyQualifiedName~GetAdsChannelById`

**Archivos que toca:** 4 archivos de `GetAdsChannelById/`, `AdsChannelsController.cs` (editado), `AdsChannelServiceExtensions.cs` (editado), `GetAdsChannelByIdUseCaseTests.cs`, `GetAdsChannelByIdEndpointTests.cs`.

---

## F3.5 — Vertical slice: GetAdsChannels — listado (`GET /ads-channels`)

**Tipo:** Historia técnica (vertical slice) · **Depende de:** F3.4 · **Épica:** AdsChannel

**Descripción:** Entregar el listado unificado, filtrado y paginado (reemplaza a las dos stored procedures legadas de listado en una sola). Es la última tarea del plan — con ella el contexto `AdsChannel` queda completo.

**Qué hacer:**
- Dentro de `Application/UseCases/GetAdsChannels/`: `IGetAdsChannelsUseCase`, `GetAdsChannelsInputDto`, `AdsChannelOutputDto`, `GetAdsChannelsMapping`, `GetAdsChannelsUseCase`.
- `ExecuteAsync`: construir `new AdsChannelFilter(input.NameContains, input.IsActive)`, llamar `repository.GetAsync(filter, page)`, mapear cada item, retornar `PagedResult<AdsChannelOutputDto>` vía `.Success(items, totalCount)` / `.Failure(error)`.
- **Editar** `AdsChannelsController.cs`: agregar `IGetAdsChannelsUseCase` al constructor y la acción `[HttpGet] [ValidateRequest] [OutputCache(Duration = 60, Tags = ["ads-channels"])]`, retorna `HttpOkPagedResult<AdsChannelOutputDto>`.
- Editar `AdsChannelServiceExtensions.cs`.
- Tests:
  - `GetAdsChannelsUseCaseTests.cs`.
  - `GetAdsChannelsEndpointTests.cs`: paginación, filtro `nameContains`, filtro `isActive`, y el **round-trip de caché cruzado con Create**: `GET` (miss) → `POST` (invalida el tag `ads-channels`) → `GET` (refleja el nuevo registro) — este caso solo es posible ahora que Create (F3.1) y List existen juntos.

**Criterios de aceptación:**
- [ ] `GET /ads-channels?pageIndex=0&pageSize=20` responde `200` con `items`/`totalCount`.
- [ ] `nameContains` e `isActive` filtran correctamente, combinados y por separado.
- [ ] Crear un `AdsChannel` nuevo y volver a listar lo refleja (la invalidación de caché funciona).
- [ ] Todos los tests de esta tarea pasan.

**Verificación:** `dotnet test tests/UnitTests/UnitTests.csproj --filter FullyQualifiedName~AdsChannel && dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter FullyQualifiedName~AdsChannel`

**Archivos que toca:** 5 archivos de `GetAdsChannels/`, `AdsChannelsController.cs` (editado), `AdsChannelServiceExtensions.cs` (editado), `GetAdsChannelsUseCaseTests.cs`, `GetAdsChannelsEndpointTests.cs`.

---

## Cierre

Ningún GAP bloqueante queda abierto para estas 10 tareas. El único GAP abierto del plan (**GAP-1**, sin índice único en `medpub_nombre`) es un riesgo aceptado, no bloqueante — afecta F3.1, F3.2 y F2.2, pero no impide crear ni ejecutar ninguna de las tareas de arriba (ver `02-plan-ads-channels.md` §9).
