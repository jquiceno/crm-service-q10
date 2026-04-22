using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Shared.Application.Interfaces;
using System.Reflection;

namespace Infrastructure.Validation.FluentValidation;

public static class ValidatorRegistrationExtensions
{
    /// <summary>
    /// Scans the Infrastructure assembly and registers validators automatically:
    /// <list type="bullet">
    ///   <item>Types directly implementing <see cref="IStructuralValidator{T}"/> → registered as
    ///   <see cref="IStructuralValidator{T}"/> (HTTP filter) and <see cref="IRequestValidator{T}"/>.</item>
    ///   <item>Types inheriting <see cref="IStructuralValidator{T}"/> from a base class → registered
    ///   as <see cref="IValidator{T}"/> (use-case level via <see cref="FluentValidationAdapter{T}"/>).</item>
    ///   <item>If no full validator exists for a DTO, the structural validator is also registered as
    ///   <see cref="IValidator{T}"/> so use cases can still resolve it.</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddContextValidators(this IServiceCollection services) => services.AddContextValidators(typeof(ValidatorRegistrationExtensions).Assembly);

    private static IServiceCollection AddContextValidators(this IServiceCollection services, Assembly assembly)
    {
        var concreteTypes = assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract);

        var baseValidators = new Dictionary<Type, Type>();
        var fullValidators = new Dictionary<Type, Type>();

        foreach (var type in concreteTypes)
        {
            foreach (var iface in type.GetInterfaces())
            {
                if (!iface.IsGenericType || iface.GetGenericTypeDefinition() != typeof(IStructuralValidator<>))
                    continue;

                var dtoType = iface.GetGenericArguments()[0];

                var isBase = type.BaseType is null ||
                    !type.BaseType.GetInterfaces().Any(i =>
                        i.IsGenericType &&
                        i.GetGenericTypeDefinition() == typeof(IStructuralValidator<>) &&
                        i.GetGenericArguments()[0] == dtoType);

                if (isBase)
                    baseValidators[dtoType] = type;
                else
                    fullValidators[dtoType] = type;
            }
        }

        foreach (var (dtoType, validatorType) in baseValidators)
        {
            services.AddScoped(typeof(IStructuralValidator<>).MakeGenericType(dtoType), validatorType);

            var adapterType = typeof(FluentRequestValidationAdapter<>).MakeGenericType(dtoType);
            services.AddScoped(typeof(IRequestValidator<>).MakeGenericType(dtoType), adapterType);

            if (!fullValidators.ContainsKey(dtoType))
                services.AddScoped(typeof(IValidator<>).MakeGenericType(dtoType), validatorType);
        }

        foreach (var (dtoType, validatorType) in fullValidators)
            services.AddScoped(typeof(IValidator<>).MakeGenericType(dtoType), validatorType);

        return services;
    }
}