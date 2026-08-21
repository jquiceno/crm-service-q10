using AdsChannel.Domain.Errors;
using Shared.Domain.Aggregates;
using Shared.Results;
using Shared.Results.Errors;

namespace AdsChannel.Domain.Aggregates;

public sealed class AdsChannelAggregate : AggregateRoot<int>
{
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private AdsChannelAggregate(int id, string name, bool isActive)
    {
        Id = id;
        Name = name;
        IsActive = isActive;
    }

    public static Result<AdsChannelAggregate> Create(CreateAdsChannelArgs input)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(input.Name))
            errors.Add(AdsChannelErrors.NameRequired);
        else if (input.Name.Length > 100)
            errors.Add(AdsChannelErrors.NameTooLong);

        if (errors.Count > 0)
            return DomainError.FromValidationDomainErrors(errors);

        // Not yet persisted: the real value is a SQL Server IDENTITY, assigned only after insert
        // (see AdsChannelRepository.CreateAsync). 0 here is a placeholder, never read before then.
        var aggregate = new AdsChannelAggregate(0, input.Name!, input.IsActive);
        aggregate.Created();
        return aggregate;
    }

    public static AdsChannelAggregate Reconstruct(int id, string? name, bool? isActive) =>
        new(id, name ?? string.Empty, isActive ?? true);

    public Result Update(UpdateAdsChannelArgs input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            return AdsChannelErrors.NameRequired;

        if (input.Name.Length > 100)
            return AdsChannelErrors.NameTooLong;

        Name = input.Name;
        IsActive = input.IsActive;
        SetUpdatedAt(DateTime.UtcNow);

        return Result.Success();
    }

    protected override void Created()
    {
        SetCreatedAt(DateTime.UtcNow);
        SetUpdatedAt(DateTime.UtcNow);
    }
}
