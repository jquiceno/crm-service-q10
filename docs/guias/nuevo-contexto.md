# Guía: nuevo bounded context

Flujo completo para implementar un bounded context desde cero. El ejemplo usa `Product` con propiedades `Name (string)` y `Price (decimal)`.

> Esta guía asume que el contexto de ejemplo `WeatherForecast` ya existe y sirve como referencia.
> Para agregar un caso de uso a un contexto existente, ver [nuevo-caso-de-uso.md](nuevo-caso-de-uso.md).

## Paso 1 — Dominio

Todos los archivos de este paso viven bajo `Contexts/Product/Domain/`.

### Value Objects

```csharp
// Contexts/Product/Domain/ValueObjects/Price.cs
public sealed class Price : ValueObject
{
    public const decimal MinValue = 0;

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

El constructor es privado; la única forma de obtener un `Price` válido es a través de `Create()`, que retorna `Result<Price, ValidationError>` en lugar de lanzar excepción.

### Entidad

```csharp
// Contexts/Product/Domain/Entities/ProductEntity.cs
// El parámetro genérico de Entity<TId> es el tipo de la clave primaria (Guid, int, string…)
public sealed class ProductEntity : Entity<Guid>
{
    public string Name  { get; private set; }
    public Price  Price { get; private set; }

    internal ProductEntity(Guid id, string name, Price price)
    {
        Id    = id;
        Name  = name;
        Price = price;
    }
}
```

`Entity<TId>` incluye las propiedades de auditoría `CreatedAtUtc` y `UpdatedAtUtc` que se actualizan automáticamente en `SaveChangesAsync`.

### Aggregate Root

```csharp
// Contexts/Product/Domain/Aggregates/ProductAggregate.cs
public sealed class ProductAggregate : AggregateRoot<ProductEntity, Guid>
{
    public string  Name  => _entity.Name;
    public decimal Price => _entity.Price.Value;

    private ProductAggregate(ProductEntity entity) : base(entity) { }

    public static Result<ProductAggregate> Create(Guid id, string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ProductErrors.NameRequired with
                { Context = ProductErrors.Context, Origin = nameof(Create) };

        var priceResult = Price.Create(price);
        if (priceResult.IsFailure)
            return priceResult.TypedError with
                { Context = ProductErrors.Context, Origin = nameof(Create) };

        var entity = new ProductEntity(id, name, priceResult.Value);
        return new ProductAggregate(entity);
    }

    public ProductEntity           ToEntity()                       => _entity;
    public static ProductAggregate FromEntity(ProductEntity entity) => new(entity);
}
```

`Create()` acumula errores: valida cada campo en orden y retorna el primero que falle junto con el contexto y el origen enriquecidos. `FromEntity()` se usa en el repositorio para reconstruir el agregado desde la base de datos.

### Errores del contexto

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
            Property = nameof(ProductAggregate.Name)   // Siempre asignar Property en ValidationError
        };

    public static readonly ValidationError InvalidPrice
        = new($"Price must be greater than or equal to {Price.MinValue}.", ErrorType.Validation)
        {
            Property   = nameof(ProductAggregate.Price),
            Attributes = new Dictionary<string, object?> { ["min"] = Price.MinValue }
        };
}
```

Centralizar todos los errores del contexto en esta clase estática facilita la reutilización y evita duplicar mensajes entre capas.

### Puerto de repositorio

```csharp
// Contexts/Product/Domain/Ports/IProductRepositoryPort.cs
public interface IProductRepositoryPort : IRepositoryBase<ProductAggregate, Guid>
{
    Task<Result<bool>> ExistsByNameAsync(string name, CancellationToken ct = default);
}
```

Extiende `IRepositoryBase<TAggregate, TId>` con las queries específicas del dominio. El dominio define la interfaz; la infraestructura la implementa.

---

## Paso 2 — Aplicación

Todos los archivos de este paso viven bajo `Contexts/Product/Application/`.

