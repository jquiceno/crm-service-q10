# Documentación de la plantilla

Guía de referencia para implementar servicios con `service-template-dotnet`.

Los documentos están organizados en dos tipos:

| Tipo | Prefijo | Responde a |
|------|---------|------------|
| Referencia | — | *¿Cómo funciona X en esta plantilla?* |
| Guía | `guias/` | *¿Cómo implemento X paso a paso?* |

---

## Referencia

### Arquitectura y estructura

- [arquitectura.md](arquitectura.md) — capas, regla de dependencias, estructura de carpetas
- [contextos.md](contextos.md) — qué es un bounded context, cuándo crear uno nuevo, y ejemplo completo de implementación
- [puertos-y-adaptadores.md](puertos-y-adaptadores.md) — qué son, driving vs. driven, y nomenclatura de puertos, adaptadores y extensiones DI
- [estandares-codigo.md](estandares-codigo.md) — convenciones C#, herramientas de análisis, modelo de severidad

### Dominio y aplicación

- [patron-result.md](patron-result.md) — tipos `Result<T>`, conversiones implícitas, patrón de uso en use cases
- [errores-dominio.md](errores-dominio.md) — `DomainError`, `ValidationError`, `ErrorType`, cómo definir errores por contexto
- [entidades-y-agregados.md](entidades-y-agregados.md) — `Entity<TId>`, `AggregateRoot<TId>`, auditoría
- [value-objects.md](value-objects.md) — cuándo crear un VO, anatomía, igualdad estructural
- [validaciones.md](validaciones.md) — mapa de las cinco capas de validación y dónde vive cada una
- [repositorio.md](repositorio.md) — `IRootRepository`, `RepositoryBaseEF`, Unit of Work, paginación
- [providers.md](providers.md) — cuándo extraer lógica auxiliar de un use case a un Provider
- [casos-de-uso.md](casos-de-uso.md) — qué es un caso de uso, su propósito, y patrones de implementación por tipo de operación (crear, actualizar, eliminar, consultar, relacionar) y cómo estructurarlo

### API y presentación

- [controllers.md](controllers.md) — qué es un controller, su propósito, y patrones de implementación por tipo de operación
- [contrato-api.md](contrato-api.md) — estructura uniforme de respuestas success/error
- [openapi.md](openapi.md) — generación de documentación OpenAPI, Swagger UI, buenas prácticas
- [cache.md](cache.md) — L1 (Output Caching, invalidación por tags, Redis vs. memoria) y L2 (cache-aside de aplicación, `ICacheStore`, `CacheKey`)

### Cross-cutting

- [logging.md](logging.md) — `ILoggerPort<T>`, Serilog, campos estructurados, bloque `http`
- [sentry.md](sentry.md) — error tracking, dónde vive el SDK, data scrubbing
- [testing.md](testing.md) — unit tests, integration tests con Testcontainers, stack y convenciones

### Operaciones

- [variables-entorno.md](variables-entorno.md) — capas de configuración, ConfigMap, Secrets Manager, Kubernetes
- [configuracion-startup.md](configuracion-startup.md) — validación fail-fast al arranque, Options Pattern

---

## Guías

- [guias/nueva-entidad-dominio.md](guias/nueva-entidad-dominio.md) — modelar entidad, aggregate root y value objects de un contexto

---

## Lectura recomendada para un desarrollador nuevo

1. **[arquitectura.md](arquitectura.md)** — entiende la estructura general antes de tocar código
2. **[puertos-y-adaptadores.md](puertos-y-adaptadores.md)** — entiende el patrón hexagonal y la nomenclatura que verás en todo el código
3. **[patron-result.md](patron-result.md)** + **[errores-dominio.md](errores-dominio.md)** — el patrón transversal más importante de la plantilla
4. **[contextos.md](contextos.md)** — implementa tu primer contexto de principio a fin
5. El resto según la funcionalidad que vayas a tocar
