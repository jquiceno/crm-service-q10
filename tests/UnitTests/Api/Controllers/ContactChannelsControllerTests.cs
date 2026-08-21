using System.Reflection;
using System.Text.Json;
using Api.Controllers;
using ContactChannel.Application.UseCases.GetContactChannels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Shared.Application.Dtos;
using Shared.Domain.Pagination;
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

    private static async Task<(int StatusCode, JsonDocument Body)> ExecuteAsync(
        HttpOkPagedResult<GetContactChannelsOutputDto> result)
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
        new ContactChannelsController().GetContactChannels(
            _getContactChannelsUseCase,
            filter ?? new GetContactChannelsInputDto(IsActive: null, SearchName: null),
            pagination ?? new PageQueryInputDto(),
            CancellationToken.None);

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

    private static OutputCacheAttribute GetOutputCacheAttribute()
    {
        var attribute = typeof(ContactChannelsController)
            .GetMethod(nameof(ContactChannelsController.GetContactChannels))!
            .GetCustomAttribute<OutputCacheAttribute>();

        attribute.ShouldNotBeNull();

        return attribute;
    }

    [Fact]
    public void GetContactChannels_IsCachedUnderTheContactChannelsTag()
    {
        var attribute = GetOutputCacheAttribute();

        attribute.NoStore.ShouldBeFalse();
        attribute.Tags.ShouldBe(["contact-channels"]);
    }

    [Fact]
    public void GetContactChannels_DeclaresItsOwnTtlOfThreeDays()
    {
        GetOutputCacheAttribute().Duration.ShouldBe(259200);
    }

    [Fact]
    public void GetContactChannels_DeclaresNoQueryVariationOfItsOwn()
    {
        GetOutputCacheAttribute().VaryByQueryKeys.ShouldBeNull();
    }
}
