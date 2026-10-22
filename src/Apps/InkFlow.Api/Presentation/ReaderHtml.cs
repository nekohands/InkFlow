using static InkFlow.Api.Presentation.ReaderAssets;
using InkFlow.Modules.Content.Application;
using System.Net;
using System.Text;
using InkFlow.Modules.Library.Application;

namespace InkFlow.Api.Presentation;

/// <summary>
/// Web Reader v1 的服务端 HTML 渲染(纯函数,便于离线测试)。
/// 页面流:搜索/书目 → 书详情(含"开始阅读") → 目录 → 正文 + 上一章/下一章。
/// 阅读设置保存在当前设备并同步账户偏好;正文只读取已落库的 Canonical Content。
/// </summary>
public static partial class ReaderHtml
{
    /// <summary>
    /// 书目列表页(含搜索)。searched=false 表示浏览全部书库;true 表示按 query 过滤,
    /// 两种空态文案不同。sourceDegraded 是来源发现部分失败提示——只渲染人话,
    /// 不暴露 SourceId/内部异常等技术细节。
    /// </summary>
    public static string BookListPage(
        IReadOnlyList<BookListItem> books,
        string? query,
        bool searched = false,
        bool sourceDegraded = false)
    {
        var sb = new StringBuilder(Head);
        sb.Append(ReaderHeader);
        sb.Append(
            """
            <main id="main-content" class="page-shell">
              <section class="page-intro" aria-labelledby="reader-page-title">
                <p class="eyebrow">你的下一本书</p>
                <h1 id="reader-page-title">发现并开始阅读</h1>
                <p class="muted">从已收录的正典书目开始，正文会保持来源独立、连续可读。</p>
              </section>
            """);

        var encodedQuery = WebUtility.HtmlEncode(query ?? string.Empty);
        sb.Append(
            $"""
            <form class="search-bar" method="get" action="/reader" role="search">
              <label class="sr-only" for="book-search">搜索书名或作者</label>
              <input id="book-search" type="search" name="q" value="{encodedQuery}" placeholder="搜索书名或作者" autocomplete="off">
              <button class="button button--primary" type="submit">搜索</button>
            </form>
            """);

        if (books.Count == 0)
        {
            sb.Append(searched
                ? "<p class=\"notice\" role=\"status\">没有找到匹配「" + encodedQuery + "」的书目。换个关键词试试,或稍后再来——线上来源正在收录中。</p>"
                : "<p class=\"notice\" role=\"status\">书库还是空的。在上方搜索一本书,会自动从已登记的线上来源查找并收录。</p>");
        }
        else
        {
            if (searched)
            {
                sb.Append($"<p class=\"result-count\" role=\"status\">找到 {books.Count} 本与「{encodedQuery}」相关的书。</p>");
            }

            sb.Append("<ul class=\"book-grid\" aria-label=\"书目列表\">");
            foreach (var book in books)
            {
                var title = WebUtility.HtmlEncode(book.Title);
                var author = WebUtility.HtmlEncode(book.Author);
                sb.Append(
                    $"<li class=\"book-card\"><a class=\"book-card__link\" href=\"/reader/books/{book.Id}\">"
                    + $"<span class=\"book-card__title\">{title}</span>"
                    + $"<span class=\"book-card__author\">{author}</span>"
                    + $"<span class=\"book-card__meta\">{book.ChapterCount} 章 · 查看目录</span></a></li>");
            }

            sb.Append("</ul>");
        }

        // 部分来源本次不可用:结果可能不全,但页面仍可用。
        if (sourceDegraded)
        {
            sb.Append("<p class=\"notice\" role=\"status\">部分线上来源暂时无法访问,以上结果可能不完整。</p>");
        }

        sb.Append("</main>").Append(Tail);
        return sb.ToString();
    }

