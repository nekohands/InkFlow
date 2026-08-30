using System.IO.Compression;
using System.Text;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Content.Domain;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class BookPackageBuilderTests
{
    [TestMethod]
    public async Task Builds_A_Single_File_Utf8_Txt()
    {
        using var output = new MemoryStream();
        var builder = new BookPackageBuilder();

        await builder.BuildAsync(CreateDocument(), BookPackageFormat.Txt, output, _ => Task.CompletedTask);

        var text = Encoding.UTF8.GetString(output.ToArray());
        StringAssert.Contains(text, "测试书");
        StringAssert.Contains(text, "作者：作者");
        StringAssert.Contains(text, "生成时间：2026-08-31T10:00:00.0000000+00:00");
        StringAssert.Contains(text, "第 1 章 第一章");
        StringAssert.Contains(text, "正文第一段");
    }

    [TestMethod]
    public async Task Builds_Zip_With_Manifest_Book_Metadata_And_Chapter_Text()
    {
        using var output = new MemoryStream();
        var builder = new BookPackageBuilder();

        await builder.BuildAsync(CreateDocument(), BookPackageFormat.Zip, output, _ => Task.CompletedTask);

        output.Position = 0;
        using var archive = new ZipArchive(output, ZipArchiveMode.Read);
        Assert.IsNotNull(archive.GetEntry("manifest.json"));
        Assert.IsNotNull(archive.GetEntry("book.json"));
        StringAssert.Contains(ReadEntry(archive, "manifest.json"), "\"formatVersion\":\"1\"");
        StringAssert.Contains(ReadEntry(archive, "manifest.json"), "\"files\"");
        var chapter = archive.GetEntry("chapters/000001.txt");
        Assert.IsNotNull(chapter);
        using var reader = new StreamReader(chapter!.Open(), Encoding.UTF8);
        StringAssert.Contains(reader.ReadToEnd(), "正文第一段");
    }

    [TestMethod]
    public async Task Builds_Epub3_With_Mimetype_Container_Opf_Nav_And_Chapters()
    {
        using var output = new MemoryStream();
        var builder = new BookPackageBuilder();
        var progress = new List<int>();

        await builder.BuildAsync(
            CreateDocument(),
            BookPackageFormat.Epub,
            output,
            value =>
            {
                progress.Add(value);
                return Task.CompletedTask;
            });

        output.Position = 0;
        using var archive = new ZipArchive(output, ZipArchiveMode.Read);
        CollectionAssert.AreEqual(
            new[] { "mimetype", "META-INF/container.xml", "OEBPS/content.opf", "OEBPS/nav.xhtml", "OEBPS/chapters/000001.xhtml" },
            archive.Entries.Select(entry => entry.FullName).ToArray());
        Assert.AreEqual("application/epub+zip", ReadEntry(archive, "mimetype"));
        StringAssert.Contains(ReadEntry(archive, "OEBPS/content.opf"), "version=\"3.0\"");
        StringAssert.Contains(ReadEntry(archive, "OEBPS/nav.xhtml"), "第一章");
        StringAssert.Contains(ReadEntry(archive, "OEBPS/chapters/000001.xhtml"), "正文第一段");
        CollectionAssert.AreEqual(new[] { 1 }, progress.ToArray());
    }

    private static BookPackageDocument CreateDocument() =>
        new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "测试书",
            "作者",
            [
                new BookPackageChapter(
                    Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    0,
                    "第一章",
                    "正文第一段\n\n正文第二段",
                    Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    "hash-1"),
            ],
            new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero));

    private static string ReadEntry(ZipArchive archive, string name)
    {
        using var reader = new StreamReader(archive.GetEntry(name)!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
