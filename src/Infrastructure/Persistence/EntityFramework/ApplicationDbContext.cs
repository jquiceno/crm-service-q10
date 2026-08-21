using Activities.Domain.Aggregates;
using Infrastructure.Persistence.EntityFramework.Activities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.EntityFramework;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
    private static readonly ActivityLegacyCodeInterceptor LegacyCodeInterceptor = new();

    public DbSet<Activity> Activities => Set<Activity>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Registered here instead of in the DI options so that every construction path — API,
        // test fixtures, tooling — carries the interceptor; without it negact_resultado would
        // silently persist and read as NULL.
        optionsBuilder.AddInterceptors(LegacyCodeInterceptor);

        // Drift-readability (D20's spirit): a bad value in a tenant column — e.g. a NULL in the
        // nullable-in-DB negact_neg_consecutivo — must name the property and column instead of
        // failing with a bare "Data is Null". The per-read try/catch cost is accepted: this
        // service's declared problem is 378 databases drifting apart.
        optionsBuilder.EnableDetailedErrors();

        base.OnConfiguring(optionsBuilder);
    }

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
