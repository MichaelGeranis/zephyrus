namespace Zephyrus.Application.Exceptions;

/// <summary>
/// The caller is signed in but their roles do not allow this change. Mapped to
/// 403; its message names the role needed.
/// </summary>
public sealed class NotPermittedException : Exception
{
    public NotPermittedException(string message) : base(message)
    {
    }
}
