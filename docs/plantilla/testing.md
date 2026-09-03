# Testing

## Estructura

```
tests/
├── UnitTests/             # Sin Docker. Mocks con NSubstitute. Rápido (<5s).
└── IntegrationTests/
    ├── Infrastructure/    # ApiFactory, fixtures y helpers compartidos — y también tests
    ├── ServiceInfo/       # Tests de endpoints, agrupados por contexto
    ├── Routing/           # RoutePrefixTests
    └── Caching/           # Tests de los adaptadores de caché contra Redis
```

Ambos proyectos están en `Service.slnx` bajo la carpeta `/tests/`.

Ojo con `Infrastructure/`: además de la plomería compartida contiene tests reales (`HealthProbesTests`, `PersistenceProviderTests`, `TraceContextTests`). No es una carpeta de solo-helpers.

`IntegrationTests` tiene **dos** patrones:

| Patrón | Contenedor | Cómo se escribe | Ejemplo |
|--------|-----------|-----------------|---------|
| API in-process contra la infraestructura real | SQL Server (`azure-sql-edge`) + Redis (`redis:7-alpine`) | Heredar `IntegrationTestBase` + `[Collection(IntegrationTestCollection.Name)]` | `ServiceInfoEndpointsTests`, `HealthProbesTests`, `TraceContextTests` |
| Componente de infraestructura aislado | el que necesite el adaptador | `IClassFixture<>` con fixture propio, instanciar el SUT a mano | `RedisCacheStoreIntegrationTests` |

Todo lo que levante la app necesita Docker, aunque el test no toque la base: la multitenencia es obligatoria (ver `AddInfrastructureServices`), no hay base en memoria a la que caer, y el arranque exige además la caché L2. Un test de pipeline HTTP —headers, middleware, trazas— igual hereda `IntegrationTestBase`; simplemente le pega a un endpoint que no toca persistencia, como hace `TraceContextTests` con `/health/live`.


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

**Prerrequisitos para integration tests:** Docker Desktop corriendo. La primera ejecución descarga `mcr.microsoft.com/azure-sql-edge:latest` (la pesada), `redis:7-alpine` y el resource reaper de Testcontainers (`testcontainers/ryuk`).

### Verificar la cobertura localmente (mismo flujo que CI)

Solo los **unit tests** cuentan para el porcentaje de cobertura. CI falla el pipeline si la cobertura de línea queda por debajo del umbral definido en la variable de repositorio de GitHub `COVERAGE_THRESHOLD` (default `90`).

> El `90` es un **piso deliberadamente permisivo** (la cobertura real del servicio es mayor):
> protege contra regresiones grandes sin bloquear PRs por fluctuaciones pequeñas. Si el equipo
> quiere proteger el nivel actual, basta subir la variable `COVERAGE_THRESHOLD` en GitHub —
> no requiere cambios de código.

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
| Fake data | `Bogus`  | Datos realistas para DTOs y propiedades primitivas. Disponible en ambos proyectos; hoy ningún test lo usa todavía |
| Web host de pruebas | `Microsoft.AspNetCore.Mvc.Testing` | Pipeline ASP.NET completo, in-process |
| Base de datos | `Testcontainers.MsSql` | SQL Server real. **No usar EF InMemory** (ignora constraints y transacciones) |
| Reset de BD | `Respawn` | Borra las filas de todas las tablas entre tests |
| Redis | `Testcontainers.Redis` + `StackExchange.Redis` | Redis real: la caché L2 que la app exige al arrancar, y los tests del adaptador de caché |


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

#### Assertar `Origin` en las ramas de fallo

Los tests de casos de uso fijan **quién** produjo el error, no solo que falló. Es lo que evita que alguien "enriquezca" un error ajeno y borre la traza real:

```csharp
[Fact]
public async Task ExecuteAsync_WhenRepositoryFails_PropagatesTheRepositoryOrigin()
{
    _repository.GetByIdAsync(Code, Arg.Any<CancellationToken>())
        .Returns(PersistenceErrors.Failure("ProductRepository"));

    var result = await _sut.ExecuteAsync(Code, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Origin.ShouldBe("ProductRepository", "the use case does not replace the origin of the failure");
}

[Fact]
public async Task ExecuteAsync_WhenDomainRejectsInput_StampsTheUseCaseOrigin()
{
    var result = await _sut.ExecuteAsync(InvalidInput, CancellationToken.None);

    result.IsFailure.ShouldBeTrue();
    result.Error.Origin.ShouldBe(nameof(UpdateProductUseCase));
}
```

