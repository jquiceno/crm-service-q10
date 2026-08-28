using BusinessStatus.Domain.Aggregates;

namespace Infrastructure.Persistence.EntityFramework.BusinessStatuses.Mappers;

public static class BusinessStatusRepositoryMapper
{
    public static BusinessStatusAggregate ToDomain(Entities.BusinessStatus row) =>
        BusinessStatusAggregate.Reconstruct(
            row.Id,
            row.Name ?? string.Empty,
            ToWholePercentage(row.Percentage),
            row.Color,
            row.IsActive ?? false);

    public static Entities.BusinessStatus ToDocument(BusinessStatusAggregate aggregate) =>
        new()
        {
            Name = aggregate.Name,
            IsActive = aggregate.IsActive,
            Percentage = aggregate.Percentage,
            Color = aggregate.Color?.Value
        };

    // A persisted percentage this close to a reserved terminal value is read as that terminal. It
    // absorbs the decimal noise of a legacy row (0.00001 in a decimal(20,5) column) without swallowing
    // a genuine intermediate: 99.6 is 0.4 away from 100 and stays intermediate.
    private const decimal TerminalSnapTolerance = 0.001m;

    /// <summary>
    /// Reads the persisted percentage as the whole number the domain models, or <c>null</c> when the
    /// row carries a percentage this service does not recognize.
    /// <para>
    /// A row within <see cref="TerminalSnapTolerance"/> of a reserved terminal (0 or 100) is read as
    /// that terminal: otherwise a legacy "Won" stored as 100.00001 would read as <c>null</c>, look
    /// intermediate, and lose the INV-2/INV-3 protection that keeps it from being deleted or moved.
    /// A dirty <em>intermediate</em> value is still surfaced as an absent percentage (<c>null</c>),
    /// so D5 holds — noise is only ever resolved towards the two reserved values, never invented in
    /// the middle. Refusing an out-of-range value here also keeps the cast below from overflowing on
    /// a number beyond <see cref="int"/> (R-9).
    /// </para>
    /// </summary>
    private static int? ToWholePercentage(decimal? persisted)
    {
        if (!persisted.HasValue)
            return null;

        var value = persisted.Value;

        if (Math.Abs(value - BusinessStatusAggregate.MinPercentage) <= TerminalSnapTolerance)
            return BusinessStatusAggregate.MinPercentage;

        if (Math.Abs(value - BusinessStatusAggregate.MaxPercentage) <= TerminalSnapTolerance)
            return BusinessStatusAggregate.MaxPercentage;

        if (value < BusinessStatusAggregate.MinPercentage || value > BusinessStatusAggregate.MaxPercentage)
            return null;

        return decimal.Truncate(value) == value ? (int)value : null;
    }
}
