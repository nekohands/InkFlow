namespace InkFlow.Modules.Operations.Application;

internal sealed partial class ConsistencyRuleEvaluation
{
    private void CheckCrawling()
    {
        foreach (var task in snapshot.CrawlerTasks)
        {
            if (!sources.ContainsKey(task.SourceId))
            {
                Add(
                    "crawler_task_source_missing",
                    "crawler_task",
                    task.Id.ToString("D"),
                    $"crawler task refers to missing source '{task.SourceId}'.");
            }

            if (!values.CrawlerTaskStatuses.Contains(task.Status))
            {
                Add(
                    "crawler_task_status_invalid",
                    "crawler_task",
                    task.Id.ToString("D"),
                    $"status value '{task.Status}' is not defined by CrawlerTaskStatus.");
            }
        }

        foreach (var deadLetter in snapshot.DeadLetters)
        {
            if (!sources.ContainsKey(deadLetter.SourceId))
            {
                Add(
                    "dead_letter_source_missing",
                    "dead_letter",
                    deadLetter.Id.ToString("D"),
                    $"dead letter refers to missing source '{deadLetter.SourceId}'.");
            }

            if (!crawlerTasks.TryGetValue(deadLetter.TaskId, out var task))
            {
                Add(
                    "dead_letter_task_missing",
                    "dead_letter",
                    deadLetter.Id.ToString("D"),
                    $"dead letter refers to missing crawler task '{deadLetter.TaskId:D}'.");
            }
            else
            {
                if (!string.Equals(task.SourceId, deadLetter.SourceId, StringComparison.Ordinal))
                {
                    Add(
                        "dead_letter_source_mismatch",
                        "dead_letter",
                        deadLetter.Id.ToString("D"),
                        "dead letter source does not match the original crawler task source.");
                }

                if (task.Status != values.DeadLetteredTaskStatus)
                {
                    Add(
                        "dead_letter_task_state_mismatch",
                        "dead_letter",
                        deadLetter.Id.ToString("D"),
                        "a dead letter must reference a crawler task still in DeadLettered state.");
                }
            }

            if (deadLetter.ReplayTaskId is { } replayTaskId && !crawlerTasks.ContainsKey(replayTaskId))
            {
                Add(
                    "dead_letter_replay_task_missing",
                    "dead_letter",
                    deadLetter.Id.ToString("D"),
                    $"dead letter replay history refers to missing crawler task '{replayTaskId:D}'.");
            }
        }

    }
}
