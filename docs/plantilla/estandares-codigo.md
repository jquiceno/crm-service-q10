# Estándares de Código

## Herramientas

Cuatro archivos imponen los estándares en toda la solución:

| Archivo | Responsabilidad |
|---------|-----------------|
| `.editorconfig` | Formato (indentación, saltos de línea, charset) y preferencias de estilo C# |
| `.globalconfig` | Severidades de diagnóstico para reglas CA del SDK y Roslynator |
| `Directory.Build.props` | Conecta `.globalconfig`, incluye Roslynator y habilita `AnalysisMode=All` |
| `docs/plantilla/estandares-codigo.md` | Este archivo — referencia legible para el equipo |

Roslynator se incluye únicamente como `<IncludeAssets>analyzers</IncludeAssets>` — sin impacto en tiempo de ejecución ni en los consumidores del proyecto.


---

## Modelo de severidad

Se mantiene `TreatWarningsAsErrors=true`. La separación de niveles se expresa mediante los niveles de severidad de Roslyn:

| Categoría | Severidad en `.globalconfig` | Efecto en compilación |
|-----------|----------------------------|-----------------------|
| Exactitud, seguridad, rendimiento | `warning`                  | Promovido a error — **rompe la compilación** |
| Calidad de código (Roslynator) | `warning`                  | Igual                 |
| Estilo, nomenclatura, formato | `suggestion`               | Solo sugerencia en el IDE — nunca rompe la compilación |
| Suprimidas intencionalmente | `none`                     | Deshabilitadas        |


---

## Convenciones C#

### Declaración de variables

Usar `var` en todos los casos.

```csharp
// correcto
var aggregate = input.ToAggregate();
var result = await useCase.ExecuteAsync(cancellationToken);

// incorrecto
ProductAggregate aggregate = input.ToAggregate();
```

### Declaración de namespaces

