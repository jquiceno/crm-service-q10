# Testing

## Estructura

```
tests/
├── UnitTests/             # Sin Docker. Mocks con NSubstitute. Rápido (<5s).
└── IntegrationTests/      # Boot completo del API contra SQL Server en Testcontainers.
```

Ambos proyectos están en `ServiceTemplate.slnx` bajo la carpeta `/tests/`.


---

## Cómo correr los tests

```bash
# Todo
dotnet test

# Solo unit tests (no requiere Docker)
dotnet test tests/UnitTests

# Solo integration tests (requiere Docker corriendo)
dotnet test tests/IntegrationTests

# Con cobertura de código
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings
```

**Prerrequisitos para integration tests:** Docker Desktop corriendo. La primera ejecución descarga la imagen de SQL Server (\~500 MB).

### Verificar la cobertura localmente (mismo flujo que CI)

Solo los **unit tests** cuentan para el porcentaje de cobertura. CI falla el pipeline si la cobertura de línea queda por debajo del umbral definido en la variable de repositorio de GitHub `COVERAGE_THRESHOLD` (default `90`).

```bash
rm -rf TestResults coverage-report
dotnet tool restore
dotnet test tests/UnitTests --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory ./TestResults
dotnet reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:"Html;JsonSummary"
```

Abrir `coverage-report/index.html` para el detalle por clase; el porcentaje de línea (`summary.linecoverage` en `coverage-report/Summary.json`) debe ser mayor o igual al umbral.


---

## Stack de testing

| Concepto | Librería | Por qué |
|----------|----------|---------|
| Test framework | `xunit`  | Estándar en .NET moderno |
| Aserciones | `Shouldly` | MIT. **No usar FluentAssertions v8+** (licencia comercial) |
| Mocks    | `NSubstitute` | Sintaxis limpia, sin SponsorLink |
| Fake data | `Bogus`  | Datos realistas para DTOs y propiedades primitivas |
| Web host de pruebas | `Microsoft.AspNetCore.Mvc.Testing` | Pipeline ASP.NET completo, in-process |
| Base de datos | `Testcontainers.MsSql` | SQL Server real. **No usar EF InMemory** (ignora constraints y transacciones) |
| Reset de BD | `Respawn` | Trunca todas las tablas entre tests en \~50ms |


---

## Escribir unit tests

### Domain — invariantes del Aggregate

```csharp
[Fact]
public void Create_WithEmptyName_ReturnsValidationError()
{
    var result = ProductAggregate.Create("", 10m);

    result.IsFailure.ShouldBeTrue();
}
```

`Create()` retorna `Result<ProductAggregate>` — nunca lanza excepción, así que el test verifica `IsFailure`, no `ShouldThrow`.

### Validators — `FluentValidation.TestHelper`

```csharp
private readonly CreateProductInputValidator _validator = new();

[Fact]
public void Validate_WithEmptyName_HasErrorOnName()
{
    var input = new CreateProductInputDto("", 10m);

    var result = _validator.TestValidate(input);

    result.ShouldHaveValidationErrorFor(x => x.Name);
}
```

### Use cases — mocks con NSubstitute

```csharp
private readonly IProductRepository _repository =
        Substitute.For<IProductRepository>();

private readonly IUnitOfWorkPort _unitOfWork =
    Substitute.For<IUnitOfWorkPort>();

[Fact]
public async Task ExecuteAsync_WithValidInput_PersistsAggregateAndReturnsSuccess()
{
    var input = new CreateProductInputDto("Keyboard", 49.90m);
    _repository.ExistsByNameAsync(input.Name!, Arg.Any<CancellationToken>()).Returns(false);
    _repository.AddAsync(Arg.Any<ProductAggregate>(), Arg.Any<CancellationToken>())
        .Returns(Result.Success());
    _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());

    var result = await _sut.ExecuteAsync(input, CancellationToken.None);

    result.IsSuccess.ShouldBeTrue();
    result.Value.Name.ShouldBe(input.Name);
    result.Value.Price.ShouldBe(input.Price);

    await _repository.Received(1).ExistsByNameAsync(input.Name!, Arg.Any<CancellationToken>());
    await _repository.Received(1).AddAsync(Arg.Any<ProductAggregate>(), Arg.Any<CancellationToken>());
    await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
}
```

### Providers — application services de resolución

Los Providers son clases concretas que se instancian directamente en el test con el mock del repositorio del que dependen. No se mockea el Provider en sí: se testea su comportamiento real.

```csharp
public sealed class ProductCategoriesProviderTests
{
    private readonly ICategoryRepository _repository =
        Substitute.For<ICategoryRepository>();
    private readonly ProductCategoriesProvider _sut;

    public ProductCategoriesProviderTests()
    {
        _sut = new ProductCategoriesProvider(_repository);
    }

    [Fact]
    public async Task GetAsync_WhenCategoriesProvided_ReturnsThem()
    {
        IReadOnlyList<string> categories = ["electronics"];

        var result = await _sut.GetAsync(categories);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(categories);
        await _repository.DidNotReceive().GetAllAsync(Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAsync_WhenCategoriesNull_FetchesFromRepository()
    {
        IReadOnlyList<CategoryData> activeCategories = [...];
        _repository.GetAllAsync(isActive: true, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<CategoryData>>.Success(activeCategories));

        var result = await _sut.GetAsync(null);

        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).GetAllAsync(isActive: true, Arg.Any<CancellationToken>());
    }
}
```

En los tests del use case que usa el Provider, se construye el Provider con un mock del repositorio que no tiene setup — así el use case test no repite la lógica del Provider y queda enfocado en orquestación:

