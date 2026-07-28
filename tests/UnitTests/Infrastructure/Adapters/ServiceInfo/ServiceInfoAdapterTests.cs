using Infrastructure.Adapters.ServiceInfo;
using Infrastructure.Settings;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Adapters.ServiceInfo;

public sealed class ServiceInfoAdapterTests
{
    [Fact]
    public void Properties_ReturnValuesFromBoundOptions()
    {
        var serviceInfo = Options.Create(new ServiceInfoSettings { Name = "service-template-dotnet", Version = "1.2.3" });
        var templateInfo = Options.Create(new TemplateSettings { Version = "4.5.6" });
        var sut = new ServiceInfoAdapter(serviceInfo, templateInfo);

        sut.Name.ShouldBe("service-template-dotnet");
        sut.Version.ShouldBe("1.2.3");
        sut.TemplateVersion.ShouldBe("4.5.6");
    }
}
