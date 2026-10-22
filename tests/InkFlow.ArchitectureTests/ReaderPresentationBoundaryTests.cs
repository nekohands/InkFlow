using InkFlow.Api.Presentation;
using InkFlow.Modules.Content.Application;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.ArchitectureTests;

[TestClass]
public sealed class ReaderPresentationBoundaryTests
{
    [TestMethod]
    public void Reader_renderer_and_browser_assets_belong_to_the_api_host()
    {
        var renderer = typeof(ReaderHtml);
        Assert.AreEqual("InkFlow.Api.Presentation", renderer.Namespace);
        Assert.AreEqual("InkFlow.Api", renderer.Assembly.GetName().Name);
        var resources = renderer.Assembly.GetManifestResourceNames();
        foreach (var asset in new[] { "reader-client.js", "reader.css", "reader-account.js", "service-worker.js", "operations.js", "operations.css" })
            CollectionAssert.Contains(resources, "InkFlow.Api.Presentation.Assets." + asset);
    }

    [TestMethod]
    public void Content_has_no_reader_page_renderer_or_browser_assets()
    {
        var content = typeof(CatalogQueryService).Assembly;
        Assert.IsFalse(content.GetTypes().Any(type => type.Name.StartsWith("ReaderHtml", StringComparison.Ordinal)));
        Assert.IsEmpty(content.GetManifestResourceNames().Where(name =>
            name.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".webmanifest", StringComparison.OrdinalIgnoreCase)).ToArray());
    }
}
