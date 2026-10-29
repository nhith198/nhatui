namespace NhaTro.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}

/// <summary>Marker for consistency boundaries: only aggregate roots are loaded and saved through repositories.</summary>
public interface IAggregateRoot { }

public enum DomainErrorKind { Validation, Conflict }

/// <summary>Thrown when a business invariant would be violated.</summary>
public class DomainException(string message, DomainErrorKind kind = DomainErrorKind.Validation) : Exception(message)
{
    public DomainErrorKind Kind { get; } = kind;
}
