---
service: crm-service
context: AdsChannel
doc: plan
status: draft
source: 01-discovery-medios-publicitarios.md
updated: 2026-08-14
---

## 0. Cómo ejecutar este plan

Este plan lo ejecuta un agente de IA con supervisión humana mínima. Seguí estas cinco instrucciones al pie de la letra:

1. **Antes de ejecutar nada, verificá el plan.** Recorré todos los pasos y confirmá que cada uno tiene `id`, un `depende_de` que existe, `estado`, `Fuente:`, `Hecho cuando:` y `Verificar:`. Confirmá que ninguna decisión de §2 que afecte tu fase está en `estado: propuesta`, y que no queda ningún GAP `BLOQUEANTE` abierto para la fase que vas a iniciar. Si falta algo, **detenete y reportalo**: no ejecutes un plan incompleto ni completes vos lo que falte.
2. Ejecutá los pasos en orden de `id`, respetando `depende_de`. No inicies pasos con `estado: blocked`.
3. Al terminar un paso, corré su comando de `Verificar`, y solo entonces cambiá `estado: pending` → `done` en este mismo archivo.
4. **Si la realidad del repositorio contradice el plan** (el archivo ya existe, la interfaz tiene otra firma, la tabla tiene otras columnas): detenete, no improvises. Reportá con el formato `⚠️ GAP` y esperá instrucción.
5. No agregues alcance. Si detectás una mejora, anotala como riesgo en §9; no la implementes.

## 1. Contexto y alcance

**Qué se construye:** un bounded context nuevo, `AdsChannel`, dentro de `crm-service`, que expone el CRUD que hoy vive en el catálogo legado `MediosPublicitarios` (`tbl_opo_medios_publicitarios`) — listar (paginado, filtrable), obtener por id, crear, editar, eliminar — como API REST siguiendo las convenciones de `service-template-dotnet`.

**Qué queda fuera de alcance, y por qué:**

- **Convivencia con Oportunidad (monolito legado) y cualquier mecanismo de corte/feature-flag.** Confirmado con el equipo [equipo, 2026-08-14]: no se considera por el momento, no tiene injerencia en lo que se va a construir ahora. `tbl_opo_medios_publicitarios` sigue siendo escrita por las stored procedures legadas en paralelo; este plan no toca el esquema de esa tabla ni intenta redirigir el tráfico del legado. Si se retoma, es un plan aparte.
- **El dominio homónimo "Formarte"** (`tbl_per_medios_publicitarios`) — fuera de alcance según Discovery §9, exclusión permanente.
- **El hardcodeo de API keys en `ApiKeyAuthenticationHandler.cs`** (Discovery D2/D6) — deuda de seguridad transversal del legado, no específica de este contexto.
- **Autenticación propia del servicio** — ningún contexto de esta plantilla implementa `[Authorize]`/JWT; el servicio confía en el Gateway aguas arriba (Discovery GAP-2, confirmado: existe Gateway en producción). No se diseña acá — ver D5.

**Ajustes de alcance posteriores al Discovery:** `NINGUNO, RIGE EL DISCOVERY`, salvo el idioma de este documento (cuerpo explicativo en español, artefactos técnicos en inglés) y el nombre técnico del contexto (ver D1), ambos por instrucción explícita del desarrollador (2026-08-14).

## 2. Decisiones cerradas (ADR)

### D1 — Nombre técnico en inglés del bounded context
`estado: aprobada · firmó: Brayan Gamboa · fecha: 2026-08-14 · origen: [template — arquitectura.md, contextos.md] + [desarrollador, 2026-08-14]`
- Decisión: el bounded context se llama `AdsChannel` (singular, coincide con `AdsChannelAggregate`) en toda carpeta, namespace, clase y ruta de dominio/aplicación; el controller y la ruta HTTP usan el plural `AdsChannels`/`ads-channels`, igual que `Product`/`ProductsController`. El identificador `MediosPublicitarios` del Discovery se conserva únicamente como nombre de archivo/front-matter del propio documento de Discovery, para trazabilidad (§3).
- Alternativas descartadas: `AdvertisingMedium` — nombre aprobado en una iteración anterior de este plan, reemplazado por instrucción explícita del desarrollador (2026-08-14); mantener `MediosPublicitarios` sin traducir en el código — viola la regla de nombrar todo artefacto técnico en inglés.
- Consecuencias: `Contexts/AdsChannel/`, `AdsChannelAggregate`, `IAdsChannelRepository`, `AdsChannelsController`, ruta `/ads-channels` (kebab-case automático vía `KebabCaseParameterTransformer`).
- Afecta: §3, §4, §5, §6, todo §8.

### D2 — Unificar los dos listados legados en uno solo
`estado: aprobada · firmó: Brayan Gamboa · fecha: 2026-08-14 · origen: [Discovery §4, §5]`
- Decisión: un único `GetAdsChannelsUseCase` reemplaza a las dos stored procedures legadas (`pa_opo_medios_publicitarios_retornar` para la pantalla admin, `pa_apis_opo_medios_publicitarios_retornar` para la API pública), exponiendo filtros `nameContains` e `isActive` más paginación estándar.
- Alternativas descartadas: mantener dos endpoints separados replicando la división del legado — replica un accidente de implementación (dos SPs escritas en momentos distintos) sin justificación de negocio; el filtro de texto del SP admin es estrictamente más capaz que el de la API, así que unificar no pierde nada.
- Consecuencias: un solo endpoint `GET /ads-channels` sirve tanto a la futura UI admin como a cualquier consumidor externo de la API.
- Afecta: §4, §6, F3.5.

### D3 — El `NOT NULL` es una regla de dominio, no un cambio de esquema
`estado: aprobada · firmó: Brayan Gamboa · fecha: 2026-08-14 · origen: [Discovery GAP-9] + [template — repositorio.md §"El agregado no es la entidad de EF Core"]`
- Decisión: `Name` e `IsActive` son obligatorios para `AdsChannelAggregate.Create()` **y** para `AdsChannelAggregate.Update()` — ambos factories/métodos de mutación validan lo mismo (ver §5) —, pero la entidad de persistencia de EF Core los mantiene nullable, reflejando el esquema real y sin alterar de `tbl_opo_medios_publicitarios`.
- Alternativas descartadas: `ALTER TABLE tbl_opo_medios_publicitarios ... NOT NULL` — la tabla la sigue escribiendo el monolito legado (la convivencia queda explícitamente fuera de alcance, ver §1); alterar un esquema que otro sistema sigue escribiendo sin su conocimiento rompe la estabilidad de contratos (regla 8) y requiere un sign-off de DBA/equipo legado que este plan no tiene.
- Consecuencias: esto **reinterpreta** la redacción literal del GAP-9 del Discovery ("agregar `NOT NULL` real en la tabla nueva") — no hay tabla nueva, esto es Database First sobre la legada. El nuevo servicio hace cumplir la regla en cada escritura propia (tanto alta como edición); las filas que el monolito legado haya escrito con `NULL` (si existen) se toleran en lectura vía `Reconstruct()`.
- Afecta: §4, §5, F1.2, F2.1, F3.1, F3.2.

