---
service: crm-service-q10
context: loss-reasons (Causas de pérdida)
doc: tasks
status: approved
source: workplan_causas.md
updated: 2026-08-21
equipo: Juan Camilo · Brayan · Juan Esteban
---

# Tareas — Causas de pérdida

> Generado con la plantilla `Services WorkFlow › Templates › Task list`. Derivado de `workplan_causas.md` §8 y de `discovery_causas.md`; **cada tarea mapea a pasos existentes del plan, sin inventar trabajo**.
>
> Este documento es una **capa de agregación**, no un backlog atómico. El **paso** (`workplan_causas.md` §8) es la unidad de trabajo del ejecutor; la **tarea** (este documento) es la unidad de revisión, PR y estimación.

## 0. Cómo se ejecuta este backlog

**No hay Jira en este proceso.** El backlog es este archivo y el tablero son los PRs. En consecuencia:

* **El estado de la tarea vive en la columna `Estado` de §2** (`⬜ pendiente` · `🔄 en curso` · `✅ hecha`), y lo actualiza quien la ejecuta al abrir y al mergear su PR.
* **El estado del paso vive en `workplan_causas.md` §8** (`estado: pending → done`), y se cambia **solo después** de correr el comando de `Verificar:` de ese paso. Es la regla 3 de §0 del plan.
* Cada paso del plan declara ahora su tarea y su responsable en el campo `tarea:`, así que se puede abrir el plan y filtrar por nombre.

### Modelo de ramas

El contexto tiene **una rama base propia**, `feat/loss-reasons`, sacada de `main`. **Toda rama de tarea sale de la base, no de `main`, y su PR va contra la base.** `main` solo recibe el contexto una vez, al final, cuando la base esté completa y verde.

```
main
 └── feat/loss-reasons                 ← rama base del contexto
      ├── feat/loss-reasons-scaffold   ← T2
      ├── feat/loss-reasons-domain     ← T3
      ├── feat/loss-reasons-persistence, …-usage-reader, …  ← T4, T5
      └── …                            ← una por tarea, siempre desde la base
```

Consecuencias prácticas:

* **Antes de abrir tu rama**, `git checkout feat/loss-reasons` + `git pull` — si no, sales de una base vieja y arrastras el trabajo de otro como conflicto.
* Cuando alguien mergea a la base, **el resto rebasa su rama sobre la base** antes de seguir. Es lo que mantiene barato el único choque de §3 (`ApplicationDbContext.cs` entre T4 y T5).
* La base **no se rompe**: cada PR entra con `dotnet build Service.slnx -c Release` y `dotnet test tests/UnitTests -c Release` en verde, que es lo que el `.githooks/pre-commit` ya exige localmente.

Cada persona, antes de tocar código:

1. Lee **§0 del plan** (`workplan_causas.md`) — las cinco reglas del ejecutor. La cuarta es la importante: **si la realidad del repo contradice el plan, detenerse y reportar con `⚠️ GAP`**, no improvisar.
2. Lee el encabezado de **su fase** en §8: dice qué decisiones la afectan y cuál es la estrategia de pruebas.
3. Ejecuta sus pasos **en orden de `id`**, respetando `depende_de`.
4. Corre el `Verificar:` del paso, y solo entonces lo marca `done`.

## 0.1 Nota sobre los flujos

`03-flujos.md` **no existe todavía** (el plan lo dejó fuera de alcance junto con la integración y el corte). Para poder cumplir R6 —toda tarea sirve a un flujo o se declara andamiaje— los flujos se derivan de la tabla de reemplazo de rutas del plan (§7.4), que es su inventario en embrión:

| Flujo | Consumidor en el legado | Endpoint objetivo |
|---|---|---|
| **F1** Listar causas | `EstructuracionComercialController.ListaCausas` · `GET api/causas` | `GET /loss-reasons` |
| **F2** Consultar una causa | `EstructuracionComercialController.EditarCausas` (carga del modelo) | `GET /loss-reasons/{id}` |
| **F3** Crear causa | `ActualizarCausas` con `tipo=creacion` | `POST /loss-reasons` |
| **F4** Editar causa | `ActualizarCausas` con `tipo=edicion` | `PUT /loss-reasons/{id}` |
| **F5** Eliminar causa | `EliminarCausas` (POST) | `DELETE /loss-reasons/{id}` |

Los identificadores son **provisionales** hasta que se escriba `03-flujos.md`; si allí se numeran distinto, esta tabla y la columna `Flujo` de §2 se corrigen, no al revés.

## 1. Bloqueos previos

**No queda ningún bloqueo abierto.** Los siete GAPs del plan se resolvieron el 2026-08-14 (plan §9.2); se conservan con su resolución para que la traza no se pierda.

