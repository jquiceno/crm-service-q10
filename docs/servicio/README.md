# Documentación del servicio — `crm-service-q10`

Documentación **funcional y de ejecución** de este servicio. Los patrones técnicos de la plantilla no van aquí, sino en [`../plantilla/`](../plantilla/), que es el contrato con `service-template-dotnet` y **no se modifica** en el fork.

## Contexto en construcción: Causas de pérdida (`LossReason`)

Migración del catálogo de causas de pérdida del CRM (área `GestionComercial` del monolito Jack, tabla `tbl_opo_causas`) a este servicio.

| Documento | Qué es | Quién lo consulta |
|---|---|---|
| [`workplan_causas.md`](workplan_causas.md) | **El plan.** 14 decisiones firmadas (D1–D14), el mapeo legado → modelo, los contratos de API y los 33 pasos ejecutables de §8, cada uno con su tarea, su responsable y su comando de verificación | Quien va a escribir código: §0 y el encabezado de su fase, **antes** de tocar nada |
| [`tasks_causas.md`](tasks_causas.md) | **El backlog.** 11 tareas repartidas entre las tres personas, con ramas, dependencias, olas de ejecución y archivos compartidos | Quien quiere saber qué le toca y qué está esperando a qué |

El **paso** (`workplan_causas.md` §8) es la unidad de trabajo; la **tarea** (`tasks_causas.md`) es la unidad de PR y revisión.

## Cómo trabaja el equipo en este contexto

* **Sin Jira.** El backlog es `tasks_causas.md` y el tablero son los PRs. El estado de la tarea vive en su columna `Estado`; el del paso, en el campo `estado:` del plan, y se cambia **solo después** de correr el `Verificar:` de ese paso.
* **Rama base del contexto: `feat/loss-reasons`.** Toda rama de tarea sale de ella y su PR va contra ella; `main` recibe el contexto una sola vez, al final. Detalle en `tasks_causas.md` §0.
* **Si la realidad del repositorio contradice el plan, hay que detenerse y reportarlo** (regla 4 de §0 del plan), no improvisar ni completar por cuenta propia.

El Discovery del legado (`discovery_causas.md`) y el handoff de sesión no se publican aquí: viven fuera del repositorio. `03-flujos.md` —integración, criterios de QA, cutover y rollback— está pendiente y es lo que fijará los identificadores de flujo definitivos.

## Pendiente de escribir

- **Despliegue** — valores reales de AWS para este servicio (cuenta, cluster EKS, repositorio ECR, rutas de Secrets Manager, ingress). El mecanismo genérico ya está en [`../plantilla/variables-entorno.md`](../plantilla/variables-entorno.md); aquí solo van los valores concretos.
- **Documentación funcional del servicio completo**, más allá del contexto en construcción.
