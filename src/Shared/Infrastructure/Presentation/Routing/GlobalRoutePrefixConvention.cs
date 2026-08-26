using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Shared.Presentation.Routing;

// Prepends the RoutePrefix to every controller route (minimal-API endpoints are prefixed at their map
// calls instead). Throws on absolute routes ([Route("/x")] / [Route("~/x")]): that override pattern
// under UsePathBase was the original 405 bug and would silently escape the prefix.
public sealed class GlobalRoutePrefixConvention : IApplicationModelConvention
{
    private readonly AttributeRouteModel _prefix;

    public GlobalRoutePrefixConvention(string prefix) =>
        _prefix = new AttributeRouteModel(new RouteAttribute(RoutePrefixConfig.Normalize(prefix)));

    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            foreach (var selector in controller.Selectors)
            {
                var template = selector.AttributeRouteModel?.Template;
                if (IsOverridePattern(template))
                {
                    throw new InvalidOperationException(
                        $"Controller '{controller.ControllerType.FullName}' declares an absolute route " +
                        $"'{template}', which escapes the global route prefix. Use a relative route.");
                }

                selector.AttributeRouteModel = selector.AttributeRouteModel is null
                    ? _prefix
                    : AttributeRouteModel.CombineAttributeRouteModel(_prefix, selector.AttributeRouteModel);
            }
        }
    }

    private static bool IsOverridePattern(string? template) =>
        template is not null && (template.StartsWith('/') || template.StartsWith("~/", StringComparison.Ordinal));
}
