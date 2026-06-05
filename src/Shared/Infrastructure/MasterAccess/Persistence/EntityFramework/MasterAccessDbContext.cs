using Infraestructure.MasterAccess.Persistence.EntityFramework.Tenants.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infraestructure.MasterAccess.Persistence.EntityFramework;

public sealed class MasterAccessDbContext(DbContextOptions<MasterAccessDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MasterAccessDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
