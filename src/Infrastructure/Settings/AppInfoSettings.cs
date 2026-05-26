using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Settings;

public sealed class AppInfoSettings
{
    public const string SectionName = "AppInfo";

    [Required]
    [MinLength(1)]
    public string ServiceName { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string Version { get; init; } = string.Empty;
}