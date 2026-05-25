namespace Health.Application.UseCases.GetHealthInfo;

public sealed record GetHealthInfoOutputDto(string Status, string ServiceName, string Version);
