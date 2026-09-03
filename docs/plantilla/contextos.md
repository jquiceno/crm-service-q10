## 1. Qué es un bounded context

Un **bounded context** es la unidad en la que la plantilla organiza un dominio de negocio completo: su propio modelo (agregados, value objects), sus propias reglas (errores, validaciones) y su propia forma de persistirse y exponerse. Vive bajo `Contexts/{Contexto}/`.

```
src/Contexts/
├── ServiceInfo/         ← contexto liviano que trae la plantilla (solo Application)
├── Product/             ← contexto de ejemplo usado en la documentación
└── {TuContexto}/        ← cada dominio de negocio nuevo
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
│   ├── Aggregates/       # {Contexto}Aggregate (AggregateRoot<TId>) + sus records de argumentos
│   ├── ValueObjects/     # VOs exclusivos de este contexto
│   ├── Enums/            # enums del dominio
│   ├── Queries/          # objetos de filtro y modelos de consulta ({Contexto}Filter)
│   ├── Models/           # modelos de lectura que no son agregados (opcional)
│   ├── Repositories/     # I{Contexto}Repository — el dominio define el contrato de persistencia
│   └── Errors/           # {Contexto}Errors — todos los errores centralizados
│
└── Application/
    ├── Ports/            # I{Capacidad}Port e I{Concepto}Reader (opcional)
    ├── Providers/        # lógica auxiliar resuelta desde repositorios (opcional)
    └── UseCases/
        └── {CasoDeUso}/  # I{CasoDeUso}UseCase + UseCase + InputDto + OutputDto + Mapping, coubicados
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
| Application | Reader (opcional) | [conceptos-reader-provider-repository.md](conceptos-reader-provider-repository.md) |
| Application | Provider (opcional) | [providers.md](providers.md) |

### Piezas externas que lo conectan (viven fuera de `Contexts/`, en `Infrastructure/` y `Api/`)

| Capa | Pieza | Referencia |
|------|-------|------------|
| Infrastructure | Entidad de persistencia + configuración EF Core + mapper | [repositorio.md](repositorio.md) |
| Infrastructure | Repositorio del contexto (`{Aggregate}Repository`) y sus readers | [repositorio.md](repositorio.md) |
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

    public static Result<ProductAggregate> Create(CreateProductArgs input)
    {
        var errors = new List<ValidationError>();

        if (string.IsNullOrWhiteSpace(input.Name))
            errors.Add(ProductErrors.NameRequired);

        // El VO se construye AQUÍ, dentro del agregado: los Args solo traen primitivos.
        var priceResult = Price.Create(input.Price);
        if (priceResult.IsFailure)
            errors.Add(priceResult.TypedError with { Value = input.Price });

        if (errors.Count > 0)
            return DomainError.FromValidationDomainErrors(errors);

        var aggregate = new ProductAggregate(Guid.NewGuid(), input.Name, priceResult.Value.Value);
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

`Create()` acumula errores: recorre cada campo y retorna todos los que fallen juntos, en lugar de detenerse en el primero. `Reconstruct()` lo usa el mapper del repositorio al leer de la base de datos — los datos ya son válidos, así que no vuelve a validar.

Los factories reciben un `record` de argumentos (`CreateProductArgs` / `UpdateProductArgs`, en `Domain/Aggregates/`) con **solo primitivos**; el agregado construye por dentro sus Value Objects. Así el llamador nunca necesita conocer los tipos de dominio, y agregar un campo no cambia la firma. Ver [entidades-y-agregados.md](entidades-y-agregados.md#args-records-de-argumentos-de-los-factories).

#### Repositorio

```csharp
// Contexts/Product/Domain/Repositories/IProductRepository.cs
public interface IProductRepository : IRootRepository<ProductAggregate, Guid>
{
    Task<Result<bool>> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);
}
```

Extiende `IRootRepository<TAggregate, TId>` (`GetByIdAsync`, `ExistsAsync`, `GetAllAsync`, `AddAsync`, `Update`, `RemoveAsync`) con las queries específicas del dominio. El dominio define la interfaz; la infraestructura la implementa. No lleva sufijo `Port` — ver [puertos-y-adaptadores.md](puertos-y-adaptadores.md#2-por-qué-el-repositorio-no-es-un-port).


---

### 5.2 Aplicación

Todos los archivos de este paso viven bajo `Contexts/Product/Application/`. El detalle completo de este paso, incluyendo los distintos patrones según el tipo de operación (crear, leer, actualizar, eliminar, relacionar), está en [casos-de-uso.md](casos-de-uso.md) — aquí solo se muestra `CreateProduct` como caso guía para dejar el contexto operativo de punta a punta.

**Interfaz del caso de uso:**

```csharp
// Contexts/Product/Application/UseCases/CreateProduct/ICreateProductUseCase.cs
public interface ICreateProductUseCase
{
    Task<Result<CreateProductOutputDto>> ExecuteAsync(
        CreateProductInputDto input, CancellationToken cancellationToken = default);
}
```

**DTOs** — todas las propiedades, de entrada y de salida, con `[property: Description(...)]` (ver [openapi.md](openapi.md#descripción-de-las-propiedades-de-los-dtos)):

```csharp
// Contexts/Product/Application/UseCases/CreateProduct/CreateProductInputDto.cs
using System.ComponentModel;

