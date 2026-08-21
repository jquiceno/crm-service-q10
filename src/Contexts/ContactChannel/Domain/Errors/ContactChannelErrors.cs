using ContactChannel.Domain.Aggregates;
using Shared.Results.Errors;

namespace ContactChannel.Domain.Errors;

public static class ContactChannelErrors
{
    public const string Context = "ContactChannel";

    public static readonly ValidationError NameRequired =
        new("The contact channel name is required.", ErrorType.Validation)
        {
            Property = nameof(ContactChannelAggregate.Name),
            Context = Context,
        };

    public static readonly ValidationError NameTooLong =
        new("The contact channel name cannot exceed 100 characters.", ErrorType.Validation)
        {
            Property = nameof(ContactChannelAggregate.Name),
            Context = Context,
        };

    public static NotFoundError NotFound(int id) =>
        new($"No contact channel exists with identifier {id}.") { Context = Context };

    public static ConflictError InUse(int id) =>
        new($"Contact channel {id} is linked to one or more opportunities and cannot be deleted.")
        {
            Context = Context,
        };
}
