using System.Reflection;
using System.Text.Json;
using Api.Controllers;
using ContactChannel.Application.UseCases.CreateContactChannel;
using ContactChannel.Application.UseCases.GetContactChannelById;
using ContactChannel.Application.UseCases.GetContactChannels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Shared.Application.Dtos;
using Shared.Domain.Pagination;
using Shared.Presentation.Filters;
using Shared.Presentation.Results;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Api.Controllers;

public sealed class ContactChannelsControllerTests
{
    private readonly IGetContactChannelsUseCase _getContactChannelsUseCase =
        Substitute.For<IGetContactChannelsUseCase>();

    private readonly IGetContactChannelByIdUseCase _getContactChannelByIdUseCase =
        Substitute.For<IGetContactChannelByIdUseCase>();

    private readonly ICreateContactChannelUseCase _createContactChannelUseCase =
        Substitute.For<ICreateContactChannelUseCase>();

    private static async Task<(int StatusCode, JsonDocument Body)> ExecuteAsync(IActionResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        await result.ExecuteResultAsync(actionContext).ConfigureAwait(false);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var json = await reader.ReadToEndAsync().ConfigureAwait(false);
        return (httpContext.Response.StatusCode, JsonDocument.Parse(json));
    }

    private Task<HttpOkPagedResult<GetContactChannelsOutputDto>> InvokeAsync(
        GetContactChannelsInputDto? filter = null,
        PageQueryInputDto? pagination = null) =>
        CreateController().GetContactChannels(
            filter ?? new GetContactChannelsInputDto(IsActive: null, SearchName: null),
            pagination ?? new PageQueryInputDto(),
            CancellationToken.None);

    private ContactChannelsController CreateController() =>
        new(_getContactChannelsUseCase, _getContactChannelByIdUseCase, _createContactChannelUseCase);

    private void Returns(PagedResult<GetContactChannelsOutputDto> result) =>
        _getContactChannelsUseCase
            .ExecuteAsync(
                Arg.Any<GetContactChannelsInputDto>(),
                Arg.Any<PageQuery>(),
                Arg.Any<CancellationToken>())
            .Returns(result);