public sealed record CreateProductInputDto(
    [property: Description("Nombre del producto. Máximo 200 caracteres.")]
    string? Name,
    [property: Description("Precio del producto. Debe ser mayor o igual a 0.")]
    decimal Price);

// Contexts/Product/Application/UseCases/CreateProduct/CreateProductOutputDto.cs
public sealed record CreateProductOutputDto(
    [property: Description("Identificador asignado al producto creado.")]
    Guid Id,
    [property: Description("Nombre del producto.")]
    string Name,
    [property: Description("Precio del producto.")]
    decimal Price,
    [property: Description("Fecha de creación, en UTC.")]
    DateTime CreatedAt);
```

`Name` es nullable en el `InputDto` para permitir que el validador de entrada reporte el error con su `Property` en lugar de que el deserializador falle.

**Mapping** — el DTO se traduce a un `record` de argumentos del dominio, no a una lista de primitivos sueltos:

```csharp
// Contexts/Product/Application/UseCases/CreateProduct/CreateProductMapping.cs
public static class CreateProductMapping
{
    public static Result<ProductAggregate> ToAggregate(this CreateProductInputDto input)
        => ProductAggregate.Create(new CreateProductArgs(input.Name!, input.Price));

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
        CreateProductInputDto input, CancellationToken cancellationToken = default)
    {
        var aggregateResult = input.ToAggregate();                        // 1. crear (valida dominio)
        if (aggregateResult.IsFailure)
            return aggregateResult.Error with { Context = ProductErrors.Context, Origin = Origin };

        var existsResult = await repository                               // 2. precondición
            .ExistsByNameAsync(input.Name!, cancellationToken)
            .ConfigureAwait(false);
        if (existsResult.IsFailure)
            return existsResult.Error;
        if (existsResult.Value)
            return ProductErrors.NameAlreadyExists with { Context = ProductErrors.Context, Origin = Origin };

        var addResult = await repository                                  // 3. persistir
            .AddAsync(aggregateResult.Value, cancellationToken)
            .ConfigureAwait(false);
        if (addResult.IsFailure)
            return addResult.Error;

        var commitResult = await unitOfWork                               // 4. commit
            .CommitAsync(cancellationToken)
            .ConfigureAwait(false);
        if (commitResult.IsFailure)
            return commitResult.Error;

        return aggregateResult.Value.ToOutputDto();                       // 5. retorno implícito
    }
}
```

El patrón es: crear agregado → precondición → persistir → commit → retornar DTO. Primero el dominio: un body malformado responde 400 sin gastar una query. El DTO se retorna de forma implícita (`Result<T>` convierte desde `T`), y solo los errores que el propio use case o el dominio originan se sellan con `Context` y `Origin`; los del repositorio y el Unit of Work se propagan intactos — ver [casos-de-uso.md](casos-de-uso.md#7-propagación-de-errores-context-y-origin).


---

### 5.3 Infraestructura — fuera del contexto

Estos archivos viven bajo `Infrastructure/Persistence/EntityFramework/Products/`, no bajo `Contexts/Product/`: implementan el repositorio que el dominio definió en el paso 5.1, sin que el contexto conozca esta implementación.

#### Entidad de persistencia

El agregado **no** es la entidad que EF Core mapea. La entidad es una clase plana que refleja la tabla real: nombres de columna heredados, nulabilidad real, y las columnas que el dominio no modela pero la tabla exige.

```csharp
// Infrastructure/Persistence/EntityFramework/Products/Entities/Product.cs
public sealed class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

#### Configuración EF Core

```csharp
// Infrastructure/Persistence/EntityFramework/Products/Configurations/ProductConfiguration.cs
public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("tbl_productos");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("pro_idP").ValueGeneratedNever();
        builder.Property(p => p.Name).HasColumnName("pro_nombre").HasMaxLength(200).IsUnicode(false).IsRequired();
        builder.Property(p => p.Price).HasColumnName("pro_precio");
    }
}
```