| id | Qué faltaba | Bloqueaba | Dueño | Estado |
|---|---|---|---|---|
| `BLQ-1` | El repositorio no compilaba: faltaba `GetServiceInfoOutputDto` (`GAP-1`) | T1 y, por dependencia, todas | tech lead | ✅ **Resuelto** vía commit `9f24956` (pull del dueño del repo); `dotnet build Service.slnx -c Release` en verde |
| `BLQ-2` | Veredictos de `discovery_causas.md` §7 sin firmar (`GAP-6`) | T3, T4, T5, T8, T9, T10 | tech lead | ✅ **Resuelto** — todas las propuestas firmadas; las catorce decisiones del plan están `aprobada` |
| `BLQ-3` | Autenticación y permisos del servicio (`GAP-2`, `GAP-3`) | T11 | tech lead + Seguridad | ✅ **Resuelto** vía D12 y D13 — no se implementan en el servicio: autenticación por infraestructura, autorización por Jack |
| `BLQ-4` | Resolución del tenant (`GAP-5`) | T12 | Infraestructura | ✅ **Resuelto** vía D14 — Jack determina y envía el tenant |
| `BLQ-5` | Filas de más de 50 caracteres tras D5 (riesgo R7) | T3 *(condicionaba la firma de D5)* | tech lead | ✅ **Cerrado como bloqueo** — D5 quedó firmada. Sigue vivo como **riesgo R7**: conviene ejecutar el conteo antes del corte, no antes de codificar |

`GAP-4` (feature flag) y `GAP-7` (escritura de `neg_cau_consecutivo`) se resolvieron declarándolos fuera del alcance de este plan; nunca bloquearon una tarea de este backlog y siguen listados en §4.

> **Un pendiente que no es un bloqueo de este backlog pero condiciona el corte:** con D13, la autorización la ejerce Jack, y las 7 acciones `[AllowAnonymous]` de `EstructuracionComercialController` siguen abiertas allí (riesgo **R9** del plan). Es trabajo del lado del monolito, en `03-flujos.md` — ver `EXT-8` en §2.6.

## 2. Tabla maestra de tareas

| # | Responsable | Título | Pasos del plan | Rama | Base | Est. | Depende de | Flujo | Estado |
|---|---|---|---|---|---|---|---|---|---|
| ~~T1~~ | — | ~~Restore missing template DTO~~ | `F0.2` | — | — | — | — | andamiaje | ✅ **Cerrada sin PR** — commit `9f24956` |
| T2 | **Juan Esteban** | Scaffold LossReason context projects | `F0.3` | `feat/loss-reasons-scaffold` | `feat/loss-reasons` | 2 | — | andamiaje | ✅ **mergeada** a la base — `96915cb`, merge `261f289` |
| T3 | **Juan Esteban** | LossReason domain model and read port | `F1.1`–`F1.6`, `F2.5` | `feat/loss-reasons-domain` | `feat/loss-reasons` | 5 | T2 | F1–F5 | ✅ **mergeada** a la base — `3500688`, merge `1dc36ec` |
| T4 | **Brayan** | LossReason persistence | `F2.1`–`F2.4`, `F2.7`–`F2.9` | `feat/loss-reasons-persistence` | `feat/loss-reasons` | 8 | T3 | F1–F5 | 🔄 **en curso** — los siete pasos `done` y verificados; commits `fbafbda`+`ef5f7c1`, **pendiente de merge** a la base |
| T5 | **Juan Camilo** | Loss reason usage reader | `F2.6` | `feat/loss-reasons-usage-reader` | `feat/loss-reasons` | 3 | T3 | F5 | ⬜ |
| T6 | **Juan Esteban** | Get loss reasons use case | `F3.1`, `F3.6` | `feat/loss-reasons-get-list` | `feat/loss-reasons` | 3 | T4 | F1 | ✅ en `feat/loss-reasons-get-list` — **PR abierto**, pendiente de revisión |
| T7 | **Juan Camilo** | Get loss reason by id use case | `F3.2`, `F3.7` | `feat/loss-reasons-get-by-id` | `feat/loss-reasons` | 2 | T4 | F2 | ⬜ |
| T8 | **Brayan** | Create loss reason use case | `F3.3`, `F3.8` | `feat/loss-reasons-create` | `feat/loss-reasons` | 3 | T4 | F3 | ⬜ |
| T9 | **Brayan** | Update loss reason use case | `F3.4`, `F3.9` | `feat/loss-reasons-update` | `feat/loss-reasons` | 3 | T4 | F4 | ⬜ |
| T10 | **Juan Camilo** | Delete loss reason use case | `F3.5`, `F3.10` | `feat/loss-reasons-delete` | `feat/loss-reasons` | 3 | T4, T5 | F5 | ⬜ |
| T11 | **Juan Camilo** | LossReason API surface | `F4.1`–`F4.4` | `feat/loss-reasons-api` | `feat/loss-reasons` | 5 | T6, T7, T8, T9, T10 | F1–F5 | ⬜ |
| T12 | **Juan Esteban** | LossReason integration tests and coverage gate | `F5.1`, `F5.2` | `test/loss-reasons-integration` | `feat/loss-reasons` | 5 | T11 | F1–F5 | ⬜ |

