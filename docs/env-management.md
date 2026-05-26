# Manejo de Variables de Entorno — service-template

## Modelo de capas

```
┌─────────────────────────────────────────────────────────────┐
│  Capa 1: appsettings.json           (defaults en código)    │
│  Capa 2: appsettings.{Env}.json     (overrides locales)     │
│  Capa 3: ConfigMap de Kubernetes    (no-sensibles, por env) │
│  Capa 4: Secret de Kubernetes       (sensibles, via ESO)    │
└─────────────────────────────────────────────────────────────┘
```

ASP.NET Core aplica las capas en orden: cada capa siguiente sobreescribe la anterior.
Las capas 3 y 4 se inyectan como variables de entorno en el pod.

---

## Qué va en cada capa

### ConfigMap (no sensible) — `k8s/base/configmap.yaml`

| Variable | Ejemplo dev | Ejemplo qa | Ejemplo prod |
|---|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` | `Staging` | `Production` |
| `ASPNETCORE_URLS` | `http://+:8080` | `http://+:8080` | `http://+:8080` |
| `AppInfo__ServiceName` | `ServiceTemplate` | `ServiceTemplate` | `ServiceTemplate` |
| `Persistence__Enabled` | `false` | `true` | `true` |
| `Sentry__Enabled` | `false` | `true` | `true` |
| `Cors__AllowedOrigins__0` | `http://localhost:3000` | `https://qa.app.q10.com` | `https://app.q10.com` |

Regla: **si el valor puede exponerse en git, va en ConfigMap.**

### Secret (sensible) — AWS Secrets Manager vía External Secrets Operator

| Clave en Secrets Manager | Descripción |
|---|---|
| `Persistence__ConnectionString` | Connection string SQL Server |
| `Sentry__Dsn` | DSN de Sentry para error tracking |

Rutas en AWS Secrets Manager:
```
/platform/dev/service-template    → JSON con las claves sensibles para dev
/platform/qa/service-template     → JSON con las claves sensibles para qa
/platform/prod/service-template   → JSON con las claves sensibles para prod
```

Formato del secreto (JSON):
```json
{
  "Persistence__ConnectionString": "Server=db.internal;Database=ServiceTemplate;User=app;Password=...",
  "Sentry__Dsn": "https://abc123@o123456.ingest.sentry.io/789"
}
```

Regla: **si el valor es una credencial, token, DSN o connection string, va en Secrets Manager.**

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
- Valores del ConfigMap (solo las claves que cambian)
- Ruta del secreto en Secrets Manager
- `replicas` del Deployment
- `minReplicas`/`maxReplicas` del HPA
- Tag de la imagen (seteado por el pipeline de CD)

---

## Deploy manual desde la máquina local

### Prerrequisitos
```powershell
# kubectl configurado con el contexto del cluster privado via SSM tunnel
# Ver docs/runbooks/kubectl-access.md en el repo cluster-infra
aws ssm start-session `
  --target i-03ac60c7f9681dc7f `
  --document-name AWS-StartPortForwardingSessionToRemoteHost `
  --parameters host=B2AF5D1FB8CA8F191DD8A3E4C60919A1.gr7.us-east-1.eks.amazonaws.com,portNumber=443,localPortNumber=6443 `
  --region us-east-1 --profile informes-staging
```

### Aplicar overlays con Kustomize
```powershell
# Verificar qué va a aplicarse (dry-run)
kubectl kustomize k8s/overlays/dev

# Aplicar al cluster de dev
kubectl apply -k k8s/overlays/dev --context q10-dev-eks-local

# Verificar rollout
kubectl rollout status deployment/service-template -n service-template --context q10-dev-eks-local

# Ver pods
kubectl get pods -n service-template --context q10-dev-eks-local

# Ver variables de entorno inyectadas en un pod
kubectl exec -n service-template deploy/service-template --context q10-dev-eks-local \
  -- env | sort
```

### Verificar que el ClusterSecretStore esté listo (lo gestiona el equipo de plataforma)
```powershell
# El ClusterSecretStore es gestionado por cluster-infra/environments/dev/addons/
# Solo verificar que exista antes del primer deploy:
kubectl get clustersecretstore aws-secrets-manager --context q10-dev-eks-local
```

---

## CI/CD en cluster privado

El cluster EKS no tiene endpoint público. El job `deploy` en `cd.yml` resuelve esto
abriendo un túnel SSM port-forward a través del bastion antes de ejecutar `kubectl`.
No se requiere self-hosted runner.

### Variables requeridas en GitHub (Settings → Environments)

| Variable | Descripción |
|---|---|
| `IMAGE_NAME` | URI completo del repositorio ECR |
| `AWS_DEPLOY_ROLE_ARN` | ARN del rol IAM con permisos de ECR + EKS + SSM |
| `BASTION_INSTANCE_ID` | ID de la instancia EC2 del bastion SSM (ej. `i-0abc...`) |

El rol de deploy necesita: `ecr:*`, `eks:DescribeCluster`, `eks:GetToken`,
`ssm:StartSession`, `ssm:TerminateSession`, `ssm:ResumeSession`.

### Deploy manual (sin pipeline)
Con el túnel SSM activo (ver sección anterior), ejecutar localmente:
```powershell
kubectl apply -k k8s/overlays/dev --context q10-dev-eks-local
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
     --secret-string '{"Persistence__ConnectionString":"...","Mi__NuevaClave":"valor"}' \
     --profile informes-staging
   ```
2. El ExternalSecret lo sincronizará automáticamente en ≤1h (o forzar con `kubectl annotate`).
3. **No modificar ningún archivo de Kubernetes** — el ExternalSecret usa `dataFrom.extract`
   que importa todas las claves del secreto automáticamente.

---

## Variables de entorno locales (desarrollo en máquina)

Para desarrollo local usar `appsettings.Development.json` o `user-secrets`:
```bash
dotnet user-secrets set "Persistence__ConnectionString" "Server=localhost;..."
dotnet user-secrets set "Sentry__Dsn" "https://..."
```

**Nunca** commitear archivos `.env`, `appsettings.local.json` con credenciales,
ni agregar secrets hardcodeados en manifests de Kubernetes.

---

## Variables requeridas en GitHub (por ambiente)

### Variables (`vars.*`) — en Settings → Environments
| Variable | dev | qa | prod |
|---|---|---|---|
| `IMAGE_NAME` | `764283926096.dkr.ecr.us-east-1.amazonaws.com/q10-service-template` | igual | igual |
| `AWS_DEPLOY_ROLE_ARN` | `arn:aws:iam::764283926096:role/q10-github-deploy` | igual | igual |

El rol `q10-github-deploy` debe tener permisos de ECR push y EKS deploy.
Ver `cluster-infra/terraform/bootstrap/` para crear el rol vía Terraform.

---

## Referencia de convenciones de nombres

| Recurso | Convención | Ejemplo |
|---|---|---|
| ConfigMap | `{servicio}-config` | `service-template-config` |
| ExternalSecret | `{servicio}-secrets` | `service-template-secrets` |
| Secret generado | igual que ExternalSecret | `service-template-secrets` |
| Secreto en AWS SM | `/platform/{env}/{servicio}` | `/platform/dev/service-template` |
| Namespace | `{servicio}` | `service-template` |
| Deployment | `{servicio}` | `service-template` |
| HPA | `{servicio}` | `service-template` |
| ECR repo | `q10-{servicio}` | `q10-service-template` |
