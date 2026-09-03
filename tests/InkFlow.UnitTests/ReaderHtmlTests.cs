using System.Text.Json;
using InkFlow.Modules.Content.Application;
using InkFlow.Modules.Library.Application;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace InkFlow.UnitTests;

[TestClass]
public sealed class ReaderHtmlTests
{
    private static readonly BookListItem ListItem = new(Guid.NewGuid(), "剑来", "烽火戏诸侯", 2);

    private static readonly BookDetail Detail = new(
        Guid.NewGuid(), "剑来", "烽火戏诸侯",
        [
            new ChapterListItem(Guid.NewGuid(), 0, "第一章"),
            new ChapterListItem(Guid.NewGuid(), 1, "第二章"),
        ]);

    [TestMethod]
    public void BookList_Renders_Search_Form_And_Entries()
    {
        var html = ReaderHtml.BookListPage([ListItem], query: null);

        StringAssert.Contains(html, "viewport");
        StringAssert.Contains(html, "type=\"search\"");
        StringAssert.Contains(html, "class=\"book-grid\"");
        StringAssert.Contains(html, "/reader/books/");
        StringAssert.Contains(html, "剑来");
        StringAssert.Contains(html, "烽火戏诸侯");
    }

    [TestMethod]
    public void BookList_Browse_Empty_State_Teaches_Discovery()
    {
        var html = ReaderHtml.BookListPage([], query: null);
        StringAssert.Contains(html, "书库还是空的");
        StringAssert.Contains(html, "自动从已登记的线上来源查找");
    }

    [TestMethod]
    public void BookList_Searched_Empty_State_Suggests_Retry()
    {
        var html = ReaderHtml.BookListPage([], query: "不存在", searched: true);
        StringAssert.Contains(html, "没有找到匹配「不存在」的书目");
    }

    [TestMethod]
    public void BookList_Search_Hit_Shows_Result_Count()
    {
        var html = ReaderHtml.BookListPage([ListItem], query: "剑来", searched: true);
        StringAssert.Contains(html, "找到 1 本与「剑来」相关的书");
    }

    [TestMethod]
    public void BookList_Degraded_Notice_Is_User_Friendly_And_Leak_Free()
    {
        // SourceId 与内部异常细节不得出现在页面上。
        var html = ReaderHtml.BookListPage([ListItem], query: "剑来", searched: true, sourceDegraded: true);

        StringAssert.Contains(html, "部分线上来源暂时无法访问");
        Assert.IsFalse(html.Contains("src-a"), "SourceId 不得泄漏到页面");
        Assert.IsFalse(html.Contains("exception"), "内部异常词不得泄漏到页面");
    }

    [TestMethod]
    public void BookList_Search_Query_Value_Is_Escaped()
    {
        var html = ReaderHtml.BookListPage([], query: "\"><script>alert(1)</script>", searched: true);

        Assert.IsFalse(html.Contains("\"><script>alert(1)"), "搜索词回显必须转义");
        StringAssert.Contains(html, "&lt;script&gt;");
    }

    [TestMethod]
    public void Book_Detail_Has_Start_Reading_Primary_Action_And_Toc()
    {
        var html = ReaderHtml.BookDetailPage(Detail);

        StringAssert.Contains(html, "开始阅读");
        Assert.IsTrue(html.Contains("/reader/read/"), "详情页应有指向第一章的阅读链接");
        StringAssert.Contains(html, "目录");
        StringAssert.Contains(html, "第一章");
    }

    [TestMethod]
    public void Book_Detail_Wraps_Long_Metadata_And_Escapes_Special_Characters()
    {
        var title = "InkFlow Edge <Metadata> " + new string('T', 480);
        var author = "InkFlow Edge & Author " + new string('A', 230);
        var detail = new BookDetail(
            Guid.NewGuid(),
            title,
            author,
            [new ChapterListItem(Guid.NewGuid(), 0, "Edge chapter")]);

        var html = ReaderHtml.BookDetailPage(detail);

        StringAssert.Contains(html, "InkFlow Edge &lt;Metadata&gt;");
        StringAssert.Contains(html, "InkFlow Edge &amp; Author");
        StringAssert.Contains(html, "overflow-wrap: anywhere;");
        Assert.IsFalse(html.Contains("InkFlow Edge <Metadata>"));
    }

