using System.ComponentModel;

namespace LossReason.Application.UseCases.GetLossReasons;

public sealed record GetLossReasonsInputDto(
    [property: Description("Filtra las causas cuyo nombre contenga este texto. Opcional; máximo 50 caracteres.")]
    string? Name,
    [property: Description("Filtra por estado: true solo activas, false solo inactivas. Opcional; si se omite se devuelven todas.")]
    bool? IsActive);
