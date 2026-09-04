---
service: crm-service-q10
doc: runbook
status: approved
updated: 2026-09-04
---

# Correr el servicio en local

Desde que la multitenencia es obligatoria (`feat(startup)!: require multitenancy and drop the
in-memory database`), **`dotnet run` no arranca solo**. El servicio aborta el arranque si le falta
cualquiera de estas cuatro cosas, y lo dice con un mensaje explícito:

| Requisito | De dónde sale en local |
|---|---|
| `TenantResolverService:Enabled = true` | `appsettings.Development.json` (ya está) |
| `TenantResolverService:BaseUrl` | `appsettings.Development.json` → `http://localhost:8443/tenants/` |
| `TenantResolverService:EncryptionKey` | **`user-secrets`** — es un secreto, no va en el repo |
| `Cache:L2Enabled` + `Cache:ConnectionString` | `appsettings.Development.json` → `localhost:6379` |

**Sí hace falta SQL Server en local.** La base de datos es la que diga la cadena de conexión que
devuelva el resolver para `?clientEnv=local`, y esa cadena apunta a la propia máquina — de ahí el
nombre del `clientEnv`. Verificado el 2026-09-04 contra el resolver real, para el tenant
`641690275906`:

| Campo de la cadena | Valor |
|---|---|
| `Server` | `tcp:127.0.0.1,1433` |
| `Initial Catalog` | `udbzq10trabajos` |
| `User ID` | `ClusterAWS` |
| `Encrypt` / `TrustServerCertificate` | `True` / `True` |

Es decir: **un SQL Server escuchando en `127.0.0.1:1433`, con el login `ClusterAWS` y la base
`udbzq10trabajos` restaurada**. Sin eso el servicio arranca, resuelve el tenant y desencripta la
cadena sin problema, pero cualquier lectura responde **500** con
`A persistence error occurred.` y en el log aparece el timeout de conexión de `Microsoft.Data.SqlClient`.

## Puesta a punto, una sola vez

```powershell
# 1. La clave de desencriptado, que nunca se commitea. Pídela al tech lead.
dotnet user-secrets set "TenantResolverService:EncryptionKey" "<la-clave>" --project src/Api

# 2. Redis, que es obligatorio con multitenencia encendida.
docker compose up -d redis
```

`user-secrets` guarda el valor en el perfil del usuario, fuera del repositorio. El `UserSecretsId`
está declarado en `src/Api/Api.csproj`, así que no hay que inicializarlo.

## Cada vez

```powershell
docker compose up -d redis          # si no está arriba
dotnet run --project src/Api
```

Los endpoints quedan bajo el prefijo de servicio: **`/crm-service/loss-reasons`**, no
`/loss-reasons`. El resolver necesita el tenant en cada petición, por header o por query:

```powershell
# por header
Invoke-RestMethod http://localhost:5199/crm-service/loss-reasons -Headers @{ 'X-Entity-Code' = '641690275906' }

# por query
Invoke-RestMethod 'http://localhost:5199/crm-service/loss-reasons?EntityCode=641690275906'
```

## `?clientEnv=local`

El resolver decide qué cadena de conexión entregar según ese parámetro, y `local` es la única que
una máquina de desarrollo puede alcanzar. Lo aporta la clave de configuración
`TenantResolverService:ClientEnv`, que el cliente HTTP añade a la petición:

```
GET http://localhost:8443/tenants/641690275906?clientEnv=local
```

**Vacío no manda el parámetro**, que es lo que hacen los entornos despliegados: el resolver responde
entonces con su cadena por defecto. Por eso la clave existe y no está escrita a fuego — con `local`
en el código, producción pediría la cadena de desarrollo.

`ClientEnv` **también entra en la llave de caché L2** (`ctx:masteraccess:v1:tenant-local:{code}` en
vez de `…:tenant:{code}`). Sin eso, dos procesos que compartan un mismo Redis bajo el mismo
`InstanceName` —una máquina de desarrollo y una instancia despliegada, por ejemplo— se servirían
mutuamente la cadena de conexión equivocada desde la caché.

## La sonda de arranque exige `/health` en el resolver

`TenantResolverStartupProbe` corre **antes de que Kestrel abra el puerto** y pide un 2xx a:

```
GET http://localhost:8443/tenants/health
```

Si eso no responde 2xx, el arranque aborta con
`Critical Error: the tenant resolver at '…' is not reachable`. Es deliberado —un servicio arriba sin
resolución de tenant se vería sano mientras pierde toda escritura—, pero significa que **el resolver
local tiene que exponer su `/health`**, no solo `/tenants/{code}`.

## Todo en contenedor

`docker compose up` levanta también la API. Ahí sí hace falta `.env`:

```powershell
Copy-Item .env.example .env         # y rellenar CONNSTRING_ENCRYPTION_KEY
docker compose up
```

`.env` está en `.gitignore`. `.env.example` no, así que **nunca debe tener la clave real**.

Dentro del contenedor, `localhost` es el contenedor, así que `docker-compose.yml` sobreescribe dos
valores: el resolver pasa a `http://host.docker.internal:8443/tenants/` y Redis a `redis:6379`.

## Diagnóstico

| Síntoma | Causa |
|---|---|
| `multitenancy … is off` | `TenantResolverService:Enabled` en `false`. Ya no existe modo single-tenant |
| `BaseUrl is missing or not a valid absolute URL` | falta la URL del resolver, o no es absoluta |
| `EncryptionKey is missing` | no corriste el `dotnet user-secrets set` |
| `the L2 application cache is off` | falta `Cache:ConnectionString`, o `L2Enabled` está en `false` |
| `the tenant resolver at '…/health' is not reachable` | el resolver no está arriba, o no expone `/health` |
| `Failed to decrypt the tenant connection string` | la clave no corresponde a la que cifró la cadena |
| 404 en `/loss-reasons` | falta el prefijo: es `/crm-service/loss-reasons` |
| 400 sin `X-Entity-Code` ni `?EntityCode=` | falta el tenant; no hay valor por defecto |
| 500 `A persistence error occurred.` | no hay SQL Server en `127.0.0.1:1433`, o le falta el login o la base. El arranque **no** lo detecta: la conexión se abre por petición, no al bootear |
