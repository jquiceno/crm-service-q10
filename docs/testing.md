# Testing

## Estructura

```
tests/
├── UnitTests/             # Sin Docker. Mocks con NSubstitute. Rápido (<5s).
└── IntegrationTests/      # Boot completo del API contra SQL Server en Testcontainers.
```

Ambos proyectos están en `ServiceTemplate.slnx` bajo el folder `/tests/`.

---

## Cómo correr los tests

```bash
# Todo
dotnet test

# Solo unit (no requiere Docker)
dotnet test tests/UnitTests

# Solo integration (requiere Docker corriendo)
dotnet test tests/IntegrationTests

# Con cobertura
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

**Prerrequisitos para integration tests:** Docker Desktop, Colima o Rancher Desktop corriendo. La primera ejecución descarga la imagen de SQL Server (~500 MB).

**Imagen de SQL Server:** El fixture usa `mcr.microsoft.com/azure-sql-edge:latest` en vez de `mcr.microsoft.com/mssql/server:2022-latest`. La razón: la imagen oficial de SQL Server 2022 crashea bajo QEMU en Apple Silicon (arm64). Azure SQL Edge tiene builds nativos arm64 y es compatible a nivel de wire protocol para el subset de features que este template usa. Como Azure SQL Edge no trae `sqlcmd`, el fixture también override-ea la wait strategy de Testcontainers para sondear el puerto 1433 en vez de ejecutar `sqlcmd`.

---

## Stack de testing

| Concepto | Librería | Por qué |
|---|---|---|
| Test framework | `xunit` | Estándar en .NET moderno |
| Aserciones | `Shouldly` | MIT, legible. **No usar FluentAssertions v8+** (licencia comercial) |
| Mocks | `NSubstitute` | Sintaxis limpia, sin SponsorLink |
| Datos fake | `Bogus` | Datos realistas para DTOs y propiedades primitivas |
| Builders de entidades | Hand-rolled | Las entidades de dominio tienen invariantes — AutoFixture pelea con eso |
| Web host de pruebas | `Microsoft.AspNetCore.Mvc.Testing` | Pipeline ASP.NET completo, in-process |
| Base de datos | `Testcontainers.MsSql` | SQL Server real. **No usar EF InMemory** (ignora constraints y transacciones) |
| Reset de BD | `Respawn` | Trunca todas las tablas entre tests, ~50ms |

---

## Escribir unit tests

### Domain — invariantes de entidad

```csharp
[Fact]
public void Constructor_WithEmptyGuid_ThrowsArgumentException()
{
    var act = () => new WeatherForecastEntity(Guid.Empty, DateTime.UtcNow, 20, "Sunny");

    act.ShouldThrow<ArgumentException>();
}
```

### Validators — `FluentValidation.TestHelper`

```csharp
private readonly CreateWeatherForecastInputValidator _validator = new();

[Fact]
public void Validate_WithEmptySummary_HasErrorOnSummary()
{
    var input = new CreateWeatherForecastInputDto(DateTime.UtcNow, 20, "");

    var result = _validator.TestValidate(input);

    result.ShouldHaveValidationErrorFor(x => x.Summary);
}
```

### Use cases — mocks con NSubstitute

```csharp
private readonly IWeatherForecastRepository _repository = Substitute.For<IWeatherForecastRepository>();
private readonly IValidator<CreateWeatherForecastInputDto> _validator = Substitute.For<IValidator<CreateWeatherForecastInputDto>>();

