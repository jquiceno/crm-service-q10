using Shared.Results.Errors;

namespace ContactChannel.Domain.Errors;

public static class ContactChannelErrors
{
    public const string Context = "ContactChannel";

    public const string NameProperty = "Name";

    public static readonly ValidationError NameRequired =
        new("El nombre del medio de contacto es obligatorio.", ErrorType.Validation)
        {
            Property = NameProperty,
            Context = Context,
        };

    public static readonly ValidationError NameTooLong =
        new("El nombre del medio de contacto no puede exceder 100 caracteres.", ErrorType.Validation)
        {
            Property = NameProperty,
            Context = Context,
        };

    public static NotFoundError NotFound(int id) =>
        new($"No existe un medio de contacto con el consecutivo {id}.") { Context = Context };

    public static ConflictError InUse(int id) =>
        new($"El medio de contacto {id} está asociado a una o más oportunidades y no se puede eliminar.")
        {
            Context = Context,
        };
}
