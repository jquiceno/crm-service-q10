using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Shared.Presentation.Filters;
using Shouldly;
using Xunit;

namespace UnitTests.Shared.Presentation;

public sealed class OutputCacheInvalidateAttributeTests
{
    private const string Tag = "announcements";

    private static ActionExecutingContext BuildExecutingContext(IServiceProvider services)
    {
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            new object());
    }

    private static ActionExecutionDelegate BuildNext(
        ActionExecutingContext executingContext, IActionResult? result, Exception? exception) =>
        () =>
        {
            var executed = new ActionExecutedContext(
                executingContext, new List<IFilterMetadata>(), executingContext.Controller)
            {
                Result = result,
                Exception = exception
            };
            return Task.FromResult(executed);
        };

    [Fact]
    public async Task OnActionExecutionAsync_WhenNextSucceedsWithStatusBelow400_EvictsCacheTag()
    {
        var store = Substitute.For<IOutputCacheStore>();
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IOutputCacheStore)).Returns(store);
        var executingContext = BuildExecutingContext(services);
        var sut = new OutputCacheInvalidateAttribute(Tag);

        await sut.OnActionExecutionAsync(
            executingContext, BuildNext(executingContext, new StatusCodeResult(StatusCodes.Status200OK), null));

        await store.Received(1).EvictByTagAsync(Tag, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenResultHasNoStatusCode_DefaultsTo200AndEvicts()
    {
        var store = Substitute.For<IOutputCacheStore>();
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IOutputCacheStore)).Returns(store);
        var executingContext = BuildExecutingContext(services);
        var sut = new OutputCacheInvalidateAttribute(Tag);

        await sut.OnActionExecutionAsync(executingContext, BuildNext(executingContext, new EmptyResult(), null));

        await store.Received(1).EvictByTagAsync(Tag, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenNextThrows_SkipsEviction()
    {
        var store = Substitute.For<IOutputCacheStore>();
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IOutputCacheStore)).Returns(store);
        var executingContext = BuildExecutingContext(services);
        var sut = new OutputCacheInvalidateAttribute(Tag);

        await sut.OnActionExecutionAsync(
            executingContext, BuildNext(executingContext, null, new InvalidOperationException("boom")));

        await store.DidNotReceiveWithAnyArgs().EvictByTagAsync(default!, default);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenStatusCodeIsErrorResponse_SkipsEviction()
    {
        var store = Substitute.For<IOutputCacheStore>();
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(IOutputCacheStore)).Returns(store);
        var executingContext = BuildExecutingContext(services);
        var sut = new OutputCacheInvalidateAttribute(Tag);

        await sut.OnActionExecutionAsync(
            executingContext,
            BuildNext(executingContext, new StatusCodeResult(StatusCodes.Status404NotFound), null));

        await store.DidNotReceiveWithAnyArgs().EvictByTagAsync(default!, default);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenOutputCacheStoreNotRegistered_DoesNotThrow()
    {
        var services = Substitute.For<IServiceProvider>();
        var executingContext = BuildExecutingContext(services);
        var sut = new OutputCacheInvalidateAttribute(Tag);

        await Should.NotThrowAsync(() => sut.OnActionExecutionAsync(
            executingContext, BuildNext(executingContext, new StatusCodeResult(StatusCodes.Status200OK), null)));
    }

    [Fact]
    public void Tag_ReturnsConstructorValue()
    {
        var sut = new OutputCacheInvalidateAttribute(Tag);

        sut.Tag.ShouldBe(Tag);
    }
}
