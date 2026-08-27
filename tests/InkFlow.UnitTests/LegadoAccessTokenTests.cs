using InkFlow.Modules.Identity.Domain;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class LegadoAccessTokenTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 14, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Token_Normalizes_Metadata_And_Revoke_Is_Idempotent()
    {
        var userId = Guid.NewGuid();
        var token = LegadoAccessToken.Create(
            userId,
            " Reading 3.0 ",
            "lf_lgd_abcd1234",
            "hash-value",
            LegadoTokenScope.Read,
            T0,
            T0.AddDays(90));

        Assert.AreEqual("Reading 3.0", token.Name);
        Assert.IsTrue(token.IsActive(T0.AddMinutes(1)));
        Assert.IsTrue(token.HasScope(LegadoTokenScope.Read));
        Assert.IsFalse(token.HasScope(LegadoTokenScope.None));

        token.Revoke(T0.AddDays(1));
        token.Revoke(T0.AddDays(2));

        Assert.AreEqual(T0.AddDays(1), token.RevokedAt);
        Assert.IsFalse(token.IsActive(T0.AddDays(2)));
    }

    [TestMethod]
    public void Token_Expires_At_The_Exact_Expiry_Instant()
    {
        var token = LegadoAccessToken.Create(
            Guid.NewGuid(),
            "Reading 3.0",
            "lf_lgd_abcd1234",
            "hash-value",
            LegadoTokenScope.Read,
            T0,
            T0.AddDays(1));

        Assert.IsTrue(token.IsActive(T0.AddHours(23)));
        Assert.IsFalse(token.IsActive(T0.AddDays(1)));
    }

    [TestMethod]
    public void Token_Rejects_Unsupported_Scope_And_Invalid_Expiry()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => LegadoAccessToken.Create(
            Guid.NewGuid(),
            "Reading 3.0",
            "lf_lgd_abcd1234",
            "hash-value",
            LegadoTokenScope.None,
            T0,
            T0.AddDays(1)));

        Assert.ThrowsExactly<ArgumentException>(() => LegadoAccessToken.Create(
            Guid.NewGuid(),
            "Reading 3.0",
            "lf_lgd_abcd1234",
            "hash-value",
            LegadoTokenScope.Read,
            T0,
            T0));
    }
}
