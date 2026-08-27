using System.Text;

namespace InkFlow.Modules.Content.Application;

public static partial class ReaderHtml
{
    private const string PwaManifestJson =
        """
        {
          "name": "墨流 · InkFlow",
          "short_name": "墨流",
          "start_url": "/reader",
          "scope": "/reader/",
          "display": "standalone",
          "display_override": ["window-controls-overlay", "standalone"],
          "theme_color": "#f6f4ef",
          "background_color": "#f6f4ef",
          "icons": [
            {
              "src": "/reader/icon-192.svg",
              "sizes": "192x192",
              "type": "image/svg+xml",
              "purpose": "any maskable"
            },
            {
              "src": "/reader/icon-512.svg",
              "sizes": "512x512",
              "type": "image/svg+xml",
              "purpose": "any maskable"
            }
          ]
        }
        """;

    private const string PwaIconSvg =
        """
        <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512">
          <rect width="512" height="512" rx="96" fill="#a65332"/>
          <path d="M72 332c54-36 104-36 158 0s104 36 158 0 82-36 108-16v96H72z" fill="#f6f4ef" opacity=".96"/>
          <path d="M72 280c54-36 104-36 158 0s104 36 158 0 82-36 108-16v36c-26-20-62-20-108 16s-104 36-158 0-104-36-158 0z" fill="#f0c9a9" opacity=".92"/>
          <circle cx="160" cy="154" r="42" fill="#f6f4ef" opacity=".96"/>
          <path d="M160 116v76M122 154h76" stroke="#a65332" stroke-width="14" stroke-linecap="round"/>
        </svg>
        """;

    private const string ReaderServiceWorkerScript =
        """
        const CACHE_NAME = "inkflow-reader-shell-v1";
        const SHELL = [
          "/reader/offline",
          "/reader/manifest.webmanifest",
          "/reader/icon-192.svg",
          "/reader/icon-512.svg"
        ];

        self.addEventListener("install", (event) => {
          event.waitUntil((async () => {
            const cache = await caches.open(CACHE_NAME);
            await Promise.all(SHELL.map(async (resource) => {
              try {
                const response = await fetch(resource, { cache: "no-store" });
                if (response.ok) await cache.put(resource, response);
              } catch { /* a later visit can populate the shell */ }
            }));
            await self.skipWaiting();
          })());
        });

        self.addEventListener("activate", (event) => {
          event.waitUntil((async () => {
            const keys = await caches.keys();
            await Promise.all(keys
              .filter((key) => key.startsWith("inkflow-reader-shell-") && key !== CACHE_NAME)
              .map((key) => caches.delete(key)));
            await self.clients.claim();
          })());
        });

        self.addEventListener("fetch", (event) => {
          const request = event.request;
          const url = new URL(request.url);
          if (request.method !== "GET" || url.origin !== self.location.origin || !url.pathname.startsWith("/reader/")) return;

          if (SHELL.includes(url.pathname)) {
            event.respondWith(caches.match(request).then((cached) => cached || fetch(request)));
            return;
          }

          if (request.mode === "navigate") {
            event.respondWith(fetch(request).catch(() => caches.match("/reader/offline")));
          }
        });
        """;

