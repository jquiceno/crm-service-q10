using System.ComponentModel.DataAnnotations;

namespace Api.Settings;

public sealed class AppInfoSettings
{
    [Required]
    [MinLength(1)]
    public string ServiceName { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string Version { get; init; } = string.Empty;
}
