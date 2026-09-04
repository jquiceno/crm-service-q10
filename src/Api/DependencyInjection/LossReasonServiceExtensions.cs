using Infrastructure.Persistence.EntityFramework.LossReasons;
using LossReason.Application.Ports;
using LossReason.Application.UseCases.CreateLossReason;
using LossReason.Application.UseCases.DeleteLossReason;
using LossReason.Application.UseCases.GetLossReasonById;
using LossReason.Application.UseCases.GetLossReasons;
using LossReason.Application.UseCases.UpdateLossReason;
using LossReason.Domain.Repositories;

namespace Api.DependencyInjection;

public static class LossReasonServiceExtensions
{
    public static IServiceCollection AddLossReasonServices(this IServiceCollection services)
    {
        // Persistence first, then the use cases that consume it. Everything Scoped: the repository
        // and the reader hang off ApplicationDbContext, whose connection is resolved per request.
        services.AddScoped<ILossReasonRepository, LossReasonRepository>();
        services.AddScoped<ILossReasonUsageReader, LossReasonUsageReader>();

        services.AddScoped<IGetLossReasonsUseCase, GetLossReasonsUseCase>();
        services.AddScoped<IGetLossReasonByIdUseCase, GetLossReasonByIdUseCase>();
        services.AddScoped<ICreateLossReasonUseCase, CreateLossReasonUseCase>();
        services.AddScoped<IUpdateLossReasonUseCase, UpdateLossReasonUseCase>();
        services.AddScoped<IDeleteLossReasonUseCase, DeleteLossReasonUseCase>();

        return services;
    }
}