    [TestMethod]
    public void Book_Detail_Offers_Authenticated_Shelf_Action_Without_Exposing_Tokens()
    {
        var html = ReaderHtml.BookDetailPage(Detail);

        StringAssert.Contains(html, "reader-shelf-toggle");
        StringAssert.Contains(html, "data-book-id=");
        StringAssert.Contains(html, "/reader/account");
        StringAssert.Contains(html, "sessionStorage");
        Assert.IsFalse(html.Contains("X-InkFlow-Legado-Token"));
    }

    [TestMethod]
    public void Chapter_Page_Escapes_Html_In_Content_And_Titles()
    {
        var content = new ChapterContent(
            Guid.NewGuid(), Guid.NewGuid(), 0, "<script>alert(1)</script>",
            "example-source", ["正文<script>bad()</script>段落"]);
        var chapter = (Guid.NewGuid(), "<b>坏标题</b>");

        var html = ReaderHtml.ChapterPage(content, null, next: chapter, Guid.NewGuid(), "书");

        Assert.IsFalse(html.Contains("<script>alert(1)</script>"), "正文中的脚本必须被转义");
        StringAssert.Contains(html, "&lt;script&gt;");
    }

    [TestMethod]
    public void Chapter_Page_Shows_Previous_And_Next_Links()
    {
        var prevId = Guid.NewGuid();
        var nextId = Guid.NewGuid();
        var content = new ChapterContent(Guid.NewGuid(), Guid.NewGuid(), 1, "第二章", "src", ["正文"]);

        var html = ReaderHtml.ChapterPage(
            content,
            previous: (prevId, "第一章"),
            next: (nextId, "第三章"),
            Guid.NewGuid(), "书");

        StringAssert.Contains(html, $"reader/read/{prevId}");
        StringAssert.Contains(html, $"reader/read/{nextId}");
        StringAssert.Contains(html, "上一章");
        StringAssert.Contains(html, "下一章");
    }

    [TestMethod]
    public void Chapter_Page_Provides_Responsive_Reader_Settings()
    {
        var content = new ChapterContent(Guid.NewGuid(), Guid.NewGuid(), 0, "第一章", "private-source", ["正文"]);

        var html = ReaderHtml.ChapterPage(content, previous: null, next: null, Guid.NewGuid(), "书");

        StringAssert.Contains(html, "reader-toolbar");
        StringAssert.Contains(html, "data-open-reader-settings");
        StringAssert.Contains(html, "id=\"reader-settings\"");
        StringAssert.Contains(html, "id=\"reader-theme\"");
        StringAssert.Contains(html, "id=\"reader-font-size\"");
        StringAssert.Contains(html, "id=\"reader-line-height\"");
        StringAssert.Contains(html, "localStorage");
        StringAssert.Contains(html, "prefers-reduced-motion");
        Assert.IsFalse(html.Contains("private-source"), "阅读页面不得暴露内部 SourceId");
    }

    [TestMethod]
    public void Chapter_Page_Connects_Progress_And_Preference_Sync_Progressively()
    {
        var bookId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var content = new ChapterContent(chapterId, bookId, 0, "第一章", "src", ["正文"]);

        var html = ReaderHtml.ChapterPage(content, previous: null, next: null, bookId, "书");

        StringAssert.Contains(html, "/api/v1/me/reading/progress/");
        StringAssert.Contains(html, "/api/v1/me/reading/preferences");
        StringAssert.Contains(html, "reader-sync-status");
        StringAssert.Contains(html, "sessionStorage");
        StringAssert.Contains(html, chapterId.ToString("D"));
        Assert.IsFalse(html.Contains("X-InkFlow-Legado-Token"));
    }

    [TestMethod]
    public void Chapter_Page_Renders_Empty_Content_State()
    {
        var content = new ChapterContent(Guid.NewGuid(), Guid.NewGuid(), 0, "第一章", "src", []);

        var html = ReaderHtml.ChapterPage(content, previous: null, next: null, Guid.NewGuid(), "书");

        StringAssert.Contains(html, "该章节暂时没有可显示的正文");
        StringAssert.Contains(html, "role=\"status\"");
    }

