using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.EntityFramework;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    public DbSet<BusinessStatuses.Entities.BusinessStatusRow> BusinessStatuses =>
        Set<BusinessStatuses.Entities.BusinessStatusRow>();

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
