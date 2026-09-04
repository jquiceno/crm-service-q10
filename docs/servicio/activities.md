# Actividades (`Activities`)

La bitácora comercial de un negocio: lo que se planea hacer con un cliente y lo que ya se hizo.

Este contexto es el primer frente del *strangler* sobre `api/actividades` del monolito Jack. El
contrato en español se queda en el monolito, que traduce y delega aquí por institución; el servicio
expone su propio contrato en inglés.

- Plan de trabajo: [working-plan-actividades.md](working-plan-actividades.md)
- Convenciones de la plantilla: [`../plantilla/`](../plantilla/README.md)

## 1. Qué expone

| Endpoint | Caso de uso | Respuesta |
|---|---|---|
| `GET /activities` | `GetActivitiesUseCase` | `200` con `{ data: { items, totalCount }, statusCode }` |
| `POST /activities` | `CreateActivityUseCase` | `201` con `{ data: { id }, statusCode }` |

No hay `PUT`, `PATCH` ni `DELETE`: el API legado tampoco los tenía, y el frente MVC (editar,
anular, cerrar en masa, adjuntos, reunión virtual) sigue en el monolito.

### `GET /activities`

| Parámetro | Tipo | Obligatorio | Default | Regla |
|---|---|---|---|---|
| `DealId` | int | condicional | — | > 0 |
| `OpportunityId` | int | condicional | — | > 0 |
| `DealStateId` | int | condicional | — | > 0 |
| `PageIndex` | int | no | 0 | ≥ 0 |
| `PageSize` | int | no | 20 | 1–5000 |

Al menos uno de los tres filtros es obligatorio; sin ninguno responde `400`. Sin esa regla la
consulta listaría todas las actividades de la institución.

> El plan escribe estos parámetros en kebab-case (`deal-id`). El servicio publica el nombre de la
> propiedad tal cual (`DealId`), porque renombrarlos exigiría atributos de ASP.NET sobre los DTO de
> `Application`, que no conoce —ni debe conocer— la capa HTTP. El kebab-case del template aplica a
> los *tokens de ruta*, no a la query. El binding es insensible a mayúsculas, así que `?dealId=…`
> también funciona; la traducción hacia el contrato legado es del adaptador del monolito, igual que
> la de los nombres en español.

Una fila cuyo negocio —o cuya oportunidad— ya no existe **no** se devuelve: es el mismo `INNER JOIN`
doble del procedimiento legado. El asesor, en cambio, va por `LEFT JOIN`: la historia migrada no
tiene asesor y esas filas sí se devuelven, sin nombre. Quien registró la actividad
(`createdById`/`createdByName`) va por el mismo `LEFT JOIN`, por la misma razón: la persona referenciada
podría ya no existir.

### `POST /activities`

| Campo | Tipo | Obligatorio | Regla | Dónde se valida |
|---|---|---|---|---|
| `dealId` | int | sí | > 0; el negocio existe; su oportunidad no está archivada | API (forma) + Application (existencia) |
| `status` | string | sí | `scheduled` \| `completed` | API (presencia) + Application |
| `type` | string | sí | `call` \| `whatsapp` \| `email` \| `note` \| `meeting` | API (presencia) + Dominio |
| `advisorIdentification` | string | sí | ≤ 20; la persona existe | API (forma) + Application |
| `activityDate` | datetime | sí | fecha válida | API |
| `description` | string | condicional | obligatoria si `scheduled`, prohibida si `completed`; ≤ 500 | API (longitud) + Dominio |
| `outcome` | string | condicional | obligatorio si `completed`, prohibido si `scheduled`; ≤ 2000 | API (longitud) + Dominio |
| `outcomeType` | string | condicional | obligatorio si `completed` y el tipo es `call` o `meeting` | Dominio |
| `dueAt` | datetime | condicional | obligatoria si `scheduled` | Dominio |
| `createdByIdentification` | string | condicional | si no se manda, se usa el asesor; si se manda, la persona debe existir | Application (existencia) |

Reglas que conviene conocer antes de integrarse:

- **`virtual-meeting` se rechaza al escribir** pero se devuelve al leer: las filas históricas de
  reunión virtual conservan su tipo. Toda la cadena de aulas virtuales sigue en el monolito.
- **`note` no puede ser `scheduled`.**
- **`deal-closed` es un resultado normal y escribible**, tanto para llamada como para reunión.
- **`activityDate` se acepta y no se persiste como fecha de creación.** El procedimiento legado
  también ignoraba el valor del cliente; el `createdAt` real lo pone el reloj del servicio.
