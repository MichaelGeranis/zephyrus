namespace Zephyrus.Application.Exceptions;

/// <summary>
/// The feature, project or artifact a request names does not exist. Mapped to
/// 404, and its message is shown to the caller, so it must not carry anything
/// the caller should not see.
/// </summary>
public sealed class ResourceNotFoundException : Exception
{
    public ResourceNotFoundException(string message) : base(message)
    {
    }
}
