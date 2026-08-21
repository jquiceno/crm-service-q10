using System.ComponentModel;

namespace LossReason.Application.UseCases.GetLossReasons;

public sealed record GetLossReasonsOutputDto(
    [property: Description("Identificador de la causa de pérdida.")]
    int Id,
    [property: Description("Nombre de la causa de pérdida.")]
    string Name,
    [property: Description("Indica si la causa está activa.")]
    bool IsActive);
