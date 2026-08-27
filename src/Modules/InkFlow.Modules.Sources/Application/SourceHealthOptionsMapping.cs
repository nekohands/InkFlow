using InkFlow.BuildingBlocks.Application;
using InkFlow.Modules.Sources.Domain;

namespace InkFlow.Modules.Sources.Application;

/// <summary>
/// 宿主配置到 Domain 运行时参数的映射。BuildingBlocks 不反向依赖模块，
/// 因此转换以扩展形式放在 Sources 模块应用层，组合根一行完成装配。
/// </summary>
public static class SourceHealthOptionsMapping
{
    /// <summary>把宿主 <see cref="SourceHealthOptions"/> 映射为 Domain 参数快照。</summary>
    public static SourceHealthParameters ToParameters(this SourceHealthOptions options) => new(
        options.UnhealthyAfterConsecutiveFailures,
        options.ProbeCooldownBaseMinutes,
        options.ProbeCooldownMaxMinutes);
}
