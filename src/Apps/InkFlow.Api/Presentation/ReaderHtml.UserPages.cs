using static InkFlow.Api.Presentation.ReaderAssets;
using System.Text;

namespace InkFlow.Api.Presentation;

public static partial class ReaderHtml
{
    public static string PwaManifest() => PwaManifestJson;

    public static string PwaIcon() => PwaIconSvg;

    public static string ServiceWorker() => ReaderServiceWorkerScript;

    public static string OfflinePage()
    {
        var sb = new StringBuilder(Head);
        sb.Append(ReaderHeader);
        sb.Append(
            """
            <main id="main-content" class="page-shell">
              <section class="page-intro" aria-labelledby="offline-title">
                <p class="eyebrow">InkFlow Reader</p>
                <h1 id="offline-title">当前处于离线状态</h1>
                <p class="muted">网络恢复后重新打开页面即可继续阅读。离线壳不会缓存账户、书架或私人内容。</p>
                <a class="button button--primary" href="/reader">返回书库</a>
              </section>
            </main>
            """);
        sb.Append(Tail);
        return sb.ToString();
    }

    public static string MissingChapterPage()
    {
        var sb = new StringBuilder(Head.Replace("<title>墨流 · InkFlow</title>", "<title>未找到</title>"));
        sb.Append(ReaderHeader);
        sb.Append(
            """
            <main id="main-content" class="page-shell">
              <p class="notice" role="status">该章节尚未发布内容。</p>
              <p><a href="/reader">返回书目</a></p>
            </main>
            """);
        sb.Append(Tail);
        return sb.ToString();
    }

