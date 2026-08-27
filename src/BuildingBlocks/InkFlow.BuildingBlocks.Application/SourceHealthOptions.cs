using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace InkFlow.BuildingBlocks.Application;

/// <summary>
/// 来源健康策略的运行时配置：连续失败阈值、探针冷却的指数退避曲线。
/// 默认值与 <c>SourceHealthPolicy</c>（source-health-v1）常量一致；宿主可经
/// 配置节 <c>SourceHealth</c> 覆盖（如 <c>SourceHealth__ProbeCooldownBaseMinutes</c>）。
///
/// 这是纯配置 POCO：不引用任何模块，宿主用 <see cref="FromConfiguration"/>
/// 构造并注册为单例；模块侧经映射扩展转为 Domain 的
/// <c>SourceHealthParameters</c> 后在读取出口装载。持久化状态与算法版本
/// 不变——仅冷却曲线的常量来源从编译期常量变为运行时配置。
/// </summary>
public sealed class SourceHealthOptions
{
    public const string ConfigurationSectionName = "SourceHealth";

    /// <summary>连续失败多少次后进入 Unhealthy。默认 3，与 v1 一致。</summary>
    public int UnhealthyAfterConsecutiveFailures { get; init; } = 3;

    /// <summary>首次进入 Unhealthy 后的基础冷却期（分钟）。默认 30，与 v1 一致。</summary>
    public int ProbeCooldownBaseMinutes { get; init; } = 30;

    /// <summary>探针冷却上限（分钟）。默认 1440（一天）。</summary>
    public int ProbeCooldownMaxMinutes { get; init; } = 24 * 60;

    /// <summary>
    /// 无配置时的 v1 默认参数；宿主未提供配置节时应使用它，
    /// 保证与既有测试断言的冷却曲线完全一致。
    /// </summary>
    public static SourceHealthOptions Default { get; } = new();

    /// <summary>从 IConfiguration 节读取；缺失键回退 v1 默认，非法整数快速失败。</summary>
    public static SourceHealthOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(ConfigurationSectionName);
        var options = new SourceHealthOptions
        {
            UnhealthyAfterConsecutiveFailures = ReadInt(
                section, nameof(UnhealthyAfterConsecutiveFailures), 3),
            ProbeCooldownBaseMinutes = ReadInt(
                section, nameof(ProbeCooldownBaseMinutes), 30),
            ProbeCooldownMaxMinutes = ReadInt(
                section, nameof(ProbeCooldownMaxMinutes), 24 * 60),
        };
        options.Validate();
        return options;
    }

    public void Validate()
    {
        ValidateRange(UnhealthyAfterConsecutiveFailures, 1, 100,
            nameof(UnhealthyAfterConsecutiveFailures));
        ValidateRange(ProbeCooldownBaseMinutes, 1, 7 * 24 * 60,
            nameof(ProbeCooldownBaseMinutes));
        ValidateRange(ProbeCooldownMaxMinutes, ProbeCooldownBaseMinutes, 30 * 24 * 60,
            nameof(ProbeCooldownMaxMinutes));
    }

    private static int ReadInt(IConfiguration section, string key, int defaultValue)
    {
        var raw = section[key];
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (!int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException(
                $"{ConfigurationSectionName}:{key} must be an integer.");
        }

        return value;
    }

    private static void ValidateRange(int value, int minimum, int maximum, string name)
    {
        if (value < minimum || value > maximum)
        {
            throw new InvalidOperationException(
                $"{ConfigurationSectionName}:{name} must be between {minimum} and {maximum}.");
        }
    }
}