    private const string ReaderAccountScript =
        """
        <script>
        (() => {
          const loginForm = document.getElementById("reader-login-form");
          const registerForm = document.getElementById("reader-register-form");
          if (!loginForm && !registerForm) return;

          const client = window.InkFlowReader;
          const forms = document.getElementById("reader-account-forms");
          const session = document.getElementById("reader-session");
          const user = document.getElementById("reader-session-user");
          const status = document.getElementById("reader-account-status");
          const setStatus = (message) => { if (status) status.textContent = message; };

          const submit = async (form, path, successMessage) => {
            const values = new FormData(form);
            const email = String(values.get("email") || "").trim();
            const password = String(values.get("password") || "");
            const button = form.querySelector("button[type=submit]");
            if (!email || !password) {
              setStatus("请填写邮箱和密码。");
              return;
            }

            if (button) button.disabled = true;
            setStatus("正在处理，请稍候…");
            const response = await client?.authFetch(path, {
              method: "POST",
              body: JSON.stringify({ email, password })
            });
            const payload = response ? await response.json().catch(() => null) : null;
            if (response?.ok && client?.saveSession(payload)) {
              setStatus(successMessage);
              window.location.assign("/reader");
              return;
            }

            setStatus(response?.status === 409
              ? "这个邮箱已经注册，请直接登录。"
              : "暂时无法完成操作，请检查信息后重试。");
            if (button) button.disabled = false;
          };

          const showSignedIn = async () => {
            if (!client?.isSignedIn()) {
              if (forms) forms.hidden = false;
              if (session) session.hidden = true;
              return;
            }

            const response = await client.apiFetch("/api/v1/auth/me");
            if (response === null) {
              if (forms) forms.hidden = true;
              if (session) session.hidden = false;
              if (user) user.textContent = "当前会话暂时无法验证，请检查网络后重试。";
              return;
            }
            if (!response.ok) {
              client.clearSession();
              if (forms) forms.hidden = false;
              if (session) session.hidden = true;
              return;
            }

            const payload = await response.json().catch(() => null);
            if (forms) forms.hidden = true;
            if (session) session.hidden = false;
            if (user) user.textContent = payload?.email ? `已登录：${payload.email}` : "已登录";
          };

          loginForm?.addEventListener("submit", (event) => {
            event.preventDefault();
            void submit(loginForm, "/api/v1/auth/login", "登录成功，正在返回书库…");
          });
          registerForm?.addEventListener("submit", (event) => {
            event.preventDefault();
            void submit(registerForm, "/api/v1/auth/register", "注册成功，正在返回书库…");
          });
          document.getElementById("reader-logout")?.addEventListener("click", async () => {
            const button = document.getElementById("reader-logout");
            if (button) button.disabled = true;
            await client?.apiFetch("/api/v1/auth/logout", { method: "POST" });
            client?.clearSession();
            if (forms) forms.hidden = false;
            if (session) session.hidden = true;
            setStatus("已退出当前会话。");
            if (button) button.disabled = false;
          });

          void showSignedIn();
        })();
        </script>
        """;