    public static string AccountPage(bool registration = false)
    {
        var pageTitle = registration ? "创建账户" : "登录";
        var pageDescription = registration
            ? "创建账户后即可同步书架、阅读历史、阅读进度和阅读偏好。"
            : "登录后同步书架、阅读历史、阅读进度和阅读偏好。";
        var statusMessage = registration
            ? "创建账户后即可开始使用 InkFlow。"
            : "请登录后继续使用 InkFlow。";
        var formTitle = registration ? "注册" : "登录";
        var formId = registration ? "reader-register-form" : "reader-login-form";
        var passwordAutocomplete = registration ? "new-password" : "current-password";
        var passwordLength = registration ? " minlength=\"12\"" : "";
        var submitLabel = registration ? "注册并登录" : "登录";
        var switchText = registration ? "已有账户？" : "没有账户？";
        var switchHref = registration ? "/reader/account" : "/reader/account/register";
        var switchLabel = registration ? "返回登录" : "创建账户";

        var sb = new StringBuilder(Head);
        sb.Append(ReaderHeader);
        sb.Append(
            $"""
            <main id="main-content" class="page-shell account-page">
              <section class="page-intro" aria-labelledby="account-title">
                <p class="eyebrow">你的阅读空间</p>
                <h1 id="account-title">{pageTitle}</h1>
                <p id="account-description" class="muted">{pageDescription}</p>
              </section>
              <p id="reader-account-status" class="notice" role="status" aria-live="polite">{statusMessage}</p>
              <div id="reader-account-forms" class="account-layout">
                <section class="form-card account-form-card" aria-labelledby="account-form-title">
                  <h2 id="account-form-title">{formTitle}</h2>
                  <form id="{formId}" class="form-stack">
                    <div class="form-field">
                      <label for="{formId}-email">邮箱</label>
                      <input id="{formId}-email" name="email" type="email" autocomplete="email" required>
                    </div>
                    <div class="form-field">
                      <label for="{formId}-password">密码</label>
                      <input id="{formId}-password" name="password" type="password" autocomplete="{passwordAutocomplete}"{passwordLength} required>
                    </div>
                    <div class="form-actions"><button class="button button--primary" type="submit">{submitLabel}</button></div>
                  </form>
                  <p class="account-switch">{switchText}<a id="reader-account-switch" href="{switchHref}">{switchLabel}</a></p>
                </section>
              </div>
              <section id="reader-session" class="account-session" hidden aria-labelledby="session-title">
                <section id="reader-session-profile" class="form-card account-profile-card" aria-labelledby="session-title">
                  <div class="account-profile__identity">
                    <div class="account-profile__copy">
                      <p class="eyebrow">个人中心</p>
                      <h2 id="session-title">我的账户</h2>
                      <p id="reader-session-display-name" class="account-profile__name"></p>
                      <p id="reader-session-email" class="account-profile__email"></p>
                      <span id="reader-session-role" class="account-role"></span>
                    </div>
                    <div class="account-avatar-wrap">
                      <div id="reader-session-avatar" class="account-avatar" aria-hidden="true">墨</div>
                      <img id="reader-session-avatar-image" class="account-avatar account-avatar--image" alt="" hidden>
                    </div>
                  </div>
                  <p class="account-profile__status" aria-live="polite">
                    <span class="account-status-dot" aria-hidden="true"></span>
                    <span id="reader-session-status">当前会话有效</span>
                  </p>
                </section>
                <nav class="account-tabs" aria-label="账户设置" role="tablist">
                  <button id="account-tab-profile" class="account-tab" type="button" role="tab" aria-selected="true" aria-controls="account-panel-profile" data-account-tab="profile">个人资料</button>
                  <button id="account-tab-security" class="account-tab" type="button" role="tab" aria-selected="false" aria-controls="account-panel-security" data-account-tab="security" tabindex="-1">账户安全</button>
                  <button id="account-tab-reader" class="account-tab" type="button" role="tab" aria-selected="false" aria-controls="account-panel-reader" data-account-tab="reader" tabindex="-1">阅读器令牌</button>
                </nav>
                <div class="account-tab-panels">
                  <section id="account-panel-profile" class="account-tab-panel" role="tabpanel" aria-labelledby="account-tab-profile" data-account-panel="profile">
                    <section class="form-card account-panel" aria-labelledby="account-info-title">
                      <h2 id="account-info-title">账户信息</h2>
                      <dl class="account-details">
                        <div class="account-detail">
                          <dt>登录邮箱</dt>
                          <dd id="reader-session-email-value"></dd>
                        </div>
                        <div class="account-detail">
                          <dt>显示名称</dt>
                          <dd id="reader-session-display-name-value"></dd>
                        </div>
                        <div class="account-detail">
                          <dt>权限角色</dt>
                          <dd id="reader-session-role-value"></dd>
                        </div>
                      </dl>
                    </section>
                    <section class="form-card account-panel" aria-labelledby="account-profile-title">
                      <h2 id="account-profile-title">个人资料</h2>
                      <p class="muted">设置一个在账户页和后续社区功能中使用的显示名称。</p>
                      <section class="account-avatar-settings" aria-labelledby="account-avatar-title">
                        <h3 id="account-avatar-title">头像</h3>
                        <p class="muted">上传 JPG、PNG 或 WebP 图片，大小不超过 2 MiB。</p>
                        <form id="reader-avatar-form" class="form-stack" enctype="multipart/form-data">
                          <div class="form-field">
                            <label for="reader-avatar-file">选择头像</label>
                            <input id="reader-avatar-file" name="file" type="file" accept="image/jpeg,image/png,image/webp" required>
                            <span id="reader-avatar-file-name" class="muted" role="status">尚未选择图片</span>
                          </div>
                          <div class="form-actions"><button class="button button--primary" type="submit">上传头像</button></div>
                        </form>
                      </section>
                      <form id="reader-profile-form" class="form-stack">
                        <div class="form-field">
                          <label for="reader-display-name">显示名称</label>
                          <input id="reader-display-name" name="displayName" type="text" maxlength="64" autocomplete="nickname" placeholder="清空后使用邮箱前缀">
                        </div>
                        <div class="form-actions"><button class="button button--primary" type="submit">保存资料</button></div>
                      </form>
                    </section>
                  </section>
                  <section id="account-panel-security" class="account-tab-panel" role="tabpanel" aria-labelledby="account-tab-security" data-account-panel="security" hidden>
                    <section class="form-card account-panel" aria-labelledby="account-security-title">
                      <h2 id="account-security-title">账户安全</h2>
                      <p class="muted">修改密码后，所有已登录设备都会退出，需要使用新密码重新登录。</p>
                      <form id="reader-password-form" class="form-stack">
                        <div class="form-field">
                          <label for="reader-current-password">当前密码</label>
                          <input id="reader-current-password" name="currentPassword" type="password" autocomplete="current-password" required>
                        </div>
                        <div class="form-field">
                          <label for="reader-new-password">新密码</label>
                          <input id="reader-new-password" name="newPassword" type="password" autocomplete="new-password" minlength="12" maxlength="256" required>
                        </div>
                        <div class="form-field">
                          <label for="reader-confirm-password">确认新密码</label>
                          <input id="reader-confirm-password" name="confirmPassword" type="password" autocomplete="new-password" minlength="12" maxlength="256" required>
                        </div>
                        <div class="form-actions"><button class="button button--primary" type="submit">修改密码</button></div>
                      </form>
                    </section>
                  </section>
                  <section id="account-panel-reader" class="account-tab-panel" role="tabpanel" aria-labelledby="account-tab-reader" data-account-panel="reader" hidden>
                    <section class="form-card account-panel" aria-labelledby="account-legado-title">
                      <div class="account-panel__heading">
                        <div>
                          <h2 id="account-legado-title">阅读器令牌</h2>
                          <p class="muted">为阅读 3.0 创建独立令牌。令牌只在创建成功时完整显示一次；撤销会立即删除记录，无法恢复，需要重新创建。</p>
                        </div>
                        <span class="account-role">阅读 3.0</span>
                      </div>
                      <form id="reader-legado-token-form" class="account-token-form">
                        <div class="form-field">
                          <label for="reader-legado-token-name">令牌名称（可选）</label>
                          <input id="reader-legado-token-name" name="name" type="text" maxlength="64" placeholder="我的阅读 3.0" autocomplete="off">
                        </div>
                        <button class="button button--primary" type="submit">创建令牌</button>
                      </form>
                      <div id="reader-legado-token-reveal" class="account-token-reveal" hidden role="status" aria-live="polite">
                        <strong>新令牌（仅显示一次）</strong>
                        <textarea id="reader-legado-token-secret" rows="3" readonly spellcheck="false" aria-label="新创建的阅读器令牌"></textarea>
                        <div class="form-actions">
                          <button id="reader-legado-token-copy" class="button" type="button">复制</button>
                          <button id="reader-legado-book-source-copy" class="button" type="button">复制书源配置</button>
                        </div>
                      </div>
                      <ul id="reader-legado-token-list" class="account-token-list" aria-label="已创建的阅读器令牌"></ul>
                    </section>
                  </section>
                </div>
                <section id="reader-admin-panel" class="form-card account-panel" hidden aria-labelledby="account-admin-title">
                  <h2 id="account-admin-title">管理入口</h2>
                  <p id="account-admin-description" class="muted">你拥有运营管理权限，可以查看采集运行和平台状态。</p>
                  <a id="account-admin-link" class="button button--primary" href="/admin/operations">进入运营中心</a>
                </section>
                <div class="account-actions"><button id="reader-logout" class="button" type="button">退出登录</button></div>
              </section>
            </main>
            """);
        sb.Append(Tail);
        return sb.ToString();
    }

