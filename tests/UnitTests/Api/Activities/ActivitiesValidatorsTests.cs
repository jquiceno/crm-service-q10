using Activities.Application.UseCases.CreateActivity;
using Activities.Application.UseCases.GetActivities;
using FluentValidation.TestHelper;
using Infrastructure.Validation.FluentValidation.Activities;
using Xunit;

namespace UnitTests.Api.Activities;

/// <summary>
/// The structural rules the request must satisfy before any use case runs (§6.1, §6.2). What each
/// status requires or forbids is the domain's, and is tested with the use case.
/// </summary>
public sealed class ActivitiesValidatorsTests
{
    private readonly GetActivitiesInputValidator _getValidator = new();
    private readonly CreateActivityInputValidator _createValidator = new();

    // --- GET ---------------------------------------------------------------------------------

    [Theory]
    [InlineData(1200, null, null)]
    [InlineData(null, 845, null)]
    [InlineData(null, null, 3)]
    public void GetActivities_WithAtLeastOneFilter_IsValid(int? dealId, int? opportunityId, int? dealStateId) =>
        _getValidator.TestValidate(new GetActivitiesInputDto(dealId, opportunityId, dealStateId))
            .ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void GetActivities_WithNoFilterAtAll_IsRejected() =>
        _getValidator.TestValidate(new GetActivitiesInputDto(null, null, null))
            .ShouldHaveValidationErrorFor(input => input.DealId)
            .WithErrorMessage("At least one of deal id, opportunity id or deal state id is required.");

    [Fact]
    public void GetActivities_WithADealThatIsNotAConsecutive_IsRejected() =>
        _getValidator.TestValidate(new GetActivitiesInputDto(0, null, null))
            .ShouldHaveValidationErrorFor(input => input.DealId);

    [Fact]
    public void GetActivities_WithAnOpportunityThatIsNotAConsecutive_IsRejected() =>
        _getValidator.TestValidate(new GetActivitiesInputDto(null, -1, null))
            .ShouldHaveValidationErrorFor(input => input.OpportunityId);

    [Fact]
    public void GetActivities_WithADealStateThatIsNotAConsecutive_IsRejected() =>
        _getValidator.TestValidate(new GetActivitiesInputDto(null, null, 0))
            .ShouldHaveValidationErrorFor(input => input.DealStateId);

    // --- POST --------------------------------------------------------------------------------

    [Fact]
    public void CreateActivity_WithEveryRequiredField_IsValid() =>
        _createValidator.TestValidate(Input()).ShouldNotHaveAnyValidationErrors();

    [Fact]
    public void CreateActivity_WithoutADeal_IsRejected() =>
        _createValidator.TestValidate(Input() with { DealId = 0 })
            .ShouldHaveValidationErrorFor(input => input.DealId);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateActivity_WithoutAStatus_IsRejected(string? status) =>
        _createValidator.TestValidate(Input() with { Status = status })
            .ShouldHaveValidationErrorFor(input => input.Status);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateActivity_WithoutAType_IsRejected(string? type) =>
        _createValidator.TestValidate(Input() with { Type = type })
            .ShouldHaveValidationErrorFor(input => input.Type);

    [Fact]
    public void CreateActivity_WithoutAnAdvisor_IsRejected() =>
        _createValidator.TestValidate(Input() with { AdvisorIdentification = null })
            .ShouldHaveValidationErrorFor(input => input.AdvisorIdentification);

    [Fact]
    public void CreateActivity_WithAnAdvisorIdentificationLongerThanTheColumn_IsRejected() =>
        _createValidator.TestValidate(Input() with { AdvisorIdentification = new string('9', 21) })
            .ShouldHaveValidationErrorFor(input => input.AdvisorIdentification);

    [Fact]
    public void CreateActivity_WithoutAnActivityDate_IsRejected() =>
        _createValidator.TestValidate(Input() with { ActivityDate = null })
            .ShouldHaveValidationErrorFor(input => input.ActivityDate);

    /// <summary>
    /// The description's own length is the domain's rule, not this layer's: reporting it here
    /// would hide the limit that <c>Description.Create</c> attaches to its error.
    /// </summary>
    [Fact]
    public void CreateActivity_WithALongDescription_IsLeftToTheDomain() =>
        _createValidator.TestValidate(Input() with { Description = new string('x', 501) })
            .ShouldNotHaveValidationErrorFor(input => input.Description);

    /// <summary>
    /// The cap is the narrowest tenant's column, enforced only at the edge during phase 1: the
    /// domain's contract for the outcome text is varchar(MAX) (DEC-3).
    /// </summary>
    [Fact]
    public void CreateActivity_WithAnOutcomeLongerThanTheNarrowestTenantColumn_IsRejected() =>
        _createValidator.TestValidate(Input() with { Outcome = new string('x', 2001) })
            .ShouldHaveValidationErrorFor(input => input.Outcome);

    [Fact]
    public void CreateActivity_WithAnOutcomeThatFitsTheNarrowestTenantColumn_IsValid() =>
        _createValidator.TestValidate(Input() with { Status = "completed", Outcome = new string('x', 2000) })
            .ShouldNotHaveAnyValidationErrors();

    private static CreateActivityInputDto Input() =>
        new(
            DealId: 1200,
            Status: "scheduled",
            Type: "call",
            AdvisorIdentification: "1017123456",
            ActivityDate: new DateTime(2026, 8, 28, 15, 0, 0, DateTimeKind.Utc),
            Description: "Llamar al cliente",
            Outcome: null,
            OutcomeType: null,
            DueAt: new DateTime(2026, 8, 29, 15, 0, 0, DateTimeKind.Utc));
}
