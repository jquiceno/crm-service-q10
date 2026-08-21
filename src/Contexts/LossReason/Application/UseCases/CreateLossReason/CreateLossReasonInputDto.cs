using System.ComponentModel;

namespace LossReason.Application.UseCases.CreateLossReason;

// Name is nullable on purpose: it lets the input validator report the error on its own
// Property instead of the deserializer failing with a generic 400.
public sealed record CreateLossReasonInputDto(
    [property: Description("Nombre de la causa de pérdida. Obligatorio, máximo 50 caracteres.")]
    string? Name,
    [property: Description("Si la causa queda visible en el catálogo. Por defecto, verdadero.")]
    bool IsActive = true);
