using Api.DependencyInjection;
using BusinessStatus.Application.UseCases.CreateBusinessStatus;
using BusinessStatus.Application.UseCases.GetBusinessStatusById;
using BusinessStatus.Domain.Repositories;
using Infrastructure.Persistence.EntityFramework.BusinessStatuses;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace UnitTests.Api.DependencyInjection;

/// <summary>
/// Every slice adds its own registration line here, and a missing one surfaces only as a 500 when
/// the controller is activated: no test of a use case or of a controller action can catch it,
/// because they all inject doubles and never go through the container.
/// </summary>
public sealed class BusinessStatusServiceExtensionsTests
{
    private static readonly IServiceCollection Registrations = new ServiceCollection().AddBusinessStatusServices();

    [Theory]
    [InlineData(typeof(IBusinessStatusRepository), typeof(BusinessStatusRepository))]
    [InlineData(typeof(ICreateBusinessStatusUseCase), typeof(CreateBusinessStatusUseCase))]
    [InlineData(typeof(IGetBusinessStatusByIdUseCase), typeof(GetBusinessStatusByIdUseCase))]
    public void AddBusinessStatusServices_RegistersTheContextScoped(Type service, Type implementation)
    {
        var descriptor = Registrations.Where(d => d.ServiceType == service).ShouldHaveSingleItem();

        descriptor.ImplementationType.ShouldBe(implementation);
        descriptor.Lifetime.ShouldBe(
            ServiceLifetime.Scoped,
            "the context shares the DbContext of the request");
    }
}
