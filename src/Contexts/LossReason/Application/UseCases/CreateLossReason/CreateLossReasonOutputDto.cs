using System.ComponentModel;

namespace LossReason.Application.UseCases.CreateLossReason;

public sealed record CreateLossReasonOutputDto(
    [property: Description("Identificador asignado a la causa de pérdida creada.")]
    int Id,
    [property: Description("Nombre de la causa de pérdida.")]
    string Name,
    [property: Description("Si la causa queda visible en el catálogo.")]
    bool IsActive);