[Fact]
public async Task ExecuteAsync_WithValidInput_PersistsEntity()
{
    _validator.ValidateAsync(Arg.Any<CreateWeatherForecastInputDto>(), Arg.Any<CancellationToken>())
        .Returns(new ValidationResult());

    var sut = new CreateWeatherForecastUseCase(_validator, _repository);

    var result = await sut.ExecuteAsync(input);

    result.IsSuccess.ShouldBeTrue();
    await _repository.Received(1).AddAsync(Arg.Any<WeatherForecastEntity>(), Arg.Any<CancellationToken>());
}
```

### Mappings — funciones puras

```csharp
[Fact]
public void ToEntity_PreservesInputFields()
{
    var input = new CreateWeatherForecastInputDto(DateTime.UtcNow, 25, "Sunny");

    var entity = input.ToEntity();

    entity.Summary.ShouldBe(input.Summary);
}
```

---

## Escribir integration tests

Heredá de `IntegrationTestBase` y declará la colección:

```csharp
[Collection(IntegrationTestCollection.Name)]
public sealed class MyEndpointTests : IntegrationTestBase
{
    public MyEndpointTests(SqlServerContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Endpoint_Scenario_ExpectedOutcome()
    {
        // Sembrar datos directamente con el DbContext
        Db.Set<WeatherForecastEntity>().Add(new WeatherForecastEntity(...));
        await Db.SaveChangesAsync();

        // Llamar al endpoint con HttpClient pre-configurado
        var response = await Client.GetAsync("/api/v1/weather-forecasts");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
```

### Garantías que da el base class

| Miembro | Qué hace |
|---|---|
| `Client` | `HttpClient` apuntando al API in-process |
| `Db` | `ApplicationDbContext` scoped contra el contenedor SQL Server |
| `InitializeAsync` (auto) | Resetea todas las tablas vía Respawn antes de cada test |
| `DisposeAsync` (auto) | Libera scope, client y factory |

### Cómo `ApiFactory` apunta al contenedor

`ApiFactory` setea tres variables de entorno en su constructor: `Persistence__Enabled=true`, `Persistence__ConnectionString=<container connection string>` y `Sentry__Dsn=""`. `InfrastructureServiceExtensions` lee esa configuración al registrar servicios y wire-ea el `DbContext` real contra el contenedor — sin reemplazos post-hoc.

**¿Por qué variables de entorno y no `ConfigureAppConfiguration`?** En .NET 8 minimal hosting, `Program.cs` lee la configuración de forma **eager** durante el registro de servicios (`AddInfrastructureServices(builder.Configuration)`), que corre **antes** del callback `ConfigureAppConfiguration` del `WebApplicationFactory`. Una fuente `AddInMemoryCollection` registrada en la factory llega demasiado tarde — los servicios ya se registraron con la config original. Las variables de entorno sí son leídas por los providers default de `CreateBuilder`, así que llegan a tiempo.

---

## Builders y Bogus

**Cuándo usar un builder (`tests/UnitTests/TestSupport/Builders/`):**
- Entidades de dominio con setters privados, ctors con invariantes, o propiedades calculadas.

**Cuándo usar Bogus directamente:**
- DTOs y records sin invariantes complicadas — `new Faker<MyDto>().RuleFor(...)`.

**No usar AutoFixture.** Pelea con DDD (private setters, ctors estrictos), genera datos no deterministas, y oculta el setup del test.

---

## Migrations en el fixture

El template no incluye migrations. `SqlServerContainerFixture` usa `Database.EnsureCreatedAsync()` para crear el esquema desde el modelo de EF.

**Cuando tu servicio agregue migrations**, cambiá esa línea por `await dbContext.Database.MigrateAsync()` para asegurar que las migrations se apliquen en tests igual que en prod.

---

## Convenciones

- **Nombres:** `MethodUnderTest_Scenario_ExpectedOutcome` (`Endpoint_Scenario_ExpectedOutcome` para integration).
- **Paralelismo:** UnitTests corre en paralelo (default xUnit). IntegrationTests **no** paraleliza — comparten el contenedor SQL.
- **Aserciones:** una sola librería — Shouldly. No mezclar con `Assert.*` nativo de xUnit.
- **Cobertura:** excluir `Program.cs`, archivos `*DependencyInjection*`, extensions de DI, migrations. Configurado en `coverlet.runsettings`.

---

## FAQ

**¿Por qué no EF InMemory?** Microsoft lo desaconseja explícitamente para integration tests. No respeta constraints, transacciones, ni SQL crudo. Tests que pasan en InMemory rompen en prod.

**¿Por qué no SQLite?** Diferencias de dialecto con SQL Server (identity, JSON, `NEWSEQUENTIALID`) generan falsos positivos y falsos negativos.

**¿Por qué no FluentAssertions?** La versión 8 (2025) cambió a licencia comercial (Xceed). Shouldly cubre el mismo caso de uso, MIT.

**¿Por qué no Moq?** Controversia de SponsorLink. NSubstitute es el reemplazo estándar.

**¿Cuánto tarda la suite de integration?** Primera corrida ~30–60s (descarga de imagen). Corridas siguientes ~10–15s.

**¿Por qué `azure-sql-edge` y no `mssql/server:2022-latest`?** Ver sección "Prerrequisitos" arriba. Resumen: compatibilidad con Apple Silicon. Si tu servicio usa features exclusivos de SQL Server (Service Broker, Full-Text Search, CLR), cambiá la imagen a `mcr.microsoft.com/mssql/server:2022-latest` — pero vas a necesitar Rosetta 2 o un runner x86_64 en CI.
