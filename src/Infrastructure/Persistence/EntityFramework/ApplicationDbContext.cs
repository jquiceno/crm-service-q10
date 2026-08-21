using Infrastructure.Persistence.EntityFramework.LossReasons.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.EntityFramework;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    // Keyless read-only projection of tbl_opo_negocios.
    // Used by LossReasonUsageReader to check whether a LossReason is in use before deletion.
    // No insert/update/delete operations: the entity has no key and no repository.
    public DbSet<DealLossReasonUsage> DealLossReasonUsages => Set<DealLossReasonUsage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }
}
