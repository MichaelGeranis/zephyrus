using Zephyrus.Core.Exceptions;

namespace Zephyrus.Core.Pipeline;

/// <summary>
/// Which repository paths an agent may write generated files to. Agent output
/// comes from a language model, and the model reads text an attacker can
/// influence (issue bodies, the codebase), so a path is never trusted as given.
/// </summary>
/// <remarks>
/// Applies to the Code and QA Agents. The DevOps Agent's workflow file goes
/// through its own Tech Lead approval gate instead.
/// </remarks>
public static class GeneratedFilePolicy
{
    /// <summary>
    /// Folders an agent may not write into. Anything here runs code or grants
    /// access outside a code review: CI pipelines run on push, with the
    /// repository's secrets, before anyone approves the pull request.
    /// </summary>
    private static readonly string[] ProtectedFolders =
    {
        // CI/CD, Dependabot and CODEOWNERS for GitHub; CI for other hosts.
        ".github/",
        ".circleci/",
        ".buildkite/",
        // Git internals and commit hooks.
        ".git/",
        ".husky/",
        // Run when a developer opens the repository in an editor or container.
        ".devcontainer/",
        ".vscode/",
        // Artifacts a person approved; the audit trail depends on them.
        "docs/prd/",
        "docs/adr/",
        "docs/tasks/",
        "docs/qa/",
    };

    private static readonly string[] ProtectedFiles =
    {
        ".gitmodules",
        ".gitlab-ci.yml",
        ".travis.yml",
        "azure-pipelines.yml",
        "bitbucket-pipelines.yml",
        "Jenkinsfile",
        "CODEOWNERS",
        "docs/CODEOWNERS",
    };

    /// <summary>
    /// Why <paramref name="path"/> may not be written, or null when it may.
    /// </summary>
    public static string? Violation(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "the path is empty";

        if (path.Any(char.IsControl))
            return "the path contains control characters";

        if (path.Contains('\\'))
            return "the path uses backslashes; use '/'";

        if (path.StartsWith('/') || (path.Length > 1 && path[1] == ':'))
            return "the path is absolute; it must be relative to the repository root";

        var segments = path.Split('/');
        if (segments.Any(s => s.Length == 0 || s == "." || s == ".."))
            return "the path has an empty, '.' or '..' segment";

        foreach (var folder in ProtectedFolders)
        {
            if (path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
                return $"'{folder}' is protected";
        }

        foreach (var file in ProtectedFiles)
        {
            if (path.Equals(file, StringComparison.OrdinalIgnoreCase))
                return $"'{file}' is protected";
        }

        // A file named .git anywhere turns its folder into a Git link.
        if (segments[^1].Equals(".git", StringComparison.OrdinalIgnoreCase))
            return "'.git' is protected";

        return null;
    }

    /// <summary>
    /// Throws if any path may not be written, naming every rejected path.
    /// Called before committing, so none of the files is committed.
    /// </summary>
    public static void EnsureAllowed(string agent, IEnumerable<string> paths)
    {
        var rejected = paths
            .Select(path => (Path: path, Reason: Violation(path)))
            .Where(p => p.Reason is not null)
            .Select(p => $"'{p.Path}' ({p.Reason})")
            .ToList();

        if (rejected.Count > 0)
            throw new GeneratedFileRejectedException(agent, rejected);
    }
}