### D4 — Sin lectura cruzada a `tbl_opo_oportunidades` al eliminar
`estado: aprobada · firmó: Brayan Gamboa · fecha: 2026-08-14 · origen: [Discovery §4 D2/D3] + [template — repositorio.md §"El 547 no dice qué constraint falló"]`
- Decisión: `DeleteAdsChannelUseCase` no consulta `tbl_opo_oportunidades` para nombrar el registro en conflicto. `AdsChannelRepository.RemoveAsync` deja que la violación de FK (`SqlException` 547) suba a través de `IUnitOfWorkPort.CommitAsync` → `SqlServerErrorClassifier.Classify`, que ya la traduce a un `409 Conflict` genérico.
- Alternativas descartadas: un reader sobre `tbl_opo_oportunidades` para responder "no se puede eliminar: N oportunidades referencian este canal" — `tbl_opo_oportunidades` está fuera del dominio de este contexto (su propio contexto todavía no existe en este servicio); leerla acá sería exactamente el tipo de acceso a tabla fuera del dominio que el template exige marcar como GAP, y el mensaje de error más específico no justifica reabrir ese alcance. El `Conflict` genérico que ya produce el template es una respuesta correcta, aunque menos específica.
- Consecuencias: `DELETE /ads-channels/{id}` sigue el patrón simple "cargar marcador → RemoveAsync → CommitAsync" sin Reader/Provider adicional.
- Afecta: §5, §6, F3.3, F2.2.

### D5 — Sin autenticación propia del servicio; se confía en el Gateway
`estado: aprobada · firmó: Brayan Gamboa · fecha: 2026-08-14 · origen: [Discovery GAP-2, confirmado] + [template — Program.cs, ningún contexto de la plantilla implementa Authorize/JWT]`
- Decisión: `AdsChannelsController` no agrega atributo `[Authorize]` ni middleware de autenticación nuevo. La resolución de tenant (`TenantMiddleware`, `X-Entity-Code`) es el único control de identidad por request que el servicio realiza, igual que en cualquier otro endpoint ya existente del servicio.
- Alternativas descartadas: agregar JWT/`[Authorize]` solo a este controller — no hay precedente en la plantilla, sería alcance no pedido por este plan, y dejaría a cualquier otro endpoint futuro del servicio sin autenticación a nivel de aplicación, lo cual sería inconsistente.
- Consecuencias: la protección real del servicio contra acceso anónimo queda enteramente delegada al Gateway confirmado en producción. Es una dependencia operativa, no una falla de diseño de este plan — se marca de nuevo en §7 y §9.
- Afecta: §6, §7.

### D6 — Caché de salida en lecturas, invalidación por tag en escrituras
`estado: aprobada · firmó: Brayan Gamboa · fecha: 2026-08-14 · origen: [template — cache.md] + [Discovery §8]`
- Decisión: `GET /ads-channels` y `GET /ads-channels/{id}` llevan `[OutputCache]` con tag `ads-channels`; `POST`, `PUT` y `DELETE` llevan `[OutputCacheInvalidate("ads-channels")]`. No se usa caché L2 (`ICacheStore`).
- Alternativas descartadas: no cachear nada, replicando la ausencia de caché del legado — el legado simplemente nunca tuvo disponible el caché L1 de esta plantilla; no usarlo acá sería desaprovechar un mecanismo que la plantilla ya da gratis para exactamente este tipo de endpoint (catálogo chico, de lectura frecuente). Caché L2 — el catálogo es lo bastante chico como para que una consulta directa ya sea barata; agregar L2 sería una abstracción no pedida (regla 3).
- Consecuencias: una escritura a través de este servicio invalida el caché; una escritura desde las stored procedures del monolito legado (todavía activas, ver §1) **no** lo hace — el caché puede servir datos obsoletos hasta por `Duration` segundos después de una escritura del lado legado. Documentado como riesgo en §9, no como bloqueante, ya que la convivencia queda explícitamente fuera de alcance.
- Afecta: §6, §7, F3.1, F3.4, F3.5.

### D7 — Orden de construcción: persistencia antes que los casos de uso
`estado: aprobada · firmó: Brayan Gamboa · fecha: 2026-08-14 · origen: [desarrollador, 2026-08-14]`
- Decisión: la entidad de persistencia, la configuración de EF Core, el mapper y la implementación completa de `AdsChannelRepository` (fase F2) se construyen **antes** que los cinco casos de uso (fase F3), en vez de después como en la primera versión de este plan.
- Alternativas descartadas: dividir la implementación del repositorio en fragmentos por caso de uso, dejando el resto de sus métodos con `throw new NotImplementedException()` hasta que le toque a cada caso de uso — descartado porque `AdsChannelRepository` no depende de ningún caso de uso para compilar ni para funcionar (solo depende del dominio, fase F1), así que fragmentarlo artificialmente no aporta nada y solo agrega código muerto temporal.
- Consecuencias: cada caso de uso (F3.1–F3.5) se escribe ya contra una implementación real y completa del repositorio, no solo contra su interfaz — se puede probar de punta a punta apenas se termina cada caso de uso.
- Afecta: §8 completo (orden de F2 y F3).