    [TestMethod]
    public void First_Chapter_Has_No_Previous_Link()
    {
        var content = new ChapterContent(Guid.NewGuid(), Guid.NewGuid(), 0, "第一章", "src", ["正文"]);
        var html = ReaderHtml.ChapterPage(content, previous: null, next: (Guid.NewGuid(), "第二章"), Guid.NewGuid(), "书");

        Assert.IsFalse(html.Contains("上一章"));
        StringAssert.Contains(html, "下一章");
    }

    [TestMethod]
    public void Pwa_Manifest_Has_Same_Origin_Reader_Install_Contract()
    {
        using var document = JsonDocument.Parse(ReaderHtml.PwaManifest());
        var root = document.RootElement;

        Assert.AreEqual("/reader", root.GetProperty("start_url").GetString());
        Assert.AreEqual("/reader/", root.GetProperty("scope").GetString());
        Assert.AreEqual("standalone", root.GetProperty("display").GetString());
        var icons = root.GetProperty("icons");
        Assert.IsTrue(icons.EnumerateArray().Any(icon => icon.GetProperty("sizes").GetString() == "192x192"));
        Assert.IsTrue(icons.EnumerateArray().Any(icon => icon.GetProperty("sizes").GetString() == "512x512"));
        StringAssert.Contains(ReaderHtml.PwaIcon(), "<svg");
    }

    [TestMethod]
    public void Reader_Shell_Exposes_Progressive_Pwa_Install_Enhancement()
    {
        var html = ReaderHtml.BookListPage([], query: null);

        StringAssert.Contains(html, "id=\"reader-install\"");
        StringAssert.Contains(html, "beforeinstallprompt");
        StringAssert.Contains(html, "event.preventDefault()");
        StringAssert.Contains(html, "installButton.hidden = false");
        StringAssert.Contains(html, "await deferredPrompt.prompt()");
        StringAssert.Contains(html, "appinstalled");
        StringAssert.Contains(html, "installButton.hidden = true");
    }

    [TestMethod]
    public void Pwa_Service_Worker_Caches_Only_Public_Shell_And_Provides_Offline_Fallback()
    {
        var script = ReaderHtml.ServiceWorker();

        StringAssert.Contains(script, "inkflow-reader-shell-v1");
        StringAssert.Contains(script, "/reader/offline");
        StringAssert.Contains(script, "request.mode === \"navigate\"");
        Assert.IsFalse(script.Contains("/api/v1/me/reading"), "service worker 不得缓存私人 Reading API");
        Assert.IsFalse(script.Contains("auth/refresh"), "service worker 不得缓存认证响应");
    }

