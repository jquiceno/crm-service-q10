#!/usr/bin/env bash
# init-service.sh — personaliza un fork/clone de dotnet-service-template.
# Ejecutar una sola vez desde la raíz del repo recién creado.
#
# Uso:
#   ./init-service.sh \
#     --name  my-service \
#     --repo  My-Service \
#     --path  /my-service \
#     [--org  Q10-Software] \
#     [--set-gh-vars]
#
# Flags:
#   --name          Nombre del servicio en minúsculas con guiones (restricción de ECR/k8s).
#   --repo          Nombre exacto del repositorio en GitHub (respeta mayúsculas; se usa en OIDC trust).
#   --path          Prefijo URL del servicio (ASPNETCORE_PATHBASE + ruta del ingress), ej: /my-service.
#   --org           Organización de GitHub (default: Q10-Software).
#   --set-gh-vars   Registra SERVICE_NAME e IMAGE_KEY como variables del repo via gh CLI.
#   --dry-run       Muestra qué cambios haría sin modificar archivos.

set -euo pipefail

# ── Colours ───────────────────────────────────────────────────────────────────
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[1;33m'; CYAN='\033[0;36m'
BOLD='\033[1m'; DIM='\033[2m'; NC='\033[0m'
info()  { printf "${BOLD}  info${NC}  %s\n" "$*"; }
ok()    { printf "${GREEN}  ok${NC}    %s\n" "$*"; }
skip()  { printf "${DIM}  skip${NC}   %s\n" "$*"; }
warn()  { printf "${YELLOW}  warn${NC}  %s\n" "$*"; }
die()   { printf "\n${RED}  error${NC} %s\n\n" "$*" >&2; exit 1; }
title() { printf "\n${CYAN}${BOLD}▶ %s${NC}\n" "$*"; }

# ── Arg parsing ───────────────────────────────────────────────────────────────
SERVICE_NAME=""
GITHUB_REPO=""
PATHBASE=""
GITHUB_ORG="Q10-Software"
SET_GH_VARS=false
DRY_RUN=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --name)        SERVICE_NAME="$2"; shift 2 ;;
    --repo)        GITHUB_REPO="$2";  shift 2 ;;
    --path)        PATHBASE="$2";     shift 2 ;;
    --org)         GITHUB_ORG="$2";   shift 2 ;;
    --set-gh-vars) SET_GH_VARS=true;  shift   ;;
    --dry-run)     DRY_RUN=true;      shift   ;;
    -h|--help)     grep '^#' "$0" | sed 's/^# \{0,2\}//' | head -20; exit 0 ;;
    *)             die "Argumento desconocido: $1" ;;
  esac
done

# ── Validaciones ──────────────────────────────────────────────────────────────
[[ -n "$SERVICE_NAME" ]] || die "--name es obligatorio  (ej: my-service)"
[[ -n "$GITHUB_REPO"  ]] || die "--repo es obligatorio  (ej: My-Service)"
[[ -n "$PATHBASE"     ]] || die "--path es obligatorio  (ej: /my-service)"

[[ "$SERVICE_NAME" =~ ^[a-z][a-z0-9-]+$ ]] \
  || die "--name '${SERVICE_NAME}' debe ser kebab-case en minúsculas (^[a-z][a-z0-9-]+$)"
[[ "$PATHBASE" =~ ^/[a-z][a-z0-9/-]*$ ]] \
  || die "--path '${PATHBASE}' debe comenzar con / y usar solo minúsculas, dígitos y guiones"

IMAGE_KEY="q10-${SERVICE_NAME}"

# PascalCase derivado de --name (kebab-case), ej: my-service → MyService.
# Se usa para el "ServiceInfo:Name" reportado por la app (appsettings, docker-compose).
PASCAL_NAME=""
IFS='-' read -ra NAME_PARTS <<< "$SERVICE_NAME"
for part in "${NAME_PARTS[@]}"; do
  PASCAL_NAME+="$(tr '[:lower:]' '[:upper:]' <<< "${part:0:1}")${part:1}"
done

# ── Resumen de parámetros ─────────────────────────────────────────────────────
printf "\n${BOLD}%-16s${NC}%s\n"  "  service-name"  "$SERVICE_NAME"
printf "${BOLD}%-16s${NC}%s\n"    "  github-repo"   "$GITHUB_REPO"
printf "${BOLD}%-16s${NC}%s\n"    "  path-base"     "$PATHBASE"
printf "${BOLD}%-16s${NC}%s\n"    "  image-key"     "$IMAGE_KEY"
printf "${BOLD}%-16s${NC}%s\n"    "  service-info"  "$PASCAL_NAME"
printf "${BOLD}%-16s${NC}%s\n"    "  github-org"    "$GITHUB_ORG"
$DRY_RUN && printf "${YELLOW}${BOLD}%-16s${NC}%s\n" "  mode"          "DRY RUN (sin cambios)"