### D8 — Cada caso de uso es un vertical slice completo hasta el endpoint HTTP, probado en su propia tarea
`estado: aprobada · firmó: Brayan Gamboa · fecha: 2026-08-14 · origen: [desarrollador, 2026-08-14]`
- Decisión: cada uno de los cinco pasos de F3 (Create, Update, Delete, GetById, List) agrega, en la misma tarea, el caso de uso, su validador (si aplica), la acción correspondiente en `AdsChannelsController` (creando el archivo en F3.1 y extendiéndolo en F3.2–F3.5), el registro en DI, su test unitario y su test de integración contra ese endpoint puntual — no se difiere la exposición HTTP ni las pruebas a una fase final.
- Alternativas descartadas: separar validadores (antigua F4.1) y controller (antigua F4.2) en una fase aparte después de los cinco casos de uso — impedía probar cada endpoint de forma aislada apenas se terminaba su caso de uso, que es exactamente lo que se busca evitar; una fase final de tests (antigua F5) desacoplada de cada caso de uso — mismo problema, además de alejar el test del código que prueba en el tiempo.
- Consecuencias: F3.2–F3.5 pasan a depender secuencialmente del paso anterior (comparten el mismo archivo de controller y el mismo archivo de registro DI, así que editarlos fuera de orden generaría conflictos); no queda ninguna fase F4/F5 separada — el plan termina en F3.5. Dominio (F1) e Infraestructura de persistencia (F2) siguen siendo tareas separadas entre sí y respecto de F3, así que la regla de "nunca mezclar dominio, infraestructura y API en el mismo PR" se sigue cumpliendo: F3 solo mezcla Aplicación + API (y sus tests), que la regla no prohíbe mezclar.
- Afecta: §8 completo (F3.1–F3.5 reemplazan a las antiguas F3, F4 y F5).

## 3. Glosario y trazabilidad

| Término de negocio (ES) | Nombre técnico (EN) | Referencia en Discovery |
|---|---|---|
| Medio publicitario | `AdsChannel` (agregado), `AdsChannelAggregate` (clase) | Discovery §2 |
| Nombre del medio | `Name` | Discovery §4, `medpub_nombre` |
| Estado (activo/inactivo) | `IsActive` | Discovery §4, `medpub_estado` |
| Oportunidad | *(no se modela en este servicio — fuera de alcance, ver §1)* | Discovery §2, §9 |
| Institución / tenant | resuelto por `TenantMiddleware` / `X-Entity-Code`, no es un campo de dominio | Discovery §3, §6 (`aplent_codigoP`) |

| Sección del Discovery | Sección del plan |
|---|---|
| §3 Estado actual (arquitectura, rutas) | §4, §7 |
| §4 Modelo de datos y SPs | §4 |
| §5 Consumidores | §7 ("qué reemplaza cada ruta legada") |
| §6 Parámetros y personalizaciones | §1 (alcance), §7 |
| §7 Defectos con veredicto | §2 (D3, D4), §4 |
| §9 Alcance | §1 |
| §10 GAPs | §9 |

## 4. Mapeo legado → modelo

| Legado (Discovery §4) | Propiedad de dominio | Tipo | Persistencia | Trampa |
|---|---|---|---|---|
| `medpub_consecutivoP` int PK identity `[verificado en BD]` | `AdsChannelAggregate.Id` | `int` | `Id`, `ValueGeneratedOnAdd`, se puebla después del `INSERT` vía `CreateAsync` | El valor de identity solo se conoce después del commit — ver F2.2 |
| `medpub_nombre` varchar(100) nullable `[verificado en BD]` | `Name` | `string` (requerido, validado en `Create` y en `Update` — D3) | `Name` nullable `string?`, `HasMaxLength(100)`, `IsUnicode(false)` | D3 — requerido en dominio, nullable en el esquema |
| `medpub_estado` bit nullable `[verificado en BD]` | `IsActive` | `bool` | `IsActive` nullable `bool?`, se mapea a `true` en `Reconstruct` si viene `null` | D3 — mismo caso |
| `aplent_codigoP` (parámetro de SP, sin columna) `[verificado en BD]` | no se modela | — | no se modela | Se replica la ausencia: la tenencia es DB-por-tenant vía `TenantMiddleware`, no una columna de fila |
| `FK_tbl_opo_medios_publicitarios_tbl_opo_oportunidades` `[verificado en BD]` | no se modela (Oportunidad queda fuera de este contexto) | — | el delete sale como `409 Conflict` genérico vía `SqlServerErrorClassifier` | D4 |
| `medpub_abreviatura` (referenciada en código legado, ausente del esquema) `[verificado en BD]` | no se modela | — | no se modela | Discovery D8/GAP-7, diferido — no se reproduce |

## 5. Dominio

**Agregado:** `AdsChannelAggregate : AggregateRoot<int>`

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

**Regla de validación aplicada dos veces, por diseño (D3):** tanto `Create()` como `Update()` deben validar `Name` con las mismas dos reglas — requerido (`AdsChannelErrors.NameRequired` si viene vacío o en blanco) y longitud máxima 100 (`AdsChannelErrors.NameTooLong` si excede) —, acumulando ambos errores si los dos fallan (no cortar en el primero). `Create()` los retorna a través de `DomainError.FromValidationDomainErrors(errors)`; `Update()` retorna el primero que falle vía `Result` (no `Result<T>`, porque no produce un valor nuevo). Esta misma validación la dispara también cada caso de uso al invocar `input.ToAggregate()` (crear, F3.1) o `aggregate.Update(input.ToUpdateArgs())` (editar, F3.2) — el caso de uso no repite la regla, pero es el punto donde el error del dominio se sella con `Context`/`Origin` antes de llegar al controller (ver casos-de-uso.md §7).

- No se crea un Value Object para `Name`: sus únicas reglas son "requerido" y "máximo 100 caracteres", que `AdsChannelErrors` + `FluentValidation` ya cubren — crear un VO solo para eso está explícitamente desaconsejado por `validaciones.md`.
- Sin enums, sin entidades hijas: la tabla legada tiene 3 columnas y ninguna jerarquía.

**Args (`Domain/Aggregates/AdsChannelArgs.cs`):**

```
public sealed record CreateAdsChannelArgs(string? Name, bool IsActive = true);
public sealed record UpdateAdsChannelArgs(string Name, bool IsActive);
```

**Errores (`Domain/Errors/AdsChannelErrors.cs`):**

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

**Filtro (`Domain/Queries/AdsChannelFilter.cs`):** `public sealed record AdsChannelFilter(string? NameContains, bool? IsActive);`

**Contrato de repositorio (`Domain/Repositories/IAdsChannelRepository.cs`):**

```
public interface IAdsChannelRepository : IRootRepository<AdsChannelAggregate, int>
{
    Task<Result<bool>> ExistsByNameAsync(string name, int? excludingId = null, CancellationToken cancellationToken = default);
    Task<PagedResult<AdsChannelAggregate>> GetAsync(AdsChannelFilter filter, PageQuery page, CancellationToken cancellationToken = default);
    Task<Result<AdsChannelAggregate>> CreateAsync(AdsChannelAggregate aggregate, CancellationToken cancellationToken = default);
}
```

`excludingId` permite que `UpdateAdsChannelUseCase` valide unicidad sin que el propio agregado choque contra sí mismo.

## 6. Contratos de API

Ruta base: `/ads-channels` (derivada automáticamente del nombre del controller `AdsChannelsController` por `KebabCaseParameterTransformer`).

