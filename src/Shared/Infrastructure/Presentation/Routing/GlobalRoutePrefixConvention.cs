using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace Shared.Presentation.Routing;

/// <summary>
/// Prepends a fixed URL prefix to every controller route so the whole API is served under a single
/// path segment (matching the ingress path) without <c>UsePathBase</c>. Applied at the controller
/// level only, so action routes inherit it once and minimal-API endpoints (health, OpenAPI) are left
/// untouched — those are prefixed explicitly where they are mapped.
/// </summary>
public sealed class GlobalRoutePrefixConvention : IApplicationModelConvention
{
    private readonly AttributeRouteModel _prefix;

    public GlobalRoutePrefixConvention(string prefix)
    {
        var normalized = (prefix ?? string.Empty).Trim('/');
        _prefix = new AttributeRouteModel(new RouteAttribute(normalized));
    }

    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
        {
            foreach (var selector in controller.Selectors)
            {
                selector.AttributeRouteModel = selector.AttributeRouteModel is null
                    ? _prefix
                    : AttributeRouteModel.CombineAttributeRouteModel(_prefix, selector.AttributeRouteModel);
            }
        }
    }
}
