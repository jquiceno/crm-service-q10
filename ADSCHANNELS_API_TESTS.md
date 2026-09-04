# AdsChannels API — curls de prueba

Base local: `http://localhost:58483` (puerto de `src/Api/Properties/launchSettings.json`; ajústalo
si corres con `run-dev.ps1`, ahí es `http://localhost:5080`).

Todas las requests requieren el tenant, vía header `X-Entity-Code` (o query param `EntityCode`).
Reemplaza `<ENTITY_CODE>` por el código real.

## Listar (GET /ads-channels)

```bash
# Sin filtros
curl -s -H "X-Entity-Code: <ENTITY_CODE>" http://localhost:58483/ads-channels

# Filtro por nombre (contains, case-insensitive)
curl -s -H "X-Entity-Code: <ENTITY_CODE>" "http://localhost:58483/ads-channels?nameContains=Web"

# Filtro por estado activo
curl -s -H "X-Entity-Code: <ENTITY_CODE>" "http://localhost:58483/ads-channels?isActive=false"

# Paginación
curl -s -H "X-Entity-Code: <ENTITY_CODE>" "http://localhost:58483/ads-channels?pageIndex=0&pageSize=1"

# 400 — pageSize fuera de rango (1-100)
curl -s -H "X-Entity-Code: <ENTITY_CODE>" "http://localhost:58483/ads-channels?pageSize=101"
```

## Obtener por id (GET /ads-channels/{id})

```bash
# 200 — existente
curl -s -H "X-Entity-Code: <ENTITY_CODE>" http://localhost:58483/ads-channels/1

# 404 — inexistente
curl -s -H "X-Entity-Code: <ENTITY_CODE>" http://localhost:58483/ads-channels/999999
```

## Crear (POST /ads-channels)

```bash
# 201 — creación válida
curl -s -X POST -H "X-Entity-Code: <ENTITY_CODE>" -H "Content-Type: application/json" \
  -d '{"name":"Google Ads","isActive":true}' \
  http://localhost:58483/ads-channels

# 409 — nombre duplicado
curl -s -X POST -H "X-Entity-Code: <ENTITY_CODE>" -H "Content-Type: application/json" \
  -d '{"name":"Google Ads","isActive":true}' \
  http://localhost:58483/ads-channels

# 400 — nombre vacío
curl -s -X POST -H "X-Entity-Code: <ENTITY_CODE>" -H "Content-Type: application/json" \
  -d '{"name":"","isActive":true}' \
  http://localhost:58483/ads-channels
```

## Editar (PUT /ads-channels/{id})

```bash
# 200 — edición válida
curl -s -X PUT -H "X-Entity-Code: <ENTITY_CODE>" -H "Content-Type: application/json" \
  -d '{"name":"Google Ads Renombrado","isActive":false}' \
  http://localhost:58483/ads-channels/1

# 404 — id inexistente
curl -s -X PUT -H "X-Entity-Code: <ENTITY_CODE>" -H "Content-Type: application/json" \
  -d '{"name":"Cualquiera","isActive":true}' \
  http://localhost:58483/ads-channels/999999

# 409 — nombre duplicado (de otro registro existente)
curl -s -X PUT -H "X-Entity-Code: <ENTITY_CODE>" -H "Content-Type: application/json" \
  -d '{"name":"<nombre de otro canal existente>","isActive":true}' \
  http://localhost:58483/ads-channels/1

# 400 — nombre vacío
curl -s -X PUT -H "X-Entity-Code: <ENTITY_CODE>" -H "Content-Type: application/json" \
  -d '{"name":"","isActive":true}' \
  http://localhost:58483/ads-channels/1
```

## Eliminar (DELETE /ads-channels/{id})

```bash
# 204 — eliminación válida
curl -s -X DELETE -H "X-Entity-Code: <ENTITY_CODE>" http://localhost:58483/ads-channels/1

# 404 — id inexistente
curl -s -X DELETE -H "X-Entity-Code: <ENTITY_CODE>" http://localhost:58483/ads-channels/999999

# 409 — canal referenciado por una Oportunidad (FK)
curl -s -X DELETE -H "X-Entity-Code: <ENTITY_CODE>" http://localhost:58483/ads-channels/<id_referenciado>
```

## Otros

```bash
# Readiness (incluye el health check del tenant resolver)
curl -s http://localhost:58483/health/ready

# Liveness
curl -s http://localhost:58483/health/live

# Especificación OpenAPI (importable en Postman: Import > File)
curl -s http://localhost:58483/openapi/v1.json -o openapi.json
```

## Notas de las pruebas realizadas en esta sesión

- Confirmado end-to-end contra un tenant real: List, GetById, filtros y paginación devuelven 200
  con datos reales; validaciones (400/404) responden con el envelope de error esperado
  (`{"error":{"type","code","message","details"},"statusCode"}`).
- **Create/Update/Delete no se pudieron probar de punta a punta** contra el tenant
  `836534535062` (BD `zudbzq10desarrollopagosregulares`): el usuario SQL que devuelve el resolver
  no tiene permiso `INSERT` sobre `tbl_opo_medios_publicitarios`. El código respondió
  correctamente con un 500 `INTERNAL` bien clasificado (no hubo crash) — es un bloqueo de permisos
  de infraestructura, no un bug de aplicación.
- Si Redis (`Cache:ConnectionString`, por defecto `localhost:6379`) no está disponible, cada
  request paga varios segundos de timeout de reconexión (L2 cache + output cache); la API sigue
  respondiendo correctamente pero de forma más lenta. Levanta Redis localmente para pruebas ágiles.
- Hallazgo pendiente de corregir (no se tocó código, solo se documentó): con Redis caído,
  `OutputCacheInvalidateAttribute.OnActionExecutionAsync`
  (`src/Shared/Infrastructure/Presentation/Filters/OutputCacheInvalidateAttribute.cs:39`) deja
  escapar una `RedisConnectionException` sin capturarla al invalidar el output-cache por tag, a
  diferencia de `RedisCacheStore`, que sí maneja sus propios fallos de conexión con un log de
  warning. Afecta un archivo compartido de la plantilla, no específico de AdsChannel.