**Total: 42 puntos en 11 tareas ejecutables** (T1 se cerró sin PR). **Base `feat/loss-reasons` en todas** — la rama base del contexto, no `main` (ver §0, *Modelo de ramas*).

### 2.1 Qué entrega cada tarea

| # | Responsabilidad única | Capa (R5) |
|---|---|---|
| T1 | Devolver el repositorio a un estado compilable. No toca el contexto | andamiaje |
| T2 | Los dos `.csproj` del contexto y su registro en `Service.slnx` | andamiaje |
| T3 | Errores, Args, agregado con sus invariantes, filtro, contrato de repositorio y puerto del Reader | dominio + contratos de aplicación |
| T4 | Entidad de persistencia, configuración EF, mapper, repositorio y su `DbSet` | infraestructura |
| T5 | Entidad keyless de `tbl_opo_negocios`, su configuración y el Reader de uso | infraestructura |
| T6–T10 | Un caso de uso cada una, con sus DTOs, su mapping y sus tests | aplicación |
| T11 | Validadores, controller, DI y política de caché L1 | API |
| T12 | Tests de integración contra SQL real y la puerta de cobertura | verificación |

### 2.2 Reparto por persona

El criterio no es repartir puntos parejos, sino que **cada quien sea dueño de una franja coherente del plan**: quien conoce una capa escribe sus tests y responde por ella en la revisión.

#### Juan Esteban — andamiaje, dominio y verificación de extremo a extremo · 15 puntos

| Tarea | Pasos | Fase del plan | Puede empezar cuando |
|---|---|---|---|
| **T2** | `F0.3` | Fase 0 | **ya** — no tiene predecesora |
| **T3** | `F1.1` → `F1.2` → `F1.3` → `F1.4` → `F1.5` → `F1.6`, más `F2.5` | Fase 1 completa (+ el puerto de Fase 2) | T2 mergeada |
| **T6** | `F3.1`, `F3.6` | Fase 3 | T4 mergeada |
| **T12** | `F5.1`, `F5.2` | Fase 5 completa | T11 mergeada |

Es dueño de las **invariantes** (D4, D5: `NameMaxLength` como fuente única del número) y del **contrato observable** (Fase 5 verifica las tres rupturas de paridad de D8/D9 y los dos casos NULL de D6). `F1.1`–`F1.6` es una cadena estricta: cada paso compila contra el anterior, no se paraleliza dentro de T3.

#### Brayan — persistencia y casos de uso de escritura · 14 puntos

| Tarea | Pasos | Fase del plan | Puede empezar cuando |
|---|---|---|---|
| **T4** | `F2.1` → `F2.2` → `F2.3` → `F2.4` → `F2.7` → `F2.8` → `F2.9` | Fase 2 (rama del agregado) | T3 mergeada |
| **T8** | `F3.3`, `F3.8` | Fase 3 | T4 mergeada (la suya) |
| **T9** | `F3.4`, `F3.9` | Fase 3 | T4 mergeada (la suya) |

Franja coherente: quien mapea la tabla legada escribe después las dos operaciones que la modifican. **T8 y T9 son deliberadamente asimétricas y él es quien tiene que notarlo:** `CreateLossReason` **no** inyecta `IUnitOfWorkPort` ni llama `CommitAsync` (D3, porque el `Id` es `IDENTITY` y lo asigna `CreateAsync`), mientras que `UpdateLossReason` **sí** hace `Update` + `CommitAsync`. `F3.8` incluye el assert explícito de que no hay commit.

**Antes de `F2.1`:** leer el dump del esquema con la trampa documentada — en `02-columnas.tsv` **vacío significa `True`**, así que `cau_nombre` y `cau_estado` son NULLABLE. De ahí sale D6 y el `string?`/`bool?` obligatorio de la entidad.

#### Juan Camilo — lectura de la tabla ajena, borrado y superficie HTTP · 13 puntos

| Tarea | Pasos | Fase del plan | Puede empezar cuando |
|---|---|---|---|
| **T5** | `F2.6` | Fase 2 (rama del Reader) | T3 mergeada — **en paralelo con T4** |
| **T7** | `F3.2`, `F3.7` | Fase 3 | T4 mergeada |
| **T10** | `F3.5`, `F3.10` | Fase 3 | T4 **y** T5 mergeadas |
| **T11** | `F4.1` → `F4.2` → `F4.3` → `F4.4` | Fase 4 completa | T6, T7, T8, T9 y T10 mergeadas |

