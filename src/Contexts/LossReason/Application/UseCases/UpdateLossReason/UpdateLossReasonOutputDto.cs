using System.ComponentModel;

namespace LossReason.Application.UseCases.UpdateLossReason;

public sealed record UpdateLossReasonOutputDto(
    [property: Description("Identificador de la causa de pérdida.")]
    int Id,
    [property: Description("Nombre vigente de la causa de pérdida.")]
    string Name,
    [property: Description("Si la causa queda visible en el catálogo.")]
    bool IsActive);
