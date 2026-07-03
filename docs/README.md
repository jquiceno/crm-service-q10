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
- [puertos-y-adaptadores.md](puertos-y-adaptadores.md) — nomenclatura de puertos, adaptadores y extensiones DI
- [estandares-codigo.md](estandares-codigo.md) — convenciones C#, herramientas de análisis, modelo de severidad

### Dominio y aplicación

- [patron-result.md](patron-result.md) — tipos `Result<T>`, conversiones implícitas, patrón de uso en use cases
- [errores-dominio.md](errores-dominio.md) — `DomainError`, `ValidationError`, `ErrorType`, cómo definir errores por contexto
- [entidades-y-agregados.md](entidades-y-agregados.md) — `Entity<TId>`, `AggregateRoot<TEntity, TId>`, auditoría, relación entidad-agregado
- [value-objects.md](value-objects.md) — cuándo crear un VO, anatomía, igualdad estructural
- [validaciones.md](validaciones.md) — mapa de las cinco capas de validación y dónde vive cada una
- [repositorio.md](repositorio.md) — `IRepositoryBase`, `BaseAggregateRepository`, Unit of Work, paginación

### API y presentación

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

- [guias/nuevo-contexto.md](guias/nuevo-contexto.md) — flujo completo para un bounded context nuevo (dominio → aplicación → infraestructura → API)
- [guias/nueva-entidad-dominio.md](guias/nueva-entidad-dominio.md) — modelar entidad, aggregate root y value objects de un contexto
- [guias/nuevo-caso-de-uso.md](guias/nuevo-caso-de-uso.md) — agregar un caso de uso a un contexto existente

---

## Lectura recomendada para un desarrollador nuevo

1. **[arquitectura.md](arquitectura.md)** — entiende la estructura general antes de tocar código
2. **[puertos-y-adaptadores.md](puertos-y-adaptadores.md)** — aprende la nomenclatura que verás en todo el código
3. **[patron-result.md](patron-result.md)** + **[errores-dominio.md](errores-dominio.md)** — el patrón transversal más importante de la plantilla
4. **[guias/nuevo-contexto.md](guias/nuevo-contexto.md)** — implementa tu primer contexto de principio a fin
5. El resto según la funcionalidad que vayas a tocar
