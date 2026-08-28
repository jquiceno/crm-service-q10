using Api.DependencyInjection;
using BusinessStatus.Application.UseCases.CreateBusinessStatus;
using BusinessStatus.Application.UseCases.GetBusinessStatuses;
using BusinessStatus.Application.UseCases.UpdateBusinessStatus;
using BusinessStatus.Domain.Repositories;
using Infrastructure.Persistence.EntityFramework.BusinessStatuses;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace UnitTests.Api.DependencyInjection;

public sealed class BusinessStatusServiceExtensionsTests
{
    private static readonly IServiceCollection Registrations = new ServiceCollection().AddBusinessStatusServices();

    [Theory]
    [InlineData(typeof(IBusinessStatusRepository), typeof(BusinessStatusRepository))]
    [InlineData(typeof(IGetBusinessStatusesUseCase), typeof(GetBusinessStatusesUseCase))]
    [InlineData(typeof(ICreateBusinessStatusUseCase), typeof(CreateBusinessStatusUseCase))]
    [InlineData(typeof(IUpdateBusinessStatusUseCase), typeof(UpdateBusinessStatusUseCase))]
    public void AddBusinessStatusServices_RegistersTheContextScoped(Type service, Type implementation)
    {
        var descriptor = Registrations.Where(d => d.ServiceType == service).ShouldHaveSingleItem();

        descriptor.ImplementationType.ShouldBe(implementation);
        descriptor.Lifetime.ShouldBe(
            ServiceLifetime.Scoped,
            "the context shares the DbContext of the request");
    }
}
