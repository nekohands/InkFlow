using Microsoft.EntityFrameworkCore;

namespace InkFlow.BuildingBlocks.Persistence;

/// <summary>
/// 模块化 DbContext 基类：每个模块拥有独立的 Postgres schema，
/// 模块间不共享表，跨模块只允许通过契约/事件访问数据（见 architecture/invariants.md）。
/// </summary>
public abstract class ModuleDbContext(DbContextOptions options, string moduleSchema) : DbContext(options)
{
    /// <summary>本模块的数据库 schema 名，同时作为表名前缀空间。</summary>
    public string ModuleSchema { get; } = moduleSchema;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(ModuleSchema);
        base.OnModelCreating(modelBuilder);
    }
}
