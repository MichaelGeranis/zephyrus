namespace Zephyrus.Application.Exceptions;

/// <summary>
/// A request the pipeline's rules reject: approving in the wrong status,
/// approving twice, re-running an unknown step. Mapped to 400, and its message
/// is shown to the caller, so it must explain the rule rather than expose
/// internals. Unexpected failures use other exception types and reach callers
/// only as a generic error.
/// </summary>
public sealed class RequestRejectedException : Exception
{
    public RequestRejectedException(string message) : base(message)
    {
    }
}