| Endpoint | Método | Caso de uso | Éxito | Caché |
|---|---|---|---|---|
| `/ads-channels` | GET | `GetAdsChannelsUseCase` | 200, `PagedPayload<GetAdsChannelsOutputDto>` | `[OutputCache(Duration=60, Tags=["ads-channels"])]` |
| `/ads-channels/{id}` | GET | `GetAdsChannelByIdUseCase` | 200, `GetAdsChannelByIdOutputDto` | `[OutputCache(Duration=120, Tags=["ads-channels"], VaryByRouteValueNames=["id"])]` |
| `/ads-channels` | POST | `CreateAdsChannelUseCase` | 201, `CreateAdsChannelOutputDto` | `[OutputCacheInvalidate("ads-channels")]` |
| `/ads-channels/{id}` | PUT | `UpdateAdsChannelUseCase` | 200, `UpdateAdsChannelOutputDto` | `[OutputCacheInvalidate("ads-channels")]` |
| `/ads-channels/{id}` | DELETE | `DeleteAdsChannelUseCase` | 204, sin cuerpo | `[OutputCacheInvalidate("ads-channels")]` |

**Tabla de validaciones (todos los campos de entrada, sin excepción):**

| DTO | Campo | Tipo | Regla estructural (FluentValidation) | Regla de dominio |
|---|---|---|---|---|
| `GetAdsChannelsInputDto` | `NameContains` | `string?` | ninguna | ninguna |
| `GetAdsChannelsInputDto` | `IsActive` | `bool?` | ninguna | ninguna |
| `PageQueryInputDto` | `PageIndex` | `int` | `>= 0` (validador compartido) | — |
| `PageQueryInputDto` | `PageSize` | `int` | `1..100` (validador compartido) | — |
| `CreateAdsChannelInputDto` | `Name` | `string?` | `NotEmpty`, `MaximumLength(100)` | `AdsChannelErrors.NameRequired` / `NameTooLong` en `Aggregate.Create` (defensa en profundidad, ver §5) |
| `CreateAdsChannelInputDto` | `IsActive` | `bool` | ninguna (default `true`) | ninguna |
| `UpdateAdsChannelInputDto` | `Name` | `string?` | `NotEmpty`, `MaximumLength(100)` | `AdsChannelErrors.NameRequired` / `NameTooLong` en `Aggregate.Update` (misma regla que crear, ver §5) |
| `UpdateAdsChannelInputDto` | `IsActive` | `bool` | ninguna | ninguna |
| Parámetro de ruta `{id}` (get/update/delete) | `Id` | `int` | model binding de ASP.NET Core (400 si no es entero) | `AdsChannelErrors.NotFound(id)` si no existe |

**Mapeo de error de dominio → HTTP** (ya lo resuelve el `ErrorHttpMapper` del template — no hace falta código de mapeo nuevo):

| `ErrorType` | Status HTTP | Cuándo ocurre acá |
|---|---|---|
| `Validation` | 400 | `Name` faltante o demasiado largo (en creación o en edición) |
| `NotFound` | 404 | El `{id}` no existe |
| `Conflict` | 409 | `Name` duplicado (`ExistsByNameAsync`) o violación de FK al eliminar (D4) |
| `Internal` | 500 | Falla de persistencia sin clasificar |

**Convención de paginación:** la que ya define el template — entra `PageQueryInputDto` (`pageIndex`, `pageSize`, máximo `100`), sale `PagedPayload<T>` (`items`, `totalCount`). No se introduce convención nueva.

## 7. Operación

**Variables de entorno:** ninguna nueva. Tenencia, caché (L1) y logging ya usan la configuración a nivel de servicio (`TenantResolverService:*`, `Cache:*`, `Serilog:*`); este contexto no agrega ninguna propia [Discovery: no hay parámetro de institución más allá de `MEDIO_PUBLICITARIO_OBLIGATORIO`, que pertenece a Oportunidad y queda fuera de alcance — ver §1].

**Caché y rendimiento:** ver D6. Riesgo: una escritura hecha por las SPs del monolito legado no invalida el caché de este servicio (documentado en §9, no es bloqueante según §1).

**Qué reemplaza cada ruta actual (informativo — este plan no ejecuta ningún corte, ver §1):**

| Ruta/SP legada | Reemplazada (conceptualmente) por |
|---|---|
| `MediosPublicitarios/Lista/New` (MVC) + `pa_opo_medios_publicitarios_retornar` | `GET /ads-channels` |
| `api/mediospublicitarios` (API v1) + `pa_apis_opo_medios_publicitarios_retornar` | `GET /ads-channels` (unificado, D2) |
| `MediosPublicitarios/{id}/Editar/New` + `pa_opo_medios_publicitarios_detalle_retornar` | `GET /ads-channels/{id}` |
| `MediosPublicitarios/ActualizarOportunidad/New` (crear) + `pa_opo_medios_publicitarios_ingresar` | `POST /ads-channels` |
| `MediosPublicitarios/ActualizarOportunidad/New` (editar) + `pa_opo_medios_publicitarios_modificar` | `PUT /ads-channels/{id}` |
| `MediosPublicitarios/{id}/Eliminar` + `pa_opo_medios_publicitarios_eliminar` | `DELETE /ads-channels/{id}` |

## 8. Fases y pasos

#### [F0.1] Verificar la documentación del template y el inventario de Shared
`id: F0.1 · depende_de: (ninguno) · tarea: (sin asignar) · estado: pending`
- Objetivo: confirmar que el repositorio actual sigue coincidiendo con los supuestos de este plan antes de escribir código.
- Fuente: regla "el template manda"; tabla de auditoría de Shared §5.5 (este documento).
- Archivos: ninguno (solo lectura).
- Detalle: leer todos los archivos bajo `docs/plantilla/`; volver a correr las verificaciones de la tabla de §5.5 de este plan contra `src/Shared/*` y `src/Infrastructure/*`; confirmar que `AggregateRoot<TId>`, `IRootRepository<TAggregate,TId>`, `IUnitOfWorkPort`, `SqlServerErrorClassifier`, `ICacheStore`/`OutputCache`, `TenantMiddleware`, `IStructuralValidator<T>` siguen existiendo con las firmas en las que este plan se apoya. Cualquier discrepancia es una `DESVIACIÓN`, se reporta y espera sign-off — no avanzar más allá de F1 hasta resolverla.
- Hecho cuando: el agente deja una confirmación por escrito (descripción del PR o log) de que la auditoría de Shared sigue vigente, o un reporte de `DESVIACIÓN`.
- Verificar: `Test-Path docs/plantilla/arquitectura.md` (y el resto del checklist de arriba, corrido a mano).