    [Fact]
    public async Task GetContactChannels_WhenTheUseCaseSucceeds_ReturnsThePageWithItsTotal()
    {
        Returns(PagedResult<GetContactChannelsOutputDto>.Success(
            [new GetContactChannelsOutputDto(1, "WhatsApp", true)],
            totalCount: 7));

        var (statusCode, body) = await ExecuteAsync(await InvokeAsync());

        statusCode.ShouldBe(StatusCodes.Status200OK);
        var data = body.RootElement.GetProperty("data");
        data.GetProperty("totalCount").GetInt32().ShouldBe(7);
        data.GetProperty("items")[0].GetProperty("name").GetString().ShouldBe("WhatsApp");
        data.GetProperty("items")[0].GetProperty("isActive").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task GetContactChannels_WithoutMatches_ReturnsOkWithAnEmptyPageInsteadOfNotFound()
    {
        Returns(PagedResult<GetContactChannelsOutputDto>.Success([], totalCount: 0));

        var (statusCode, body) = await ExecuteAsync(await InvokeAsync());

        statusCode.ShouldBe(StatusCodes.Status200OK);
        body.RootElement.GetProperty("data").GetProperty("items").GetArrayLength().ShouldBe(0);
        body.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task GetContactChannels_TranslatesThePaginationInputIntoAPageQuery()
    {
        Returns(PagedResult<GetContactChannelsOutputDto>.Success([], totalCount: 0));

        await InvokeAsync(pagination: new PageQueryInputDto(PageIndex: 3, PageSize: 25));

        await _getContactChannelsUseCase.Received(1).ExecuteAsync(
            Arg.Any<GetContactChannelsInputDto>(),
            Arg.Is<PageQuery>(p => p.PageIndex == 3 && p.PageSize == 25),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetContactChannels_ForwardsTheFiltersUntouched()
    {
        Returns(PagedResult<GetContactChannelsOutputDto>.Success([], totalCount: 0));

        await InvokeAsync(new GetContactChannelsInputDto(IsActive: false, SearchName: "wha"));

        await _getContactChannelsUseCase.Received(1).ExecuteAsync(
            Arg.Is<GetContactChannelsInputDto>(i => i.IsActive == false && i.SearchName == "wha"),
            Arg.Any<PageQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetContactChannels_WhenTheUseCaseFails_ReturnsTheMappedErrorStatus()
    {
        Returns(PagedResult<GetContactChannelsOutputDto>.Failure(
            new DomainError("boom", ErrorType.Internal)));

        var (statusCode, _) = await ExecuteAsync(await InvokeAsync());

        statusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    private static OutputCacheAttribute GetOutputCacheAttribute(string actionName)
    {
        var attribute = typeof(ContactChannelsController)
            .GetMethod(actionName)!
            .GetCustomAttribute<OutputCacheAttribute>();

        attribute.ShouldNotBeNull();

        return attribute;
    }

    [Fact]
    public void GetContactChannels_IsCachedUnderTheContactChannelsTag()
    {
        var attribute = GetOutputCacheAttribute(nameof(ContactChannelsController.GetContactChannels));

        attribute.NoStore.ShouldBeFalse();
        attribute.Tags.ShouldBe(["contact-channels"]);
    }

    [Fact]
    public void GetContactChannels_DeclaresItsOwnTtlOfThreeDays()
    {
        GetOutputCacheAttribute(nameof(ContactChannelsController.GetContactChannels))
            .Duration.ShouldBe(259200);
    }

    [Fact]
    public void GetContactChannels_DeclaresNoQueryVariationOfItsOwn()
    {
        GetOutputCacheAttribute(nameof(ContactChannelsController.GetContactChannels))
            .VaryByQueryKeys.ShouldBeNull();
    }

    private Task<HttpOkResult<GetContactChannelByIdOutputDto>> InvokeByIdAsync(int id = 7) =>
        CreateController().GetContactChannelById(id, CancellationToken.None);

    private void ReturnsById(Result<GetContactChannelByIdOutputDto> result) =>
        _getContactChannelByIdUseCase
            .ExecuteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(result);

    [Fact]
    public async Task GetContactChannelById_WhenTheChannelExists_ReturnsIt()
    {
        ReturnsById(new GetContactChannelByIdOutputDto(7, "WhatsApp", true));

        var (statusCode, body) = await ExecuteAsync(await InvokeByIdAsync());

        statusCode.ShouldBe(StatusCodes.Status200OK);
        var data = body.RootElement.GetProperty("data");
        data.GetProperty("id").GetInt32().ShouldBe(7);
        data.GetProperty("name").GetString().ShouldBe("WhatsApp");
        data.GetProperty("isActive").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task GetContactChannelById_ForwardsTheIdentifier()
    {
        ReturnsById(new GetContactChannelByIdOutputDto(42, "Feria", false));

        await InvokeByIdAsync(42);

        await _getContactChannelByIdUseCase.Received(1)
            .ExecuteAsync(42, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetContactChannelById_WithAnUnknownId_ReturnsNotFound()
    {
        ReturnsById(Result<GetContactChannelByIdOutputDto>.Failure(
            new NotFoundError("No contact channel exists with identifier 404.")));

        var (statusCode, body) = await ExecuteAsync(await InvokeByIdAsync(404));

        statusCode.ShouldBe(StatusCodes.Status404NotFound);
        body.RootElement.GetProperty("statusCode").GetInt32().ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetContactChannelById_WhenTheUseCaseFails_ReturnsTheMappedErrorStatus()
    {
        ReturnsById(Result<GetContactChannelByIdOutputDto>.Failure(
            new DomainError("boom", ErrorType.Internal)));

        var (statusCode, _) = await ExecuteAsync(await InvokeByIdAsync());

        statusCode.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public void GetContactChannelById_IsCachedUnderTheSameTagAsTheListing()
    {
        var attribute = GetOutputCacheAttribute(nameof(ContactChannelsController.GetContactChannelById));

        attribute.NoStore.ShouldBeFalse();
        attribute.Tags.ShouldBe(["contact-channels"]);
        attribute.Duration.ShouldBe(259200);
    }

    [Fact]
    public void GetContactChannelById_DeclaresNoRouteVariationOfItsOwn()
    {
        GetOutputCacheAttribute(nameof(ContactChannelsController.GetContactChannelById))
            .VaryByRouteValueNames.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task GetContactChannelById_WithANonPositiveId_StillReachesTheUseCase(int id)
    {
        ReturnsById(Result<GetContactChannelByIdOutputDto>.Failure(
            new NotFoundError($"No contact channel exists with identifier {id}.")));

        var (statusCode, _) = await ExecuteAsync(await InvokeByIdAsync(id));

        await _getContactChannelByIdUseCase.Received(1)
            .ExecuteAsync(id, Arg.Any<CancellationToken>());
        statusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    private Task<HttpCreatedResult<CreateContactChannelOutputDto>> InvokeCreateAsync(
        CreateContactChannelInputDto? input = null) =>
        CreateController().CreateContactChannel(
            input ?? new CreateContactChannelInputDto("WhatsApp", IsActive: true),
            CancellationToken.None);

    private void ReturnsCreated(Result<CreateContactChannelOutputDto> result) =>
        _createContactChannelUseCase
            .ExecuteAsync(Arg.Any<CreateContactChannelInputDto>(), Arg.Any<CancellationToken>())
            .Returns(result);

    [Fact]
    public async Task CreateContactChannel_WhenTheUseCaseSucceeds_Returns201WithTheGeneratedIdentifier()
    {
        ReturnsCreated(Result<CreateContactChannelOutputDto>.Success(
            new CreateContactChannelOutputDto(42, "WhatsApp", true)));

        var (statusCode, body) = await ExecuteAsync(await InvokeCreateAsync());

        statusCode.ShouldBe(StatusCodes.Status201Created);
        var data = body.RootElement.GetProperty("data");
        data.GetProperty("id").GetInt32().ShouldBe(42);
        data.GetProperty("name").GetString().ShouldBe("WhatsApp");
        data.GetProperty("isActive").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task CreateContactChannel_ForwardsTheInputUntouched()
    {
        ReturnsCreated(Result<CreateContactChannelOutputDto>.Success(
            new CreateContactChannelOutputDto(1, "Feria", false)));

        await InvokeCreateAsync(new CreateContactChannelInputDto("  Feria  ", IsActive: false));

        await _createContactChannelUseCase.Received(1).ExecuteAsync(
            Arg.Is<CreateContactChannelInputDto>(i => i.Name == "  Feria  " && i.IsActive == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateContactChannel_WhenTheNameIsRejected_ReturnsTheMappedErrorStatus()
    {
        ReturnsCreated(Result<CreateContactChannelOutputDto>.Failure(
            new DomainError("Domain validation failed.", ErrorType.DomainError)));

        var (statusCode, _) = await ExecuteAsync(await InvokeCreateAsync());

        statusCode.ShouldBe(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void CreateContactChannel_InvalidatesTheCacheOfTheListing()
    {
        var attribute = typeof(ContactChannelsController)
            .GetMethod(nameof(ContactChannelsController.CreateContactChannel))!
            .GetCustomAttribute<OutputCacheInvalidateAttribute>();

        attribute.ShouldNotBeNull();
        attribute.Tag.ShouldBe(
            GetOutputCacheAttribute(nameof(ContactChannelsController.GetContactChannels)).Tags!.Single(),
            "a creation must evict the very tag the listing is cached under");
    }
}
