# syntax=docker/dockerfile:1

# Stage 1: Restore
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS restore
WORKDIR /src

COPY Directory.Build.props ServiceTemplate.slnx ./
COPY --parents src/**/*.csproj ./

RUN dotnet restore src/Api/Api.csproj

# Stage 2: Publish
FROM restore AS publish
COPY src/ src/
RUN dotnet publish src/Api/Api.csproj -c Release -o /app/publish --no-restore

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ARG APP_PORT=8080
ENV ASPNETCORE_URLS=http://+:${APP_PORT}
EXPOSE ${APP_PORT}

RUN adduser --disabled-password --gecos "" appuser
USER appuser

COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Api.dll"]