    [TestMethod]
    public void Account_Shelf_And_History_Pages_Expose_Progressive_Reader_Shell()
    {
        var account = ReaderHtml.AccountPage();
        var register = ReaderHtml.AccountPage(registration: true);
        var shelf = ReaderHtml.ShelfPage();
        var history = ReaderHtml.HistoryPage();
        var offline = ReaderHtml.OfflinePage();

        StringAssert.Contains(account, "reader-login-form");
        Assert.IsFalse(account.Contains("id=\"reader-register-form\""), "登录页不得平铺注册表单");
        StringAssert.Contains(account, "/reader/account/register");
        StringAssert.Contains(account, "reader-account-switch");
        StringAssert.Contains(account, "getSafeReturnTo");
        StringAssert.Contains(account, "switchLink.href");
        StringAssert.Contains(account, "当前会话已验证");
        StringAssert.Contains(account, "reader-session-profile");
        StringAssert.Contains(account, "reader-session-avatar");
        StringAssert.Contains(account, "reader-session-avatar-image");
        StringAssert.Contains(account, "reader-session-email");
        StringAssert.Contains(account, "reader-session-role");
        StringAssert.Contains(account, "account-panel");
        StringAssert.Contains(account, "account-tabs");
        StringAssert.Contains(account, "role=\"tablist\"");
        StringAssert.Contains(account, "data-account-tab=\"profile\"");
        StringAssert.Contains(account, "account-panel-profile");
        StringAssert.Contains(account, "account-panel-security");
        StringAssert.Contains(account, "account-panel-reader");
        StringAssert.Contains(account, "payload?.role");
        StringAssert.Contains(account, "reader-admin-panel");
        StringAssert.Contains(account, "hasOperationsAccess(accountRole)");
        StringAssert.Contains(account, "进入运营中心");
        StringAssert.Contains(account, "reader-profile-form");
        StringAssert.Contains(account, "/api/v1/me/profile");
        StringAssert.Contains(account, "reader-avatar-form");
        StringAssert.Contains(account, "reader-avatar-file");
        StringAssert.Contains(account, "accept=\"image/jpeg,image/png,image/webp\"");
        StringAssert.Contains(account, "/api/v1/me/profile/avatar");
        StringAssert.Contains(account, "2 MiB");
        StringAssert.Contains(account, "reader-password-form");
        StringAssert.Contains(account, "/api/v1/me/password");
        StringAssert.Contains(account, "reader-legado-token-form");
        StringAssert.Contains(account, "/api/v1/me/legado/tokens");
        StringAssert.Contains(account, "记录也会立即删除");
        StringAssert.Contains(account, "撤销会立即删除记录");
        StringAssert.Contains(account, "reader-legado-token-reveal");
        StringAssert.Contains(account, "仅显示一次");
        Assert.IsFalse(account.Contains("innerHTML"), "账户数据必须通过安全 DOM API 写入");
        Assert.IsFalse(account.Contains("reader-account-links"), "账户页不应重复展示阅读空间入口");
        StringAssert.Contains(register, "id=\"reader-register-form\"");
        Assert.IsFalse(register.Contains("id=\"reader-login-form\""), "注册页不得平铺登录表单");
        StringAssert.Contains(register, "/reader/account");
        StringAssert.Contains(register, "reader-account-switch");
        StringAssert.Contains(account, "sessionStorage");
        StringAssert.Contains(shelf, "data-reader-dashboard=\"shelf\"");
        StringAssert.Contains(shelf, "reader-dashboard-list");
        StringAssert.Contains(history, "data-reader-dashboard=\"history\"");
        StringAssert.Contains(history, "reader-dashboard-list");
        StringAssert.Contains(offline, "离线状态");
        foreach (var page in new[] { account, shelf, history, offline })
        {
            StringAssert.Contains(page, "rel=\"manifest\"");
            StringAssert.Contains(page, "/reader/sw.js");
            StringAssert.Contains(page, "/reader/shelf");
            StringAssert.Contains(page, "/reader/history");
        }
    }

    [TestMethod]
    public void Anonymous_Reader_Pages_Require_Authentication()
    {
        var html = ReaderHtml.BookListPage([ListItem], query: null);

        StringAssert.Contains(html, "reader-auth-pending");
        StringAssert.Contains(html, "/reader/account");
        StringAssert.Contains(html, "location.replace");
        Assert.IsFalse(html.Contains("不登录也可以继续阅读"));
    }

    [TestMethod]
    public void Missing_Chapter_Page_Uses_The_Reader_Authentication_Gate()
    {
        var html = ReaderHtml.MissingChapterPage();

        StringAssert.Contains(html, "reader-auth-pending");
        StringAssert.Contains(html, "该章节尚未发布内容");
        StringAssert.Contains(html, "location.replace");
    }

