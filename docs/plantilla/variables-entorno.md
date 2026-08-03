# Manejo de Variables de Entorno

## Modelo de capas

```
┌─────────────────────────────────────────────────────────────┐
│  Capa 1: appsettings.json           (defaults en código)    │
│  Capa 2: appsettings.{Env}.json     (overrides locales)     │
│  Capa 3: ConfigMap de Kubernetes    (no-sensibles, por env) │
│  Capa 4: Secret de Kubernetes       (sensibles, via ESO)    │
└─────────────────────────────────────────────────────────────┘
```

ASP.NET Core aplica las capas en orden: cada capa siguiente sobreescribe la anterior. Las capas 3 y 4 se inyectan como variables de entorno en el pod.


---

## Qué va en cada capa

### ConfigMap (no sensible) — `k8s/base/configmap.yaml`

Las columnas son los **ambientes desplegados** (dev/qa/prod de Kubernetes), no la máquina del
desarrollador: en local la multitenencia arranca apagada — ver
[Variables de entorno locales](#variables-de-entorno-locales-desarrollo-en-máquina).

| Variable | Ejemplo dev | Ejemplo qa | Ejemplo prod |
|----------|-------------|------------|--------------|
| `ASPNETCORE_ENVIRONMENT` | `Development` | `Staging`  | `Production` |
| `ASPNETCORE_URLS` | `http://+:8080` | `http://+:8080` | `http://+:8080` |
| `ServiceInfo__Name` | `ServiceTemplate` | `ServiceTemplate` | `ServiceTemplate` |
| `TenantResolverService__Enabled` | `true`      | `true`     | `true`       |
| `TenantResolverService__TimeoutSeconds` | `15`        | `15`       | `15`         |
| `TenantResolverService__CacheTtlMinutes` | `10`        | `10`       | `10`         |
| `Cache__L2Enabled` | `true`      | `true`     | `true`       |
| `Sentry__Enabled` | `false`     | `true`     | `true`       |
| `Sentry__TracesSampleRate` | `0.2`       | `0.2`      | `0.2`        |
| `Sentry__MinimumEventLevel` | `Error`     | `Error`    | `Error`      |
| `Sentry__MinimumBreadcrumbLevel` | `Warning`   | `Warning`  | `Warning`    |
| `Sentry__DeniedHeaders` | (lista de headers sensibles) | (idem) | (idem) |
| `Cors__AllowedOrigins__0` | `http://localhost:3000` | `https://qa.app.q10.com` | `https://app.q10.com` |

Regla: **si el valor puede exponerse en git, va en ConfigMap.**

### Secret (sensible) — AWS Secrets Manager vía External Secrets Operator

| Clave en Secrets Manager | Descripción |
|--------------------------|-------------|
| _(claves propias del servicio)_ | Credenciales/tokens exclusivos de este servicio |

> El DSN de Sentry **no** va acá: es una clave compartida por toda la plataforma y
> vive en el secreto compartido como `SENTRY_DSN`
> (ver [Secretos compartidos](#secretos-compartidos-platform-shared)).

> El connection string de la base **no** va acá. Con la multitenencia activa lo entrega
> el tenant-resolver cifrado por petición, y se descifra con `CONNSTRING_ENCRYPTION_KEY`
> del secreto compartido (ver [Secretos compartidos](#secretos-compartidos-platform-shared)).

Rutas en AWS Secrets Manager:

```
/platform/dev/service-template    → JSON con las claves sensibles para dev
/platform/qa/service-template     → JSON con las claves sensibles para qa
/platform/prod/service-template   → JSON con las claves sensibles para prod
```

> **Importante:** el `extract.key` del ExternalSecret debe coincidir
> **exactamente** con el nombre real del secreto en Secrets Manager, slash
> inicial incluido. Existen secretos legados creados **sin** slash inicial
> (p. ej. `platform/dev/announcements-service`); verifica el nombre real con
> `aws secretsmanager list-secrets` antes de configurar los overlays.

Formato del secreto (JSON):

```json
{
  "Mi__ClavePropia": "valor-sensible-del-servicio"
}
```

Regla: **si el valor es una credencial, token, DSN o connection string, va en Secrets Manager.**


---

## Secretos compartidos (platform-shared)

Además del secreto propio del servicio, existe un Secret **`platform-shared`** que la
plataforma inyecta automáticamente en cada namespace de servicio. Contiene la
configuración común a **todos** los servicios de la plataforma, con una **fuente única**.

### Qué es

Un `ClusterExternalSecret` de External Secrets Operator, gestionado a nivel cluster por
el equipo de plataforma, materializa el Secret `platform-shared` en los namespaces
elegibles. Su origen es un único secreto en AWS Secrets Manager:

```
/platform/{env}/shared    → JSON con la configuración compartida por todos los servicios
```

### Qué claves contiene hoy

| Clave | Descripción |
|-------|-------------|
| `CONNSTRING_ENCRYPTION_KEY` | Clave de cifrado de connection strings compartida por la plataforma |
| `TENANT_RESOLVER_SERVICE_URL` | URL del servicio de resolución de tenants |
| `SENTRY_DSN` | DSN de Sentry para error tracking (mapeada a `Sentry:Dsn` en el arranque) |
| `Cache__ConnectionString`   | Host del Redis compartido (ElastiCache) |

### Cómo lo recibe el servicio

Es **automático** — el servicio no declara nada en su propio kustomization:

1. El namespace del template ya trae el label `q10.com/platform: "true"`
   (`k8s/base/namespace.yaml`). Ese label es lo que hace que el
   `ClusterExternalSecret` materialice `platform-shared` en el namespace del servicio.
2. El deployment del template ya incluye `envFrom: secretRef: platform-shared`
   (`k8s/base/deployment.yaml`), de modo que todas las claves entran como variables de
   entorno del pod.

> No agregues `platform-shared` al `kustomization.yaml` del servicio ni crees un
> ExternalSecret para él: viene del cluster.

### Precedencia

En el `envFrom` del deployment, `platform-shared` va **antes** que el secreto propio del
servicio (`service-template-secrets`). Como en .NET la última fuente gana, el servicio
**puede sobreescribir** una clave compartida si la define en su propio secreto. En el
caso normal no se sobreescribe nada: `platform-shared` actúa como base común.

### Rotación

El valor se cambia **una sola vez** en `/platform/{env}/shared`. A partir de ahí:

1. External Secrets re-sincroniza `platform-shared` en todos los namespaces.
2. Reloader reinicia el pod automáticamente, gracias a la anotación
   `reloader.stakater.com/auto: "true"` del deployment (ya incluida en el template).

No hay que tocar ningún archivo del servicio ni hacer redeploy manual.


---

## Estructura de archivos Kubernetes

```
k8s/
├── base/
│   ├── kustomization.yaml
│   ├── namespace.yaml
│   ├── configmap.yaml              # Valores por defecto (producción)
│   ├── external-secret.yaml        # Lee de Secrets Manager → crea k8s Secret
│   ├── deployment.yaml             # 2 réplicas, resources, probes
│   ├── service.yaml                # ClusterIP en puerto 80 → 8080
│   └── hpa.yaml                    # CPU 70%, Memory 80%, max 10 réplicas
└── overlays/
    ├── dev/kustomization.yaml      # 1 réplica, min HPA 1, tag dev-latest
    ├── qa/kustomization.yaml       # 2 réplicas, min HPA 2, tag qa-latest
    └── prod/kustomization.yaml     # 2 réplicas base, min HPA 2, max 10
```

Cada overlay sobreescribe mediante **patches estratégicos**:

* Valores del ConfigMap (solo las claves que cambian)
* Ruta del secreto en Secrets Manager
* `replicas` del Deployment
* `minReplicas`/`maxReplicas` del HPA
* Tag de la imagen (seteado por el pipeline de CD)


---

## Deploy manual desde la máquina local

> Los valores concretos de cada servicio forkeado (nombre de cluster, rol de despliegue, contexto de kubectl, etc.) deben documentarse en su propio `docs/servicio/despliegue.md`. Esta sección solo describe el mecanismo genérico.

### Prerrequisitos

El cluster EKS es privado (sin endpoint público). El acceso desde una máquina local requiere un túnel SSM port-forward hacia el bastion antes de poder usar `kubectl`:

```powershell
aws ssm start-session `
  --target <bastion-instance-id> `
  --document-name AWS-StartPortForwardingSessionToRemoteHost `
  --parameters host=<endpoint-del-cluster-eks>,portNumber=443,localPortNumber=6443 `
  --region <region> --profile <perfil-aws>
```

El bastion se resuelve dinámicamente por tag (`Name=q10-*-ssm-bastion`), no por un ID fijo — ver la plantilla de Terraform de IAM.

### Aplicar overlays con Kustomize

```powershell
# Verificar qué va a aplicarse (dry-run)
kubectl kustomize k8s/overlays/{env}

# Aplicar al cluster
kubectl apply -k k8s/overlays/{env} --context {contexto-kubectl}

# Verificar rollout
kubectl rollout status deployment/{servicio} -n {servicio} --context {contexto-kubectl}

# Ver pods
kubectl get pods -n {servicio} --context {contexto-kubectl}

# Ver variables de entorno inyectadas en un pod
kubectl exec -n {servicio} deploy/{servicio} --context {contexto-kubectl} \
  -- env | sort
```

### Verificar que el ClusterSecretStore esté listo (lo gestiona el equipo de plataforma)

```powershell
# Gestionado centralmente por el repo de infraestructura del cluster.
# Solo verificar que exista antes del primer deploy:
kubectl get clustersecretstore aws-secrets-manager --context {contexto-kubectl}
```


---

## CI/CD en cluster privado

El cluster EKS no tiene endpoint público. El job `deploy` del pipeline resuelve esto abriendo un túnel SSM port-forward a través del bastion antes de ejecutar `kubectl`. No se requiere self-hosted runner.

### Variables requeridas en GitHub (Settings → Environments)

| Variable | Descripción |
|----------|-------------|
| `IMAGE_NAME` | URI completo del repositorio ECR |
| `AWS_DEPLOY_ROLE_ARN` | ARN del rol IAM con permisos de ECR + EKS + SSM |

El rol de deploy necesita: `ecr:*`, `eks:DescribeCluster`, `eks:GetToken`, `ssm:StartSession`, `ssm:TerminateSession`, `ssm:ResumeSession`. El bastion se resuelve por tag en tiempo de `apply`, no requiere una variable con su ID.

### Deploy manual (sin pipeline)

Con el túnel SSM activo (ver sección anterior), ejecutar localmente:

```powershell
kubectl apply -k k8s/overlays/{env} --context {contexto-kubectl}
```


---

## Agregar una nueva variable de entorno

### Si es NO sensible (feature flag, URL, configuración):


1. Agregar al `k8s/base/configmap.yaml` con el valor de producción por defecto
2. Si el valor cambia por ambiente, parchear en el `kustomization.yaml` del overlay correspondiente:

   ```yaml
   patches:
     - patch: |-
         apiVersion: v1
         kind: ConfigMap
         metadata:
           name: service-template-config
         data:
           Mi__NuevaVariable: "valor-dev"
   ```

### Si es SENSIBLE (credencial, token, DSN):


1. Agregar el par clave-valor al secreto en **cada** ambiente de AWS Secrets Manager:

   ```bash
   # Actualizar el secreto existente (no crear nuevo)
   aws secretsmanager put-secret-value \
     --secret-id /platform/dev/service-template \
     --secret-string '{"Sentry__Dsn":"...","Mi__NuevaClave":"valor"}' \
     --profile informes-staging
   ```
2. El ExternalSecret lo sincronizará automáticamente en ≤1h (o forzar con `kubectl annotate`).
3. **No modificar ningún archivo de Kubernetes** — el ExternalSecret usa `dataFrom.extract` que importa todas las claves del secreto automáticamente.


---

## Variables de entorno locales (desarrollo en máquina)

Para desarrollo local usar `appsettings.Development.json` o `user-secrets`.

Ningún `.csproj` de la plantilla declara un `UserSecretsId`, así que **la primera vez hay que
inicializarlo** o `dotnet user-secrets set` falla con «Could not find the global property
'UserSecretsId'»:

```bash
dotnet user-secrets init --project src/Api
dotnet user-secrets set --project src/Api "Sentry__Dsn" "https://..."
```

Por defecto la multitenencia queda **apagada** en local (`TenantResolverService:Enabled=false`
en `appsettings.json`), y el servicio arranca con base en memoria. Para probar contra un
tenant-resolver real hacen falta **cuatro** cosas además del toggle, y el arranque aborta con un
error explícito si falta cualquiera: la URL del resolver, la clave de cifrado, y la caché L2
prendida **con** su connection string.

```bash
dotnet user-secrets set --project src/Api "TenantResolverService:Enabled" "true"
dotnet user-secrets set --project src/Api "TENANT_RESOLVER_SERVICE_URL" "http://tenant-resolver.interno"
dotnet user-secrets set --project src/Api "CONNSTRING_ENCRYPTION_KEY" "<clave compartida de plataforma>"
dotnet user-secrets set --project src/Api "Cache:L2Enabled" "true"
dotnet user-secrets set --project src/Api "Cache:ConnectionString" "localhost:6379"
```

**Nunca** commitear archivos `.env`, `appsettings.local.json` con credenciales, ni agregar secrets hardcodeados en manifests de Kubernetes.


---

## Variables requeridas en GitHub (por ambiente)

### Variables (`vars.*`) — en Settings → Environments

| Variable | Descripción |
|----------|-------------|
| `IMAGE_NAME` | `<account-id>.dkr.ecr.<region>.amazonaws.com/q10-{servicio}` |
| `AWS_DEPLOY_ROLE_ARN` | `arn:aws:iam::<account-id>:role/q10-{servicio}-github-deploy` |

> Valores reales de cada servicio en su propio `docs/servicio/despliegue.md`.

El rol `q10-{servicio}-github-deploy` debe tener permisos de ECR push y EKS deploy — se crea vía el `terraform/` de este mismo repo (ver `bootstrap.yml`).


---

## Referencia de convenciones de nombres

| Recurso | Convención | Ejemplo |
|---------|------------|---------|
| ConfigMap | `{servicio}-config` | `service-template-config` |
| ExternalSecret | `{servicio}-secrets` | `service-template-secrets` |
| Secret generado | igual que ExternalSecret | `service-template-secrets` |
| Secreto en AWS SM | `/platform/{env}/{servicio}` | `/platform/dev/service-template` |
| Namespace | `{servicio}` | `service-template` |
| Deployment | `{servicio}` | `service-template` |
| HPA     | `{servicio}` | `service-template` |
| ECR repo | `q10-{servicio}` | `q10-service-template` |
