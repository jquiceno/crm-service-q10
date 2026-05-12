namespace Shared.Domain.Errors;

public static class AddressErrors
{
    public static readonly ValidationError StreetRequired =
        new("Street is required.", ErrorType.Validation);

    public static readonly ValidationError CityRequired =
        new("City is required.", ErrorType.Validation);

    public static readonly ValidationError ZipCodeRequired =
        new("Zip code is required.", ErrorType.Validation);
}
