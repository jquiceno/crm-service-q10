using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage(
    "Design",
    "CA1008:Enums should have zero value",
    Justification = "The domain enums start at 1 on purpose: default(T) must not be a valid value. " +
                    "An unassigned type or status is then rejected by Enum.IsDefined instead of " +
                    "silently passing as a legitimate state, and a 'None' member would be a state " +
                    "no activity can actually be in.",
    Scope = "NamespaceAndDescendants",
    Target = "~N:Activities.Domain.Enums")]