#### [F1.1] Crear los contratos de valor de AdsChannel (Args y Filter)
`id: F1.1 · depende_de: F0.1 · tarea: (sin asignar) · estado: pending`
- Objetivo: definir las formas de datos de entrada/consulta del contexto, sin comportamiento ni reglas de negocio todavía.
- Fuente: Discovery §4; este plan §4, §5.
- Archivos: `src/Contexts/AdsChannel/Domain/AdsChannel.Domain.csproj`, `Domain/Aggregates/AdsChannelArgs.cs`, `Domain/Queries/AdsChannelFilter.cs`.
- Detalle: `AdsChannel.Domain.csproj` refleja el SDK/target framework de `Shared.Domain.csproj` y referencia `Shared.Domain` y `Shared.Results`. Registrar el proyecto en `Service.slnx` bajo una carpeta nueva `/src/Contexts/AdsChannel/`. `CreateAdsChannelArgs` y `UpdateAdsChannelArgs` (records planos, sin dependencia del agregado) y `AdsChannelFilter` (record plano) exactamente como en §5.
- Hecho cuando: `dotnet build` compila el proyecto nuevo — estos tres tipos no dependen de nada más del contexto.
- Verificar: `dotnet build src/Contexts/AdsChannel/Domain/AdsChannel.Domain.csproj`

#### [F1.2] Crear el agregado AdsChannel, sus errores y el contrato de repositorio
`id: F1.2 · depende_de: F1.1 · tarea: (sin asignar) · estado: pending`
- Objetivo: modelar el comportamiento y las invariantes del contexto — el agregado, sus errores y el contrato de persistencia que el dominio exige.
- Fuente: este plan §4, §5, D1, D3.
- Archivos: `Domain/Errors/AdsChannelErrors.cs`, `Domain/Aggregates/AdsChannelAggregate.cs`, `Domain/Repositories/IAdsChannelRepository.cs`.
- Detalle: firmas exactamente como en §5 de este plan. `AdsChannelAggregate.Create()` **y** `Update()` validan `Name` requerido y máximo 100 caracteres (D3, ver la nota de validación en §5) — no es responsabilidad exclusiva de `Create()`. `IAdsChannelRepository` extiende `IRootRepository<AdsChannelAggregate, int>`.
- Hecho cuando: `dotnet build` compila el proyecto completo de dominio.
- Verificar: `dotnet build src/Contexts/AdsChannel/Domain/AdsChannel.Domain.csproj`

#### [F2.1] Crear la entidad de persistencia, la configuración de EF y el mapper
`id: F2.1 · depende_de: F1.2 · tarea: (sin asignar) · estado: pending`
- Objetivo: describir cómo `AdsChannelAggregate` se mapea sobre la tabla legada `tbl_opo_medios_publicitarios`, sin alterarla (D3).
- Fuente: este plan §4, D3, D7; template repositorio.md.
- Archivos: `src/Infrastructure/Persistence/EntityFramework/AdsChannels/Entities/AdsChannel.cs`, `Configurations/AdsChannelConfiguration.cs`, `Mappers/AdsChannelRepositoryMapper.cs`, más el `DbSet` agregado a `src/Infrastructure/Persistence/EntityFramework/ApplicationDbContext.cs`.
- Detalle: entidad — `public int Id { get; set; }`, `public string? Name { get; set; }`, `public bool? IsActive { get; set; }`. Configuración — `ToTable("tbl_opo_medios_publicitarios")`, `HasKey(x => x.Id)`, `Property(x => x.Id).HasColumnName("medpub_consecutivoP").ValueGeneratedOnAdd()`, `Property(x => x.Name).HasColumnName("medpub_nombre").HasMaxLength(100).IsUnicode(false)`, `Property(x => x.IsActive).HasColumnName("medpub_estado")`. No se declara relación con `tbl_opo_oportunidades` (D4 — este contexto no necesita hacer `Include` de esa tabla). Mapper — `ToDomain` llama a `AdsChannelAggregate.Reconstruct(document.Id, document.Name, document.IsActive)`; `ToDocument` escribe `Id`, `Name`, `IsActive` desde el agregado.
- Hecho cuando: `dotnet build` compila `Infrastructure.csproj`; `DbSet<AdsChannel> AdsChannels` es consultable en una corrida descartable de `dotnet ef dbcontext info`.
- Verificar: `dotnet build src/Infrastructure/Infrastructure.csproj`

#### [F2.2] Implementar AdsChannelRepository
`id: F2.2 · depende_de: F2.1 · tarea: (sin asignar) · estado: pending`
- Objetivo: implementar `IAdsChannelRepository` completo contra `ApplicationDbContext`, antes de que exista ningún caso de uso (D7).
- Fuente: este plan §5, D3, D4, D7; template repositorio.md.
- Archivos: `src/Infrastructure/Persistence/EntityFramework/AdsChannels/AdsChannelRepository.cs`, `src/Api/DependencyInjection/AdsChannelServiceExtensions.cs` (registra `IAdsChannelRepository`; los casos de uso se agregan a este mismo archivo en F3.1–F3.5).
- Detalle: `GetByIdAsync`/`ExistsAsync`/`GetAsync(filter,page)` usan `AsNoTracking()` y ordenan por `Name` y luego `Id` (desempate, según la regla de paginación de repositorio.md); `ExistsByNameAsync(name, excludingId)` es una query LINQ equivalente a `SELECT 1 ... WHERE medpub_nombre = @name AND (@excludingId IS NULL OR medpub_consecutivoP <> @excludingId)`; `CreateAsync` sigue el patrón `CreateAsync` de repositorio.md (`AddAsync` + `SaveChangesAsync` dentro del repositorio, y luego lee el `Id` generado), capturando `DbUpdateException` y revisando primero `SqlServerErrorClassifier.IsUniqueViolation`; `Update(aggregate)` marca la entidad mapeada como `Modified` vía `context.Entry(...)`; `RemoveAsync(id)` carga la entidad con tracking, llama a `_set.Remove(entity)`, retorna `AdsChannelErrors.NotFound(id)` si no existe — **no** captura `SqlException` para el caso de la FK acá (D4): eso sube desde `IUnitOfWorkPort.CommitAsync`, invocado por el caso de uso de borrado (F3.3), no desde este método. `Origin = nameof(AdsChannelRepository)` en cada error que produce esta clase. La implementación queda completa en este paso — no se posponen métodos para pasos posteriores (D7).
- Hecho cuando: `dotnet build` compila; `AddAdsChannelServices` registra `IAdsChannelRepository` y se invoca desde `Api/DependencyInjection/ApplicationServiceExtensions.cs`.
- Verificar: `dotnet build` (de toda la solución)