    public static string ShelfPage()
    {
        var sb = new StringBuilder(Head);
        sb.Append(ReaderHeader);
        sb.Append(
            """
            <main id="main-content" class="page-shell">
              <section class="page-intro" aria-labelledby="shelf-title">
                <p class="eyebrow">你的阅读空间</p>
                <h1 id="shelf-title">书架</h1>
                <p class="muted">把想读和正在读的作品放在一起，继续阅读时直接回到上次位置。</p>
              </section>
              <section id="reader-dashboard" data-reader-dashboard="shelf" aria-labelledby="shelf-title">
                <p id="reader-dashboard-status" class="notice" role="status" aria-live="polite">正在加载书架…</p>
                <ul id="reader-dashboard-list" class="dashboard-list" aria-label="我的书架"></ul>
              </section>
            </main>
            """);
        sb.Append(Tail);
        return sb.ToString();
    }

    public static string HistoryPage()
    {
        var sb = new StringBuilder(Head);
        sb.Append(ReaderHeader);
        sb.Append(
            """
            <main id="main-content" class="page-shell">
              <section class="page-intro" aria-labelledby="history-title">
                <p class="eyebrow">你的阅读空间</p>
                <h1 id="history-title">阅读历史</h1>
                <p class="muted">从最近打开的章节继续，不需要重新寻找目录位置。</p>
              </section>
              <section id="reader-dashboard" data-reader-dashboard="history" aria-labelledby="history-title">
                <p id="reader-dashboard-status" class="notice" role="status" aria-live="polite">正在加载阅读历史…</p>
                <ul id="reader-dashboard-list" class="dashboard-list" aria-label="我的阅读历史"></ul>
              </section>
            </main>
            """);
        sb.Append(Tail);
        return sb.ToString();
    }

    private static string ReaderProgressScript(Guid bookId, Guid chapterId) =>
        ReaderAssets.ProgressScript.Replace("{{BOOK_ID}}", bookId.ToString("D"), StringComparison.Ordinal)
            .Replace("{{CHAPTER_ID}}", chapterId.ToString("D"), StringComparison.Ordinal);
}
