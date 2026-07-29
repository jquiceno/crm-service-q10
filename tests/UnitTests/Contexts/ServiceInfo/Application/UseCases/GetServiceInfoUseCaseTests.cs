using NSubstitute;
using ServiceInfo.Application.Ports;
using ServiceInfo.Application.UseCases.GetServiceInfo;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.ServiceInfo.Application.UseCases;

public sealed class GetServiceInfoUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsServiceInfoFromPort()
    {
        var port = Substitute.For<IServiceInfoPort>();
        port.Name.Returns("Announcements-service");
        port.Version.Returns("1.2.3");
        port.TemplateVersion.Returns("4.5.6");
        var sut = new GetServiceInfoUseCase(port);

        var result = await sut.ExecuteAsync();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Status.ShouldBe("ok");
        result.Value.Name.ShouldBe("Announcements-service");
        result.Value.Version.ShouldBe("1.2.3");
        result.Value.TemplateVersion.ShouldBe("4.5.6");
    }
}