Las relaciones con otras tablas se declaran por **propiedad de navegación** (`HasOne(x => x.Padre).WithMany().HasForeignKey(...)`), y sobre esquemas heredados con `OnDelete(DeleteBehavior.Restrict)`. Ver [repositorio.md](repositorio.md#relaciones-se-modelan-por-navegación).

#### Mapper

```csharp
// Infrastructure/Persistence/EntityFramework/Products/Mappers/ProductRepositoryMapper.cs
public static class ProductRepositoryMapper
{
    public static ProductAggregate ToDomain(Entities.Product document)
        => ProductAggregate.Reconstruct(document.Id, document.Name, document.Price);

    public static Entities.Product ToDocument(ProductAggregate aggregate)
        => new() { Id = aggregate.Id, Name = aggregate.Name, Price = aggregate.Price };
}
```

#### DbSet

```csharp
// Infrastructure/Persistence/EntityFramework/ApplicationDbContext.cs
public DbSet<Products.Entities.Product> Products => Set<Products.Entities.Product>();
```

#### Repositorio

Sin sufijo `Adapter` y fuera de `Adapters/`: vive junto a su entidad, su configuración y su mapper.

```csharp
// Infrastructure/Persistence/EntityFramework/Products/ProductRepository.cs
public sealed class ProductRepository(
    ApplicationDbContext context,
    ILoggerPort<ProductRepository> logger) : IProductRepository
{
    private const string Origin = nameof(ProductRepository);

    private readonly DbSet<Entities.Product> _products = context.Set<Entities.Product>();

    public async Task<Result<ProductAggregate>> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var document = await _products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
                .ConfigureAwait(false);

            if (document is null)
                return ProductErrors.NotFound(id) with { Origin = Origin };

            return ProductRepositoryMapper.ToDomain(document);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.Error(ex, "Error retrieving Product with id {Id}", id);
            return PersistenceErrors.Failure(Origin);
        }
    }

    // ExistsByNameAsync, GetAsync(filter, page), AddAsync, Update, RemoveAsync…
}
```

El repositorio implementa `IProductRepository` directamente y estampa su propio `Origin` en cada error. `RepositoryBaseEF<TAggregate, TId>` sigue en la plantilla pero asume que el agregado es la entidad mapeada, así que no aplica a este patrón — ver [repositorio.md](repositorio.md#repositorybaseeftaggregate-tid--solo-para-agregados-que-sí-son-la-entidad).


---

### 5.4 API — fuera del contexto

Estos archivos viven bajo `Api/` y `Infrastructure/`, no bajo `Contexts/Product/`: invocan el caso de uso que la aplicación definió en el paso 5.2, sin conocer su implementación concreta. El detalle completo de este paso, incluyendo los distintos patrones según el tipo de operación, está en [controllers.md](controllers.md) — aquí solo se muestra `Create` para dejar el contexto expuesto de punta a punta.

#### Controller

```csharp
// Api/Controllers/ProductsController.cs
[ApiController]
[Route("[controller]")]
[Tags("Products")]
public sealed class ProductsController(
    ICreateProductUseCase createProductUseCase) : ControllerBase   // ← inyección por constructor
{
    private const string CacheTag = "products";

    [HttpPost]
    [ValidateRequest]
    [EndpointSummary("Create product")]
    [EndpointDescription("Creates a new product in the database.")]
    [ProducesResponseType(typeof(ApiSuccessResponse<CreateProductOutputDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status409Conflict)]
    [OutputCacheInvalidate(CacheTag)]
    public async Task<HttpCreatedResult<CreateProductOutputDto>> CreateProduct(
        [FromBody] CreateProductInputDto input,
        CancellationToken cancellationToken = default)
    {
        return await createProductUseCase.ExecuteAsync(input, cancellationToken).ConfigureAwait(false);
    }
}
```

Los casos de uso se inyectan en el constructor del controller, no como parámetro de la action; `[Tags(...)]` se declara una sola vez a nivel de clase. Ver [controllers.md](controllers.md).

`[ValidateRequest]` ejecuta el validador de FluentValidation antes de entrar al Use Case. `HttpCreatedResult<T>` retorna `201 Created` en éxito y el error HTTP correspondiente en fallo — ver [patron-result.md](patron-result.md).

#### Validador de entrada

```csharp
// Infrastructure/Validation/FluentValidation/Product/CreateProductInputValidator.cs
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
        // Primero persistencia y lecturas auxiliares…
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductCategoryReader, ProductCategoryReader>();

        // …después los casos de uso que las consumen
        services.AddScoped<ICreateProductUseCase, CreateProductUseCase>();
        services.AddScoped<IGetProductsUseCase, GetProductsUseCase>();
        services.AddScoped<IGetProductByIdUseCase, GetProductByIdUseCase>();
        services.AddScoped<IUpdateProductUseCase, UpdateProductUseCase>();
        services.AddScoped<IDeleteProductUseCase, DeleteProductUseCase>();

        return services;
    }
}
```

```csharp
// Api/DependencyInjection/ApplicationServiceExtensions.cs
public static IServiceCollection AddApplicationServices(this IServiceCollection services)
{
    services.AddSharedServices();
    services.AddServiceInfoServices();
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
* [conceptos-reader-provider-repository.md](conceptos-reader-provider-repository.md) — Reader vs. Provider vs. Repository


