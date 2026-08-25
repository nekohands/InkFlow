using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace InkFlow.Modules.Crawling.Infrastructure.Persistence;

/// <summary>dotnet-ef 设计时工厂：迁移生成不依赖运行环境连接串。</summary>
public sealed class CrawlingDbContextFactory : IDesignTimeDbContextFactory<CrawlingDbContext>
{
    public CrawlingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CrawlingDbContext>()
            .UseNpgsql("Host=localhost;Database=inkflow-design-time;Username=postgres;Password=postgres")
            .Options;

        return new CrawlingDbContext(options);
    }
}
