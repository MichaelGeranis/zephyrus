namespace Zephyrus.Core.Agents;

/// <summary>
/// A single message in a multi-turn conversation with a language model.
/// </summary>
public sealed record ConversationMessage(string Role, string Content);
