using Shared.Application.Dtos;
using Shared.Domain.ValueObjects;

namespace Shared.Application.Mapping;

public static class AddressMappingExtensions
{
    public static AddressOutputDto ToDto(this Address address) =>
        new(address.Street, address.City, address.ZipCode);
}
