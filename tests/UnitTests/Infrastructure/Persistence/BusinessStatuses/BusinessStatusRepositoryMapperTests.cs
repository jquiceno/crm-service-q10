using BusinessStatus.Domain.Aggregates;
using Infrastructure.Persistence.EntityFramework.BusinessStatuses.Mappers;
using Shouldly;
using Xunit;
using Entities = Infrastructure.Persistence.EntityFramework.BusinessStatuses.Entities;

namespace UnitTests.Infrastructure.Persistence.BusinessStatuses;

public sealed class BusinessStatusRepositoryMapperTests
{
    private static Entities.BusinessStatus Row(
        int id = 7,
        string? name = "Negotiation",
        bool? isActive = true,
        decimal? percentage = 50m,
        string? color = "49ff7c") =>
        new()
        {
            Id = id,
            Name = name,
            IsActive = isActive,
            Percentage = percentage,
            Color = color
        };

    // ── ToDomain ──────────────────────────────────────────────────────────────

    [Fact]
    public void ToDomain_WithEveryFieldPopulated_MapsAllOfThem()
    {
        var aggregate = BusinessStatusRepositoryMapper.ToDomain(Row());

        aggregate.Id.ShouldBe(7);
        aggregate.Name.ShouldBe("Negotiation");
        aggregate.Percentage.ShouldBe(50);
        aggregate.Color!.Value.ShouldBe("49ff7c");
        aggregate.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void ToDomain_WithEveryNullableColumnNull_DoesNotThrow()
    {
        var aggregate = BusinessStatusRepositoryMapper.ToDomain(
            Row(name: null, isActive: null, percentage: null, color: null));

        aggregate.Name.ShouldBe(string.Empty);
        aggregate.Percentage.ShouldBeNull();
        aggregate.Color.ShouldBeNull();
        aggregate.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void ToDomain_WithEmptyColor_LeavesTheColorAbsent()
    {
        var aggregate = BusinessStatusRepositoryMapper.ToDomain(Row(color: string.Empty));

        aggregate.Color.ShouldBeNull();
    }

    [Fact]
    public void ToDomain_DoesNotValidateLegacyData()
    {
        var aggregate = BusinessStatusRepositoryMapper.ToDomain(
            Row(name: new string('x', 201), color: "zzzzzz"));

        aggregate.Name.Length.ShouldBe(201);
        aggregate.Color!.Value.ShouldBe("zzzzzz");
    }

    [Theory]
    [InlineData(0.4)]
    [InlineData(50.5)]
    [InlineData(99.9)]
    public void ToDomain_WithADirtyIntermediatePercentage_ReadsItAsAbsent(double dirty)
    {
        // A residue that is not near a reserved terminal is an unknown percentage: D5 never invents a
        // value in the middle, so 99.9 stays absent rather than being snapped up to 100.
        var aggregate = BusinessStatusRepositoryMapper.ToDomain(Row(percentage: (decimal)dirty));

        aggregate.Percentage.ShouldBeNull();
        aggregate.IsLost.ShouldBeFalse();
        aggregate.IsWon.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void ToDomain_WithAnExactTerminalPercentage_KeepsIt(int terminal)
    {
        var aggregate = BusinessStatusRepositoryMapper.ToDomain(
            Row(percentage: terminal + 0.00000m));

        aggregate.Percentage.ShouldBe(terminal);
        aggregate.IsTerminal.ShouldBeTrue();
    }

    [Theory]
    [InlineData(100.00001, 100)]
    [InlineData(99.9995, 100)]
    [InlineData(0.00001, 0)]
    [InlineData(0.0005, 0)]
    public void ToDomain_WithATerminalCarryingATinyResidue_ReadsItAsTheTerminal(double dirty, int expected)
    {
        // INV-2/INV-3 must keep protecting a legacy terminal that drifted by a hair: reading it as the
        // reserved value keeps IsTerminal true, so it stays undeletable and its percentage immutable.
        var aggregate = BusinessStatusRepositoryMapper.ToDomain(Row(percentage: (decimal)dirty));

        aggregate.Percentage.ShouldBe(expected);
        aggregate.IsTerminal.ShouldBeTrue();
    }

    [Theory]
    [InlineData(150)]
    [InlineData(-5)]
    [InlineData(3000000000)]
    public void ToDomain_WithAPercentageOutOfRange_ReadsItAsAbsentAndNeverThrows(decimal outOfRange)
    {
        // Out of the reserved range is not a percentage this service recognizes; refusing it here also
        // guards the decimal-to-int cast from an OverflowException on a number beyond int (R-9).
        var aggregate = BusinessStatusRepositoryMapper.ToDomain(Row(percentage: outOfRange));

        aggregate.Percentage.ShouldBeNull();
        aggregate.IsTerminal.ShouldBeFalse();
    }

    // ── ToDocument ────────────────────────────────────────────────────────────

    [Fact]
    public void ToDocument_WritesEveryColumnTheDomainModels()
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Negotiation", 50, "49ff7c", isActive: true);

        var row = BusinessStatusRepositoryMapper.ToDocument(aggregate);

        row.Name.ShouldBe("Negotiation");
        row.Percentage.ShouldBe(50m);
        row.Color.ShouldBe("49ff7c");
        row.IsActive.ShouldBe(true);
    }

    [Fact]
    public void ToDocument_DoesNotAssignTheIdentity()
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Negotiation", 50, "49ff7c", isActive: true);

        var row = BusinessStatusRepositoryMapper.ToDocument(aggregate);

        row.Id.ShouldBe(0);
    }

    [Fact]
    public void ToDocument_WithoutColor_WritesNullAndNeverTheLegacyDefault()
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Negotiation", 50, color: null, isActive: false);

        var row = BusinessStatusRepositoryMapper.ToDocument(aggregate);

        row.Color.ShouldBeNull();
        row.IsActive.ShouldBe(false);
    }

    [Fact]
    public void ToDocument_WithoutPercentage_WritesNull()
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Negotiation", percentage: null, color: null, isActive: true);

        var row = BusinessStatusRepositoryMapper.ToDocument(aggregate);

        row.Percentage.ShouldBeNull();
    }

    // ── Round trip ────────────────────────────────────────────────────────────

    [Fact]
    public void ToDocumentThenToDomain_PreservesEveryModelledValue()
    {
        var original = BusinessStatusAggregate.Reconstruct(7, "Negotiation", 50, "49FF7C", isActive: true);

        var row = BusinessStatusRepositoryMapper.ToDocument(original);
        row.Id = original.Id;

        var roundTripped = BusinessStatusRepositoryMapper.ToDomain(row);

        roundTripped.Id.ShouldBe(original.Id);
        roundTripped.Name.ShouldBe(original.Name);
        roundTripped.Percentage.ShouldBe(original.Percentage);
        roundTripped.Color!.Value.ShouldBe("49FF7C");
        roundTripped.IsActive.ShouldBe(original.IsActive);
    }
}
