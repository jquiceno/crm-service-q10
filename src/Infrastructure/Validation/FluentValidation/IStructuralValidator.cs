using FluentValidation;

namespace Infrastructure.Validation.FluentValidation;

/// <summary>
/// Marks a FluentValidation validator as structural (format, ranges, required fields) for HTTP-level validation.
/// </summary>
public interface IStructuralValidator<T> : IValidator<T> { }