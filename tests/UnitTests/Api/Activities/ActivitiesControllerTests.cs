using System.Text.Json;
using Activities.Application.UseCases.CreateActivity;
using Activities.Application.UseCases.GetActivities;
using Activities.Domain.Errors;
using Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using NSubstitute;
using Shared.Application.Dtos;
using Shared.Domain.Pagination;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Api.Activities;

/// <summary>
/// The controller's own job: hand the request to the use case and turn its <c>Result</c> into the
/// contract. One case per row of the errors-to-HTTP table (§6.x).
/// </summary>
public sealed class ActivitiesControllerTests
{
    private readonly IGetActivitiesUseCase _getActivities = Substitute.For<IGetActivitiesUseCase>();
    private readonly ICreateActivityUseCase _createActivity = Substitute.For<ICreateActivityUseCase>();

    private ActivitiesController Sut => new(_getActivities, _createActivity);

    // --- GET /activities ---------------------------------------------------------------------

    [Fact]
    public async Task GetActivities_WhenTheUseCaseSucceeds_AnswersTheItemsAndTheTotal()
    {
        GetReturns(PagedResult<GetActivitiesOutputDto>.Success([Row(380995), Row(380996)], 128));

        var (statusCode, body) = await ExecuteAsync(
            await Sut.GetActivities(new GetActivitiesInputDto(1200, null, null), new PageQueryInputDto()));

        statusCode.ShouldBe(StatusCodes.Status200OK);
        var data = body.RootElement.GetProperty("data");
        data.GetProperty("totalCount").GetInt32().ShouldBe(128);
        data.GetProperty("items").GetArrayLength().ShouldBe(2);
        data.GetProperty("items")[0].GetProperty("type").GetString().ShouldBe("call");
        body.RootElement.GetProperty("statusCode").GetInt32().ShouldBe(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task GetActivities_TranslatesThePaginationOfTheRequest()
    {
        GetReturns(PagedResult<GetActivitiesOutputDto>.Success([], 0));

        await Sut.GetActivities(
            new GetActivitiesInputDto(null, 845, null),
            new PageQueryInputDto(PageIndex: 2, PageSize: 5000));

        await _getActivities.Received(1).ExecuteAsync(
            new GetActivitiesInputDto(null, 845, null),
            Arg.Is<PageQuery>(page => page.PageIndex == 2 && page.PageSize == 5000),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetActivities_WhenTheUseCaseFails_AnswersTheMappedStatus()
    {
        GetReturns(PagedResult<GetActivitiesOutputDto>.Failure(
            new DomainError("Persistence failure.", ErrorType.Internal)));

        var (statusCode, body) = await ExecuteAsync(
            await Sut.GetActivities(new GetActivitiesInputDto(1200, null, null), new PageQueryInputDto()));

        statusCode.ShouldBe(StatusCodes.Status500InternalServerError);
        body.RootElement.GetProperty("error").GetProperty("type").GetString().ShouldBe("INTERNAL");
    }

    // --- POST /activities --------------------------------------------------------------------

    [Fact]
    public async Task CreateActivity_WhenTheUseCaseSucceeds_Answers201WithTheConsecutiveAlone()
    {
        CreateReturns(Result<CreateActivityOutputDto>.Success(new CreateActivityOutputDto(380996)));

        var (statusCode, body) = await ExecuteAsync(await Sut.CreateActivity(Input()));

        statusCode.ShouldBe(StatusCodes.Status201Created);
        var data = body.RootElement.GetProperty("data");
        data.GetProperty("id").GetInt32().ShouldBe(380996);
        data.EnumerateObject().Count().ShouldBe(1, "the legacy POST answered the consecutive and nothing else");
    }

    /// <summary>
    /// One row per line of §6.x, against the real mapper of <c>Shared</c>. Every 400 of this
    /// endpoint is a <c>DOMAIN_VALIDATION</c>: the use case puts even its own validation errors in
    /// the aggregate's envelope, so that the offending field always travels in <c>details</c>.
    /// <c>VALIDATION</c> is what the request filter answers, before the use case runs.
    /// </summary>
    [Theory]
    [InlineData(nameof(ActivityErrors.DealNotFound), StatusCodes.Status404NotFound, "NOT_FOUND")]
    [InlineData(nameof(ActivityErrors.AdvisorNotFound), StatusCodes.Status404NotFound, "NOT_FOUND")]
    [InlineData(nameof(ActivityErrors.OpportunityArchived), StatusCodes.Status400BadRequest, "DOMAIN_VALIDATION")]
    [InlineData(nameof(ActivityErrors.InvalidActivityStatus), StatusCodes.Status400BadRequest, "DOMAIN_VALIDATION")]
    [InlineData(nameof(ActivityErrors.StatusNotCreatable), StatusCodes.Status400BadRequest, "DOMAIN_VALIDATION")]
    [InlineData(nameof(ActivityErrors.InvalidActivityType), StatusCodes.Status400BadRequest, "DOMAIN_VALIDATION")]
    [InlineData(nameof(ActivityErrors.OutcomeNotAllowedWhenScheduled), StatusCodes.Status400BadRequest, "DOMAIN_VALIDATION")]
    [InlineData(nameof(ActivityErrors.OutcomeTypeNotAllowedWhenScheduled), StatusCodes.Status400BadRequest, "DOMAIN_VALIDATION")]
    [InlineData(nameof(ActivityErrors.DescriptionNotAllowedWhenCompleted), StatusCodes.Status400BadRequest, "DOMAIN_VALIDATION")]
    [InlineData(nameof(ActivityErrors.TypeNotWritable), StatusCodes.Status400BadRequest, "DOMAIN_VALIDATION")]
    [InlineData(nameof(ActivityErrors.NoteCannotBeScheduled), StatusCodes.Status400BadRequest, "DOMAIN_VALIDATION")]
    [InlineData(nameof(ActivityErrors.DescriptionRequired), StatusCodes.Status400BadRequest, "DOMAIN_VALIDATION")]
    [InlineData(nameof(ActivityErrors.DueDateRequired), StatusCodes.Status400BadRequest, "DOMAIN_VALIDATION")]
    [InlineData(nameof(ActivityErrors.OutcomeRequired), StatusCodes.Status400BadRequest, "DOMAIN_VALIDATION")]
    [InlineData(nameof(ActivityErrors.OutcomeTypeRequired), StatusCodes.Status400BadRequest, "DOMAIN_VALIDATION")]
    public async Task CreateActivity_MapsEachDomainErrorToItsStatus(
        string errorName, int expectedStatus, string expectedType)
    {
        CreateReturns(Result<CreateActivityOutputDto>.Failure(ErrorNamed(errorName)));

        var (statusCode, body) = await ExecuteAsync(await Sut.CreateActivity(Input()));

        statusCode.ShouldBe(expectedStatus);
        var error = body.RootElement.GetProperty("error");
        error.GetProperty("type").GetString().ShouldBe(expectedType);
        error.GetProperty("code").GetString().ShouldBe($"HTTP.{expectedType}");
        body.RootElement.GetProperty("statusCode").GetInt32().ShouldBe(expectedStatus);

        if (expectedStatus == StatusCodes.Status400BadRequest)
            error.GetProperty("details").GetArrayLength()
                .ShouldBeGreaterThan(0, "a 400 must name the field that caused it");
    }

    [Fact]
    public async Task CreateActivity_WhenItFails_ReportsTheOffendingFieldInTheDetails()
    {
        CreateReturns(Result<CreateActivityOutputDto>.Failure(
            DomainError.FromValidationDomainErrors(
                [ActivityErrors.DescriptionRequired with { Value = "" }])));

        var (_, body) = await ExecuteAsync(await Sut.CreateActivity(Input()));

        var detail = body.RootElement.GetProperty("error").GetProperty("details")[0];
        detail.GetProperty("property").GetString().ShouldBe("description");
        detail.GetProperty("errors")[0].GetString().ShouldBe(ActivityErrors.DescriptionRequired.Message);
    }

    // --- Helpers -----------------------------------------------------------------------------

    /// <summary>
    /// The errors the use case can surface, in the shape it actually surfaces them: the not-found
    /// ones bare, and every validation error inside the envelope its <c>Enrich</c> builds — which
    /// is the difference between a payload that names the offending field and one with an empty
    /// <c>details</c>. Anything not listed here is a mistake in the theory, not a missing case.
    /// </summary>
    private static DomainError ErrorNamed(string name) => name switch
    {
        nameof(ActivityErrors.DealNotFound) => ActivityErrors.DealNotFound(1200),
        nameof(ActivityErrors.AdvisorNotFound) => ActivityErrors.AdvisorNotFound("1017123456"),
        _ => DomainError.FromValidationDomainErrors([ValidationErrorNamed(name)]),
    };

    private static ValidationError ValidationErrorNamed(string name) => name switch
    {
        nameof(ActivityErrors.OpportunityArchived) => ActivityErrors.OpportunityArchived,
        nameof(ActivityErrors.InvalidActivityStatus) => ActivityErrors.InvalidActivityStatus,
        nameof(ActivityErrors.StatusNotCreatable) => ActivityErrors.StatusNotCreatable,
        nameof(ActivityErrors.InvalidActivityType) => ActivityErrors.InvalidActivityType,
        nameof(ActivityErrors.OutcomeNotAllowedWhenScheduled) => ActivityErrors.OutcomeNotAllowedWhenScheduled,
        nameof(ActivityErrors.OutcomeTypeNotAllowedWhenScheduled) => ActivityErrors.OutcomeTypeNotAllowedWhenScheduled,
        nameof(ActivityErrors.DescriptionNotAllowedWhenCompleted) => ActivityErrors.DescriptionNotAllowedWhenCompleted,
        nameof(ActivityErrors.TypeNotWritable) => ActivityErrors.TypeNotWritable,
        nameof(ActivityErrors.NoteCannotBeScheduled) => ActivityErrors.NoteCannotBeScheduled,
        nameof(ActivityErrors.DescriptionRequired) => ActivityErrors.DescriptionRequired,
        nameof(ActivityErrors.DueDateRequired) => ActivityErrors.DueDateRequired,
        nameof(ActivityErrors.OutcomeRequired) => ActivityErrors.OutcomeRequired,
        nameof(ActivityErrors.OutcomeTypeRequired) => ActivityErrors.OutcomeTypeRequired,
        _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unlisted activity error."),
    };

    private static GetActivitiesOutputDto Row(int id) =>
        new(id, 1200, "Negocio", 845, "Oportunidad", "call", "completed", null, "Se contactó",
            "contacted", "advisor-01", "Ana Pérez", "1017123456",
            new DateTime(2026, 8, 1, 10, 15, 0, DateTimeKind.Utc), null,
            new DateTime(2026, 8, 1, 10, 20, 0, DateTimeKind.Utc));

    private static CreateActivityInputDto Input() =>
        new(1200, "scheduled", "call", "1017123456", DateTime.UtcNow, "Llamar", null, null,
            DateTime.UtcNow.AddDays(1));

    private void GetReturns(PagedResult<GetActivitiesOutputDto> result) =>
        _getActivities
            .ExecuteAsync(Arg.Any<GetActivitiesInputDto>(), Arg.Any<PageQuery>(), Arg.Any<CancellationToken>())
            .Returns(result);

    private void CreateReturns(Result<CreateActivityOutputDto> result) =>
        _createActivity
            .ExecuteAsync(Arg.Any<CreateActivityInputDto>(), Arg.Any<CancellationToken>())
            .Returns(result);

    private static async Task<(int StatusCode, JsonDocument Body)> ExecuteAsync(IActionResult result)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        await result.ExecuteResultAsync(actionContext).ConfigureAwait(true);

        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(httpContext.Response.Body);
        var json = await reader.ReadToEndAsync().ConfigureAwait(true);
        return (httpContext.Response.StatusCode, JsonDocument.Parse(json));
    }
}
