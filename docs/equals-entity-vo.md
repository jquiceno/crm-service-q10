# Documentación Técnica: Igualdad en Entidades y Value Objects

En el Diseño Guiado por el Dominio (DDD), modelamos la realidad del negocio usando dos conceptos tácticos fundamentales: **Entidades** y **Objetos de Valor (Value Objects)**. Aunque ambos representan conceptos del dominio, la forma en que el negocio los identifica es completamente opuesta.

Implementar helpers abstractos para manejar su igualdad es una práctica recomendada por tres razones principales:

## 1. Entidades: Igualdad por Identidad (Id)

Las entidades representan objetos que tienen un ciclo de vida y una identidad única que prevalece en el tiempo, sin importar si sus atributos cambian.

- **El porqué:** Un Usuario sigue siendo el mismo usuario si cambia su correo electrónico. Por lo tanto, dos instancias de una entidad son iguales **única y exclusivamente si sus IDs son iguales**.
- **Beneficio de la clase base:** Centralizar `Equals`, `GetHashCode` y los operadores `==` / `!=` garantiza que nunca compares erróneamente dos entidades basándote en sus datos temporales, sino en su identidad real (`Guid Id`).

## 2. Value Objects: Igualdad Estructural (Atributos)

Los Objetos de Valor no tienen una identidad conceptual propia; se definen únicamente por el conjunto de sus propiedades. Son inmutables.

- **El porqué:** Un Dinero de USD 100 es exactamente igual a otro Dinero de USD 100. Si cambias el valor a USD 50, ya no es el mismo objeto, es uno nuevo. Dos Value Objects son iguales **si y solo si todos sus componentes son iguales**.
- **Beneficio de la clase base:** El método abstracto `GetEqualityComponents()` obliga a los Value Objects que hereden de él a exponer sus propiedades. La clase base se encarga de usar `SequenceEqual` para compararlos todos automáticamente, evitando tener que escribir a mano un `Equals` gigantesco cada vez que se crea un Value Object nuevo.

---

## ¿Por qué es una Buena Práctica en Clean Architecture?

| Ventaja | Explicación |
|---|---|
| **Consistencia del Dominio** | Todo el equipo evaluará la igualdad de la misma manera bajo las reglas de DDD, reduciendo bugs en las capas de aplicación y dominio. |
| **Prevención de Bugs en Colecciones** | Al sobreescribir `GetHashCode`, aseguras que el motor de .NET maneje correctamente estas clases dentro de `List.Contains()`, `HashSet` o llaves de `Dictionary`. |
| **Sintaxis Limpia (`==` y `!=`)** | Sobrecargar los operadores permite escribir código más natural y legible en los casos de uso (ej. `if (direccionActual == nuevaDireccion)`) en lugar de recurrir siempre a `.Equals()`). |
| **DRY (Don't Repeat Yourself)** | La lógica compleja de comparación estructural (como el cálculo hash combinado) se escribe **una sola vez** en la capa compartida (`Shared.Domain`). |
