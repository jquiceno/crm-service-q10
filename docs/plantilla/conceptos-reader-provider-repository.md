# Reader, Provider y Repository

Este documento define tres piezas de acceso a datos y, sobre todo, **cómo distinguirlas**. Las tres tocan datos, pero responden a responsabilidades distintas: mezclarlas erosiona la separación entre lectura y fuente de verdad que sostiene la arquitectura.

> Para el patrón de persistencia del Aggregate en detalle, ver [repositorio.md](repositorio.md). Para saber por qué el Repository no es un "Port", ver [puertos-y-adaptadores.md](puertos-y-adaptadores.md).

---

## Cuadro comparativo

| Criterio | **Reader** | **Provider** | **Repository** |
|---|---|---|---|
| Propósito | Solo lectura | Solo lectura, orientada a **completar el input** | **Fuente de verdad** (lectura y escritura) |
| ¿Usa agregados / dominio? | No | No | **Sí — solo agregados** |
| Fuentes permitidas | Repositorios, conexión directa, o lo que requiera | **Solo repositorios** | — (es la fuente) |
| Dónde vive la interfaz | Aplicación (`Application/Ports`) | Aplicación (`Application/Providers`) | Dominio (`Domain/Repositories`) |
| Implementación | Infraestructura (`Persistence/EntityFramework/{Contexto}/`) | Aplicación (clase concreta, sin interfaz) | Infraestructura (`Persistence/EntityFramework/{Contexto}/`) |

La distinción de una frase:

- **Reader** — lee lo que sea, de donde sea, para *servir* una lectura.
- **Provider** — lee **solo de repositorios**, para *completar/enriquecer un input* antes de que el use case lo procese.
- **Repository** — la única pieza que trabaja con **agregados** y la única fuente de verdad.

---

## Reader

Pieza de **solo lectura** que no requiere dominio, por lo que **no usa agregados**. Su interfaz vive en la capa de aplicación (es un Port), y es **libre en su fuente**: puede apoyarse en repositorios, en una conexión directa a base de datos, o en cualquier medio que la lectura requiera.

Se usa cuando necesitas leer datos que **no forman parte del Aggregate del contexto** — típicamente tablas foráneas de solo lectura o catálogos — para responder una consulta, servir un endpoint o validar una referencia.

**Cuándo es un Reader (y no un Provider):**

- Lee de una fuente que **no es un repositorio** (conexión directa, tabla foránea, vista), **o**
- Su resultado *sirve una lectura* en lugar de *completar un input* de escritura.

### Naming y ubicación

```
Contexts/{Contexto}/Application/Ports/I{Concepto}Reader.cs          → contrato
Infrastructure/Persistence/EntityFramework/{Contexto}/{Concepto}Reader.cs  → implementación
```

