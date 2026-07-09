# Documentación — dotnet-service-template

La documentación está dividida en dos carpetas con propósitos distintos:

| Carpeta | Responde a | Público |
|---|---|---|
| [plantilla/](plantilla/README.md) | *¿Cómo está construida esta plantilla y cómo extiendo el código?* | Desarrolladores de cualquier servicio creado a partir de este template |
| [servicio/](servicio/README.md) | *¿Qué hace el servicio concreto forkeado de esta plantilla y cómo se opera?* | Cada servicio real llena esta carpeta con su propia doc funcional/operativa |

Este repo es la **plantilla**: no tiene un servicio de negocio real detrás, así que `docs/servicio/` aquí es solo un placeholder que explica la convención — cada fork la reemplaza con su propio contenido (ver [servicio/README.md](servicio/README.md)).

Al forkear este repo para crear un servicio nuevo:

1. Ejecuta `./init-service.sh` (personaliza `k8s/`, `terraform/` y nombres del servicio).
2. Reemplaza el contenido de `docs/servicio/` con la documentación funcional real del nuevo servicio.
3. Deja `docs/plantilla/` tal cual — es el contrato con la plantilla. Si necesitas corregir o mejorar algo genérico ahí, considera aportarlo de vuelta a este repo (`service-template-dotnet`) en lugar de solo parchearlo en el fork, para que todos los servicios se beneficien.
