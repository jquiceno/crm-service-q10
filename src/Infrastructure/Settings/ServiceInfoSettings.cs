using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Settings;

public sealed class ServiceInfoSettings
{
    public const string SectionName = "ServiceInfo";

    [Required]
    [MinLength(1)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string Version { get; init; } = string.Empty;
}