    public static string BookDetailPage(BookDetail book)
    {
        var sb = new StringBuilder(Head);
        var title = WebUtility.HtmlEncode(book.Title);
        var author = WebUtility.HtmlEncode(book.Author);

        sb.Append(ReaderHeader);
        sb.Append("<main id=\"main-content\" class=\"page-shell\">");
        sb.Append(
            $"""
            <nav class="breadcrumbs" aria-label="面包屑"><a href="/reader">书库</a><span aria-hidden="true">/</span><span aria-current="page">{title}</span></nav>
            <section class="book-hero" aria-labelledby="book-title">
              <p class="eyebrow">InkFlow 书目</p>
              <h1 id="book-title">{title}</h1>
              <p class="book-author">{author}</p>
              <div class="book-actions">
            """);

        if (book.Chapters.Count > 0)
        {
            // 主操作:开始阅读 = 第一章。
            sb.Append($"<a class=\"button button--primary\" href=\"/reader/read/{book.Chapters[0].ChapterId}\">开始阅读</a>");
        }

        sb.Append($"<button id=\"reader-shelf-toggle\" class=\"button\" type=\"button\" data-book-id=\"{book.Id:D}\" hidden>加入书架</button>");
        sb.Append(
            $"""
              <div id="reader-package-actions" class="book-package-actions" data-book-id="{book.Id:D}" hidden>
                <label class="book-package-format" for="reader-package-format">下载格式
                  <select id="reader-package-format">
                    <option value="epub" selected>EPUB</option>
                    <option value="txt">单文件 TXT</option>
                    <option value="zip">ZIP</option>
                  </select>
                </label>
                <button id="reader-package-download" class="button" type="button">下载</button>
              </div>
              <p id="reader-package-status" class="book-package-status muted" role="status" aria-live="polite">登录后可创建下载任务。</p>
            """);

        sb.Append(
            """
              </div>
              <p id="reader-shelf-status" class="muted" role="status" aria-live="polite">登录后可同步书架。<a href="/reader/account">登录</a></p>
            </section>
            <section class="content-panel" aria-labelledby="toc-title">
              <h2 id="toc-title">目录</h2>
            """);
        sb.Append("<ol class=\"toc\">");
        foreach (var chapter in book.Chapters)
        {
            var chapterTitle = WebUtility.HtmlEncode(chapter.Title);
            sb.Append(
                $"<li><a href=\"/reader/read/{chapter.ChapterId}\">第 {chapter.Index + 1} 章 · {chapterTitle}</a></li>");
        }

        if (book.Chapters.Count == 0)
        {
            sb.Append("<li class=\"muted\" role=\"status\">目录尚未同步。</li>");
        }

        sb.Append("</ol></section></main>").Append(ReaderDetailScript).Append(Tail);
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
        var encodedTitle = WebUtility.HtmlEncode(title);
        var encodedBookTitle = WebUtility.HtmlEncode(bookTitle);

        sb.Append("<div class=\"reader-page\">");
        sb.Append(ReaderHeader);
        sb.Append(
            """
            <div id="reading-progress" class="reader-progress" role="progressbar" aria-label="阅读进度" aria-valuemin="0" aria-valuemax="100" aria-valuenow="0"></div>
            <main id="main-content" class="reader-shell">
            """);
        sb.Append(
            $"""
            <nav class="reader-toolbar" aria-label="阅读工具栏">
              <a class="toolbar-button" href="/reader/books/{bookId}#toc" aria-label="打开目录"><span aria-hidden="true">☰</span><span>目录</span></a>
              <button class="toolbar-button" type="button" data-open-reader-settings aria-label="打开阅读设置" aria-haspopup="dialog"><span aria-hidden="true">Aa</span><span>阅读设置</span></button>
              <span class="reader-chapter" aria-label="当前章节">第 {content.Index + 1} 章</span>
              <span id="reader-sync-status" class="reader-sync-status" role="status" aria-live="polite"></span>
            </nav>
            <article id="reader-content" class="reader-content" tabindex="-1">
              <p class="eyebrow">第 {content.Index + 1} 章</p>
              <h1 class="reader-content__title">{encodedTitle}</h1>
              <p class="reader-content__book">《{encodedBookTitle}》</p>
              <div class="reader-content__body">
            """);

        if (content.Paragraphs.Count == 0)
        {
            sb.Append("<p class=\"notice\" role=\"status\">该章节暂时没有可显示的正文。</p>");
        }
        else
        {
            foreach (var paragraph in content.Paragraphs)
            {
                sb.Append($"<p>{WebUtility.HtmlEncode(paragraph)}</p>");
            }
        }

        sb.Append(
            """
              </div>
              <p class="reader-end">本章结束</p>
            </article>
            <nav class="chapter-nav" aria-label="章节导航">
            """);

        if (previous is { } prev)
        {
            var previousTitle = WebUtility.HtmlEncode(prev.Title);
            sb.Append($"<a class=\"button\" href=\"/reader/read/{prev.ChapterId}\" rel=\"prev\">← 上一章<span class=\"sr-only\"> {previousTitle}</span></a>");
        }
        else
        {
            sb.Append("<span></span>");
        }

        if (next is { } nxt)
        {
            var nextTitle = WebUtility.HtmlEncode(nxt.Title);
            sb.Append($"<a class=\"button button--primary\" href=\"/reader/read/{nxt.ChapterId}\" rel=\"next\">下一章 →<span class=\"sr-only\"> {nextTitle}</span></a>");
        }

        sb.Append(
            """
            </nav>
            </main>
            <dialog id="reader-settings" aria-labelledby="reader-settings-title">
              <div class="settings-dialog__inner">
                <div class="settings-dialog__header">
                  <h2 id="reader-settings-title">阅读设置</h2>
                  <button id="reader-settings-close" class="icon-button" type="button" aria-label="关闭阅读设置">×</button>
                </div>
                <form id="reader-settings-form">
                  <div class="setting">
                    <label for="reader-theme">主题</label>
                    <select id="reader-theme" name="theme">
                      <option value="system">跟随系统</option>
                      <option value="light">明亮</option>
                      <option value="sepia">暖色</option>
                      <option value="dark">深色</option>
                    </select>
                  </div>
                  <div class="setting">
                    <label for="reader-font-size">字号</label>
                    <div class="setting__range">
                      <input id="reader-font-size" name="font-size" type="range" min="90" max="140" step="5" value="100">
                      <output id="reader-font-size-output" for="reader-font-size">100%</output>
                    </div>
                  </div>
                  <div class="setting">
                    <label for="reader-line-height">行高</label>
                    <div class="setting__range">
                      <input id="reader-line-height" name="line-height" type="range" min="150" max="230" step="5" value="180">
                      <output id="reader-line-height-output" for="reader-line-height">1.80×</output>
                    </div>
                  </div>
                  <p id="reader-settings-status" class="sr-only" role="status" aria-live="polite"></p>
                </form>
              </div>
            </dialog>
            <noscript><p class="notice" role="status">开启 JavaScript 后可使用阅读主题、字号和行高设置；正文与章节导航无需脚本即可使用。</p></noscript>
            """);
        sb.Append(ReaderScript)
            .Append(ReaderProgressScript(bookId, content.ChapterId))
            .Append("</div>")
            .Append(Tail);
        return sb.ToString();
    }
}