Franja coherente: escribe el Reader (`F2.6`) y después el único caso de uso que lo consume (`F3.5`, el 409 por causa en uso, D7). Cierra con la Fase 4, que es el punto de reunión.

Los dos puntos donde su parte se rompe en silencio si se descuida:
* **Orden en `F3.5`:** primero `ExistsAsync`, después `IsUsedAsync`. Invertirlo hace que cada 404 pague un scan de ~300.000 filas, porque `neg_cau_consecutivo` no está indexado (R2).
* **Política de caché en `F4.3`:** la política `loss-reasons-list` debe **partir de la base** (conserva `SetVaryByHeader("X-Entity-Code", …)`, que es lo que aísla los tenants) y **añadir** `SetVaryByQuery("name", "isActive", "pageIndex", "pageSize")`. Sin esa variación, el listado sirve el resultado de un filtro para otro: es R8, un fallo de correctitud que se ve como datos equivocados, no como error.

### 2.3 Olas de ejecución

Una ola es un tramo donde nadie espera a nadie dentro de la ola. Se avanza de ola cuando **todas** sus tareas están mergeadas.

| Ola | Juan Esteban | Brayan | Juan Camilo | Qué la desbloquea |
|---|---|---|---|---|
| **0 · Preparación** | `F0.1` | `F0.1` | `F0.1` | nada — arranca ya |
| **1 · Andamiaje** | **T2** (`F0.3`) | sin código: `F0.1` + entorno | sin código: `F0.1` + entorno | ola 0 |
| **2 · Dominio** | **T3** (`F1.1`–`F1.6`, `F2.5`) | sin código: preparar `F2.1`–`F2.3` sobre el dump | sin código: leer `cache.md`, `controllers.md`, `validaciones.md` para T11 | T2 mergeada |
| **3 · Persistencia** | revisión de PRs | **T4** (`F2.1`–`F2.4`, `F2.7`–`F2.9`) | **T5** (`F2.6`) | T3 mergeada |
| **4 · Aplicación** | **T6** (`F3.1`, `F3.6`) | **T8** (`F3.3`, `F3.8`) y **T9** (`F3.4`, `F3.9`) | **T7** (`F3.2`, `F3.7`) y **T10** (`F3.5`, `F3.10`) | T4 mergeada · T10 además espera T5 |
| **5 · API** | revisión + preparar el seed de `F5.1` | revisión | **T11** (`F4.1`–`F4.4`) | T6, T7, T8, T9, T10 mergeadas |
| **6 · Verificación** | **T12** (`F5.1`, `F5.2`) | revisión | revisión | T11 mergeada |

**Las olas 1 y 2 son de una sola persona y no hay forma de repartirlas.** `F0.3` crea los `.csproj` que todo lo demás referencia, y `F1.1`→`F1.6` es una cadena de compilación. Los otros dos no tienen código que escribir: la ola 0 y las columnas «sin código» de las olas 1–2 existen para que ese tiempo se use en `F0.1` (leer los catorce documentos de `docs/plantilla/`, que el plan exige antes de escribir una línea) y en dejar el entorno listo — `dotnet build Service.slnx -c Release` en verde y **Docker Desktop corriendo**, que T12 va a necesitar con Testcontainers.

**La ola 3 es la primera con dos frentes reales.** T4 y T5 comparten un archivo (§3) y nada más.

**La ola 4 es la más ancha: cinco tareas, ninguna comparte archivo.** Cada caso de uso vive en su propia carpeta con sus cinco archivos coubicados; por eso las cinco pueden abrirse a la vez sin conflictos.

> **Aceleración posible, que hoy NO se aplica.** Los cinco casos de uso solo consumen `ILossReasonRepository` (`F1.5`) e `ILossReasonUsageReader` (`F2.5`) — ambos de T3, no de T4. Técnicamente la ola 4 podría solaparse con la ola 3 y quitarle 8 puntos al camino crítico. Pero el plan declara `depende_de: F2.7` para `F3.1`–`F3.4` y `F2.7, F2.6` para `F3.5`, y **cambiar una dependencia del plan es una enmienda de §8, no una decisión de ejecución** (regla 5 de §0: no agregar alcance ni improvisar). Si el equipo quiere el solape, se propone la enmienda y se firma; hasta entonces, la ola 4 espera a T4.

### 2.4 Camino crítico

```
   Juan Esteban        Brayan / Juan Camilo              Juan Camilo
   ────────────        ────────────────────              ───────────
   T2 → T3 ──┬── T4 (BR) ─┬─→ T6  (JE) ─┐
             │            ├─→ T7  (JC) ─┤
             │            ├─→ T8  (BR) ─┼─→ T11 (JC) → T12 (JE)
             │            ├─→ T9  (BR) ─┤
             └── T5 (JC) ─┴─→ T10 (JC) ─┘
```