El Reader **no** lleva sufijo `Port` ni `Adapter`, y su implementación **no** vive en `Infrastructure/Adapters/` — es una pieza de persistencia, y vive junto al repositorio del contexto (misma regla que en [repositorio.md](repositorio.md#ubicación-y-naming-del-repositorio)).

### Ejemplos reales

**`IProgramClassificationReader`** (academic-service) — lee el catálogo de clasificaciones de programa: una tabla que no es agregado de este contexto. Sirve dos usos: validar que el `ClassificationId` de un POST/PUT existe, y exponer el catálogo completo como listado.

```csharp
// Contexts/AcademicProgram/Application/Ports/IProgramClassificationReader.cs
public interface IProgramClassificationReader
{
    Task<Result<bool>> ExistsAsync(int id, CancellationToken cancellationToken = default);

    Task<PagedResult<ProgramClassification>> GetAsync(
        PageQuery page,
        CancellationToken cancellationToken = default);
}
```

```csharp
// Infrastructure/Persistence/EntityFramework/AcademicPrograms/ProgramClassificationReader.cs
public sealed class ProgramClassificationReader(
    ApplicationDbContext context,
    ILoggerPort<ProgramClassificationReader> logger) : IProgramClassificationReader
{
    private const string Origin = nameof(ProgramClassificationReader);
    // ...
}
```

El Reader proyecta a un modelo de lectura propio del dominio (`Domain/Models/ProgramClassification.cs`), no a un agregado: es un `sealed record` sin identidad ni reglas, no un `AggregateRoot<TId>`.

**`IAuditStatisticsReader`** (audits-service) — lee series de estadísticas desde tablas foráneas de la institución (`Payments`, `CommercialOpportunities`, `People`) por conexión directa vía `DbContext`. No es un Repository: lee tablas externas en vez de persistir/recuperar el Aggregate de auditoría.

```csharp
// Contexts/Audit/Application/Ports/IAuditStatisticsReader.cs
public interface IAuditStatisticsReader
{
    Task<Result<IReadOnlyList<AuditStatisticsSeries>>> GetAllAsync(
        DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
    // ...
}
```

**`IPersonNameReader`** (audits-service) — resuelve el nombre completo de una persona a partir de su código, leyendo `tbl_per_personas` por conexión directa. Aunque su consumidor principal es el ingest (rellena `aud_usuario` en el insert), es un **Reader**, no un Provider: su única fuente posible es una tabla foránea de lectura, no un repositorio.

```csharp
// Contexts/Audit/Application/Ports/IPersonNameReader.cs
public interface IPersonNameReader
{
    Task<string?> GetFullNameAsync(string? personCode, CancellationToken cancellationToken = default);
}
```

> **Nota de diseño.** `IPersonNameReader` "completa el input" (parecería Provider), pero la regla de Provider *"solo puede usar repositorios como fuente"* es insatisfacible aquí: no existe —ni puede existir— un repositorio de `Person`, porque los repositorios solo trabajan con agregados y `Person` es una tabla foránea de lectura. Como su única capacidad real es leer directo, se clasifica como **Reader**. La regla de decisión: cuando "propósito" y "fuente disponible" entran en conflicto, **manda la capacidad** (de dónde y cómo lee).

### Registro en DI

Junto al repositorio del contexto, antes de los use cases que lo consumen:

```csharp
services.AddScoped<IProgramRepository, ProgramRepository>();
services.AddScoped<IProgramClassificationReader, ProgramClassificationReader>();

services.AddScoped<ICreateProgramUseCase, CreateProgramUseCase>();
```

---

## Provider

Pieza de **solo lectura** que no requiere dominio y **no usa agregados**. Vive en aplicación, como clase concreta sin interfaz. Se diferencia del Reader en dos puntos:

1. **Su fuente está restringida a repositorios** (no conexión directa ni tablas foráneas).
2. **Su propósito es completar/enriquecer el input** de un use case, no servir una lectura al exterior.

El caso típico es un valor con fallback: si el cliente envía el dato en el input, se usa; si no, el Provider lo resuelve desde un repositorio. Esa decisión es lógica de aplicación y extraerla al Provider mantiene el use case enfocado en orquestación.

**Cuándo extraer un Provider:**

- El use case depende de uno o más **repositorios** para resolver su input.
- Hay una condición tipo *"si vacío, búscalo"* que oscurece el flujo principal.
- La misma resolución podría reutilizarse desde otro use case.

No extraer por anticipación: si un solo use case necesita la lógica y el flujo es simple, mantenerlo inline es preferible.

```csharp
// Contexts/{Contexto}/Application/Providers/{Contexto}{Concepto}Provider.cs
public sealed class ProductCategoriesProvider(ICategoryRepository repository)
{
    public async Task<Result<IReadOnlyList<string>>> GetAsync(
        IReadOnlyList<string>? categories, CancellationToken cancellationToken = default)
    {
        if (categories is { Count: > 0 })
            return Result<IReadOnlyList<string>>.Success(categories);

        var result = await repository.GetAllAsync(isActive: true, cancellationToken).ConfigureAwait(false);
        return result.IsFailure
            ? result.Error
            : Result<IReadOnlyList<string>>.Success(result.Value.Select(c => c.Code).Distinct().ToList());
    }
}
```

> En la práctica, los servicios levantados hasta hoy (`audits-service`, `academic-service`) no han necesitado ningún Provider: toda su resolución auxiliar lee de catálogos o tablas foráneas, y por tanto es un **Reader**. El Provider sigue siendo la pieza correcta cuando la fuente sí es un repositorio del propio servicio.

> Para naming, anatomía completa, registro en DI y testing de Providers, ver [providers.md](providers.md).

---

## Repository

Es la **fuente de verdad** del contexto: la única pieza con acceso de **lectura y escritura** sobre el Aggregate, y la única que **usa agregados**. Su contrato vive en el **dominio** (`Domain/Repositories`) porque expresa una necesidad del dominio; su implementación vive en infraestructura.

```csharp
// Contexts/AcademicProgram/Domain/Repositories/IProgramRepository.cs
public interface IProgramRepository : IRootRepository<ProgramAggregate, string>
{
    Task<PagedResult<ProgramAggregate>> GetAsync(
        ProgramFilter filter, PageQuery page, CancellationToken cancellationToken = default);

    Task<Result<ProgramAggregate>> CreateAsync(
        ProgramAggregate aggregate, CancellationToken cancellationToken = default);
}
```

Un contexto tiene un repositorio por Aggregate, y nada más: tablas foráneas, catálogos y vistas **no** llevan repositorio propio — se acceden mediante Readers.

> Detalle completo del patrón (métodos, `Result`, entidad de persistencia + mapper, ubicación y naming) en [repositorio.md](repositorio.md).

---

## Árbol de decisión

```
¿Escribe, o es la fuente de verdad de un Aggregate del contexto?
│
├── Sí ─────────────────────────────► Repository   (usa agregados; interfaz en Domain)
│
└── No (solo lectura, sin agregados)
    │
    ├── ¿Su fuente son SOLO repositorios
    │    Y su fin es completar el input? ─► Provider  (clase concreta en Application/Providers)
    │
    └── En otro caso
        (lee de conexión directa / tabla
         foránea, o sirve una lectura) ───► Reader     (interfaz en Application/Ports)
```

---

## Errores comunes

- **Un Reader/Provider tocando agregados.** Si necesitas el Aggregate, la pieza correcta es el Repository.
- **Un Provider leyendo por conexión directa o de una tabla foránea.** Si la fuente no es un repositorio, es un Reader.
- **Crear un "repositorio" sobre algo que no es agregado** (p. ej. `Person`, un catálogo) solo para satisfacer la regla de Provider. Rompe *"Repository solo usa agregados"*; usa un Reader.
- **Poner la interfaz del Repository en aplicación.** El contrato del Repository pertenece al dominio; los de Reader y Provider, a aplicación.
- **Nombrar la implementación del Reader como `...Adapter` o colocarla en `Infrastructure/Adapters/`.** Va en `Infrastructure/Persistence/EntityFramework/{Contexto}/` y termina en `Reader`.

---

## Ver también

- [repositorio.md](repositorio.md) — patrón de Repositorio en detalle
- [providers.md](providers.md) — anatomía, naming, DI y testing de Providers
- [puertos-y-adaptadores.md](puertos-y-adaptadores.md) — Ports vs. Repository
- [arquitectura.md](arquitectura.md) — estructura de capas
