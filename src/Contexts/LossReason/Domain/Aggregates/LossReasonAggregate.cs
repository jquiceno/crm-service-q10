using LossReason.Domain.Errors;
using Shared.Domain.Aggregates;
using Shared.Results;
using Shared.Results.Errors;

namespace LossReason.Domain.Aggregates;

public sealed class LossReasonAggregate : AggregateRoot<int>
{
    public const int NameMaxLength = 50;

    public string Name { get; private set; }
    public bool IsActive { get; private set; }

    private LossReasonAggregate(string name, bool isActive)
    {
        Name = name;
        IsActive = isActive;
    }

    private LossReasonAggregate(int id, string name, bool isActive)
        : this(name, isActive)
    {
        Id = id;
    }

    public static Result<LossReasonAggregate> Create(CreateLossReasonArgs input)
    {
        var errors = ValidateName(input.Name);

        if (input.IsActive is null)
            errors.Add(LossReasonErrors.IsActiveRequired);

        if (errors.Count > 0)
            return DomainError.FromValidationDomainErrors(errors);

        var aggregate = new LossReasonAggregate(name: input.Name!, isActive: input.IsActive!.Value);
        aggregate.Created();

        return aggregate;
    }

    public static LossReasonAggregate Reconstruct(int id, string name, bool isActive) =>
        new(id, name, isActive);

    public Result Update(UpdateLossReasonArgs input)
    {
        var errors = ValidateName(input.Name);

        if (errors.Count > 0)
            return DomainError.FromValidationDomainErrors(errors);

        Name = input.Name!;
        IsActive = input.IsActive;
        SetUpdatedAt(DateTime.UtcNow);

        return Result.Success();
    }

    protected override void Created() => SetCreatedAt(DateTime.UtcNow);

    private static List<ValidationError> ValidateName(string? name)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(name))
            errors.Add(LossReasonErrors.NameRequired);

        if (name is { Length: > NameMaxLength })
            errors.Add(LossReasonErrors.NameTooLong);

        return errors;
    }
}