Cada paso de F3 es un vertical slice completo: caso de uso, validador (si aplica), acción del controller, registro en DI, test unitario y test de integración de ese endpoint puntual — todo en la misma tarea (D8). `AdsChannelsController` y `AdsChannelServiceExtensions` se crean en F3.1 y se **extienden** (no se recrean) en F3.2–F3.5, por lo que estos pasos son secuenciales entre sí.

#### [F3.1] Vertical slice — CreateAdsChannel
`id: F3.1 · depende_de: F2.2 · tarea: (sin asignar) · estado: pending`
- Objetivo: entregar el endpoint `POST /ads-channels` completo y probado, incluyendo la precondición de unicidad (Discovery D4) y la validación requerido+longitud del agregado (D3).
- Fuente: este plan §5, §6, D3, D8; template casos-de-uso.md §5.1, controllers.md, validaciones.md, testing.md.
- Archivos: `src/Contexts/AdsChannel/Application/AdsChannel.Application.csproj`, `Application/UseCases/CreateAdsChannel/ICreateAdsChannelUseCase.cs`, `CreateAdsChannelInputDto.cs`, `CreateAdsChannelOutputDto.cs`, `CreateAdsChannelMapping.cs`, `CreateAdsChannelUseCase.cs`; `src/Infrastructure/Validation/FluentValidation/AdsChannel/CreateAdsChannelInputValidator.cs`; `src/Api/Controllers/AdsChannelsController.cs` (nuevo); `src/Api/DependencyInjection/AdsChannelServiceExtensions.cs` (agrega `ICreateAdsChannelUseCase`, ver F2.2); `tests/UnitTests/Contexts/AdsChannel/Domain/AdsChannelAggregateTests.cs` (nuevo, casos de `Create`); `tests/UnitTests/Contexts/AdsChannel/Application/CreateAdsChannelUseCaseTests.cs`; `tests/IntegrationTests/Contexts/AdsChannel/CreateAdsChannelEndpointTests.cs`.
- Detalle: orden de `ExecuteAsync` — `input.ToAggregate()` (dispara `AdsChannelAggregate.Create`, que valida `Name` requerido y máximo 100 caracteres — D3) → `repository.ExistsByNameAsync(name)` **contra el nombre ya normalizado (trimeado) por `Create()`, no el crudo del input** (ver GAP-2: dominio-primero evita gastar una query en un request que igual daría 400, y cierra la colisión por whitespace) → `repository.CreateAsync(aggregate)` (sin `IUnitOfWorkPort`: `CreateAsync` confirma internamente para recuperar el `IDENTITY`) → `ToOutputDto()`. El error `NameAlreadyExists` se sella con `Context`/`Origin` en el caso de uso; los errores de validación de `Aggregate.Create` también se sellan; los errores de `CreateAsync` se propagan tal cual. `CreateAdsChannelInputValidator` implementa `IStructuralValidator<T>` con `RuleFor(x => x.Name).NotEmpty().MaximumLength(AdsChannelAggregate.MaxNameLength);`. El controller nace con `[ApiController] [Route("[controller]")] [Tags("AdsChannels")]`, constructor `AdsChannelsController(ICreateAdsChannelUseCase createAdsChannelUseCase)`, una sola acción `[HttpPost] [ValidateRequest] [OutputCacheInvalidate("ads-channels")]` que retorna `HttpCreatedResult<CreateAdsChannelOutputDto>` (D6). El test de dominio cubre: crear válido setea `CreatedAt`/`UpdatedAt`; `Name` vacío/en blanco falla con `NameRequired`; `Name` de más de 100 caracteres falla con `NameTooLong`. El test de integración cubre `POST /ads-channels` → 201, nombre duplicado → 409, `Name` vacío o demasiado largo → 400.
- Hecho cuando: `dotnet build` compila y **todos** los tests de este paso pasan contra el endpoint real (`POST /ads-channels` con `X-Entity-Code` válido responde 201 con el recurso creado; repetir el mismo `Name` responde 409; `Name` vacío responde 400).
- Verificar: `dotnet test tests/UnitTests/UnitTests.csproj --filter FullyQualifiedName~AdsChannel && dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter FullyQualifiedName~CreateAdsChannel`

#### [F3.2] Vertical slice — UpdateAdsChannel
`id: F3.2 · depende_de: F3.1 · tarea: (sin asignar) · estado: pending`
- Objetivo: entregar el endpoint `PUT /ads-channels/{id}` completo y probado, revalidando unicidad de nombre excluyendo el propio registro y la validación requerido+longitud del agregado (D3).
- Fuente: este plan §5, §6, D3, D8.
- Archivos: `Application/UseCases/UpdateAdsChannel/IUpdateAdsChannelUseCase.cs`, `UpdateAdsChannelInputDto.cs`, `UpdateAdsChannelOutputDto.cs`, `UpdateAdsChannelMapping.cs`, `UpdateAdsChannelUseCase.cs`; `src/Infrastructure/Validation/FluentValidation/AdsChannel/UpdateAdsChannelInputValidator.cs`; edita `AdsChannelsController.cs` (agrega el parámetro `IUpdateAdsChannelUseCase` al constructor y la acción `PUT`); edita `AdsChannelServiceExtensions.cs` (agrega `IUpdateAdsChannelUseCase`); edita `AdsChannelAggregateTests.cs` (agrega casos de `Update`); `tests/UnitTests/Contexts/AdsChannel/Application/UpdateAdsChannelUseCaseTests.cs`; `tests/IntegrationTests/Contexts/AdsChannel/UpdateAdsChannelEndpointTests.cs`.
- Detalle: `ExecuteAsync(int id, UpdateAdsChannelInputDto input, ...)` — `repository.GetByIdAsync(id)` (propaga `NotFound` tal cual) → `aggregate.Update(input.ToUpdateArgs())` (dispara la misma validación requerido+longitud que `Create`, D3; se sella si falla) → `repository.ExistsByNameAsync(aggregate.Name, excludingId: id)` **contra el nombre ya normalizado por `Update()`, no el crudo del input** (se sella si hay conflicto; ver GAP-2, mismo razonamiento que F3.1) → `repository.Update(aggregate)` → `unitOfWork.CommitAsync()` → `ToOutputDto()`. `UpdateAdsChannelInputValidator` con las mismas reglas que el de creación. La acción del controller es `[HttpPut("{id}")] [ValidateRequest] [OutputCacheInvalidate("ads-channels")]`, retorna `HttpOkResult<UpdateAdsChannelOutputDto>`. El test de dominio agregado cubre: editar válido setea `UpdatedAt` y no toca `CreatedAt`; `Name` vacío falla con `NameRequired`; `Name` de más de 100 caracteres falla con `NameTooLong`. El test de integración cubre `PUT /ads-channels/{id}` → 200, `{id}` inexistente → 404, nombre duplicado → 409, `Name` inválido → 400.
- Hecho cuando: `dotnet build` compila y todos los tests de este paso pasan contra el endpoint real.
- Verificar: `dotnet test tests/UnitTests/UnitTests.csproj --filter FullyQualifiedName~AdsChannel && dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter FullyQualifiedName~UpdateAdsChannel`

