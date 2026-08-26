using System.Net;
using System.Text;
using InkFlow.Modules.Library.Application;

namespace InkFlow.Modules.Content.Application;

/// <summary>
/// Minimal Web Reader 的服务端 HTML 渲染(纯函数,便于离线测试)。
/// 页面流:搜索/书目 → 书详情(含"开始阅读") → 目录 → 正文 + 上一章/下一章。
/// 移动优先:viewport、语义化标签、正文宽度受限、触控目标足够大。
/// </summary>
public static class ReaderHtml
{
    private const string Head =
        """
        <!DOCTYPE html>
        <html lang="zh-CN">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <style>
            body { font-family: system-ui, sans-serif; line-height: 1.8; margin: 0; color: #222; background: #fdfdfb; }
            main { max-width: 42em; margin: 0 auto; padding: 1rem; }
            nav a, .actions a { display: inline-block; padding: 0.6em 1em; margin-right: 0.5em; border: 1px solid #ccc; border-radius: 6px; text-decoration: none; color: inherit; }
            h1 { font-size: 1.4rem; } h2 { font-size: 1.2rem; }
            ol.toc { list-style: none; padding-left: 0; }
            ol.toc li { padding: 0.35em 0; border-bottom: 1px solid #eee; }
            article p { margin: 0 0 1.2em; text-indent: 2em; }
            form.search { display: flex; gap: 0.5em; margin-bottom: 1rem; }
            input[type=search] { flex: 1; padding: 0.6em; font-size: 1rem; }
            button { padding: 0.6em 1em; font-size: 1rem; }
            .primary { display: inline-block; padding: 0.7em 1.4em; background: #1a5fb4; color: #fff; border-radius: 6px; text-decoration: none; }
            .muted { color: #777; }
            footer { max-width: 42em; margin: 1rem auto; padding: 0 1rem 2rem; display: flex; justify-content: space-between; }
          </style>
        </head>
        <body>
        """;

    private const string Tail = "</body></html>";

    public static string BookListPage(IReadOnlyList<BookListItem> books, string? query)
    {
        var sb = new StringBuilder(Head);
        sb.Append("<main><h1>墨流 · InkFlow</h1>");
        var encodedQuery = WebUtility.HtmlEncode(query ?? string.Empty);
        sb.Append(
            $"""
            <form class="search" method="get" action="/reader">
              <input type="search" name="q" value="{encodedQuery}" placeholder="搜索书名或作者" aria-label="搜索书名或作者">
              <button type="submit">搜索</button>
            </form>
            """);

        if (books.Count == 0)
        {
            sb.Append("<p role=\"status\">没有找到匹配的书目。</p>");
        }
        else
        {
            sb.Append("<ul>");
            foreach (var book in books)
            {
                sb.Append($"<li><a href=\"/reader/books/{book.Id}\">{WebUtility.HtmlEncode(book.Title)}</a> "
                          + $"<span class=\"muted\">{WebUtility.HtmlEncode(book.Author)}</span></li>");
            }

            sb.Append("</ul>");
        }

        sb.Append("</main>").Append(Tail);
        return sb.ToString();
    }

    public static string BookDetailPage(BookDetail book)
    {
        var sb = new StringBuilder(Head);
        sb.Append($"<main><nav><a href=\"/reader\">← 全部书目</a></nav>");
        sb.Append($"<h1>{WebUtility.HtmlEncode(book.Title)}</h1>");
        sb.Append($"<p class=\"muted\">{WebUtility.HtmlEncode(book.Author)}</p>");

        if (book.Chapters.Count > 0)
        {
            // 主操作:开始阅读 = 第一章。
            sb.Append($"<p class=\"actions\"><a class=\"primary\" href=\"/reader/read/{book.Chapters[0].ChapterId}\">开始阅读</a></p>");
        }

        sb.Append("<h2 id=\"toc\">目录</h2><ol class=\"toc\">");
        foreach (var chapter in book.Chapters)
        {
            sb.Append($"<li><a href=\"/reader/read/{chapter.ChapterId}\">{chapter.Index + 1}. "
                      + $"{WebUtility.HtmlEncode(chapter.Title)}</a></li>");
        }

        if (book.Chapters.Count == 0)
        {
            sb.Append("<li class=\"muted\" role=\"status\">目录尚未同步。</li>");
        }

        sb.Append("</ol></main>").Append(Tail);
        return sb.ToString();
    }

    public static string ChapterPage(
        ChapterContent content,
        (Guid ChapterId, string Title)? previous,
        (Guid ChapterId, string Title)? next,
        Guid bookId,
        string bookTitle)
    {
        var sb = new StringBuilder(Head);
        var title = string.IsNullOrEmpty(content.Title) ? "正文" : content.Title;

        sb.Append("<main><nav>")
          .Append($"<a href=\"/reader/books/{bookId}#toc\">← 目录</a>")
          .Append("</nav>");
        sb.Append($"<h1>{WebUtility.HtmlEncode(title)}</h1>");
        sb.Append($"<p class=\"muted\">《{WebUtility.HtmlEncode(bookTitle)}》</p>");

        sb.Append("<article>");
        foreach (var paragraph in content.Paragraphs)
        {
            sb.Append($"<p>{WebUtility.HtmlEncode(paragraph)}</p>");
        }

        sb.Append("</article>");

        sb.Append("<footer>");
        if (previous is { } prev)
        {
            sb.Append($"<a href=\"/reader/read/{prev.ChapterId}\">← 上一章</a>");
        }

        if (next is { } nxt)
        {
            sb.Append($"<a href=\"/reader/read/{nxt.ChapterId}\" style=\"margin-left:auto\">下一章 →</a>");
        }

        sb.Append("</footer></main>").Append(Tail);
        return sb.ToString();
    }
}