- **El rol del asesor no se valida aquí.** Es responsabilidad de quien llama: en fase 1, el
  adaptador del monolito conserva su verificación antes de delegar.
- **El servicio no escribe fuera de su tabla.** Ni `opo_fecha_ultimo_registro` ni la auditoría: el
  adaptador del monolito los sigue escribiendo, con su mecanismo actual.
- **Quien registra la actividad es información no verificada**, igual que `advisorIdentification`:
  el servicio no comprueba que quien llama realmente sea esa persona (autenticación, GAP-P10, sigue
  sin resolver).

### Errores

Forma única, la del template: `{ "error": { "type", "code", "message", "details" }, "statusCode" }`.

| Error | `type` | HTTP |
|---|---|---|
| `DealNotFound`, `AdvisorNotFound`, `CreatedByNotFound` | `NOT_FOUND` | 404 |
| Todo lo que rechaza el caso de uso o el agregado: `OpportunityArchived`, `InvalidActivityStatus`, `StatusNotCreatable`, `InvalidActivityType`, `TypeNotWritable`, `NoteCannotBeScheduled`, `DescriptionRequired`, `DueDateRequired`, `OutcomeRequired`, `OutcomeTypeRequired`, `OutcomeNotAllowedWhenScheduled`, … | `DOMAIN_VALIDATION` | 400 |
| Forma inválida del request y falta de filtro en el `GET` (los rechaza el filtro de validación, antes del caso de uso) | `VALIDATION` | 400 |
| Fallo de persistencia | `INTERNAL` | 500 |

`VALIDATION` y `DOMAIN_VALIDATION` responden ambos `400`; la diferencia es *cuándo* se rechazó: en
el borde HTTP, antes de ejecutar nada, o ya dentro del caso de uso. En los dos casos `details` trae
el campo ofensor, que es por donde conviene rutear la traducción al mensaje español.

## 2. Procedencia legada

El contexto persiste sobre la **base de datos de la institución**, sin migración de datos: la
tabla `tbl_opo_negocios_actividades` tal como existe hoy, mapeada explícitamente y solo en las
columnas que existen en todas las instituciones.

| Columna legada | Propiedad | Trampa |
|---|---|---|
| `negact_consecutivoP` | `Id` | único índice de la tabla |
| `negact_neg_consecutivo` | `DealId` | nullable en BD, obligatoria en el dominio |
| `negact_opo_consecutivo` | `OpportunityId` | se deriva del negocio; nunca se acepta como entrada |
| `negact_tipo` | `Type` | `'3'` y `'5'` son ambos "reunión"; el char vive solo en el converter |
| `negact_titulo` | `Description` | ⚠️ semántica invertida: la UI lo llama "descripción" |
| `negact_descripcion` | `Outcome` | ⚠️ invertida: la UI lo llama "resultado". `varchar(2000)` o `MAX` según la institución |
| `negact_resultado` | `OutcomeType` | su significado depende del tipo de la fila |
| `negact_completada` + `negact_anulada` | `Status` | `bit NULL`; `NULL` se lee como no completada / no anulada |
| `ConsecutivoActMiG` | — | **no se referencia jamás**: no existe en todas las instituciones |

El esquema difiere entre instituciones (columnas ausentes, tipos distintos, orden distinto). Ante
cualquier discrepancia, la regla es detenerse y reportar: ninguna base es "la" canónica.

## 3. Estado y pendientes

Fase 1 (paridad del API) en construcción. Lo que **falta** antes de cortar tráfico real:

1. **Autenticación.** Todos los endpoints deben exigir identidad, y hoy el servicio no tiene ningún
   esquema configurado. Falta además definir cómo se autentica el adaptador del monolito contra el
   servicio. Mientras tanto, lo único que mantiene estos endpoints privados es el despliegue.
2. **Reloj por institución.** Hoy el servicio sella en UTC; el legado graba en hora local de la
   institución. Falta el puerto de reloj con zona horaria y horario de verano.
3. **Adaptador con feature flag** en el monolito y **pruebas doradas de paridad** contra el
   comportamiento legado.
4. **Pruebas de integración contra dos variantes de esquema** reales, no una sola.

Divergencias deliberadas respecto del legado, ya decididas:

- El `POST` responde errores con la taxonomía del template, no con el `@MsgError VARCHAR(100)`
  legado. El adaptador del monolito traduce a los mensajes en español del contrato viejo.
- Los `PageIndex`/`PageSize` son los del template (base 0, default 20), no el `page` base 1 con
  default 30 del contrato legado. La traducción es del adaptador.