    private const string ReaderDashboardScript =
        """
        <script>
        (() => {
          const dashboard = document.getElementById("reader-dashboard");
          if (!dashboard) return;

          const client = window.InkFlowReader;
          const mode = dashboard.dataset.readerDashboard;
          const status = document.getElementById("reader-dashboard-status");
          const list = document.getElementById("reader-dashboard-list");
          const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
          const asGuid = (value) => typeof value === "string" && guidPattern.test(value) ? value : null;
          const text = (value, fallback) => value === null || value === undefined || String(value).trim() === ""
            ? fallback
            : String(value);
          const setStatus = (message) => { if (status) status.textContent = message; };
          const showLogin = () => {
            if (!status) return;
            status.replaceChildren(document.createTextNode("登录后可以在这里查看自己的阅读记录。"));
            const link = document.createElement("a");
            link.href = "/reader/account";
            link.textContent = "登录";
            status.append(" ", link);
          };
          const showError = () => setStatus("暂时无法加载，请检查网络后重试。");
          const statusLabel = (value) => ({
            Reading: "阅读中",
            WantToRead: "想读",
            Paused: "已暂停",
            Completed: "已完成"
          }[value] || "已收藏");
          const dateLabel = (value) => {
            const parsed = new Date(value);
            return Number.isNaN(parsed.valueOf()) ? "" : parsed.toLocaleString();
          };
          const link = (href, label, primary = false) => {
            const anchor = document.createElement("a");
            anchor.className = primary ? "button button--primary" : "button";
            anchor.href = href;
            anchor.textContent = label;
            return anchor;
          };

          const renderShelfItem = (item) => {
            const bookId = asGuid(item?.bookId);
            if (!bookId) return null;
            const currentChapterId = asGuid(item?.currentChapterId);
            const row = document.createElement("li");
            row.className = "dashboard-item";
            const main = document.createElement("div");
            main.className = "dashboard-item__main";
            const title = document.createElement("a");
            title.className = "dashboard-item__title";
            title.href = currentChapterId ? `/reader/read/${currentChapterId}` : `/reader/books/${bookId}`;
            title.textContent = text(item.title, "未命名书目");
            const meta = document.createElement("span");
            meta.className = "dashboard-item__meta";
            const progress = item.progressPercent === null || item.progressPercent === undefined
              ? ""
              : ` · ${Math.max(0, Math.min(100, Number(item.progressPercent) || 0))}%`;
            meta.textContent = `${statusLabel(item.status)} · ${Number(item.chapterCount) || 0} 章${progress}`;
            main.append(title, meta);

            const actions = document.createElement("div");
            actions.className = "dashboard-item__actions";
            actions.append(link(title.href, currentChapterId ? "继续阅读" : "查看目录", true));
            const remove = document.createElement("button");
            remove.className = "button";
            remove.type = "button";
            remove.textContent = "移出书架";
            remove.addEventListener("click", async () => {
              remove.disabled = true;
              const response = await client.apiFetch(`/api/v1/me/reading/shelf/${bookId}`, { method: "DELETE" });
              if (response?.ok) {
                row.remove();
                setStatus(list?.children.length ? "书架已更新。" : "书架暂无书目。");
              } else {
                remove.disabled = false;
                showError();
              }
            });
            actions.append(remove);
            row.append(main, actions);
            return row;
          };

          const renderHistoryItem = (item) => {
            const bookId = asGuid(item?.bookId);
            const chapterId = asGuid(item?.chapterId);
            if (!bookId || !chapterId) return null;
            const row = document.createElement("li");
            row.className = "dashboard-item";
            const main = document.createElement("div");
            main.className = "dashboard-item__main";
            const title = document.createElement("a");
            title.className = "dashboard-item__title";
            title.href = `/reader/read/${chapterId}`;
            title.textContent = text(item.title, "未命名书目");
            const meta = document.createElement("span");
            meta.className = "dashboard-item__meta";
            const index = Number.isFinite(Number(item.chapterIndex)) ? Number(item.chapterIndex) + 1 : "?";
            meta.textContent = `${text(item.chapterTitle, "未命名章节")} · 第 ${index} 章${dateLabel(item.lastReadAt) ? ` · ${dateLabel(item.lastReadAt)}` : ""}`;
            main.append(title, meta);
            const actions = document.createElement("div");
            actions.className = "dashboard-item__actions";
            actions.append(link(title.href, "继续阅读", true), link(`/reader/books/${bookId}`, "查看目录"));
            row.append(main, actions);
            return row;
          };

          const load = async () => {
            if (!client?.isSignedIn()) {
              showLogin();
              return;
            }
            const path = mode === "history"
              ? "/api/v1/me/reading/history?limit=100"
              : "/api/v1/me/reading/shelf?limit=100";
            const response = await client.apiFetch(path);
            if (response === null) {
              showError();
              return;
            }
            if (response.status === 401 || !client.isSignedIn()) {
              showLogin();
              return;
            }
            if (!response.ok) {
              showError();
              return;
            }

            const values = await response.json().catch(() => null);
            if (!Array.isArray(values) || values.length === 0) {
              setStatus(mode === "history" ? "还没有阅读历史。" : "书架暂无书目。浏览书库，加入一本想读的书吧。");
              return;
            }
            list?.replaceChildren();
            for (const value of values) {
              const row = mode === "history" ? renderHistoryItem(value) : renderShelfItem(value);
              if (row) list?.append(row);
            }
            setStatus(list?.children.length ? "已加载。" : "没有可显示的记录。");
          };

          void load();
        })();
        </script>
        """;

    private const string ReaderDetailScript =
        """
        <script>
        (() => {
          const toggle = document.getElementById("reader-shelf-toggle");
          const status = document.getElementById("reader-shelf-status");
          const client = window.InkFlowReader;
          const bookId = toggle?.dataset.bookId;
          const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
          if (!toggle || !client?.isSignedIn() || !guidPattern.test(bookId || "")) return;

          toggle.hidden = false;
          if (status) status.hidden = true;
          toggle.addEventListener("click", async () => {
            toggle.disabled = true;
            const response = await client.apiFetch(`/api/v1/me/reading/shelf/${bookId}`, {
              method: "PUT",
              body: JSON.stringify({ status: "Reading" })
            });
            if (response?.ok) {
              toggle.textContent = "已加入书架";
              toggle.setAttribute("aria-pressed", "true");
              toggle.disabled = true;
              if (status) {
                status.hidden = false;
                status.textContent = "已同步到你的书架。";
              }
              return;
            }
            toggle.disabled = false;
            if (status) {
              status.hidden = false;
              status.textContent = "暂时无法同步书架，请稍后重试。";
            }
          });
        })();
        </script>
        """;

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

