using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Settings;

public sealed class TemplateSettings
{
    public const string SectionName = "Template";

    [Required]
    [MinLength(1)]
    public string Version { get; init; } = string.Empty;
}
