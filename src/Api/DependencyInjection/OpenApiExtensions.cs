using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Api.DependencyInjection;

public static class OpenApiExtensions
{
    public static IServiceCollection AddOpenApiDocumentation(
        this IServiceCollection services,
        IHostEnvironment environment)
    {
        if (!environment.IsDevelopment())
            return services;

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, __) =>
            {
                document.Info = new()
                {
                    Title = "Service API",
                    Version = "v1",
                    Description = "API description",
                    Contact = new OpenApiContact
                    {
                        Name = "Service API",
                        Email = "info@example.com",
                        Url = new Uri("https://example.com")
                    }
                };
                return Task.CompletedTask;
            });

            options.AddSchemaTransformer((schema, context, _) =>
            {
                // Enums may surface as Nullable<TEnum>, whose own IsEnum is false; unwrap first.
                var enumType = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
                if (enumType.IsEnum)
                {
                    var lines = Enum.GetValues(enumType)
                        .Cast<object>()
                        .Select(v => $"- `{Convert.ToInt32(v)}` = {v}");
                    schema.Description = $"{schema.Description}\n\n{string.Join("\n", lines)}".Trim();
                }
                return Task.CompletedTask;
            });
        });

        return services;
    }

    public static WebApplication UseOpenApiDocumentation(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            return app;

        app.MapOpenApi();
        app.MapScalarApiReference("/openapi", options =>
        {
            options.OpenApiRoutePattern = "/openapi/{documentName}.json";
        });
        app.MapGet("/openapi", () => Results.Redirect("/openapi/v1")).ExcludeFromDescription();

        return app;
    }
}