* **Camino crítico:** T2 → T3 → T4 → *(cualquiera de T6–T10)* → T11 → T12 — 6 tareas, **26 de los 42 puntos**. Cruza a las tres personas, así que **cada merge del camino crítico desbloquea a otro**: mergear rápido importa más que empezar rápido.
* **T4 y T5** van en paralelo tras T3 (Brayan y Juan Camilo).
* **T6 a T10** van en paralelo tras T4; T10 además tras T5. Estaban encadenadas en el plan sin razón de código y la enmienda del 2026-08-14 quitó esa serialización.
* **T11 es el punto de reunión**: el controller inyecta los cinco casos de uso por constructor primario (D11), así que espera a los cinco.
* **Los dos únicos puntos donde una persona espera a otra por código, no por ola:** T4 y T5 esperan el `ILossReasonRepository` de Juan Esteban (`F1.5`), y T10 espera el Reader de su propio autor más la persistencia de Brayan.

### 2.5 Revisión de PRs

Round-robin, para que nadie revise lo suyo y las tres capas se conozcan de a dos:

| Autor | Revisa |
|---|---|
| Juan Esteban (T2, T3, T6, T12) | Brayan |
| Brayan (T4, T8, T9) | Juan Camilo |
| Juan Camilo (T5, T7, T10, T11) | Juan Esteban |

Qué mirar en la revisión, además del `Hecho cuando` del paso: que **nada lance** (todo error esperado vuelve como `Result`), que **el `Error.Origin` ajeno no se reescriba** (cada pieza sella solo lo que origina) y que no aparezca un `50` literal donde debe ir `LossReasonAggregate.NameMaxLength`.

### 2.6 Tareas externas

No producen código, no tienen rama, no se estiman. **Ninguna la ejecuta el equipo de desarrollo**: son del tech lead y quedan aquí para que no se pierdan.

| id | Qué | Dueño | Cierra | Estado |
|---|---|---|---|---|
| `EXT-1` | Firmar los veredictos de `discovery_causas.md` §7 | tech lead | `GAP-6` → `BLQ-2` | ✅ Hecha |
| `EXT-2` | Recuperar `GetServiceInfoOutputDto` | dueño del repositorio | `GAP-1` → `BLQ-1` | ✅ Hecha — commit `9f24956` |
| `EXT-3` | Definir autenticación y autorización del servicio | tech lead + Seguridad | `GAP-2`, `GAP-3` → `BLQ-3` | ✅ Hecha — D12 y D13: no se implementan en el servicio |
| `EXT-4` | Confirmar cómo llega el tenant | tech lead | `GAP-5` → `BLQ-4` | ✅ Hecha — D14: lo envía Jack |
| `EXT-5` | Ejecutar el conteo de R7 (`… WHERE LEN(cau_nombre) > 50`) en los tenants objetivo | tech lead | riesgo R7 | ⬜ Pendiente — antes del corte, no del código |
| `EXT-6` | Decidir el destino de la escritura de `neg_cau_consecutivo` | tech lead | `GAP-7` | ✅ Hecha — se queda en el monolito |
| `EXT-7` | Escribir `03-flujos.md` con el inventario definitivo, los criterios de aceptación y el plan de rollback | tech lead + QA | fija los ids de §0.1 | ⬜ Pendiente |
| `EXT-8` | **Autorizar del lado de Jack las rutas que hoy son `[AllowAnonymous]`**, y darle sus filas de función al controller que llame al servicio | tech lead + dueño de `GestionComercial` | riesgo **R9** | ⬜ Pendiente — condición del corte |
| `EXT-9` | **Contar filas con NULL** (`SELECT COUNT(*) FROM tbl_opo_causas WHERE cau_nombre IS NULL OR cau_estado IS NULL;`) en los tenants objetivo, y limpiarlas si las hay | tech lead + DBA | riesgo **R10** | ⬜ Pendiente — **condición del corte**: con D6 el servicio exige `NOT NULL` donde la BD no lo exige, y una sola fila con NULL tumba el listado completo de ese tenant |

**Ninguna externa bloquea ya el camino crítico.** Las cuatro pendientes (`EXT-5`, `EXT-7`, `EXT-8`, `EXT-9`) son condiciones del **corte**, no de la construcción: corren en paralelo a las olas 0–6 sin detener a nadie.

`EXT-5` y `EXT-9` son la misma clase de trabajo —contar filas que el servicio ya no va a tolerar— y conviene correrlas juntas, en una sola pasada por los tenants: `EXT-5` mira los nombres de más de 50 caracteres (R7, que responde 400 en el `PUT`) y `EXT-9` los NULL (R10, que tumba el listado entero).

## 3. Archivos compartidos

Regla R8: el archivo lo crea la primera tarea que lo necesita; las demás solo añaden.