# ── Guardia: ¿ya inicializado? ────────────────────────────────────────────────
if ! grep -rl "service-template" k8s/ terraform/ 2>/dev/null | grep -q .; then
  warn "No se encontraron placeholders 'service-template' — el repo puede estar ya inicializado."
  exit 0
fi

# ── sed portable (macOS vs Linux) ─────────────────────────────────────────────
# GNU sed: -i sin argumento   |   BSD sed (macOS): -i ''
if sed --version &>/dev/null 2>&1; then
  SED_INPLACE=(-i)
else
  SED_INPLACE=(-i '')
fi

sub() {
  # sub <archivo> <old> <new>
  local file="$1" old="$2" new="$3"
  [[ -f "$file" ]] || return 0
  grep -qF "$old" "$file" 2>/dev/null || return 0

  if $DRY_RUN; then
    printf "    ${DIM}%-45s${NC}  ${RED}%s${NC}  →  ${GREEN}%s${NC}\n" \
      "$(basename "$file")" "$old" "$new"
    return 0
  fi

  sed "${SED_INPLACE[@]}" "s|${old}|${new}|g" "$file"
}

# ── Archivos a procesar ───────────────────────────────────────────────────────
K8S_FILES=(
  k8s/base/configmap.yaml
  k8s/base/deployment.yaml
  k8s/base/external-secret.yaml
  k8s/base/hpa.yaml
  k8s/base/ingress.yaml
  k8s/base/namespace.yaml
  k8s/base/pdb.yaml
  k8s/base/service.yaml
  k8s/overlays/dev/kustomization.yaml
  k8s/overlays/qa/kustomization.yaml
  k8s/overlays/prod/kustomization.yaml
)

TERRAFORM_FILES=(
  terraform/backend.tf
  terraform/variables.tf
  terraform/env/dev.tfvars
  terraform/env/qa.tfvars
  terraform/env/prod.tfvars
)

APP_FILES=(
  src/Api/appsettings.json
  src/Api/appsettings.example.json
  docker-compose.example.yml
)

# ── Sustituciones ─────────────────────────────────────────────────────────────
# Orden: más específico primero para evitar sustituciones parciales.
#   1. service-template-dotnet  → $GITHUB_REPO   (contiene "service-template")
#   2. /service-template        → $PATHBASE       (prefijo de ruta)
#   3. service-template         → $SERVICE_NAME   (todos los demás usos)

title "k8s"
for f in "${K8S_FILES[@]}"; do
  if [[ ! -f "$f" ]]; then
    skip "$f"
    continue
  fi
  sub "$f" "service-template-dotnet" "$GITHUB_REPO"
  sub "$f" "/service-template"       "$PATHBASE"
  sub "$f" "service-template"        "$SERVICE_NAME"
  $DRY_RUN || ok "$f"
done

title "terraform"
for f in "${TERRAFORM_FILES[@]}"; do
  if [[ ! -f "$f" ]]; then
    skip "$f"
    continue
  fi
  sub "$f" "service-template-dotnet" "$GITHUB_REPO"
  sub "$f" "/service-template"       "$PATHBASE"
  sub "$f" "service-template"        "$SERVICE_NAME"
  $DRY_RUN || ok "$f"
done

title "app settings"
for f in "${APP_FILES[@]}"; do
  if [[ ! -f "$f" ]]; then
    skip "$f"
    continue
  fi
  sub "$f" "ServiceTemplate" "$PASCAL_NAME"
  $DRY_RUN || ok "$f"
done

title "Dockerfile"
if [[ -f "Dockerfile.example" ]]; then
  if $DRY_RUN; then
    printf "    ${DIM}%-45s${NC}  ${RED}%s${NC}  →  ${GREEN}%s${NC}\n" "." "Dockerfile.example" "Dockerfile"
  else
    mv Dockerfile.example Dockerfile
    ok "Dockerfile.example → Dockerfile"
  fi
else
  skip "Dockerfile.example (no encontrado)"
fi

# ── github_repo_id (claim OIDC sub con IDs inmutables) ───────────────────────
# GitHub emite el claim OIDC sub con IDs inmutables embebidos
# (repo:org@ORG_ID/repo@REPO_ID:environment:env). El trust policy de terraform
# necesita el ID numérico del repo en env/*.tfvars; sin él, el primer deploy
# falla con "Not authorized to perform sts:AssumeRoleWithWebIdentity".
title "github_repo_id"
GITHUB_REPO_ID=""
if ! command -v gh &>/dev/null; then
  warn "gh CLI no encontrado — github_repo_id queda vacío en terraform/env/*.tfvars (ver checklist)"
