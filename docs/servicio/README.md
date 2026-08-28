# Documentación del servicio (placeholder)

Esta carpeta es un placeholder. En la plantilla no contiene nada real porque no hay un servicio de negocio detrás — existe para que todo fork nazca con la misma convención de dos carpetas (`docs/plantilla/` vs `docs/servicio/`) y no tenga que inventarla después.

Al inicializar un servicio nuevo a partir de esta plantilla, reemplaza este archivo y agrega aquí (no en `docs/plantilla/`):

- **Documentación funcional** — qué hace el servicio, modelo de negocio, actores, reglas, alcance (qué hace y qué no).
- **Despliegue** — datos reales de AWS para este servicio: cuenta, cluster EKS, repositorio ECR, rutas de Secrets Manager, ingress. El mecanismo genérico ya está en [`../plantilla/variables-entorno.md`](../plantilla/variables-entorno.md); aquí solo van los valores concretos.
- **Decisiones de diseño / ADRs específicas del servicio** (carpeta `decisiones/`, opcional).
- **Notas de revisiones de PR relevantes** (carpeta `revisiones-pr/`, opcional).

Ver `Announcements-service` (`Q10-Software/service-template-dotnet` fork) como ejemplo de esta carpeta ya completa.

## Documentos de este servicio

- [activities.md](activities.md) — el contexto `Activities`: qué expone, sus reglas, su procedencia legada y qué falta antes de cortar tráfico.
- [working-plan-actividades.md](working-plan-actividades.md) — plan de trabajo del contexto `Activities` (decisiones DEC-1…DEC-10, mapeo legado→modelo, contratos, fases F0–F3, riesgos y GAPs). Guía de ejecución para el frente API-first del strangler.