> Los seis ya existen en el repositorio, así que ninguna tarea los crea. La columna declara **quién los toca primero**, para que las demás solo añadan y el conflicto sea de una línea.

| Archivo | Lo toca primero | Lo tocan también |
|---|---|---|
| `Service.slnx` | T2 · **Juan Esteban** (añade los dos `.csproj`) | — |
| `src/Infrastructure/Persistence/EntityFramework/ApplicationDbContext.cs` | T4 · **Brayan** (`DbSet` de `LossReason`, paso `F2.7`) | **T5 · Juan Camilo** (`DbSet` keyless del Reader, paso `F2.6`) |
| `src/Infrastructure/Infrastructure.csproj` | T4 · **Brayan** (`ProjectReference` a `LossReason.Application`, para que compile el mapper de `F2.3`) | — · **ya cubre a T5**: la referencia a `Application` arrastra `Domain`, así que `F2.6` no lo toca |
| `src/Api/DependencyInjection/ApplicationServiceExtensions.cs` | T11 · **Juan Camilo** (llamada a `AddLossReasonServices`) | — |
| `src/Api/DependencyInjection/OutputCacheExtensions.cs` | T11 · **Juan Camilo** (política `loss-reasons-list`, paso `F4.3`) | — |
| `tests/UnitTests/UnitTests.csproj` | T3 · **Juan Esteban** (`ProjectReference` a `LossReason.Domain`, para que compile el test de `F1.6`) | ~~T6–T10~~ **Ya resuelto: T6 añadió la `ProjectReference` a `LossReason.Application`.** T7–T10 no tienen que tocar el archivo, solo rebasar sobre la base |

Ninguna tarea toca archivos de seguridad ni de configuración de acceso: con D12, D13 y D14 el servicio no implementa autenticación, permisos ni resolución propia de tenant.

**El único choque real es `ApplicationDbContext.cs` entre T4 (Brayan) y T5 (Juan Camilo)**, las dos tareas paralelas de la ola 3. Cada una añade su propio `DbSet` y ninguna reescribe el archivo: **quien mergee segundo rebasa y resuelve un conflicto de una línea**. Declararlo es lo que permite que vayan en paralelo en vez de apilar ramas para evitarlo.

T6–T10 van en paralelo y **no comparten ningún archivo**: cada caso de uso vive en su propia carpeta con sus cinco archivos coubicados. Ese es el motivo por el que la división por caso de uso, además de respetar R2, no genera conflictos entre las tres personas en la ola más ancha.

## 4. Fuera de este backlog

| Qué | Por qué | Dueño |
|---|---|---|
| Client en el monolito, feature flag y cutover | Fuera del alcance del plan (§1); es materia de `03-flujos.md` | tech lead |
| Escritura de `tbl_opo_negocios.neg_cau_consecutivo` | Pertenece al agregado *Negocio*, con 4 escritores y dos fuera de `GestionComercial` (`GAP-7`) | tech lead |
| Las 4 vistas Razor de `Causas/` y el dropdown de `Negocios/_Estados.cshtml` | No migran: son presentación del monolito | — |
| Los 8 SPs que leen `tbl_opo_causas` por `LEFT JOIN` | Siguen sirviendo al monolito hasta el decomiso | — |
| Exportable `pa_inf_opo_excel_oportunidades_dinamico` | Fuera de alcance de esta iteración | — |
| Borrar las copias muertas `…_VERSION_ANTERIOR` y `…_brayan` | Fuera de alcance permanente; ticket propio de limpieza de esquema | DBA |
| Caché L2 (`ICacheStore`) | D10 la descarta para esta iteración | — |
| Autenticación dentro del servicio | D12 — el control es de infraestructura; no se difiere, **no se hace** | — |
| Modelo de permisos dentro del servicio | D13 — lo ejerce Jack; **no se hace** | — |
| Autorizar las rutas `[AllowAnonymous]` del lado de Jack | Trabajo en el monolito, no en este servicio — pero **condición del corte** (R9) | tech lead + dueño de `GestionComercial` (`EXT-8`) |

Sin dueño, es trabajo que se pierde: por eso las diez filas lo declaran.

---

## Verificación contra las diez reglas

