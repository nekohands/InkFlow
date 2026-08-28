using System.IO.Compression;
using System.Text;
using InkFlow.Modules.Library.Application;
using InkFlow.Modules.Library.Domain;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class PrivateBookImportTests
{
    private static readonly Guid UserId = Guid.Parse("01908d2a-2d44-7b3b-9ec2-123456789abc");
    private static readonly DateTimeOffset T0 = new(2026, 8, 28, 20, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Txt_Import_Extracts_Metadata_And_Chapter_Headings()
    {
        const string raw = """
            InkFlow Private Book v1
            Title: 导出的书
            Author: 示例作者

            ## Chapter 1: 第一章

            第一段

            第二段
            ## Chapter 2: 第二章
            另一章正文
            """;

        var result = await new PrivateBookImportParser().ParseAsync(
            "ignored.txt",
            "text/plain",
            new MemoryStream(Encoding.UTF8.GetBytes(raw)));

        Assert.AreEqual(PrivateLibraryResultStatus.Success, result.Status);
        Assert.IsNotNull(result.Draft);
        Assert.AreEqual("导出的书", result.Draft!.Title);
        Assert.AreEqual("示例作者", result.Draft.Author);
        Assert.AreEqual(2, result.Draft.Chapters.Count);
        Assert.AreEqual("第一章", result.Draft.Chapters[0].Title);
        CollectionAssert.AreEqual(
            new[] { "第一段", "第二段" },
            result.Draft.Chapters[0].Paragraphs.ToArray());
    }

    [TestMethod]
    public async Task Txt_Import_Uses_File_Name_And_A_Single_Body_Chapter()
    {
        var result = await new PrivateBookImportParser().ParseAsync(
            "我的书.txt",
            "text/plain",
            new MemoryStream(Encoding.UTF8.GetBytes("第一行\n\n第二行")));

        Assert.AreEqual(PrivateLibraryResultStatus.Success, result.Status);
        Assert.AreEqual("我的书", result.Draft!.Title);
        Assert.AreEqual("正文", result.Draft.Chapters.Single().Title);
        Assert.AreEqual(2, result.Draft.Chapters.Single().Paragraphs.Count);
    }

    [TestMethod]
    public async Task Epub_Import_Reads_Metadata_Spine_And_Xhtml_Text()
    {
        var bytes = CreateEpub(
            ("OEBPS/chapter.xhtml", "<html><body><h1>第一章</h1><p>正文一</p><p>正文二</p></body></html>"));

        var result = await new PrivateBookImportParser().ParseAsync(
            "book.epub",
            "application/epub+zip",
            new MemoryStream(bytes));

        Assert.AreEqual(PrivateLibraryResultStatus.Success, result.Status);
        Assert.IsNotNull(result.Draft);
        Assert.AreEqual("EPUB 书", result.Draft!.Title);
        Assert.AreEqual("EPUB 作者", result.Draft.Author);
        Assert.AreEqual(1, result.Draft.Chapters.Count);
        Assert.AreEqual("第一章", result.Draft.Chapters[0].Title);
        CollectionAssert.AreEqual(
            new[] { "第一章", "正文一", "正文二" },
            result.Draft.Chapters[0].Paragraphs.ToArray());
    }

    [TestMethod]
    public async Task Epub_Import_Rejects_Path_Traversal_Entries()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry("../escape.txt").Open());
            writer.Write("not safe");
        }

        stream.Position = 0;
        var result = await new PrivateBookImportParser().ParseAsync(
            "unsafe.epub",
            "application/epub+zip",
            stream);

        Assert.AreEqual(PrivateLibraryResultStatus.InvalidFile, result.Status);
    }

    [TestMethod]
    public async Task Txt_Export_Is_Reimportable_And_Epub_Uses_Valid_Mimetype_First()
    {
        var book = PrivateBook.Create(UserId, "我的/私有书", "作者", T0);
        var chapter = PrivateChapter.Create(
            UserId,
            book.Id,
            0,
            "第一章",
            PrivateContentDocument.FromParagraphs(["正文 <安全>", "第二段"]),
            T0);

        var txt = PrivateBookExportFormatter.Export(
            book,
            [chapter],
            PrivateLibraryExportFormat.Txt);
        var parsed = await new PrivateBookImportParser().ParseAsync(
            txt.FileName,
            txt.ContentType,
            new MemoryStream(txt.Content));

        Assert.AreEqual(PrivateLibraryResultStatus.Success, parsed.Status);
        Assert.AreEqual(book.Title, parsed.Draft!.Title);
        Assert.AreEqual("正文 <安全>", parsed.Draft.Chapters.Single().Paragraphs[0]);

        var epub = PrivateBookExportFormatter.Export(
            book,
            [chapter],
            PrivateLibraryExportFormat.Epub);
        using var archive = new ZipArchive(
            new MemoryStream(epub.Content), ZipArchiveMode.Read);
        Assert.AreEqual("mimetype", archive.Entries[0].FullName);
        using var reader = new StreamReader(archive.Entries[0].Open());
        Assert.AreEqual("application/epub+zip", reader.ReadToEnd());
        Assert.IsTrue(archive.GetEntry("OEBPS/chapter-00001.xhtml") is not null);
        Assert.IsFalse(epub.FileName.Contains("/", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Content_Service_Imports_And_Reads_Only_Within_Owner_Scope()
    {
        var repository = new InMemoryPrivateBookRepository();
        var service = new PrivateLibraryContentService(
            repository,
            new PrivateBookImportParser(),
            new FixedClock(T0));

        var result = await service.ImportAsync(
            UserId,
            "私有书.txt",
            "text/plain",
            new MemoryStream(Encoding.UTF8.GetBytes("第一段\n\n第二段")));

        Assert.AreEqual(PrivateLibraryResultStatus.Success, result.Status);
        Assert.AreEqual(1, result.Value!.ChapterCount);
        var bookId = result.Value.Book.PrivateBookId;
        var chapters = await service.ListChaptersAsync(UserId, bookId);
        Assert.AreEqual(1, chapters.Count);

        var chapter = await service.GetChapterAsync(
            UserId,
            bookId,
            chapters[0].PrivateChapterId);
        Assert.IsNotNull(chapter);
        CollectionAssert.AreEqual(
            new[] { "第一段", "第二段" },
            chapter!.Paragraphs.ToArray());
        Assert.IsNull(await service.GetChapterAsync(
            Guid.CreateVersion7(),
            bookId,
            chapters[0].PrivateChapterId));

        var export = await service.ExportAsync(UserId, bookId, "txt");
        Assert.AreEqual(PrivateLibraryResultStatus.Success, export.Status);
        StringAssert.Contains(
            Encoding.UTF8.GetString(export.Value!.Content),
            "第一段");
        Assert.AreEqual(
            PrivateLibraryResultStatus.InvalidRequest,
            (await service.ExportAsync(UserId, bookId, "pdf")).Status);
    }

    private static byte[] CreateEpub(params (string Path, string Content)[] chapterEntries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "mimetype", "application/epub+zip");
            WriteEntry(
                archive,
                "META-INF/container.xml",
                "<?xml version=\"1.0\"?><container xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\"><rootfiles><rootfile full-path=\"OEBPS/content.opf\"/></rootfiles></container>");
            WriteEntry(
                archive,
                "OEBPS/content.opf",
                "<?xml version=\"1.0\"?><package xmlns=\"http://www.idpf.org/2007/opf\"><metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:title>EPUB 书</dc:title><dc:creator>EPUB 作者</dc:creator></metadata><manifest><item id=\"chapter\" href=\"chapter.xhtml\" media-type=\"application/xhtml+xml\"/></manifest><spine><itemref idref=\"chapter\"/></spine></package>");
            foreach (var entry in chapterEntries)
            {
                WriteEntry(archive, entry.Path, entry.Content);
            }
        }

        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        using var writer = new StreamWriter(
            archive.CreateEntry(path).Open(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class InMemoryPrivateBookRepository : IPrivateBookRepository
    {
        private readonly Dictionary<(Guid UserId, Guid BookId), PrivateBook> books = [];
        private readonly Dictionary<(Guid UserId, Guid BookId, Guid ChapterId), PrivateChapter> chapters = [];

        public Task AddAsync(PrivateBook book, CancellationToken cancellationToken = default)
        {
            books[(book.UserId, book.Id)] = book;
            return Task.CompletedTask;
        }

        public Task AddWithChaptersAsync(
            PrivateBook book,
            IReadOnlyCollection<PrivateChapter> bookChapters,
            CancellationToken cancellationToken = default)
        {
            books[(book.UserId, book.Id)] = book;
            foreach (var chapter in bookChapters)
            {
                chapters[(chapter.UserId, chapter.PrivateBookId, chapter.Id)] = chapter;
            }

            return Task.CompletedTask;
        }

        public Task<PrivateBook?> GetAsync(
            Guid userId,
            Guid privateBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(books.TryGetValue((userId, privateBookId), out var book) ? book : null);

        public Task<IReadOnlyList<PrivateBook>> ListAsync(
            Guid userId,
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PrivateBook>>(books
                .Where(pair => pair.Key.UserId == userId)
                .Select(pair => pair.Value)
                .Take(limit)
                .ToList());

        public Task<bool> SaveAsync(
            PrivateBook book,
            CancellationToken cancellationToken = default)
        {
            if (!books.ContainsKey((book.UserId, book.Id)))
            {
                return Task.FromResult(false);
            }

            books[(book.UserId, book.Id)] = book;
            return Task.FromResult(true);
        }

        public Task<bool> DeleteAsync(
            Guid userId,
            Guid privateBookId,
            CancellationToken cancellationToken = default)
        {
            var removed = books.Remove((userId, privateBookId));
            foreach (var key in chapters.Keys
                         .Where(key => key.UserId == userId && key.BookId == privateBookId)
                         .ToList())
            {
                chapters.Remove(key);
            }

            return Task.FromResult(removed);
        }

        public Task<IReadOnlyList<PrivateChapter>> ListChaptersAsync(
            Guid userId,
            Guid privateBookId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PrivateChapter>>(chapters
                .Where(pair => pair.Key.UserId == userId && pair.Key.BookId == privateBookId)
                .Select(pair => pair.Value)
                .OrderBy(chapter => chapter.Index)
                .ToList());

        public Task<PrivateChapter?> GetChapterAsync(
            Guid userId,
            Guid privateBookId,
            Guid privateChapterId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(chapters.TryGetValue(
                (userId, privateBookId, privateChapterId),
                out var chapter)
                ? chapter
                : null);
    }
}
