## 1. Qué es un bounded context

Un **bounded context** es la unidad en la que la plantilla organiza un dominio de negocio completo: su propio modelo (agregados, value objects), sus propias reglas (errores, validaciones) y su propia forma de persistirse y exponerse. Vive bajo `Contexts/{Contexto}/`.

```
src/Contexts/
├── WeatherForecast/     ← contexto de ejemplo de la plantilla
├── Product/             ← contexto de ejemplo usado en la documentación
└── {TuContexto}/         ← cada dominio de negocio nuevo
```

Cada contexto es una isla: no importa tipos de otro contexto directamente. Lo único que un contexto puede compartir con otros vive en `Shared/` (tipos base como `AggregateRoot<TId>`, `Result<T>`, `IUnitOfWorkPort`, etc. — ver [arquitectura.md](arquitectura.md)).


---

## 2. Propósito y cuándo crear uno nuevo

Un contexto agrupa **todos los casos de uso que operan sobre el mismo modelo de negocio**. La pregunta para decidir si algo es un contexto nuevo o un caso de uso más dentro de uno existente es: *¿este concepto tiene su propio ciclo de vida, sus propias reglas de negocio y su propia identidad, o es solo una operación adicional sobre un concepto que ya modelé?*

| Situación | Decisión |
|-----------|----------|
| Agregar "actualizar precio" a `Product` | Caso de uso nuevo dentro del contexto `Product` — ver [casos-de-uso.md](casos-de-uso.md) |
| Agregar el concepto "Categoría", con su propio ciclo de vida e identidad | Contexto nuevo (`Category`), aunque se relacione con `Product` |
| Agregar un endpoint de solo lectura sin modelo de dominio propio (ej. info del servicio) | Contexto liviano: puede no tener `Domain/Aggregates` si no hay reglas de negocio que proteger — ej. `ServiceInfo` en este mismo servicio, que solo tiene `Application` |

No crear un contexto nuevo solo para "namespacing" — si dos operaciones comparten agregado, errores y reglas de negocio, van en el mismo contexto.


---

## 3. Cómo se organiza un contexto

Internamente, todo contexto separa **Domain** (reglas de negocio, sin dependencias externas) de **Application** (orquestación de casos de uso). La infraestructura (persistencia concreta) y la API (controllers) viven fuera del contexto, referenciándolo:

```
Contexts/{Contexto}/
├── Domain/
│   ├── Aggregates/       # {Contexto}Aggregate — el agregado ES la entidad (AggregateRoot<TId>)
│   ├── ValueObjects/     # VOs exclusivos de este contexto
│   ├── Repositories/     # I{Contexto}Repository — el dominio define el contrato de persistencia
│   └── Errors/           # {Contexto}Errors — todos los errores centralizados
│
└── Application/
    ├── Ports/            # I{Capacidad}Port — capacidad externa del contexto que no es persistencia (opcional)
    ├── Providers/         # lógica auxiliar reutilizable entre casos de uso (opcional)
    └── UseCases/
        └── {CasoDeUso}/   # I{CasoDeUso}UseCase + UseCase + InputDto + OutputDto + Mapping, coubicados
```

La regla de dependencias de [arquitectura.md](arquitectura.md) aplica también dentro del contexto: `Domain` no conoce `Application`, y ninguno de los dos conoce `Infrastructure` ni `Api`.

