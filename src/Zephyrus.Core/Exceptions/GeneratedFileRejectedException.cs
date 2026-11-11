namespace Zephyrus.Core.Exceptions;

/// <summary>
/// An agent produced files at paths it may not write to. None of its files are
/// committed; the message names each path and why it was refused.
/// </summary>
public sealed class GeneratedFileRejectedException : Exception
{
    public IReadOnlyList<string> RejectedPaths { get; }

    public GeneratedFileRejectedException(string agent, IReadOnlyList<string> rejectedPaths)
        : base($"The {agent} Agent tried to write to paths it may not change: {string.Join(", ", rejectedPaths)}. " +
               "None of its files were committed. Re-run the step, or change the task if it needs these files.")
    {
        RejectedPaths = rejectedPaths;
    }
}
