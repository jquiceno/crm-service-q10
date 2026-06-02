# Estándares de Código

## Herramientas

Cuatro archivos imponen los estándares en toda la solución:

| Archivo | Responsabilidad |
|---------|-----------------|
| `.editorconfig` | Formato (indentación, saltos de línea, charset) y preferencias de estilo C# |
| `.globalconfig` | Severidades de diagnóstico para reglas CA del SDK y Roslynator |
| `Directory.Build.props` | Conecta `.globalconfig`, incluye Roslynator y habilita `AnalysisMode=All` |
| `docs/coding-standards.md` | Este archivo — referencia legible para el equipo |

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
var entity = input.ToEntity();
var result = await useCase.ExecuteAsync(cancellationToken);

// incorrecto
WeatherForecastEntity entity = input.ToEntity();
```

### Declaración de namespaces

Solo namespaces con ámbito de archivo (C# 10+):

```csharp
// correcto
namespace WeatherForecast.Application.UseCases.GetWeatherForecast;

// incorrecto
namespace WeatherForecast.Application.UseCases.GetWeatherForecast { }
```

### Modificadores de acceso

Siempre escribirlos de forma explícita — nunca depender de los valores por defecto:

```csharp
// correcto
public sealed class GetWeatherForecastUseCase(...) { }

// incorrecto
sealed class GetWeatherForecastUseCase(...) { }
```

### Nomenclatura

| Símbolo | Convención | Ejemplo |
|---------|------------|---------|
| Interfaces | Prefijo `I` + PascalCase | `IWeatherForecastRepository` |
| Parámetros de tipo | Prefijo `T` + PascalCase | `TEntity` |
| Campos privados | `_camelCase` | `_repository` |
| Constantes | PascalCase | `SectionName` |
| Métodos asíncronos | PascalCase + sufijo `Async` | `ExecuteAsync` |
| Todos los demás miembros | PascalCase | `CreateWeatherForecastUseCase` |

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
public sealed class CreateWeatherForecastUseCase(...) : ICreateWeatherForecastUseCase { }
public sealed class WeatherForecastRepository(...) : BaseRepository<WeatherForecastEntity> { }
```

### Constructores primarios

Usar constructores primarios (C# 12+) para inyección de dependencias:

```csharp
public sealed class GetWeatherForecastUseCase(
    IWeatherForecastRepository repository,
    ILoggerService<GetWeatherForecastUseCase> logger) : IGetWeatherForecastUseCase
```

### Manejo de excepciones

Nunca capturar `Exception` fuera de los límites de infraestructura. `GlobalExceptionMiddleware` es el único lugar donde se permite un catch-all, suprimido con `#pragma warning disable CA1031`.

```csharp
// correcto — capturar excepciones específicas
catch (DomainException ex) { ... }
catch (OperationCanceledException) when (...) { ... }

// incorrecto — fuera de GlobalExceptionMiddleware
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
