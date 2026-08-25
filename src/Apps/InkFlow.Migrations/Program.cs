// 数据库迁移入口：对目标库应用所有未执行的 EF 迁移。
// 连接串来自环境变量 ConnectionStrings__Database（docker compose 注入），
// 本地默认指向 compose 的 postgres 实例。
// 进程必须以退出码 0 结束——compose 中 api/worker/scheduler 依赖 migrations 成功完成。
using InkFlow.Modules.Crawling.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var connectionString =
    Environment.GetEnvironmentVariable("ConnectionStrings__Database")
    ?? "Host=localhost;Port=5432;Database=inkflow;Username=inkflow;Password=inkflow";

var options = new DbContextOptionsBuilder<CrawlingDbContext>()
    .UseNpgsql(connectionString)
    .Options;

await using var db = new CrawlingDbContext(options);

var applied = await db.Database.GetPendingMigrationsAsync().ConfigureAwait(false);
await db.Database.MigrateAsync().ConfigureAwait(false);

Console.WriteLine(
    applied.Any()
        ? $"InkFlow.Migrations: applied {applied.Count()} migration(s)."
        : "InkFlow.Migrations: database already up to date.");
return 0;
