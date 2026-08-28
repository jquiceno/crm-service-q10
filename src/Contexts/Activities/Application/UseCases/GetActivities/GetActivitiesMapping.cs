using Activities.Application.Mapping;
using Activities.Domain.Models;

namespace Activities.Application.UseCases.GetActivities;

/// <summary>Translates a listing row into its contract shape.</summary>
public static class GetActivitiesMapping
{
    public static GetActivitiesOutputDto ToOutputDto(this ActivityListItem item)
    {
        var activity = item.Activity;

        return new GetActivitiesOutputDto(
            activity.Id,
            activity.DealId,
            item.DealName,
            activity.OpportunityId,
            item.OpportunityName,
            ContractNames.ToContract(activity.Type),
            ContractNames.ToContract(activity.Status),
            activity.Description?.Value,
            activity.Outcome?.Value,
            activity.OutcomeType is null ? null : ContractNames.ToOutcomeContract(activity.OutcomeType.Name),
            activity.AdvisorId?.Value,
            item.AdvisorName,
            item.AdvisorIdentification,
            activity.CreatedAt,
            activity.DueAt,
            activity.CompletedAt);
    }
}
