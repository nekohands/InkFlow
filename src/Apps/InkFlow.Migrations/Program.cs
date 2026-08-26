// 数据库迁移入口：对目标库应用所有未执行的 EF 迁移。
// 连接串来自环境变量 ConnectionStrings__Database（docker compose 注入），
// 本地默认指向 compose 的 postgres 实例。
// 进程必须以退出码 0 结束——compose 中 api/worker/scheduler 依赖 migrations 成功完成。
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using InkFlow.Modules.Library.Infrastructure.Persistence;
using InkFlow.Modules.Sources.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var connectionString =
    Environment.GetEnvironmentVariable("ConnectionStrings__Database")
    ?? "Host=localhost;Port=5432;Database=inkflow;Username=inkflow;Password=inkflow";

// 各模块 DbContext 依次应用自己的迁移；迁移 ID 全局唯一，共享历史表不冲突。
var contexts = new DbContext[]
{
    new CrawlingDbContext(new DbContextOptionsBuilder<CrawlingDbContext>().UseNpgsql(connectionString).Options),
    new LibraryDbContext(new DbContextOptionsBuilder<LibraryDbContext>().UseNpgsql(connectionString).Options),
    new SourcesDbContext(new DbContextOptionsBuilder<SourcesDbContext>().UseNpgsql(connectionString).Options),
};

foreach (var context in contexts)
{
    await using (context)
    {
        var pending = await context.Database.GetPendingMigrationsAsync().ConfigureAwait(false);
        await context.Database.MigrateAsync().ConfigureAwait(false);
        Console.WriteLine(pending.Any()
            ? $"InkFlow.Migrations[{context.GetType().Name}]: applied {pending.Count()} migration(s)."
            : $"InkFlow.Migrations[{context.GetType().Name}]: already up to date.");
    }
}

return 0;