```csharp
_sut = new CreateProductUseCase(
    _repository,
    new ProductCategoriesProvider(Substitute.For<ICategoryRepository>()));
```

Dado que el `ValidInput()` de los tests del use case siempre provee `Categories` explícitas, el repositorio del Provider nunca se llama — no necesita setup.

### Mappings — funciones puras

```csharp
[Fact]
public void ToAggregate_PreservesInputFields_AndAssignsId()
{
    var input = new CreateProductInputDto("Keyboard", 49.90m);

    var result = input.ToAggregate();

    result.IsSuccess.ShouldBeTrue();
    var aggregate = result.Value;
    aggregate.Id.ShouldNotBe(Guid.Empty);
    aggregate.Name.ShouldBe(input.Name);
    aggregate.Price.ShouldBe(input.Price);
}
```


---

## Escribir integration tests

Hereda de `IntegrationTestBase` y declara la colección:

```csharp
[Collection(IntegrationTestCollection.Name)]
public sealed class ProductEndpointsTests : IntegrationTestBase
{
    public ProductEndpointsTests(SqlServerContainerFixture fixture) : base(fixture) { }

    [Fact]
    public async Task GetById_Scenario_ReturnsOk()
    {
        // Hacer seed de datos — el agregado ES la entidad, se agrega directamente
        var product = ProductAggregate.Create("Keyboard", 49.90m).Value;
        Db.Set<ProductAggregate>().Add(product);
        await Db.SaveChangesAsync();

        // Llamar al endpoint con HttpClient pre-configurado
        var response = await Client.GetAsync($"/api/v1/product/{product.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
```

### IntegrationTestBase

| Miembro | Qué hace |
|---------|----------|
| `Client` | `HttpClient` apuntando al API in-process |
| `Db`    | `ApplicationDbContext` scoped contra el contenedor SQL Server |
| `InitializeAsync` (auto) | Resetea todas las tablas vía Respawn antes de cada test |
| `DisposeAsync` (auto) | Libera scope, client y factory |

### Cómo `ApiFactory` apunta al contenedor

`ApiFactory` establece tres variables de entorno en el constructor: `Persistence__Enabled=true`, `Persistence__ConnectionString=<container connection string>` y `Sentry__Dsn=""`. `InfrastructureServiceExtensions` lee esa configuración al registrar servicios y conecta el `DbContext` real contra el contenedor.

**¿Por qué variables de entorno y no** `**ConfigureAppConfiguration**`**?**

En .NET minimal hosting, `Program.cs` resuelve la configuración de forma **eager**: cuando `AddInfrastructureServices(builder.Configuration)` se ejecuta, la config ya está fija. El callback `ConfigureAppConfiguration` del `WebApplicationFactory` corre después de ese punto, así que cualquier fuente que se añada ahí, como `AddInMemoryCollection`, llega tarde y no tiene efecto sobre los servicios que ya se registraron.

Las variables de entorno, en cambio, son leídas por los providers que `CreateBuilder` inicializa al arrancar, antes de que se registre cualquier servicio. Por eso llegan a tiempo.


---

## Builders y Bogus

**Cuándo usar un builder (**`**tests/UnitTests/TestSupport/Builders/**`**):**

* Entidades de dominio con setters privados, ctors con invariantes, o propiedades calculadas.

**Cuándo usar Bogus directamente:**

* DTOs y records sin invariantes complicadas — `new Faker<MyDto>().RuleFor(...)`.

**No usar AutoFixture.** No recomendado en DDD (private setters, ctors estrictos), genera datos no determinísticos, y oculta el setup.


---

## Migrations en el fixture

El template no incluye migraciones. `SqlServerContainerFixture` usa `Database.EnsureCreatedAsync()` para crear el esquema desde el modelo de EF.

**Cuando el servicio tenga migrations**, se debe cambiar esa línea por `await dbContext.Database.MigrateAsync()` para asegurar que las migraciones se apliquen en tests igual que en prod.


---

## Convenciones

* **Nombres:** `MethodUnderTest_Scenario_ExpectedOutcome` (`Endpoint_Scenario_ExpectedOutcome` para integration).
* **Paralelismo:** UnitTests corre en paralelo (default xUnit). IntegrationTests **no** paraleliza porque comparten el contenedor SQL.
* **Aserciones:** una sola librería — Shouldly. No mezclar con `Assert.*` nativo de xUnit.
* **Cobertura:** excluir `Program.cs`, archivos `*DependencyInjection*`, extensions de DI, migrations. Configurado en `coverlet.runsettings`.


---

## FAQ

**¿Por qué no EF InMemory?** Microsoft lo desaconseja explícitamente para integration tests. No respeta constraints, transacciones, ni raw SQL. Tests que pasan en InMemory rompen en prod.

**¿Por qué no SQLite?** Diferencias de lenguaje con SQL Server (identity, JSON, `NEWSEQUENTIALID`) generan falsos positivos y falsos negativos.

**¿Por qué no FluentAssertions?** La versión 8 (2025) cambió a licencia comercial (Xceed). Shouldly cubre el mismo caso de uso, MIT.

**¿Por qué no Moq?** [SponsorLink](https://github.com/devlooped/moq/issues/1372).

**¿Cuánto tarda la suite de integration?** Primera corrida \~30–60s (descarga de imagen). Corridas siguientes \~10–15s.

**¿Por qué** `**azure-sql-edge**` **y no** `**mssql/server:2022-latest**`**?** Compatibilidad con Apple Silicon. Si el servicio usa features exclusivos de SQL Server (Service Broker, Full-Text Search, CLR), se debe cambiar la imagen a `mcr.microsoft.com/mssql/server:2022-latest`.