    [TestMethod]
    public void Operations_Page_Exposes_Protected_Snapshot_And_Audited_Action_Shell()
    {
        var html = ReaderHtml.OperationsPage();

        StringAssert.Contains(html, "运维中心");
        StringAssert.Contains(html, "/api/v1/admin/operations/overview?limit=50");
        StringAssert.Contains(html, "/api/v1/admin/operations/alerts/history?");
        StringAssert.Contains(html, "operations-history");
        StringAssert.Contains(html, "operations-history-more");
        StringAssert.Contains(html, "renderHistoryRestricted");
        StringAssert.Contains(html, "URLSearchParams");
        StringAssert.Contains(html, "仅管理员可查看平台级历史");
        StringAssert.Contains(html, "operations-policy-form");
        StringAssert.Contains(html, "operations-policy-book-id");
        StringAssert.Contains(html, "当前下架书籍列表");
        StringAssert.Contains(html, "/api/v1/admin/content/takedowns?limit=50");
        StringAssert.Contains(html, "policyAction");
        StringAssert.Contains(html, "bookId: pendingAction.bookId");
        StringAssert.Contains(html, "已恢复");
        StringAssert.Contains(html, "/api/v1/admin/crawler/dead-letters/");
        StringAssert.Contains(html, "/api/v1/admin/sources/");
        StringAssert.Contains(html, "operations-action-reason");
        StringAssert.Contains(html, "aria-live=\"polite\"");
        StringAssert.Contains(html, "hasMoreDeadLetters");
        StringAssert.Contains(html, "isReplayed");
        StringAssert.Contains(html, "/api/v1/admin/packages?limit=50");
        StringAssert.Contains(html, "remainingTaskCount");
        StringAssert.Contains(html, "inFlightTaskCount");
        StringAssert.Contains(html, "cancelledTaskCount");
        StringAssert.Contains(html, "artifactLength");
        StringAssert.Contains(html, "expiresAt");
        StringAssert.Contains(html, "operations-package-card__progress");
        StringAssert.Contains(html, "打包进度");
        StringAssert.Contains(html, "taskPollTimer");
        StringAssert.Contains(html, "collectionHasActive");
        StringAssert.Contains(html, "按状态分类");
        StringAssert.Contains(html, "operations-run-group");
        StringAssert.Contains(html, "operations-tabs");
        StringAssert.Contains(html, "data-operations-tab=\"collection\"");
        StringAssert.Contains(html, "data-operations-tab=\"packages\"");
        StringAssert.Contains(html, "operations-run-tabs");
        StringAssert.Contains(html, "data-collection-status");
        StringAssert.Contains(html, "bookTitle");
        StringAssert.Contains(html, "采集地址：");
        StringAssert.Contains(html, "operations-run-card__summary");
        StringAssert.Contains(html, "detailsOpenState");
        StringAssert.Contains(html, "captureDetailsOpenState");
        StringAssert.Contains(html, "restoreDetailsOpenState");
        StringAssert.Contains(html, "data-operations-details-key");
        StringAssert.Contains(html, "node(\"details\"");
        StringAssert.Contains(html, "operations-package-card");
        StringAssert.Contains(html, "operations-card");
        StringAssert.Contains(html, "operations-policy-card");
        StringAssert.Contains(html, "source-disable");
        StringAssert.Contains(html, "source-enable");
        StringAssert.Contains(html, "run-delete");
        StringAssert.Contains(html, "/api/v1/admin/collection-runs/");
        StringAssert.Contains(html, "删除失败任务");
        StringAssert.Contains(html, "重试");
        StringAssert.Contains(html, "清理已取消任务");
        StringAssert.Contains(html, "cancelled-cleanup");
        StringAssert.Contains(html, "/api/v1/admin/collection-runs/cancelled/cleanup");
        Assert.IsFalse(html.Contains("window.setInterval("), "无活动任务时不应固定高频轮询");
    }

    [TestMethod]
    public void Operations_Page_Includes_Run_Control_Action_In_Request_Body()
    {
        var html = ReaderHtml.OperationsPage();

        StringAssert.Contains(html, "const requestBody = pendingAction.action === \"run-control\"");
        StringAssert.Contains(html, "action: pendingAction.controlAction");
        StringAssert.Contains(html, "body: JSON.stringify(requestBody)");
    }

    [TestMethod]
    public void Operations_Page_Does_Not_Render_Secrets_Or_Unsafe_Html_Sinks()
    {
        var html = ReaderHtml.OperationsPage();

        Assert.IsFalse(html.Contains("innerHTML"), "运维数据必须通过 textContent/DOM 节点写入");
        Assert.IsFalse(html.Contains("CredentialReferenceId"));
        Assert.IsFalse(html.Contains("Variables"));
        Assert.IsFalse(html.Contains("X-InkFlow-Legado-Token"));
        StringAssert.Contains(html, "credentials: \"same-origin\"");
        StringAssert.Contains(html, "cache: \"no-store\"");
    }
}
