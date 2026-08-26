namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>Crawling 模块拥有的 schema。其他模块禁止直接读写本 schema 内的表。</summary>
public static class CrawlingSchema
{
    public const string Name = "crawler";
}
