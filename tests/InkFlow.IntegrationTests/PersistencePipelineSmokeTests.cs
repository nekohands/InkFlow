using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.IntegrationTests;

/// <summary>
/// 集成测试管线守卫：确认 EF Core / Npgsql 依赖链可用。
/// Testcontainers.PostgreSql 已在依赖中就绪，真实容器化集成测试随后续阶段引入。
/// </summary>
[TestClass]
public sealed class PersistencePipelineSmokeTests
{
    [TestMethod]
    public void EntityFramework_Core_Is_Resolvable()
    {
        Assert.IsNotNull(typeof(DbContext));
    }
}
