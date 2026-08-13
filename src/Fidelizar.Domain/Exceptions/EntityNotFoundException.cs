namespace Fidelizar.Domain.Exceptions;

/// <summary>Thrown when a requested entity does not exist. Maps to HTTP 404 in Fidelizar.Api.</summary>
public class EntityNotFoundException : AppException
{
    public EntityNotFoundException(string entityName)
        : base($"The entity '{entityName}' was not found.", "ENTITY_NOT_FOUND")
    {
    }
}