| # | Regla | Resultado |
|---|---|---|
| R1 | Una tarea, una responsabilidad, un PR | ✅ Ningún título necesita una "y" salvo T11 ("controller, DI y caché"), que es una sola responsabilidad —la superficie HTTP del contexto— y se sostiene como un PR |
| R2 | ≤ 400 líneas de diff de producción **o** ≤ 10 archivos de `src/` | ✅ Máximo **5 archivos de `src/`** (T3, T4, T5, T6–T9) y 7 en T11. Es la regla que obligó a dividir las escrituras: agrupadas llegaban a 12 |
| R3 | Testable sola, con los casos escritos | ✅ Cada tarea de T3 en adelante incluye su paso de test con los casos enumerados en el plan |
| R4 | Deja el repo compilable y los tests en verde | ✅ Todo paso de §8 tiene `Verificar:` con `dotnet build` o `dotnet test` |
| R5 | No mezcla capas de riesgo | ✅ T3 dominio + contratos, T4/T5 infraestructura, T6–T10 aplicación, T11 API. **No hay migraciones** (Database First sobre tabla existente) y **`Shared` no se toca**: la auditoría del plan §5.5 concluyó que no falta ninguna capacidad transversal |
| R6 | Sirve a un flujo, o se declara andamiaje | ✅ T1 y T2 son andamiaje; las diez restantes declaran flujo. Ids provisionales por §0.1 |
| R7 | Sin dependencias artificiales | ✅ Auditado: se eliminaron las cuatro dependencias encadenadas entre los casos de uso. Las que quedan son de código real, salvo el `depende_de: F2.7` de la ola 4, declarado y razonado en §2.3 |
| R8 | Archivos compartidos declarados | ✅ Los seis en §3, con el único choque real señalado y sus dos dueños con nombre |
| R9 | Se estima la tarea, nunca la fase | ✅ Once estimaciones Fibonacci individuales (2–8), ninguna por fase |
| R10 | Tarea reabierta se cierra y se recrea | ✅ Sin Jira, la regla se ejerce sobre el PR y sobre la columna `Estado` de §2: si una tarea vuelve, se cierra su PR y se abre uno nuevo; no se reabre el mergeado |

## Criterio de cierre

El documento está listo cuando:

- [x] Cada tarea tiene responsable, rama, base, estimación y dependencias.
- [x] Cada persona sabe qué puede empezar hoy y qué espera a un merge ajeno → §2.2 y §2.3.
- [x] Cada tarea mapea a un rango de pasos del plan, sin pasos huérfanos ni duplicados → de los **34 pasos** de §8 (`F0.1`–`F5.2`, incluido el `F2.9` de la enmienda del 2026-08-21), **33 están cubiertos exactamente una vez**. El único no cubierto por una tarea es `F0.1`, que es lectura y no produce PR: **lo ejecutan las tres personas** en la ola 0.
- [x] Cada paso del plan declara su tarea y su responsable en el campo `tarea:` de §8.
- [x] Cada tarea sirve a un flujo o está etiquetada como andamiaje.
- [x] Ninguna tarea excede el techo de R2.
- [x] Los archivos compartidos están declarados en §3, con dueño y orden de merge.
- [x] Cada bloqueo previo declara qué tarea bloquea.

**El backlog está en ejecución.** **T2 y T3 están mergeadas a `feat/loss-reasons`** y la base está verde. **T4 está terminada y verificada en su rama** (`feat/loss-reasons-persistence`, commit `fbafbda`, 359 tests unitarios), esperando revisión de Juan Camilo y merge a la base. La **ola 4 se abre cuando T4 entre**: Juan Esteban con **T6**, Brayan con **T8** y **T9**, Juan Camilo con **T7**. De la ola 3 sigue pendiente **T5** (Juan Camilo), que no bloquea a nadie salvo a **T10**. Quien tome un caso de uso rebasa sobre la base después de ese merge: T4 tocó `ApplicationDbContext.cs` e `Infrastructure.csproj`, y este último ya deja resuelto lo que T5 necesitaba de él.

## Changelog