#### [F3.3] Vertical slice — DeleteAdsChannel
`id: F3.3 · depende_de: F3.2 · tarea: (sin asignar) · estado: pending`
- Objetivo: entregar el endpoint `DELETE /ads-channels/{id}` completo y probado, sin validación cruzada de existencia (D4).
- Fuente: este plan D4, §6, D8.
- Archivos: `Application/UseCases/DeleteAdsChannel/IDeleteAdsChannelUseCase.cs`, `DeleteAdsChannelUseCase.cs`; edita `AdsChannelsController.cs` (agrega el parámetro `IDeleteAdsChannelUseCase` y la acción `DELETE`); edita `AdsChannelServiceExtensions.cs`; `tests/UnitTests/Contexts/AdsChannel/Application/DeleteAdsChannelUseCaseTests.cs`; `tests/IntegrationTests/Contexts/AdsChannel/DeleteAdsChannelEndpointTests.cs`.
- Detalle: `Task<Result> ExecuteAsync(int id, CancellationToken ct)` → `repository.RemoveAsync(id)` (retorna `NotFound` si la fila ya no existe) → `unitOfWork.CommitAsync()`. Sin DTOs, sin archivo de Mapping (coincide exactamente con el patrón de borrado del template). La acción del controller es `[HttpDelete("{id}")] [OutputCacheInvalidate("ads-channels")]`, retorna `HttpNoContentResult`. El test de integración siembra, para el caso de conflicto, una fila de `tbl_opo_oportunidades` que referencia al `AdsChannel` a borrar, y cubre: eliminar una fila sin referencias → 204, `{id}` inexistente → 404, eliminar una fila referenciada → 409 (D4).
- Hecho cuando: `dotnet build` compila y todos los tests de este paso pasan contra el endpoint real, incluyendo el caso de conflicto por FK.
- Verificar: `dotnet test tests/UnitTests/UnitTests.csproj --filter FullyQualifiedName~AdsChannel && dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter FullyQualifiedName~DeleteAdsChannel`

#### [F3.4] Vertical slice — GetAdsChannelById
`id: F3.4 · depende_de: F3.3 · tarea: (sin asignar) · estado: pending`
- Objetivo: entregar el endpoint `GET /ads-channels/{id}` completo y probado, incluyendo caché (D6).
- Fuente: este plan §5, §6, D6, D8.
- Archivos: `Application/UseCases/GetAdsChannelById/IGetAdsChannelByIdUseCase.cs`, `GetAdsChannelByIdOutputDto.cs`, `GetAdsChannelByIdMapping.cs`, `GetAdsChannelByIdUseCase.cs`; edita `AdsChannelsController.cs` (agrega el parámetro `IGetAdsChannelByIdUseCase` y la acción `GET("{id}")`); edita `AdsChannelServiceExtensions.cs`; `tests/UnitTests/Contexts/AdsChannel/Application/GetAdsChannelByIdUseCaseTests.cs`; `tests/IntegrationTests/Contexts/AdsChannel/GetAdsChannelByIdEndpointTests.cs`.
- Detalle: lectura pura, sin necesidad de constante `Origin` (refleja `GetProductByIdUseCase`): `repository.GetByIdAsync(id)` → `ToOutputDto()`. La acción del controller es `[HttpGet("{id}")] [OutputCache(Duration=120, Tags=["ads-channels"], VaryByRouteValueNames=["id"])]`, retorna `HttpOkResult<GetAdsChannelByIdOutputDto>`. El test de integración cubre: `{id}` existente → 200, `{id}` inexistente → 404, y un hit de caché real (se muta la fila entre ambas llamadas y se verifica que la segunda respuesta sigue sirviendo el cuerpo cacheado, no solo idempotencia — patrón de test de `cache.md`).
- Hecho cuando: `dotnet build` compila y todos los tests de este paso pasan contra el endpoint real, incluyendo el hit de caché.
- Verificar: `dotnet test tests/UnitTests/UnitTests.csproj --filter FullyQualifiedName~AdsChannel && dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter FullyQualifiedName~GetAdsChannelById`

#### [F3.5] Vertical slice — GetAdsChannels (listado)
`id: F3.5 · depende_de: F3.4 · tarea: (sin asignar) · estado: pending`
- Objetivo: entregar el endpoint `GET /ads-channels` completo y probado — listado unificado, filtrado y paginado (D2) — y cerrar la cobertura de caché con el round-trip cruzado que solo es posible una vez que Create y List existen (D6).
- Fuente: este plan D2, §5, §6, D6, D8.
- Archivos: `Application/UseCases/GetAdsChannels/IGetAdsChannelsUseCase.cs`, `GetAdsChannelsInputDto.cs`, `GetAdsChannelsOutputDto.cs`, `GetAdsChannelsMapping.cs`, `GetAdsChannelsUseCase.cs`; edita `AdsChannelsController.cs` (agrega el parámetro `IGetAdsChannelsUseCase` y la acción `GET` de listado); edita `AdsChannelServiceExtensions.cs`; `tests/UnitTests/Contexts/AdsChannel/Application/GetAdsChannelsUseCaseTests.cs`; `tests/IntegrationTests/Contexts/AdsChannel/GetAdsChannelsEndpointTests.cs`.
- Detalle: construye `new AdsChannelFilter(input.NameContains, input.IsActive)`, llama a `repository.GetAsync(filter, page)`, mapea cada item, retorna `PagedResult<GetAdsChannelsOutputDto>` vía `PagedResult<T>.Success(items, totalCount)` / `.Failure(error)` (la única excepción explícita a la conversión implícita de `Result`). La acción del controller es `[HttpGet] [ValidateRequest] [OutputCache(Duration=60, Tags=["ads-channels"])]`, retorna `HttpOkPagedResult<GetAdsChannelsOutputDto>`. El test de integración cubre: paginación, ambos filtros (`nameContains`, `isActive`), y el round-trip de caché completo `GET` (miss) → `POST` (invalida) → `GET` (refleja el nuevo registro) combinando los endpoints de F3.1 y F3.5, según el patrón de test de `cache.md`. Con este paso el plan termina: no queda ninguna fase posterior.
- Hecho cuando: `dotnet build` compila y todos los tests de este paso pasan, incluyendo el round-trip de caché cruzado con Create.
- Verificar: `dotnet test tests/UnitTests/UnitTests.csproj --filter FullyQualifiedName~AdsChannel && dotnet test tests/IntegrationTests/IntegrationTests.csproj --filter FullyQualifiedName~AdsChannel`

