using InkFlow.Modules.Crawling.Application;
using InkFlow.Modules.Crawling.Domain;
using InkFlow.Modules.Sources.Application;

namespace InkFlow.Modules.Crawling.Infrastructure;

/// <summary>
/// 规则型任务执行器：把 CrawlPayload 翻译成一次 RuleAdapter 调用。
/// 来源不存在、未安装规则、能力缺失都归类为失败原因——
/// 调度层不关心适配器是 Rule 还是 Code，本类型只服务规则型来源。
/// </summary>
public sealed class RuleCrawlerTaskExecutor(
    ISourceRepository sourceRepository,
    RuleAdapter ruleAdapter) : ICrawlerTaskExecutor
{
    public async Task<CrawlOutcome> ExecuteAsync(CrawlerTask task, CancellationToken cancellationToken = default)
    {
        var source = await sourceRepository.GetAsync(task.Payload.SourceId, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            return CrawlOutcome.Fail($"source '{task.Payload.SourceId}' does not exist.");
        }

        if (!source.IsEnabled)
        {
            return CrawlOutcome.Fail($"source '{task.Payload.SourceId}' is disabled.");
        }

        if (source.RuleDsl is null)
        {
            return CrawlOutcome.Fail($"source '{task.Payload.SourceId}' has no rule DSL installed.");
        }

        var rule = source.FindRule(task.Payload.Capability);
        if (rule is null)
        {
            return CrawlOutcome.Fail(
                $"source '{task.Payload.SourceId}' declares no rule for capability {task.Payload.Capability}.");
        }

        var result = await ruleAdapter
            .ExecuteAsync(
                rule,
                source.BaseUrl,
                task.Payload.Variables,
                cancellationToken,
                new SourceExecutionContext(
                    source.Id,
                    source.ResolveCredentialReference(task.Payload.CredentialReferenceId),
                    SourceCredentialOwnerScope.Platform))
            .ConfigureAwait(false);

        return result.IsSuccess
            ? CrawlOutcome.Ok()
            : CrawlOutcome.Fail(string.Join("; ", result.Errors));
    }
}