elif ! gh auth status &>/dev/null 2>&1; then
  warn "gh CLI no autenticado — github_repo_id queda vacío en terraform/env/*.tfvars (ver checklist)"
else
  GITHUB_REPO_ID="$(gh api "repos/${GITHUB_ORG}/${GITHUB_REPO}" --jq .id 2>/dev/null || true)"
  if [[ -n "$GITHUB_REPO_ID" ]]; then
    for f in terraform/env/dev.tfvars terraform/env/qa.tfvars terraform/env/prod.tfvars; do
      sub "$f" "github_repo_id    = \"\"" "github_repo_id    = \"${GITHUB_REPO_ID}\""
    done
    $DRY_RUN || ok "github_repo_id = $GITHUB_REPO_ID"
  else
    warn "No se pudo resolver el ID de ${GITHUB_ORG}/${GITHUB_REPO} — github_repo_id queda vacío (ver checklist)"
  fi
fi

$DRY_RUN && { echo ""; warn "Dry-run completado. Ejecuta sin --dry-run para aplicar los cambios."; echo ""; exit 0; }

# ── GitHub repository variables ───────────────────────────────────────────────
if $SET_GH_VARS; then
  title "GitHub variables"
  if ! command -v gh &>/dev/null; then
    warn "gh CLI no encontrado — omitiendo (instalar: https://cli.github.com)"
  elif ! gh auth status &>/dev/null 2>&1; then
    warn "gh CLI no autenticado — ejecuta 'gh auth login' primero"
  else
    gh variable set SERVICE_NAME --body "$SERVICE_NAME"
    gh variable set IMAGE_KEY    --body "$IMAGE_KEY"
    ok "SERVICE_NAME = $SERVICE_NAME"
    ok "IMAGE_KEY    = $IMAGE_KEY"
    warn "IMAGE_NAME se debe registrar después de crear el repo ECR (terraform apply):"
    printf "       ${DIM}gh variable set IMAGE_NAME --body \"<account>.dkr.ecr.us-east-1.amazonaws.com/%s\"${NC}\n" "$IMAGE_KEY"
  fi
fi

# ── Checklist de pasos manuales ───────────────────────────────────────────────
cat <<EOF

$(printf "${BOLD}${GREEN}")✔  Inicialización completada.$(printf "${NC}")  Revisa los cambios con: git diff

$(printf "${BOLD}")Pasos manuales restantes:$(printf "${NC}")

  1. Variables del repositorio en GitHub  (Settings → Secrets and variables → Actions → Variables):
       SERVICE_NAME = $SERVICE_NAME
       IMAGE_KEY    = $IMAGE_KEY
       IMAGE_NAME   = <account>.dkr.ecr.us-east-1.amazonaws.com/$IMAGE_KEY
          └─ Registrar después del primer 'terraform apply'.

  2. Bootstrap de Terraform (crea repo ECR + rol IAM por entorno):
       # IMPORTANTE: si github_repo_id quedó vacío en terraform/env/*.tfvars
       # (gh CLI no disponible o sin autenticar), rellénalo ANTES del apply:
       #   gh api repos/$GITHUB_ORG/$GITHUB_REPO --jq .id
       # El trust policy OIDC del rol de deploy lo necesita (claim sub con IDs
       # inmutables); sin él, el deploy falla en AssumeRoleWithWebIdentity.
       cd terraform
       terraform init \\
         -backend-config="bucket=q10-terraform-state-<account_id>" \\
         -backend-config="key=services/$SERVICE_NAME/dev/terraform.tfstate" \\
         -backend-config="profile=<aws-profile-dev>"
       terraform apply -var-file=env/dev.tfvars
       # Bucket por entorno: dev/qa → q10-terraform-state-764283926096,
       #                     prod   → q10-terraform-state-451828143717.
       # Repetir para qa y prod con sus perfiles/buckets correspondientes
       # (borrar .terraform/ o usar -reconfigure entre entornos).

  3. Overlays qa y prod — agregar ARN del certificado ACM en el patch del ingress:
       alb.ingress.kubernetes.io/certificate-arn: arn:aws:acm:us-east-1:<account>:certificate/<id>

  4. ExternalSecret — crear/verificar el secreto (un solo JSON) en Secrets Manager:
       /platform/dev/$SERVICE_NAME
       /platform/qa/$SERVICE_NAME
       /platform/prod/$SERVICE_NAME
     IMPORTANTE: el extract.key de cada overlay debe coincidir EXACTAMENTE con
     el nombre real del secreto, slash inicial incluido.
     NOTA: las claves compartidas (encrypt key, tenant-resolver URL, redis) NO
     se ponen en el secreto propio del servicio: vienen de platform-shared
     (Secret inyectado por el cluster; ver docs/plantilla/variables-entorno.md).

  5. Eliminar este script del repositorio:
       git rm init-service.sh

EOF