### Puerto de caso de uso

```csharp
// Contexts/Product/Application/Ports/ICreateProductPort.cs
public interface ICreateProductPort
{
    Task<Result<CreateProductOutputDto>> ExecuteAsync(
        CreateProductInputDto input, CancellationToken ct = default);
}
```

El controller depende de esta interfaz, nunca de la implementación concreta.

### DTOs

```csharp
// Contexts/Product/Application/UseCases/CreateProduct/CreateProductInputDto.cs
public sealed record CreateProductInputDto(string? Name, decimal Price);

// Contexts/Product/Application/UseCases/CreateProduct/CreateProductOutputDto.cs
public sealed record CreateProductOutputDto(Guid Id, string Name, decimal Price, DateTime CreatedAt);
```

`Name` es nullable en el `InputDto` para permitir que el validador de entrada reporte el error con su `Property` en lugar de que el deserializador falle.

### Mapping

```csharp
// Contexts/Product/Application/UseCases/CreateProduct/CreateProductMapping.cs
public static class CreateProductMapping
{
    public static Result<ProductAggregate> ToAggregate(this CreateProductInputDto input)
        => ProductAggregate.Create(Guid.NewGuid(), input.Name!, input.Price);

    public static CreateProductOutputDto ToOutputDto(this ProductAggregate aggregate)
        => new(aggregate.Id, aggregate.Name, aggregate.Price, aggregate.CreatedAtUtc);
}
```

`ToAggregate()` delega toda la validación al Aggregate Root. `ToOutputDto()` proyecta el estado del agregado al contrato de salida.

### Use Case

```csharp
// Contexts/Product/Application/UseCases/CreateProduct/CreateProductUseCase.cs
public sealed class CreateProductUseCase(
    IProductRepositoryPort repository,
    IUnitOfWorkPort unitOfWork) : ICreateProductPort
{
    private const string Origin = nameof(CreateProductUseCase);

    public async Task<Result<CreateProductOutputDto>> ExecuteAsync(
        CreateProductInputDto input, CancellationToken ct = default)
    {
        // Precondición: verificar que el nombre no esté en uso
        var existsResult = await repository.ExistsByNameAsync(input.Name!, ct);
        if (existsResult.IsFailure)
            return existsResult.Error with { Context = ProductErrors.Context, Origin = Origin };
        if (existsResult.Value)
            return ProductErrors.NameRequired with { Context = ProductErrors.Context, Origin = Origin };

        // Crear el agregado (valida dominio)
        var aggregateResult = input.ToAggregate();
        if (aggregateResult.IsFailure)
            return aggregateResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        // Persistir
        var addResult = await repository.AddAsync(aggregateResult.Value, ct);
        if (addResult.IsFailure)
            return addResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        var commitResult = await unitOfWork.CommitAsync(ct);
        if (commitResult.IsFailure)
            return commitResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        return aggregateResult.Value.ToOutputDto();
    }
}
```

El patrón es: precondición → crear agregado → persistir → commit → retornar DTO. Cada paso enriquece el error con `Context` y `Origin` antes de propagarlo.

---

## Paso 3 — Infraestructura

### Configuración EF Core

```csharp
// Infrastructure/Persistence/EntityFramework/Product/Configurations/ProductEntityConfiguration.cs
public sealed class ProductEntityConfiguration : IEntityTypeConfiguration<ProductEntity>
{
    public void Configure(EntityTypeBuilder<ProductEntity> builder)
    {
        builder.OwnsOne(e => e.Price, p =>
        {
            p.Property(x => x.Value).HasColumnName("Price");
        });
    }
}
```

`OwnsOne` indica que `Price` es un Value Object propiedad de `ProductEntity`. La columna se llama `Price` en lugar del nombre generado por convención `Price_Value`.

### DbSet

Agregar la siguiente propiedad en `ApplicationDbContext.cs`:

