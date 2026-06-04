using Infraestructure.MasterAccess.Persistence.EntityFramework.Tenants.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infraestructure.MasterAccess.Persistence.EntityFramework;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
