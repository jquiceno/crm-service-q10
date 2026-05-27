using Shared.Results;
using Shared.Results.Errors;

namespace Shared.Domain.ValueObjects;

public sealed class Address : ValueObject
{
    public string Street { get; private set; } = null!;
    public string City { get; private set; } = null!;
    public string ZipCode { get; private set; } = null!;

    private Address() { }

    private Address(string street, string city, string zipCode)
    {
        Street = street;
        City = city;
        ZipCode = zipCode;
    }

    public static Result<Address, ValidationErrorList> Create(string? street, string? city, string? zipCode)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(street))
            errors.Add(AddressErrors.StreetRequired with { Property = nameof(Street), Value = street });

        if (string.IsNullOrWhiteSpace(city))
            errors.Add(AddressErrors.CityRequired with { Property = nameof(City), Value = city });

        if (string.IsNullOrWhiteSpace(zipCode))
            errors.Add(AddressErrors.ZipCodeRequired with { Property = nameof(ZipCode), Value = zipCode });

        if (errors.Count > 0)
            return new ValidationErrorList(errors);

        return new Address(street!, city!, zipCode!);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return ZipCode;
    }
}