```csharp
// Infrastructure/Persistence/EntityFramework/ApplicationDbContext.cs
public DbSet<ProductEntity> Products => Set<ProductEntity>();
```

### Adaptador de repositorio

```csharp
// Infrastructure/Adapters/Persistence/Product/ProductRepositoryAdapter.cs
public sealed class ProductRepositoryAdapter(
    ApplicationDbContext context,
    ILoggerPort<ProductRepositoryAdapter> logger)
    : BaseAggregateRepository<ProductAggregate, ProductEntity, Guid>(context, logger),
      IProductRepositoryPort
{
    protected override ProductAggregate ToAggregate(ProductEntity entity)
        => ProductAggregate.FromEntity(entity);

    protected override ProductEntity ToEntity(ProductAggregate aggregate)
        => aggregate.ToEntity();

    protected override DomainError GetNotFoundError(Guid id)
        => ProductErrors.NotFound(id);

    public async Task<Result<bool>> ExistsByNameAsync(string name, CancellationToken ct = default)
    {
        try
        {
            return await DbSet.AnyAsync(e => e.Name == name, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error checking product name {Name}", name);
            return PersistenceErrors.Failure();
        }
    }
}
```

`BaseAggregateRepository` implementa `GetByIdAsync`, `GetAllAsync`, `AddAsync`, `Update` y `Remove`. Solo hay que implementar `ToAggregate()`, `ToEntity()`, `GetNotFoundError()` y las queries específicas del contexto.

---

## Paso 4 — API

### Controller

```csharp
// Api/Controllers/ProductController.cs
[ApiController]
[Route("api/v1/[controller]")]
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
        ICreateProductPort createProduct,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(createProduct);
        ArgumentNullException.ThrowIfNull(input);

        return await createProduct
            .ExecuteAsync(input, ct)
            .ConfigureAwait(false);
    }
}
```

`[ValidateRequest]` ejecuta `ValidateRequestFilter` antes de entrar al action: si el DTO no pasa la validación estructural, retorna `400` con los errores antes de invocar el Use Case. `HttpCreatedResult<T>` retorna `201 Created` en éxito y el error HTTP correspondiente en fallo (ver [patron-result.md](../patron-result.md)).

### Validador de entrada

```csharp
// Api/Validators/CreateProductInputValidator.cs
public sealed class CreateProductInputValidator
    : AbstractValidator<CreateProductInputDto>, IStructuralValidator<CreateProductInputDto>
{
    public CreateProductInputValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0);
    }
}
```

Implementar `IStructuralValidator<T>` hace que el validador se registre automáticamente en el contenedor de dependencias via reflection. Las reglas aquí cubren la estructura del DTO (campos requeridos, longitudes máximas, rangos); las reglas de negocio viven en el dominio.

---

## Paso 5 — Registro de dependencias

### Extension method

```csharp
// Api/DependencyInjection/ProductServiceExtensions.cs
public static class ProductServiceExtensions
{
    public static IServiceCollection AddProductServices(this IServiceCollection services)
    {
        services.AddScoped<ICreateProductPort, CreateProductUseCase>();
        services.AddScoped<IProductRepositoryPort, ProductRepositoryAdapter>();
        return services;
    }
}
```

Use Cases y repositorios se registran como `Scoped` para que compartan el mismo `DbContext` durante el request.

### Conexión en ApplicationServiceExtensions

Agregar la llamada a `AddProductServices()` en el método `AddApplicationServices()`:

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

---

## Ver también

- [repositorio.md](../repositorio.md) — detalles de BaseAggregateRepository y Unit of Work
- [patron-result.md](../patron-result.md) — patrón Result y errores de dominio
- [nueva-entidad-dominio.md](nueva-entidad-dominio.md) — solo el modelado del dominio, con más detalle
- [nuevo-caso-de-uso.md](nuevo-caso-de-uso.md) — agregar casos de uso adicionales al contexto