    public static string AccountPage()
    {
        var sb = new StringBuilder(Head);
        sb.Append(ReaderHeader);
        sb.Append(
            """
            <main id="main-content" class="page-shell">
              <section class="page-intro" aria-labelledby="account-title">
                <p class="eyebrow">你的阅读空间</p>
                <h1 id="account-title">账户</h1>
                <p class="muted">登录后同步书架、阅读历史、阅读进度和阅读偏好；不登录也可以继续阅读。</p>
              </section>
              <p id="reader-account-status" class="notice" role="status" aria-live="polite">可以登录或注册一个账户。</p>
              <div id="reader-account-forms">
                <section class="form-card" aria-labelledby="login-title">
                  <h2 id="login-title">登录</h2>
                  <form id="reader-login-form" class="form-stack">
                    <div class="form-field">
                      <label for="reader-login-email">邮箱</label>
                      <input id="reader-login-email" name="email" type="email" autocomplete="email" required>
                    </div>
                    <div class="form-field">
                      <label for="reader-login-password">密码</label>
                      <input id="reader-login-password" name="password" type="password" autocomplete="current-password" required>
                    </div>
                    <div class="form-actions"><button class="button button--primary" type="submit">登录</button></div>
                  </form>
                </section>
                <section class="form-card" aria-labelledby="register-title">
                  <h2 id="register-title">注册</h2>
                  <form id="reader-register-form" class="form-stack">
                    <div class="form-field">
                      <label for="reader-register-email">邮箱</label>
                      <input id="reader-register-email" name="email" type="email" autocomplete="email" required>
                    </div>
                    <div class="form-field">
                      <label for="reader-register-password">密码</label>
                      <input id="reader-register-password" name="password" type="password" autocomplete="new-password" minlength="12" required>
                    </div>
                    <div class="form-actions"><button class="button" type="submit">注册并登录</button></div>
                  </form>
                </section>
              </div>
              <section id="reader-session" class="form-card account-session" hidden aria-labelledby="session-title">
                <h2 id="session-title">当前账户</h2>
                <p id="reader-session-user" class="muted"></p>
                <button id="reader-logout" class="button" type="button">退出登录</button>
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
        $$"""
        <script>
        (() => {
          const client = window.InkFlowReader;
          const bookId = "{{bookId:D}}";
          const chapterId = "{{chapterId:D}}";
          const syncStatus = document.getElementById("reader-sync-status");
          const paragraphs = [...document.querySelectorAll(".reader-content__body p")];
          let saveTimer = null;

          const metrics = () => {
            const maximum = document.documentElement.scrollHeight - window.innerHeight;
            const progressPercent = maximum <= 0 ? 100 : Math.round((window.scrollY / maximum) * 100);
            const paragraphIndex = paragraphs.findIndex((paragraph) => paragraph.getBoundingClientRect().bottom >= window.innerHeight * 0.25);
            return {
              progressPercent: Math.min(100, Math.max(0, progressPercent)),
              paragraphIndex: Math.max(0, paragraphIndex)
            };
          };

          const save = async () => {
            if (!client?.isSignedIn()) return;
            const values = metrics();
            const response = await client.apiFetch(`/api/v1/me/reading/progress/${bookId}`, {
              method: "PUT",
              body: JSON.stringify({ chapterId, paragraphIndex: values.paragraphIndex, progressPercent: values.progressPercent })
            });
            if (response?.ok && syncStatus) syncStatus.textContent = "已同步";
          };

          const scheduleSave = () => {
            if (!client?.isSignedIn()) return;
            window.clearTimeout(saveTimer);
            saveTimer = window.setTimeout(() => void save(), 850);
          };

          const restore = async () => {
            if (!client?.isSignedIn()) return;
            const response = await client.apiFetch(`/api/v1/me/reading/progress/${bookId}`);
            if (!response?.ok) return;
            const saved = await response.json().catch(() => null);
            if (!saved || saved.chapterId !== chapterId || Number(saved.progressPercent) <= 0) return;
            await new Promise((resolve) => window.requestAnimationFrame(resolve));
            window.scrollTo({
              top: Math.max(0, document.documentElement.scrollHeight - window.innerHeight) * Math.min(100, Number(saved.progressPercent)) / 100,
              behavior: "auto"
            });
          };

          const initialise = async () => {
            if (!client?.isSignedIn()) return;
            await restore();
            await new Promise((resolve) => window.requestAnimationFrame(resolve));
            await save();
          };

          window.addEventListener("scroll", scheduleSave, { passive: true });
          window.addEventListener("pagehide", () => {
            if (saveTimer !== null) void save();
          });
          void initialise();
        })();
        </script>
        """;
}
