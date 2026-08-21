using ContactChannel.Domain.Errors;
using Shared.Domain.Aggregates;
using Shared.Results;
using Shared.Results.Errors;

namespace ContactChannel.Domain.Aggregates;

public sealed class ContactChannelAggregate : AggregateRoot<int>
{
    public const int NameMaxLength = 100;

    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    private ContactChannelAggregate(string name, bool isActive)
    {
        Name = name;
        IsActive = isActive;
    }

    public static Result<ContactChannelAggregate> Create(CreateContactChannelArgs input)
    {
        var name = Normalize(input.Name);
        var errors = ValidateName(name);

        if (errors.Count > 0)
            return DomainError.FromValidationDomainErrors(errors);

        var aggregate = new ContactChannelAggregate(name: name, isActive: input.IsActive);

        aggregate.Created();

        return aggregate;
    }

    public static ContactChannelAggregate Reconstruct(int id, string name, bool isActive) =>
        new(name: name, isActive: isActive) { Id = id };

    public Result Update(UpdateContactChannelArgs input)
    {
        var name = Normalize(input.Name);
        var errors = ValidateName(name);

        if (errors.Count > 0)
            return DomainError.FromValidationDomainErrors(errors);

        Name = name;
        IsActive = input.IsActive;

        SetUpdatedAt(DateTime.UtcNow);

        return Result.Success();
    }

    protected override void Created()
    {
        SetCreatedAt(DateTime.UtcNow);
    }

    private static string Normalize(string? name) => name?.Trim() ?? string.Empty;

    private static List<ValidationError> ValidateName(string name)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrEmpty(name))
        {
            errors.Add(ContactChannelErrors.NameRequired with { Value = name });
            return errors;
        }

        if (name.Length > NameMaxLength)
            errors.Add(ContactChannelErrors.NameTooLong with { Value = name });

        return errors;
    }
}