Infrastructure y API son necesarios para que el contexto sea *usable* (persistir el agregado, exponerlo por HTTP), pero no viven dentro de `Contexts/{Contexto}/` — son piezas externas que referencian al contexto a través de sus contratos (el caso de uso y el repositorio), no parte de él. Por qué el repositorio no se llama "Port" está explicado en [puertos-y-adaptadores.md](puertos-y-adaptadores.md#2-por-qué-el-repositorio-no-es-un-port).

---

## 4. Piezas de un contexto y piezas que lo conectan

### Piezas del contexto (viven en `Contexts/{Contexto}/`)

| Capa | Pieza | Referencia |
|------|-------|------------|
| Domain | Value Objects | [value-objects.md](value-objects.md) |
| Domain | Aggregate Root | [entidades-y-agregados.md](entidades-y-agregados.md) |
| Domain | Errores del contexto | [errores-dominio.md](errores-dominio.md) |
| Domain | Repositorio | [repositorio.md](repositorio.md) |
| Application | Interfaz + Use Case, DTOs, Mapping | [casos-de-uso.md](casos-de-uso.md) |
| Application | Port específico del contexto (opcional) | [puertos-y-adaptadores.md](puertos-y-adaptadores.md) |
| Application | Provider (opcional) | [providers.md](providers.md) |

### Piezas externas que lo conectan (viven fuera de `Contexts/`, en `Infrastructure/` y `Api/`)

| Capa | Pieza | Referencia |
|------|-------|------------|
| Infrastructure | Configuración EF Core | [repositorio.md](repositorio.md) |
| Infrastructure | Adaptador de repositorio (implementa el repositorio del contexto) | [repositorio.md](repositorio.md) |
| API | Controller, validadores (invocan los casos de uso del contexto) | [contrato-api.md](contrato-api.md), [validaciones.md](validaciones.md) |
| API | Registro de dependencias | [puertos-y-adaptadores.md](puertos-y-adaptadores.md) |

La sección siguiente recorre primero las piezas del contexto y luego las piezas externas, construyendo `Product` de principio a fin.


---

## 5. Ejemplo — de un contexto nuevo a un endpoint operativo

El ejemplo usa `Product` con propiedades `Name (string)` y `Price (decimal)`. Los pasos 5.1 y 5.2 son **el contexto en sí** (lo único que vive en `Contexts/Product/`); los pasos 5.3 a 5.5 son las piezas externas que lo conectan al resto del sistema para dejarlo operativo.

### 5.1 Dominio

Todos los archivos de este paso viven bajo `Contexts/Product/Domain/`.

#### Errores del contexto

Crearlos primero — el Value Object y el Aggregate los referencian al compilar:

```csharp
// Contexts/Product/Domain/Errors/ProductErrors.cs
public static class ProductErrors
{
    public const string Context = "Product";

    public static DomainError NotFound(Guid id)
        => new($"Product with id '{id}' was not found.", ErrorType.NotFound);

    public static readonly ValidationError NameRequired
        = new("Product name is required.", ErrorType.Validation)
        {
            Property = nameof(ProductAggregate.Name)
        };

    public static readonly ValidationError InvalidPrice
        = new($"Price must be greater than or equal to {Price.MinValue}.", ErrorType.Validation)
        {
            Property   = nameof(ProductAggregate.Price),
            Attributes = new Dictionary<string, object?> { ["min"] = Price.MinValue }
        };
}
```

#### Value Object

```csharp
// Contexts/Product/Domain/ValueObjects/Price.cs
public sealed class Price : ValueObject
{
    public const decimal MinValue = 0m;

    public decimal Value { get; }

    private Price(decimal value) => Value = value;

    public static Result<Price, ValidationError> Create(decimal value)
    {
        if (value < MinValue)
            return ProductErrors.InvalidPrice;

        return new Price(value);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
```

El constructor es privado; la única forma de obtener un `Price` válido es `Create()`, que retorna `Result<Price, ValidationError>` en lugar de lanzar excepción. Ver [value-objects.md](value-objects.md).

#### Aggregate Root

El agregado **es** la entidad — hereda directamente de `AggregateRoot<TId>` (no hay una clase `Entity` separada; ver [entidades-y-agregados.md](entidades-y-agregados.md)):

```csharp
// Contexts/Product/Domain/Aggregates/ProductAggregate.cs
public sealed class ProductAggregate : AggregateRoot<Guid>
{
    public string  Name  { get; private set; } = string.Empty;
    public decimal Price { get; private set; }

    private ProductAggregate(Guid id, string name, decimal price)
    {
        Id    = id;
        Name  = name;
        Price = price;
    }

    public static Result<ProductAggregate> Create(string name, decimal price)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(name))
            errors.Add(ProductErrors.NameRequired);

        var priceResult = Price.Create(price);
        if (priceResult.IsFailure)
            errors.Add(priceResult.TypedError with { Property = nameof(Price), Value = price });

        if (errors.Count > 0)
            return DomainError.FromValidationDomainErrors(errors);

        var aggregate = new ProductAggregate(Guid.NewGuid(), name, priceResult.Value.Value);
        aggregate.Created();
        return aggregate;
    }

    // Reconstruye desde persistencia — sin validaciones ni auditoría
    public static ProductAggregate Reconstruct(Guid id, string name, decimal price)
        => new(id, name, price);

    protected override void Created()
    {
        SetCreatedAt(DateTime.UtcNow);
        SetUpdatedAt(DateTime.UtcNow);
    }
}
```

`Create()` acumula errores: recorre cada campo y retorna todos los que fallen juntos, en lugar de detenerse en el primero. `Reconstruct()` lo usa el repositorio al leer de la base de datos — los datos ya son válidos, así que no vuelve a validar.

#### Repositorio

```csharp
// Contexts/Product/Domain/Repositories/IProductRepository.cs
public interface IProductRepository : IRootRepository<ProductAggregate, Guid>
{
    Task<Result<bool>> ExistsByNameAsync(string name, CancellationToken ct = default);
}
```

Extiende `IRootRepository<TAggregate, TId>` (`GetByIdAsync`, `ExistsAsync`, `GetAllAsync`, `AddAsync`, `Update`, `Remove`) con las queries específicas del dominio. El dominio define la interfaz; la infraestructura la implementa. No lleva sufijo `Port` — ver [puertos-y-adaptadores.md](puertos-y-adaptadores.md#2-por-qué-el-repositorio-no-es-un-port).


---

### 5.2 Aplicación

Todos los archivos de este paso viven bajo `Contexts/Product/Application/`. El detalle completo de este paso, incluyendo los distintos patrones según el tipo de operación (crear, leer, actualizar, eliminar, relacionar), está en [casos-de-uso.md](casos-de-uso.md) — aquí solo se muestra `CreateProduct` como caso guía para dejar el contexto operativo de punta a punta.

**Interfaz del caso de uso:**

```csharp
// Contexts/Product/Application/UseCases/CreateProduct/ICreateProductUseCase.cs
public interface ICreateProductUseCase
{
    Task<Result<CreateProductOutputDto>> ExecuteAsync(
        CreateProductInputDto input, CancellationToken ct = default);
}
```

**DTOs:**

```csharp
// Contexts/Product/Application/UseCases/CreateProduct/CreateProductInputDto.cs
public sealed record CreateProductInputDto(string? Name, decimal Price);

// Contexts/Product/Application/UseCases/CreateProduct/CreateProductOutputDto.cs
public sealed record CreateProductOutputDto(Guid Id, string Name, decimal Price, DateTime CreatedAt);
```

`Name` es nullable en el `InputDto` para permitir que el validador de entrada reporte el error con su `Property` en lugar de que el deserializador falle.

**Mapping:**

```csharp
// Contexts/Product/Application/UseCases/CreateProduct/CreateProductMapping.cs
public static class CreateProductMapping
{
    public static Result<ProductAggregate> ToAggregate(this CreateProductInputDto input)
        => ProductAggregate.Create(input.Name!, input.Price);

    public static CreateProductOutputDto ToOutputDto(this ProductAggregate aggregate)
        => new(aggregate.Id, aggregate.Name, aggregate.Price, aggregate.CreatedAt!.Value);
}
```

`ToAggregate()` delega toda la validación al Aggregate Root. `ToOutputDto()` proyecta el estado del agregado al contrato de salida.

**Use Case:**

```csharp
// Contexts/Product/Application/UseCases/CreateProduct/CreateProductUseCase.cs
public sealed class CreateProductUseCase(
    IProductRepository repository,
    IUnitOfWorkPort unitOfWork) : ICreateProductUseCase
{
    private const string Origin = nameof(CreateProductUseCase);

    public async Task<Result<CreateProductOutputDto>> ExecuteAsync(
        CreateProductInputDto input, CancellationToken ct = default)
    {
        var existsResult = await repository.ExistsByNameAsync(input.Name!, ct);   // precondición
        if (existsResult.IsFailure)
            return existsResult.Error with { Context = ProductErrors.Context, Origin = Origin };
        if (existsResult.Value)
            return ProductErrors.NameRequired with { Context = ProductErrors.Context, Origin = Origin };

        var aggregateResult = input.ToAggregate();                                 // crear (valida dominio)
        if (aggregateResult.IsFailure)
            return aggregateResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        var addResult = await repository.AddAsync(aggregateResult.Value, ct);      // persistir
        if (addResult.IsFailure)
            return addResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        var commitResult = await unitOfWork.CommitAsync(ct);                       // commit
        if (commitResult.IsFailure)
            return commitResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        return aggregateResult.Value.ToOutputDto();
    }
}
```

El patrón es: precondición → crear agregado → persistir → commit → retornar DTO. Cada paso enriquece el error con `Context` y `Origin` antes de propagarlo.


---

### 5.3 Infraestructura — fuera del contexto

Estos archivos viven bajo `Infrastructure/`, no bajo `Contexts/Product/`: implementan el repositorio que el dominio definió en el paso 5.1, sin que el contexto conozca esta implementación.

#### Configuración EF Core

Como el agregado es directamente la entidad, la configuración de EF Core apunta al Aggregate — no hay una entidad intermedia que mapear:

```csharp
// Infrastructure/Persistence/EntityFramework/Product/Configurations/ProductConfiguration.cs
public sealed class ProductConfiguration : IEntityTypeConfiguration<ProductAggregate>
{
    public void Configure(EntityTypeBuilder<ProductAggregate> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Price);
    }
}
```

#### DbSet

```csharp
// Infrastructure/Persistence/EntityFramework/ApplicationDbContext.cs
public DbSet<ProductAggregate> Products => Set<ProductAggregate>();
```

#### Adaptador de repositorio

```csharp
// Infrastructure/Adapters/Persistence/Product/ProductRepositoryAdapter.cs
public sealed class ProductRepositoryAdapter(
    ApplicationDbContext context,
    ILoggerPort<ProductRepositoryAdapter> logger)
    : RepositoryBaseEF<ProductAggregate, Guid>(context, logger), IProductRepository
{
    protected override DomainError GetNotFoundError(Guid id)
        => ProductErrors.NotFound(id);

    public async Task<Result<bool>> ExistsByNameAsync(string name, CancellationToken ct = default)
    {
        try
        {
            return await DbSet.AnyAsync(p => p.Name == name, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error checking product name {Name}", name);
            return PersistenceErrors.Failure();
        }
    }
}
```

`RepositoryBaseEF<TAggregate, TId>` implementa `GetByIdAsync`, `ExistsAsync`, `GetAllAsync`, `AddAsync`, `Update` y `Remove`. Solo hay que implementar `GetNotFoundError()` y las queries específicas del contexto. Ver [repositorio.md](repositorio.md).


---

### 5.4 API — fuera del contexto

Estos archivos viven bajo `Api/`, no bajo `Contexts/Product/`: invocan el caso de uso que la aplicación definió en el paso 5.2, sin conocer su implementación concreta. El detalle completo de este paso, incluyendo los distintos patrones según el tipo de operación, está en [controllers.md](controllers.md) — aquí solo se muestra `Create` para dejar el contexto expuesto de punta a punta.

#### Controller

```csharp
// Api/Controllers/ProductController.cs
[ApiController]
[Route("[controller]")]
public sealed class ProductController : ControllerBase
{
    [HttpPost]
    [Tags("products")]
    [ValidateRequest]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [EndpointSummary("Create a new product")]
    [EndpointDescription("Creates a new product in the database.")]
    public async Task<HttpCreatedResult<CreateProductOutputDto>> Create(
        [FromBody] CreateProductInputDto input,
        ICreateProductUseCase createProduct,
        CancellationToken ct)
        => await createProduct.ExecuteAsync(input, ct).ConfigureAwait(false);
}
```

`[ValidateRequest]` ejecuta el validador de FluentValidation antes de entrar al Use Case. `HttpCreatedResult<T>` retorna `201 Created` en éxito y el error HTTP correspondiente en fallo — ver [patron-result.md](patron-result.md).

#### Validador de entrada

```csharp
// Api/Validators/CreateProductInputValidator.cs
public sealed class CreateProductInputValidator
    : AbstractValidator<CreateProductInputDto>, IStructuralValidator<CreateProductInputDto>
{
    public CreateProductInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
    }
}
```

Implementar `IStructuralValidator<T>` hace que el validador se registre automáticamente en el contenedor de dependencias vía reflection. Las reglas aquí cubren la estructura del DTO; las reglas de negocio viven en el dominio — ver [validaciones.md](validaciones.md).


---

### 5.5 Registro de dependencias

```csharp
// Api/DependencyInjection/ProductServiceExtensions.cs
public static class ProductServiceExtensions
{
    public static IServiceCollection AddProductServices(this IServiceCollection services)
    {
        services.AddScoped<ICreateProductUseCase, CreateProductUseCase>();
        services.AddScoped<IProductRepository, ProductRepositoryAdapter>();
        return services;
    }
}
```

```csharp
// Api/DependencyInjection/ApplicationServiceExtensions.cs
public static IServiceCollection AddApplicationServices(this IServiceCollection services)
{
    services.AddSharedServices();
    services.AddWeatherForecastServices();
    services.AddProductServices();   // ← agregar aquí
    return services;
}
```

Use Cases y repositorios se registran como `Scoped` para que compartan el mismo `DbContext` durante el request.


---

## Ver también

* [arquitectura.md](arquitectura.md) — capas, regla de dependencias, estructura de carpetas completa
* [entidades-y-agregados.md](entidades-y-agregados.md) — jerarquía `Entity<TId>` / `AggregateRoot<TId>`, auditoría
* [casos-de-uso.md](casos-de-uso.md) — patrones de implementación por tipo de operación
* [controllers.md](controllers.md) — cómo se expone un caso de uso como endpoint HTTP
* [repositorio.md](repositorio.md) — `IRootRepository`, `RepositoryBaseEF`, Unit of Work, paginación
* [guias/nueva-entidad-dominio.md](guias/nueva-entidad-dominio.md) — modelado de dominio con más detalle (solo Domain)


