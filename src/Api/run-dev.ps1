# Local run script for src/Api. Secrets (tenant resolver URL, encryption key, cache connection
# string) live in dotnet user-secrets, not here — see docs/plantilla/variables-entorno.md.
# To (re)configure them:
#   dotnet user-secrets set --project src/Api "TenantResolverService:Enabled" "true"
#   dotnet user-secrets set --project src/Api "TENANT_RESOLVER_SERVICE_URL" "<resolver url>"
#   dotnet user-secrets set --project src/Api "CONNSTRING_ENCRYPTION_KEY" "<platform shared key>"
#   dotnet user-secrets set --project src/Api "Cache:Enabled" "true"
#   dotnet user-secrets set --project src/Api "Cache:L2Enabled" "true"
#   dotnet user-secrets set --project src/Api "Cache:ConnectionString" "localhost:6379"
#
# Usage (from the repo root): powershell -File src/Api/run-dev.ps1

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5080"

dotnet run --project src/Api --no-launch-profile
