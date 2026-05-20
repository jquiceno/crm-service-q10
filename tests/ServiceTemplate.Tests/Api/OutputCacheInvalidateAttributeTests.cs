using ServiceTemplate.Tests.Api.Doubles;

namespace ServiceTemplate.Tests.Api;

/// <summary>
/// Unit tests for <see cref="OutputCacheInvalidateAttribute"/> in isolation from the
/// HTTP pipeline. Exercises the filter directly with a fake <see cref="IOutputCacheStore"/>
/// so assertions focus on the filter's contract: evict on success, skip on failure.
/// </summary>
public sealed class OutputCacheInvalidateAttributeTests
{
    [Fact]
    public async Task Evicts_Tag_When_Handler_Returns_Created()
    {
        var store = new FakeOutputCacheStore();
        var filter = new OutputCacheInvalidateAttribute("weather-forecasts");

        await RunFilter(filter, store, () => new CreatedResult());

        store.EvictedTags.Should().ContainSingle().Which.Should().Be("weather-forecasts");
    }

    [Fact]
    public async Task Evicts_Tag_When_Handler_Returns_Ok()
    {
        var store = new FakeOutputCacheStore();
        var filter = new OutputCacheInvalidateAttribute("orders");

        await RunFilter(filter, store, () => new OkResult());

        store.EvictedTags.Should().ContainSingle().Which.Should().Be("orders");
    }

    [Fact]
    public async Task Does_Not_Evict_When_Handler_Returns_BadRequest()
    {
        var store = new FakeOutputCacheStore();
        var filter = new OutputCacheInvalidateAttribute("weather-forecasts");

        await RunFilter(filter, store, () => new BadRequestResult());

        store.EvictedTags.Should().BeEmpty();
    }

    [Fact]
    public async Task Does_Not_Evict_When_Handler_Returns_NotFound()
    {
        var store = new FakeOutputCacheStore();
        var filter = new OutputCacheInvalidateAttribute("weather-forecasts");

        await RunFilter(filter, store, () => new NotFoundResult());

        store.EvictedTags.Should().BeEmpty();
    }

    [Fact]
    public async Task Does_Not_Evict_When_Handler_Throws()
    {
        var store = new FakeOutputCacheStore();
        var filter = new OutputCacheInvalidateAttribute("weather-forecasts");

        await RunFilter(filter, store, () => throw new InvalidOperationException("boom"));

        store.EvictedTags.Should().BeEmpty();
    }

    [Fact]
    public async Task Multiple_Attributes_Each_Evict_Their_Own_Tag()
    {
        // Simulates [OutputCacheInvalidate("orders")] + [OutputCacheInvalidate("inventory")]
        // on the same action. Each attribute runs independently.
        var store = new FakeOutputCacheStore();
        var orders = new OutputCacheInvalidateAttribute("orders");
        var inventory = new OutputCacheInvalidateAttribute("inventory");

        await RunFilter(orders, store, () => new OkResult());
        await RunFilter(inventory, store, () => new OkResult());

        store.EvictedTags.Should().BeEquivalentTo(new[] { "orders", "inventory" });
    }

    // ---------------------------------------------------------------------------
    // Test harness — invokes the filter with a fake HttpContext/ActionContext and
    // simulates the next delegate returning (or throwing) a specific result.
    // ---------------------------------------------------------------------------

    private static Task RunFilter(
        OutputCacheInvalidateAttribute filter,
        IOutputCacheStore store,
        Func<IActionResult> nextResultFactory)
    {
        var services = new ServiceCollection();
        services.AddSingleton(store);

        var httpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ControllerActionDescriptor());

        var executing = new ActionExecutingContext(
            actionContext,
            filters: [],
            actionArguments: new Dictionary<string, object?>(),
            controller: new object());

        ActionExecutionDelegate next = () =>
        {
            ActionExecutedContext executed;
            try
            {
                executed = new ActionExecutedContext(actionContext, [], new object())
                {
                    Result = nextResultFactory(),
                };
            }
            catch (Exception ex)
            {
                executed = new ActionExecutedContext(actionContext, [], new object())
                {
                    Exception = ex,
                };
            }
            return Task.FromResult(executed);
        };

        return filter.OnActionExecutionAsync(executing, next);
    }
}
