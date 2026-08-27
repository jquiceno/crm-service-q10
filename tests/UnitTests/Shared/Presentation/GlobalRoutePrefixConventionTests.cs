using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Shared.Presentation.Routing;
using Shouldly;
using Xunit;

namespace UnitTests.Shared.Presentation;

public sealed class GlobalRoutePrefixConventionTests
{
    private sealed class DummyController
    {
        public static void Get() { }
    }

    private static ControllerModel BuildController(string? controllerTemplate)
    {
        var controller = new ControllerModel(typeof(DummyController).GetTypeInfo(), []);
        var selector = new SelectorModel();
        if (controllerTemplate is not null)
            selector.AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(controllerTemplate));
        controller.Selectors.Add(selector);
        return controller;
    }

    private static string? Apply(string prefix, string? controllerTemplate)
    {
        var controller = BuildController(controllerTemplate);
        var application = new ApplicationModel();
        application.Controllers.Add(controller);

        new GlobalRoutePrefixConvention(prefix).Apply(application);

        return controller.Selectors[0].AttributeRouteModel?.Template;
    }

    private static void ApplyWithActionRoute(string prefix, string actionTemplate)
    {
        var controller = BuildController(controllerTemplate: "");
        var action = new ActionModel(typeof(DummyController).GetMethod(nameof(DummyController.Get))!, []);
        action.Selectors.Add(new SelectorModel
        {
            AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(actionTemplate)),
        });
        controller.Actions.Add(action);

        var application = new ApplicationModel();
        application.Controllers.Add(controller);

        new GlobalRoutePrefixConvention(prefix).Apply(application);
    }

    [Theory]
    [InlineData("service-template", "", "service-template")]
    [InlineData("/service-template/", "", "service-template")]
    [InlineData("service-template", "info", "service-template/info")]
    [InlineData("service-template", "{id:int}", "service-template/{id:int}")]
    [InlineData("service-template", "{parentId:int}/children", "service-template/{parentId:int}/children")]
    public void Apply_PrependsPrefixToControllerRoute(string prefix, string controllerTemplate, string expected) =>
        Apply(prefix, controllerTemplate).ShouldBe(expected);

    [Fact]
    public void Apply_WhenControllerHasNoRoute_UsesThePrefixAsTheRoute() =>
        Apply("service-template", controllerTemplate: null).ShouldBe("service-template");

    [Theory]
    [InlineData("/absolute")]
    [InlineData("~/tilde")]
    public void Apply_WhenControllerRouteIsAbsolute_Throws(string absoluteTemplate)
    {
        var act = () => Apply("service-template", absoluteTemplate);

        act.ShouldThrow<InvalidOperationException>();
    }

    [Theory]
    [InlineData("/absolute")]
    [InlineData("~/tilde")]
    public void Apply_WhenActionRouteIsAbsolute_Throws(string absoluteTemplate)
    {
        var act = () => ApplyWithActionRoute("service-template", absoluteTemplate);

        act.ShouldThrow<InvalidOperationException>();
    }
}