Solo namespaces con ámbito de archivo (C# 10+):

```csharp
// correcto
namespace Product.Application.UseCases.GetProductById;

// incorrecto
namespace Product.Application.UseCases.GetProductById { }
```

### Modificadores de acceso

Siempre escribirlos de forma explícita — nunca depender de los valores por defecto:

```csharp
// correcto
public sealed class GetProductByIdUseCase(...) { }

// incorrecto
sealed class GetProductByIdUseCase(...) { }
```

### Nomenclatura

| Símbolo | Convención | Ejemplo |
|---------|------------|---------|
| Interfaces | Prefijo `I` + PascalCase | `IProductRepository` |
| Parámetros de tipo | Prefijo `T` + PascalCase | `TAggregate` |
| Campos privados | `_camelCase` | `_repository` |
| Constantes | PascalCase | `SectionName` |
| Métodos asíncronos | PascalCase + sufijo `Async` | `ExecuteAsync` |
| Todos los demás miembros | PascalCase | `CreateProductUseCase` |

### Verificaciones de nulos

Preferir pattern matching sobre operadores de igualdad:

```csharp
// correcto
if (entity is null) return;
if (entity is not null) { ... }

// incorrecto
if (entity == null) return;
```


---

## Convenciones de arquitectura

### Clases selladas

Sellar toda clase concreta que no esté diseñada para herencia:

```csharp
public sealed class CreateProductUseCase(...) : ICreateProductUseCase { }
public sealed class ProductRepository(...) : IProductRepository { }
public sealed class ProductsController(...) : ControllerBase { }
```

### Constructores primarios

Usar constructores primarios (C# 12+) para inyección de dependencias — en use cases, repositorios, readers **y controllers**:

```csharp
public sealed class GetProductByIdUseCase(
    IProductRepository repository,
    ILoggerPort<GetProductByIdUseCase> logger) : IGetProductByIdUseCase

public sealed class ProductsController(
    IGetProductByIdUseCase getProductByIdUseCase) : ControllerBase
```

En los controllers, los casos de uso van en el constructor y **nunca** como parámetro de una action — ver [controllers.md](controllers.md#3-cómo-se-usan).

### Nombre del `CancellationToken`

El parámetro se llama `cancellationToken` (no `ct`) y se declara al final de la lista, con `= default` en los métodos públicos de casos de uso, repositorios, readers y actions.

### Manejo de excepciones

No capturar `Exception` sin un filtro `when` que acote los tipos esperados o preserve la cancelación. La regla `CA1031` se aplica con severidad `warning` (`.globalconfig`) y la solución no contiene supresiones de esta regla.

La captura de `Exception` con filtro se restringe a los límites de infraestructura y del host, y cada uno tiene una salida definida:

| Límite | Ejemplos | Qué hace con la excepción |
|--------|----------|---------------------------|
| Persistencia y clientes HTTP | `RepositoryBaseEF`, `UnitOfWorkAdapter`, `TenantResolverServiceClient` | La traduce al patrón `Result` (`PersistenceErrors.Failure(Origin)`, `InternalError`) |
| Caché | `RedisCacheStore` | Registra un `Warning` y degrada: la lectura devuelve `null` y la escritura se omite |
| Host / presentación | `ValidateRequestFilter` | La descarta y continúa con la validación de `ModelState` |
| Host / presentación | `TenantResolverStartupProbe` | La envuelve en `InvalidOperationException` para abortar el arranque |

En todos los casos el filtro `when` es obligatorio: acota los tipos esperados (`ex is JsonException or IOException or NotSupportedException`) o deja pasar la cancelación (`ex is not OperationCanceledException`).

El manejo de excepciones no controladas corresponde a `GlobalExceptionHandler` (`src/Shared/Infrastructure/Presentation/Middleware/GlobalExceptionHandler.cs`), registrado mediante `AddExceptionHandler<GlobalExceptionHandler>()` en `src/Api/DependencyInjection/ErrorHandlingServiceExtensions.cs`. Al implementar `IExceptionHandler`, recibe la excepción como parámetro en lugar de capturarla, por lo que no requiere un bloque catch-all ni supresión alguna.

```csharp
// correcto — capturar excepciones específicas
catch (DomainException ex) { ... }
catch (OperationCanceledException) when (...) { ... }

// correcto — límite de infraestructura, con filtro que preserva la cancelación
catch (Exception ex) when (ex is not OperationCanceledException) { ... }

// incorrecto — catch-all sin filtro, en cualquier capa
catch (Exception ex) { ... }
```


---

## Ejecutar las verificaciones

```bash
# Compilación completa — reglas de corrección, seguridad y rendimiento activas
dotnet build

# Verificar formato sin modificar archivos
dotnet format --verify-no-changes

# Aplicar correcciones de formato automáticas
dotnet format
```


---

## Git hooks — validación pre-commit local

El repositorio incluye un hook de git en `.githooks/pre-commit` que ejecuta la compilación y los tests unitarios antes de cada commit, bloqueándolo si alguno falla.

### Activación automática

El hook se activa automáticamente la primera vez que se ejecuta `dotnet build` en el clon. No se requiere ningún paso manual.

El target `ConfigureGitHooks` en `Directory.Build.props` ejecuta `git config core.hooksPath .githooks` antes de cada build. Solo aplica cuando existe el directorio `.git` y la variable de entorno `CI` no está definida — por lo que no tiene efecto en GitHub Actions ni en ningún otro sistema de CI estándar.

### Qué ejecuta el hook

1. `dotnet build -c Release --no-restore` — compila la solución con `TreatWarningsAsErrors=true` activo.
2. `dotnet test tests/UnitTests/UnitTests.csproj -c Release --no-build --verbosity minimal` — ejecuta los tests unitarios.

Los tests de integración (Testcontainers, Docker) no se incluyen en el hook para mantenerlo rápido; se ejecutan en CI.

### Saltarse el hook en casos excepcionales

```bash
git commit --no-verify
```


---

## Agregar o suprimir reglas

### Suprimir una regla globalmente

Agregar a la sección de suprimidas en `.globalconfig` con un comentario que justifique:

```ini
# Motivo por el que esta regla no aplica a este proyecto
dotnet_diagnostic.CAXXXX.severity = none
```

### Suprimir para un caso puntual justificado

Usar `#pragma warning` en línea:

```csharp
#pragma warning disable CA1031
catch (Exception ex)
#pragma warning restore CA1031
{
    // manejador global de excepciones — captura intencional de todo
}
```

### Promover una sugerencia a error de compilación

Cambiar la severidad de la regla en `.globalconfig` de `suggestion` a `warning`. Con `TreatWarningsAsErrors=true` se convierte en error de compilación.

### Sobrescribir reglas solo para proyectos de prueba

Agregar una sección en `.editorconfig` con ámbito al path de tests:

```ini
[tests/**/*.cs]
dotnet_diagnostic.CA2007.severity = none
```
