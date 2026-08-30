namespace InkFlow.Modules.Crawling.Domain;

/// <summary>一次书籍采集运行的生命周期状态。</summary>
public enum CollectionRunStatus
{
    /// <summary>已创建，等待首个 BookInfo 子任务领取。</summary>
    Pending,
    /// <summary>至少有一个子任务正在执行或等待继续执行。</summary>
    Running,
    /// <summary>暂停领取新子任务；已领取的原子工作可以完成。</summary>
    Paused,
    /// <summary>停止中；不再领取或安排后续工作，当前原子工作可以完成。</summary>
    Stopping,
    /// <summary>所有必需子任务均已完成且正文已发布。</summary>
    Completed,
    /// <summary>至少一个必需子任务永久失败。</summary>
    Failed,
    /// <summary>优雅停止完成，不可继续。</summary>
    Stopped,
    /// <summary>已取消，不可继续；已发布内容不回滚。</summary>
    Cancelled,
}

/// <summary>采集阶段。阶段推进只能向前，不代表子任务的运行状态。</summary>
public enum CollectionRunStage
{
    BookInfo,
    Toc,
    Content,
}
