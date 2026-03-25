# Stage 1: Restore
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS restore
WORKDIR /src

COPY Directory.Build.props .
COPY ServiceTemplate.slnx .
COPY src/Context/WeatherForecast/Domain/WeatherForecast.Domain.csproj src/Context/WeatherForecast/Domain/
COPY src/Context/WeatherForecast/Application/WeatherForecast.Application.csproj src/Context/WeatherForecast/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/Api/Api.csproj src/Api/

RUN dotnet restore ServiceTemplate.slnx

# Stage 2: Publish
FROM restore AS publish
COPY src/ src/
RUN dotnet publish src/Api/Api.csproj -c Release -o /app/publish --no-restore

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

RUN adduser --disabled-password --gecos "" appuser
USER appuser

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]
