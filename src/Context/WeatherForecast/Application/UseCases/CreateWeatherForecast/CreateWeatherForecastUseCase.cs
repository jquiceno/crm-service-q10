using FluentValidation;
using WeatherForecast.Domain.Common;
using WeatherForecast.Domain.Entities;
using WeatherForecast.Domain.Interfaces;

namespace WeatherForecast.Application.UseCases.CreateWeatherForecast;

public sealed class CreateWeatherForecastUseCase(
    IValidator<CreateWeatherForecastInputDto> validator,
    IRepository<WeatherForecastEntity> repository) : ICreateWeatherForecastUseCase
{
    public async Task<Result<CreateWeatherForecastOutputDto>> ExecuteAsync(
        CreateWeatherForecastInputDto input, CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(input, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<CreateWeatherForecastOutputDto>.Failure(
                new Error("Validation", errors));
        }

        var entity = input.ToEntity();

        await repository.AddAsync(entity, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return Result<CreateWeatherForecastOutputDto>.Success(entity.ToCreateDto());
    }
}
