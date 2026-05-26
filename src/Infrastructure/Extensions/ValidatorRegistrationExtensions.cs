using Infrastructure.Adapters.Validation;
using Infrastructure.Validation.FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Shared.Application.Ports;
using System.Reflection;

namespace Infrastructure.Extensions;

public static class ValidatorRegistrationExtensions
{
    /// <summary>
    /// Scans the Infrastructure assembly for types implementing
    /// <see cref="IStructuralValidator{T}"/> and registers them as both the structural
    /// validator and the <see cref="IRequestValidatorPort{T}"/> consumed by the HTTP filter
    /// (via <see cref="FluentRequestValidationAdapter{T}"/>).
    /// </summary>
    public static IServiceCollection AddContextValidators(this IServiceCollection services) =>
        services.AddContextValidators(typeof(ValidatorRegistrationExtensions).Assembly);

    private static IServiceCollection AddContextValidators(this IServiceCollection services, Assembly assembly)
    {
        var structuralValidators = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStructuralValidator<>))
                .Select(i => (DtoType: i.GetGenericArguments()[0], ValidatorType: t)));

        foreach (var (dtoType, validatorType) in structuralValidators)
        {
            services.AddScoped(typeof(IStructuralValidator<>).MakeGenericType(dtoType), validatorType);

            var adapterType = typeof(FluentRequestValidationAdapter<>).MakeGenericType(dtoType);
            services.AddScoped(typeof(IRequestValidatorPort<>).MakeGenericType(dtoType), adapterType);
        }

        return services;
    }
}