## 9. Riesgos, GAPs y changelog

**Riesgos:**

1. **Datos obsoletos en caché por escrituras del legado.** Una escritura hecha por las stored procedures del monolito legado no invalida el tag de caché de salida `ads-channels` de este servicio, así que se puede servir una respuesta obsoleta hasta por su `Duration`. Aceptado según D6/§1 (convivencia fuera de alcance). No se implementa una corrección — se deja registrado acá según la regla 5 ("no implementes una mejora no pedida").
2. **Mensaje de conflicto genérico al eliminar.** Por D4, un `DELETE` sobre un canal referenciado por una Oportunidad retorna un `409 Conflict` genérico sin nombrar el registro que lo referencia. Aceptado según D4.
3. **Carrera en nombre duplicado.** `ExistsByNameAsync` seguido de `INSERT`/`UPDATE` no es atómico; una request concurrente todavía podría producir un `Name` duplicado, que solo saldría como `Conflict` genérico vía `SqlServerErrorClassifier.IsUniqueViolation` **si** la columna tuviera un índice único — el Discovery no reportó ninguno sobre `medpub_nombre` `[verificado en BD]`. Sin índice único, una carrera puede crear en silencio dos filas con el mismo nombre. No es un GAP (es una evaluación de riesgo vigente, no información faltante) — se deja registrado; no se toma acción, según la regla 5.

**GAPs (consolidados):**

⚠️ **GAP-1 (ABIERTO)**: no existe índice único sobre `medpub_nombre` en `tbl_opo_medios_publicitarios`, así que `ExistsByNameAsync` es solo una verificación de aplicación, no una garantía forzada por la base de datos (ver Riesgo 3) · Afecta: F3.1, F3.2, F2.2 · Confirmar con: DBA
Recomendación por defecto: aceptar la carrera tal cual — agregar un índice único a una tabla que todavía escribe el monolito legado es un cambio de esquema sobre una tabla compartida, fuera de alcance según §1, mismo razonamiento que D3.

✅ **GAP-2 (CERRADO, 2026-08-28)**: la implementación de F3.1/F3.2 invirtió el orden descrito en este plan — valida dominio primero (`ToAggregate()`/`Update()`) y solo entonces llama a `ExistsByNameAsync`, contra el nombre ya normalizado (trimeado), no el crudo del input · Afecta: F3.1, F3.2. No se detuvo la ejecución a reportarlo en su momento (violación de la regla 4 de §0), detectado recién en auditoría posterior. Resolución: el orden implementado es superior al planeado — evita gastar una query de unicidad en un request que de todos modos respondería 400, y cierra una colisión por nombre que difiere solo en espacios en blanco. Este documento se actualiza para reflejar el orden real en vez de revertir el código.

El resto de los GAPs del Discovery y del Turno 1 de este plan quedan cerrados:

| GAP | Resolución |
|---|---|
| Discovery GAP-1 a GAP-10 | Cerrados — ver Discovery §10 |
| Discovery GAP-11 (convivencia con Oportunidad) | Diferido explícitamente, fuera de alcance de este plan (§1) |
| Plan GAP-P1 (ruta de salida) | Cerrado — `docs/servicio/02-plan-ads-channels.md` |
| Plan GAP-P2 (estrategia de corte) | Cerrado — fuera de alcance, la fase de Cutover se eliminó por completo en vez de dejarla `blocked` |
| Plan GAP-P3 (variables de entorno nuevas) | Cerrado — no se requiere ninguna |

**Changelog:**

- **2026-08-14** — Plan creado a partir de `docs/servicio/01-discovery-medios-publicitarios.md`. Decisiones D1–D6 aprobadas por Brayan Gamboa. Ruta de salida fijada en `docs/servicio/02-plan-advertising-medium.md`. Convivencia/corte con Oportunidad excluida de alcance.
- **2026-08-14** — Documento redactado en español con artefactos técnicos en inglés, según instrucción explícita del desarrollador, corrigiendo la versión anterior que estaba enteramente en inglés.
- **2026-08-14** — Renombrado el bounded context de `AdvertisingMedium` a `AdsChannel` (D1 actualizada) por instrucción explícita del desarrollador; el archivo se renombró de `docs/servicio/02-plan-advertising-medium.md` a `docs/servicio/02-plan-ads-channels.md`. Se hizo explícita en §5 la validación de requerido y longitud tanto en `Create()` como en `Update()` del agregado, y su relación con los casos de uso F3.1/F3.2. Se agregó D7 y se reordenó §8: la persistencia (F2, antes F3) ahora precede a los casos de uso (F3, antes F2); se dividió el antiguo paso F1.1 en F1.1 (Args + Filter) y F1.2 (Aggregate + Errors + Repository interface) por ser dos unidades coherentes y compilables por separado.
- **2026-08-14** — Se agregó D8: cada paso de F3 pasa a ser un vertical slice completo (caso de uso + validador + acción del controller + DI + test unitario + test de integración del endpoint), en vez de diferir validadores, controller y tests a fases separadas. Se eliminaron las antiguas fases F4 (validadores, controller) y F5 (tests), absorbidas dentro de F3.1–F3.5. F3.2–F3.5 pasan de depender solo de F2.2 a depender secuencialmente del paso anterior, porque ahora comparten los mismos archivos de controller y de registro DI.
- **2026-08-28** — Corrección post-implementación (deriva de documentación detectada en auditoría de código, GAP-2): §6 y el detalle de F3.1/F3.2/F3.4/F3.5 actualizados para reflejar (a) que cada slice usa su propio `{UseCase}OutputDto` en vez del `AdsChannelOutputDto` compartido que describía el plan, y (b) que `ExistsByNameAsync` se llama después de la validación de dominio, contra el nombre ya normalizado — no antes, contra el nombre crudo, como decía el plan original. El código no cambió; solo este documento, para que deje de contradecir la implementación real.
