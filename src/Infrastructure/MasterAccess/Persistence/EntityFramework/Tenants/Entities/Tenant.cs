namespace Infraestructure.MasterAccess.Persistence.EntityFramework.Tenants.Entities;

public sealed class Tenant
{
    public string Code { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;
    public int ServerDatabase { get; set; }
}
