using Zephyrus.Core.Enums;

namespace Zephyrus.Core.Entities;

/// <summary>
/// An atomic unit of work assigned to the Code Agent.
/// Each TaskItem maps to an issue in the code host and eventually a pull request.
/// Named TaskItem to avoid collision with System.Threading.Tasks.Task.
/// </summary>
public class TaskItem
{
    public Guid Id { get; private set; }
    public Guid FeatureId { get; private set; }
    public int? ExternalIssueId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public TaskItemStatus Status { get; private set; }
    public int? PrId { get; private set; }
    public AgentType AgentType { get; private set; }

    public Feature Feature { get; private set; } = null!;

    private TaskItem() { }

    public static TaskItem Create(Guid featureId, string title, AgentType agentType)
    {
        return new TaskItem
        {
            Id = Guid.NewGuid(),
            FeatureId = featureId,
            Title = title,
            Status = TaskItemStatus.Pending,
            AgentType = agentType
        };
    }

    /// <summary>
    /// Links this task to an issue in the code host.
    /// </summary>
    public void SetExternalIssueId(int issueId)
    {
        ExternalIssueId = issueId;
    }

    /// <summary>
    /// Links this task to a pull request.
    /// </summary>
    public void SetPrId(int prId)
    {
        PrId = prId;
        Status = TaskItemStatus.PrOpen;
    }

    /// <summary>
    /// Marks this task as in progress.
    /// </summary>
    public void MarkInProgress()
    {
        Status = TaskItemStatus.InProgress;
    }

    /// <summary>
    /// Marks this task as done.
    /// </summary>
    public void MarkDone()
    {
        Status = TaskItemStatus.Done;
    }
}