| Fecha | Cambio |
|---|---|
| 2026-08-21 | **La puerta de cobertura obliga a probar el repositorio con unitarios.** El pipeline de T4 falló en **89,6 %** (piso 90): los 77 renglones de `LossReasonRepository` no los cubre ningún unit test y los de integración de F5.1 **no cuentan** para el porcentaje. Se enmienda la estrategia de pruebas de la Fase 2 y nace el paso **`F2.9`**, con 24 tests sobre `ApplicationDbContext` + EF InMemory — cobertura **97,1 %**. Es una desviación de `testing.md` («No usar EF InMemory») **pendiente de la firma del tech lead**; lo que depende de constraints sigue en F5.1. La estimación de T4 no cambia |
| 2026-08-21 | **T4 ejecutada** (`F2.1`–`F2.4`, `F2.7`–`F2.9`, Brayan): entidad de persistencia con la nulabilidad real de `tbl_opo_causas`, configuración EF sobre la tabla legada, mapper que normaliza los dos NULL de D6, repositorio con los 8 miembros del contrato y el primer `DbSet` del servicio, con los 4 tests del mapper en verde (359 en la suite). Commit `fbafbda` en `feat/loss-reasons-persistence`, **pendiente de merge a la base**. Se descubre un **segundo archivo compartido no declarado**, `src/Infrastructure/Infrastructure.csproj` —no referenciaba el contexto y el mapper no compilaba—; se reportó como GAP y, autorizado, se añadió la referencia a `LossReason.Application`, que **también cubre a T5**. §3 lo registra. Cuando T4 entre a la base quedan abiertas **T6, T7, T8 y T9**; T10 seguirá esperando T5 |
| 2026-08-14 | Versión inicial, derivada de `workplan_causas.md` §8 tras las cuatro enmiendas del plan del mismo día (§9.3) |
| 2026-08-14 | **Resolución de los siete GAPs.** Los cinco bloqueos previos quedan cerrados; `T1` se cierra sin PR (el dueño del repositorio resolvió `GAP-1` en el commit `9f24956`, build verificado en verde) y `T2` pasa a ser la cabeza del camino crítico. Se añade `EXT-8` (autorizar del lado de Jack, riesgo R9) y las externas quedan con estado. Total: 42 puntos en 11 tareas |
| 2026-08-21 | **Revisión de QA sobre el PR de T6.** Descriptions de los DTOs **en inglés**, **sin archivo `{X}Mapping.cs`** (mapeo inline en el caso de uso) y sin el comentario del catálogo vacío. Las dos primeras son reglas del contexto: **T7–T10 se escriben así desde el arranque** (plan §3.1 y §5.6). Sin cambios de comportamiento; los 4 tests siguen en verde |
| 2026-08-21 | **T6 ejecutada** (`F3.1`, `F3.6`, Juan Esteban) en paralelo a T5, de la que no depende: el listado paginado y filtrado con sus cinco archivos coubicados y sus 4 tests en verde. Añade la `ProjectReference` a `LossReason.Application` que §3 dejaba pendiente para la primera de T6–T10, así que **T7–T10 ya no tocan `UnitTests.csproj`**. Entregada como PR contra la base, sin merge directo |
| 2026-08-21 | **T4 mergeada y validada.** Build limpio y 381 tests unitarios en verde. De la validación salen tres resoluciones del tech lead: los nombres de entidad en inglés son la convención; los unitarios del repositorio con EF InMemory se quedan (la puerta de cobertura de GitHub exige >90 % y solo cuenta unit tests); y **D6 se mantiene en el código pero se reescribe en el plan**, porque su motivo declarado era falso: la BD **sí** admite NULL, y tratar las columnas como obligatorias es una decisión técnica de integridad, no un reflejo del esquema. Se abre **R10** y la externa **`EXT-9`** (contar y limpiar nulos antes del corte) |
| 2026-08-21 | **Revisión de QA sobre T3**, aplicada en `feat/loss-reasons-domain`: el agregado deja de narrar el `IDENTITY` en un comentario, `Create` deja de pasar el `Id` (constructor privado partido en dos) y `Created()` deja de fijar `UpdatedAt`. Los 11 tests siguen en verde con un assert cambiado. El plan queda enmendado en §5.2 y F1.3 (§9.3) |
| 2026-08-21 | **T3 ejecutada** (`F1.1`–`F1.6` + `F2.5`, Juan Esteban): catálogo de errores, Args, agregado con sus dos invariantes de `Name`, filtro, contrato de repositorio y puerto del Reader, con los 11 tests del dominio en verde (355 en la suite). Commit `3500688` en `feat/loss-reasons-domain`. **La Fase 1 del plan queda `done`.** Se descubre un archivo compartido que §3 no declaraba, `tests/UnitTests/UnitTests.csproj`, y se agrega con su dueño |
| 2026-08-21 | **Rama base del contexto.** Se crea `feat/loss-reasons` desde `main`; toda rama de tarea sale de ella y su PR va contra ella, y `main` recibe el contexto una sola vez al final (§0, *Modelo de ramas*). La columna `Base` pasa de `main` a `feat/loss-reasons` en las once tareas |
| 2026-08-21 | **T2 ejecutada** (`F0.3`, Juan Esteban): `LossReason.Domain` y `LossReason.Application` creados y registrados en `Service.slnx` bajo `/src/Contexts/LossReason/`. `dotnet build Service.slnx -c Release` en verde (13 proyectos, 0 advertencias) y 344 tests unitarios en verde por el pre-commit. Commit `96915cb` en `feat/loss-reasons-scaffold`, pendiente de merge a la base |
| 2026-08-21 | **Se elimina Jira del proceso.** La columna `Jira` se reemplaza por `Responsable` y se añade `Estado`: el backlog es este archivo y el tablero son los PRs (§0). **Reparto entre Juan Camilo, Brayan y Juan Esteban** (§2.2), con las seis olas de ejecución y sus esperas (§2.3), el camino crítico anotado por persona (§2.4) y el round-robin de revisión (§2.5). Los archivos compartidos quedan con dueño y orden de merge (§3). Se declara y se descarta la aceleración de solapar la ola 4 con la ola 3, por requerir enmienda del plan. Ningún paso, alcance ni estimación cambia |
