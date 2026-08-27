using InkFlow.Modules.Identity.Infrastructure.Credentials;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class Pbkdf2PasswordHasherTests
{
    [TestMethod]
    public void Hash_Verifies_Only_The_Original_Password()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.Hash("correct horse battery staple");

        Assert.IsTrue(hash.StartsWith(Pbkdf2PasswordHasher.FormatPrefix, StringComparison.Ordinal));
        Assert.IsTrue(hasher.Verify("correct horse battery staple", hash));
        Assert.IsFalse(hasher.Verify("correct horse battery staph", hash));
        Assert.IsFalse(hash.Contains("correct horse", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Hash_Uses_A_New_Salt_For_Each_Password()
    {
        var hasher = new Pbkdf2PasswordHasher();

        var first = hasher.Hash("correct horse battery staple");
        var second = hasher.Hash("correct horse battery staple");

        Assert.AreNotEqual(first, second);
        Assert.IsTrue(hasher.Verify("correct horse battery staple", first));
        Assert.IsTrue(hasher.Verify("correct horse battery staple", second));
    }

    [TestMethod]
    public void Malformed_Or_Tampered_Hash_Fails_Closed()
    {
        var hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.Hash("correct horse battery staple");

        Assert.IsFalse(hasher.Verify("correct horse battery staple", "not-a-hash"));
        Assert.IsFalse(hasher.Verify("correct horse battery staple", hash + "tampered"));
        Assert.IsFalse(hasher.Verify("", hash));
    }
}
