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
            // Action-level absolute routes ([HttpGet("/x")]) override the controller route and escape
            // the prefix just like a controller-level one, so guard both levels.
            foreach (var action in controller.Actions)
                foreach (var selector in action.Selectors)
                    ThrowIfOverride(controller, selector.AttributeRouteModel?.Template);

            foreach (var selector in controller.Selectors)
            {
                ThrowIfOverride(controller, selector.AttributeRouteModel?.Template);
                selector.AttributeRouteModel = selector.AttributeRouteModel is null
                    ? _prefix
                    : AttributeRouteModel.CombineAttributeRouteModel(_prefix, selector.AttributeRouteModel);
            }
        }
    }

    private static void ThrowIfOverride(ControllerModel controller, string? template)
    {
        if (template is null || !(template.StartsWith('/') || template.StartsWith("~/", StringComparison.Ordinal)))
            return;

        throw new InvalidOperationException(
            $"Controller '{controller.ControllerType.FullName}' declares an absolute route '{template}', " +
            "which escapes the global route prefix. Use a relative route.");
    }
}
