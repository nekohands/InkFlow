using InkFlow.Modules.Developers.Domain;
using InkFlow.Modules.Developers.Infrastructure.Credentials;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class DeveloperApiKeySecretGeneratorTests
{
    [TestMethod]
    public void Generate_Returns_UrlSafe_Opaque_Secret_With_Matching_Hash_And_Prefix()
    {
        var generated = new DeveloperApiKeySecretGenerator().Generate();

        StringAssert.StartsWith(generated.RawKey, "lf_dev_");
        Assert.AreEqual(15, generated.Prefix.Length);
        Assert.AreEqual(generated.RawKey[..generated.Prefix.Length], generated.Prefix);
        Assert.IsTrue(
            generated.RawKey.All(character =>
                char.IsLetterOrDigit(character) || character is '_' or '-'),
            "The key must be safe to carry in an HTTP header and JSON response.");
        Assert.AreEqual(
            DeveloperApiKey.HashSecret(generated.RawKey),
            generated.SecretHash);
        Assert.AreEqual(64, generated.SecretHash.Length);
    }

    [TestMethod]
    public void Generate_Produces_Different_Secrets_For_Separate_Issuances()
    {
        var generator = new DeveloperApiKeySecretGenerator();

        var first = generator.Generate();
        var second = generator.Generate();

        Assert.AreNotEqual(first.RawKey, second.RawKey);
        Assert.AreNotEqual(first.SecretHash, second.SecretHash);
    }
}
