using LossReason.Domain.Errors;
using Shared.Domain.Aggregates;
using Shared.Results;
using Shared.Results.Errors;

namespace LossReason.Domain.Aggregates;

public sealed class LossReasonAggregate : AggregateRoot<int>
{
    public const int NameMaxLength = 50;

    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private LossReasonAggregate(int id, string name, bool isActive)
    {
        Id = id;
        Name = name;
        IsActive = isActive;
    }

    public static Result<LossReasonAggregate> Create(CreateLossReasonArgs input)
    {
        var errors = ValidateName(input.Name);

        if (errors.Count > 0)
            return DomainError.FromValidationDomainErrors(errors);

        // Id stays at its default: the database assigns it through IDENTITY.
        var aggregate = new LossReasonAggregate(id: default, input.Name!, input.IsActive);
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

    protected override void Created()
    {
        SetCreatedAt(DateTime.UtcNow);
        SetUpdatedAt(DateTime.UtcNow);
    }

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
