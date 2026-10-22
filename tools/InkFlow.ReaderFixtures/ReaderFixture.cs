using InkFlow.Api.Presentation;
using InkFlow.Modules.Content.Application;

namespace InkFlow.ReaderFixtures;

public sealed record ReaderFixturePage(string Path, string Body, string ContentType = "text/html; charset=utf-8", int Status = 200);

public static class ReaderFixture
{
    public static Guid Id(int value) => new(value, 0, 0, new byte[8]);

    public static IReadOnlyDictionary<string, ReaderFixturePage> Create()
    {
        var pages = new Dictionary<string, ReaderFixturePage>();
        var bookId = Id(100000);
        var title = "基线书目 <标签> & 安全转义";
        var books = new[] { new BookListItem(bookId, title, "基线作者", 100) };
        pages.Add("catalog", new("/reader", ReaderHtml.BookListPage(books, null)));
        pages.Add("empty", new("/reader/empty", ReaderHtml.BookListPage([], null)));
        pages.Add("search-empty", new("/reader/search-empty", ReaderHtml.BookListPage([], "不存在", true)));
        pages.Add("search-degraded", new("/reader/search-degraded", ReaderHtml.BookListPage(books, "基线", true, true)));
        pages.Add("search-xss", new("/reader/search-xss", ReaderHtml.BookListPage([], "\"><script>window.xss=true</script>", true)));
        foreach (var count in new[] { 0, 100, 1000, 10000 })
        {
            var id = count == 100 ? bookId : Id(100000 + count + 1);
            var chapters = Enumerable.Range(1, count).Select(i => new ChapterListItem(Id(i), i - 1, $"第 {i} 章 <安全> & 目录")).ToArray();
            pages.Add("toc-" + count, new($"/reader/books/{id:D}", ReaderHtml.BookDetailPage(new(id, title, "基线作者", chapters))));
        }
        pages.Add("short", new($"/reader/read/{Id(1):D}", ReaderHtml.ChapterPage(
            new(Id(1), bookId, 0, "第一章 <安全>", "fixture", ["春风吹过山谷。", "<script>window.xss=true</script>", "第二段 & 尾声。"]),
            null, (Id(2), "第二章"), bookId, title)));
        pages.Add("long", new($"/reader/read/{Id(2):D}", ReaderHtml.ChapterPage(
            new(Id(2), bookId, 1, "第二章 长文", "fixture", Enumerable.Range(1, 500).Select(i => $"第 {i} 段：" + string.Concat(Enumerable.Repeat("晨光照在山间，读者继续沿着故事前行。", 12))).ToArray()),
            (Id(1), "第一章"), null, bookId, title)));
        pages.Add("missing", new($"/reader/read/{Id(99999):D}", ReaderHtml.MissingChapterPage(), Status: 404));
        pages.Add("account", new("/reader/account", ReaderHtml.AccountPage()));
        pages.Add("register", new("/reader/account/register", ReaderHtml.AccountPage(true)));
        pages.Add("shelf", new("/reader/shelf", ReaderHtml.ShelfPage()));
        pages.Add("history", new("/reader/history", ReaderHtml.HistoryPage()));
        pages.Add("offline", new("/reader/offline", ReaderHtml.OfflinePage()));
        pages.Add("operations", new("/admin/operations", ReaderHtml.OperationsPage()));
        pages.Add("manifest", new("/reader/manifest.webmanifest", ReaderHtml.PwaManifest(), "application/manifest+json; charset=utf-8"));
        pages.Add("service-worker", new("/reader/sw.js", ReaderHtml.ServiceWorker(), "application/javascript; charset=utf-8"));
        pages.Add("icon", new("/reader/icon-192.svg", ReaderHtml.PwaIcon(), "image/svg+xml"));
        pages.Add("icon-large", new("/reader/icon-512.svg", ReaderHtml.PwaIcon(), "image/svg+xml"));
        return pages;
    }
}