La regla que estos tests fijan está en [casos-de-uso.md](casos-de-uso.md#7-propagación-de-errores-context-y-origin).

### Readers — dobles de las lecturas auxiliares

Un Reader se mockea como cualquier otro contrato de aplicación (`Substitute.For<IProductCategoryReader>()`), y su implementación se testea aparte contra la base de datos, igual que el repositorio:

```csharp
_categoryReader.ExistsAsync(CategoryId, Arg.Any<CancellationToken>()).Returns(true);
```

Cubrir siempre las tres ramas: existe, no existe, y el Reader falla (el error se propaga con el `Origin` del Reader, no del use case).

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


### Tipos `internal` de infraestructura

Un helper como `SqlServerErrorClassifier` no se hace `public` para poder testearlo. `Infrastructure.csproj` le da visibilidad al proyecto de tests:

```xml
<ItemGroup>
  <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
    <_Parameter1>UnitTests</_Parameter1>
  </AssemblyAttribute>
</ItemGroup>
```

Cuando el tipo decide en base a algo que el test no puede construir — `SqlException` no tiene constructor público —, se cubre la parte alcanzable (sobrecargas con primitivos, fallbacks) y el resto queda para los integration tests contra el contenedor.


---

## Escribir integration tests

### Endpoints del API — heredar `IntegrationTestBase`

Hereda de `IntegrationTestBase` y declara la colección:

```csharp
[Collection(IntegrationTestCollection.Name)]
public sealed class ProductEndpointsTests : IntegrationTestBase
{
    public ProductEndpointsTests(SqlServerContainerFixture fixture, RedisContainerFixture cache)
        : base(fixture, cache) { }

    [Fact]
    public async Task GetById_Scenario_ReturnsOk()
    {
        // Hacer seed con la ENTIDAD DE PERSISTENCIA, no con el agregado:
        // el agregado no es el tipo que EF Core mapea (ver repositorio.md)
        var product = new Infrastructure.Persistence.EntityFramework.Products.Entities.Product
        {
            Id = Guid.NewGuid(),
            Name = "Keyboard",
            Price = 49.90m,
        };
        Db.Products.Add(product);
        await Db.SaveChangesAsync();

        // Llamar al endpoint con HttpClient pre-configurado
        var response = await Client.GetAsync($"/product/{product.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // El body viene envuelto: leerlo con ApiResponse<T> y assertar sobre .Data
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ProductOutputDto>>(
            JsonSerializerOptions.Web);
        body!.Data.Name.ShouldBe("Keyboard");
    }
}
```

**Ojo con las rutas:** no hay prefijo de **versión**, pero sí un **prefijo de servicio** (`RoutePrefix`) bajo el que se sirve **todo**, sin `UsePathBase`. `GlobalRoutePrefixConvention` lo antepone a los controllers y `Program.cs` a los minimal-API (health, OpenAPI). Las rutas de controller salen de su `[Route(...)]` relativo, con una vuelta de tuerca: `RouteTokenTransformerConvention` + `KebabCaseParameterTransformer` pasan los **tokens** `[controller]`/`[action]` a kebab-case; los segmentos literales quedan tal cual. Así, un `ProductCatalogController` con `[Route("[controller]")]` responde en `/{RoutePrefix}/product-catalog`, y health en `/{RoutePrefix}/health/{live,ready}`.

El valor por defecto del prefijo vive en `appsettings.json` (por eso los tests in-process llaman a la ruta **con** prefijo — ver `ServiceInfoEndpointsTests.cs` y `HealthProbesTests.cs`); en los ambientes desplegados lo define el **ConfigMap** (`k8s/base/configmap.yaml`), que es el que debe coincidir con el `path` del ingress.

**El envelope:** los endpoints que devuelven `HttpOkResult<T>` responden `{ "data": ..., "statusCode": ... }`, no el DTO desnudo. Para deserializarlos está `ApiResponse<T>`, en `tests/IntegrationTests/Infrastructure/`. Los paginados de `HttpOkPagedResult<T>` van **doblemente** envueltos —`{ "data": { "items": [...], "totalCount": n }, "statusCode": 200 }`— así que el tipo de lectura es `ApiResponse<ApiPagedData<T>>`, no `ApiPagedData<T>` a secas: `ApiPagedData<T>` es solo el payload interno. Assertar solo el `StatusCode` compila y pasa, pero no verifica nada del contrato. La excepción son los endpoints que devuelven `HttpNoContentResult`: ahí **no hay envelope que deserializar** y assertar el `204` sí es la verificación completa del camino de éxito — pero conviene comprobar además que el cuerpo viene vacío, porque un fallo del `Result` cambia el status y sí escribe un `ApiErrorResponse`.

### Componentes de infraestructura aislados — fixture propio

Cuando lo que se prueba es un adaptador contra un servicio externo y no hace falta el pipeline HTTP, no se hereda `IntegrationTestBase`: se usa un `IClassFixture<>` con su propio contenedor y se instancia el SUT a mano. Así lo hacen los tests de caché (`tests/IntegrationTests/Caching/`):

```csharp
public sealed class RedisCacheStoreIntegrationTests : IClassFixture<RedisContainerFixture>, IAsyncLifetime
{
    private readonly RedisContainerFixture _fixture;
    private readonly RedisCacheStore _sut;

    public RedisCacheStoreIntegrationTests(RedisContainerFixture fixture)
    {
        _fixture = fixture;
        _sut = new RedisCacheStore(fixture.Connection, Substitute.For<ILoggerPort<RedisCacheStore>>());
    }

    // Aislamiento entre tests: FLUSHDB, el equivalente del Respawn de SQL Server
    public Task InitializeAsync() => _fixture.FlushAsync();
    public Task DisposeAsync() => Task.CompletedTask;
}
```

Estos tests **no** están en `IntegrationTestCollection`, así que su contenedor de Redis es independiente del que usa la colección (esa la levanta para la caché L2 que la multitenencia exige al arrancar) y vive lo que dure la clase.

### IntegrationTestBase

| Miembro | Qué hace |
|---------|----------|
| `Client` | `HttpClient` apuntando al API in-process |
| `Db`    | `ApplicationDbContext` scoped contra el contenedor SQL Server |
| `InitializeAsync` (auto) | Resetea todas las tablas vía Respawn antes de cada test |
| `DisposeAsync` (auto) | Libera scope, client y factory |

### Cómo `ApiFactory` levanta la app

La multitenencia es **obligatoria**: el servicio no tiene modo single-tenant ni base en memoria, y `AddInfrastructureServices` aborta el arranque si `TenantResolverService:Enabled` está en `false`. Los tests entonces la prenden y corren el camino real —middleware de tenant → `DbContext` por tenant → SQL Server—. Lo único que se reemplaza es la dependencia externa que el suite no puede hospedar: el tenant-resolver.

**Variables de entorno (antes de construir el host).** `Program.cs` lee la configuración de forma **eager** —los prerequisitos de multitenencia se verifican antes de registrar un solo servicio—, así que `ConfigureAppConfiguration` llega tarde. Las variables de entorno sí llegan a tiempo: los providers que `CreateBuilder` inicializa las leen antes de registrar servicios. El `ApiFactory` fija en su constructor:

| Variable | Valor | Por qué |
|----------|-------|---------|
| `TenantResolverService__Enabled` | `true` | Sin esto el arranque aborta |
| `TENANT_RESOLVER_SERVICE_URL` | `http://tenant-resolver.invalid` | Solo tiene que parsear como URL absoluta: nadie la llama |
| `CONNSTRING_ENCRYPTION_KEY` | `integration-tests` | Requerida por el guard; el descifrado no se ejercita |
| `Cache__L2Enabled` / `Cache__ConnectionString` | `true` / contenedor Redis | La multitenencia exige caché L2 al arrancar |
| `Sentry__Enabled` / `Sentry__Dsn` / `SENTRY_DSN` | apagado / vacío | No mandar eventos desde los tests |

**`ConfigureTestServices` (después de que la app registró lo suyo).** Ahí se reemplazan cuatro registros:

1. `ITenantResolverServiceClient` → un stub que resuelve **cualquier** código de tenant al connection string del contenedor SQL Server. Eso es lo que hace que el `DbContext` por tenant apunte a la base de los tests sin tocar el registro de EF.
2. `ITenantConnectionInitializer` / `IDbConnectionProvider` → una implementación con el connection string ya cargado. El `TenantContext` real tira si se lee antes de que el middleware resuelva el tenant, y `IntegrationTestBase.Db` resuelve el contexto en un scope propio, fuera de toda petición.
3. Se quita el `TenantResolverStartupProbe`: aborta el boot cuando el `/health` del resolver no responde, y acá nunca responde.
4. Se quita el health check `tenant-info` de readiness, que apunta al mismo resolver inexistente, para que `/health/ready` reporte sobre lo que el suite realmente levanta.

Aparte de eso el `ApiFactory` fija el entorno `Testing` y silencia el logging (`ClearProviders` + mínimo `Warning`).

**El guard:** `PersistenceProviderTests` afirma que `Db.Database.ProviderName` es `Microsoft.EntityFrameworkCore.SqlServer`. Son dos `[Fact]` con un assert cada uno: el del `ProviderName` y otro que ejecuta `ExecuteSqlRawAsync("SELECT 1")` contra el contenedor. El segundo es deliberadamente **relacional** y no `CanConnectAsync()`: ese devuelve `true` contra casi cualquier provider, así que no distingue nada.

**¿Por qué no apuntar el `DbContext` directo con `AddDbContext` en `ConfigureTestServices`?** Se puede —era el enfoque anterior—, pero exige borrar del `IServiceCollection` el descriptor de `ApplicationDbContext` **y todo genérico cerrado que lo tenga entre sus argumentos de tipo** (los callbacks internos de configuración de EF incluidos), porque dos providers registrados a la vez hacen que EF tire al resolver el contexto. Stubbear el resolver deja intacto el registro de EF de la app, así que el suite ejercita exactamente el `AddDbContext` que corre en producción.

---

## Builders y Bogus

**Cuándo usar un builder:**

* Entidades de dominio con setters privados, ctors con invariantes, o propiedades calculadas.

**Cuándo usar Bogus directamente:**

* DTOs y records sin invariantes complicadas — `new Faker<MyDto>().RuleFor(...)`.

**Dónde ponerlos:** la convención de la plantilla es `tests/UnitTests/TestSupport/Builders/`, que hoy **no existe** porque el template todavía no tiene dominio propio. Los únicos helpers actuales (`AesTestCrypto`, `JsonRoundTripCacheStore`) viven junto a los tests que los usan, en `tests/UnitTests/Infrastructure/`. Para un helper de un solo test eso alcanza; al segundo consumidor, crear `TestSupport/`.

**No usar AutoFixture.** No recomendado en DDD (private setters, ctors estrictos), genera datos no determinísticos, y oculta el setup.


---

## Migrations en el fixture

El template no incluye migraciones. `SqlServerContainerFixture` usa `Database.EnsureCreatedAsync()` para crear el esquema desde el modelo de EF.

**Cuando el servicio tenga migrations**, se debe cambiar esa línea por `await dbContext.Database.MigrateAsync()` para asegurar que las migraciones se apliquen en tests igual que en prod.


---

## Convenciones

* **Nombres:** `MethodUnderTest_Scenario_ExpectedOutcome` (`Endpoint_Scenario_ExpectedOutcome` para integration).
* **Paralelismo:** UnitTests corre en paralelo (default xUnit). IntegrationTests **no** paraleliza: `AssemblyInfo.cs` lo desactiva a nivel assembly con `[assembly: CollectionBehavior(DisableTestParallelization = true)]`, porque los tests comparten contenedor y `DatabaseResetter` limpia la base entera entre tests.
* **Aserciones:** una sola librería — Shouldly. No mezclar con `Assert.*` nativo de xUnit.
* **Cobertura:** configurado en `coverlet.runsettings`. Por archivo excluye `Program.cs`, `*DependencyInjection*.cs`, migrations y `**/Extensions/*Extensions.cs`; ojo que este último patrón es más amplio de lo que sugiere «extensions de DI» y también saca de la medición archivos con lógica real como `Infrastructure/Extensions/EfCorePersistenceExtensions.cs`. Además excluye los assemblies `[*.Tests]*` y `[*]*.Migrations.*`, y lo marcado con `GeneratedCode`/`ExcludeFromCodeCoverage`/`CompilerGenerated`.


---

## FAQ

**¿Por qué no EF InMemory?** Microsoft lo desaconseja explícitamente para integration tests. No respeta constraints, transacciones, ni raw SQL. Tests que pasan en InMemory rompen en prod. Por eso el paquete `Microsoft.EntityFrameworkCore.InMemory` no está en `Infrastructure` sino en `UnitTests`, único lugar donde se usa y solo como doble liviano de la plomería del `DbContext`: la app no tiene modo de persistencia en memoria.

**¿Por qué no SQLite?** Diferencias de lenguaje con SQL Server (identity, JSON, `NEWSEQUENTIALID`) generan falsos positivos y falsos negativos.

**¿Por qué no FluentAssertions?** La versión 8 (2025) cambió a licencia comercial (Xceed). Shouldly cubre el mismo caso de uso, MIT.

**¿Por qué no Moq?** [SponsorLink](https://github.com/devlooped/moq/issues/1372).

**¿Cuánto tarda la suite de integration?** La primera corrida paga la descarga de las dos imágenes; después el costo dominante es arrancar los contenedores, no los tests en sí.

**¿Por qué** `**azure-sql-edge**` **y no** `**mssql/server:2022-latest**`**?** Compatibilidad con Apple Silicon. Si el servicio usa features exclusivos de SQL Server (Service Broker, Full-Text Search, CLR), se debe cambiar la imagen a `mcr.microsoft.com/mssql/server:2022-latest`.
