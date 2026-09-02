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
          const pageTitle = document.getElementById("account-title");
          const pageDescription = document.getElementById("account-description");
          const initialPageTitle = pageTitle?.textContent || "登录";
          const initialPageDescription = pageDescription?.textContent || "";
          const avatar = document.getElementById("reader-session-avatar");
          const email = document.getElementById("reader-session-email");
          const emailValue = document.getElementById("reader-session-email-value");
          const role = document.getElementById("reader-session-role");
          const roleValue = document.getElementById("reader-session-role-value");
          const displayName = document.getElementById("reader-session-display-name");
          const displayNameValue = document.getElementById("reader-session-display-name-value");
          const profileForm = document.getElementById("reader-profile-form");
          const passwordForm = document.getElementById("reader-password-form");
          const legadoTokenForm = document.getElementById("reader-legado-token-form");
          const legadoTokenList = document.getElementById("reader-legado-token-list");
          const legadoTokenReveal = document.getElementById("reader-legado-token-reveal");
          const legadoTokenSecret = document.getElementById("reader-legado-token-secret");
          const legadoBookSourceButton = document.getElementById("reader-legado-book-source-copy");
          const legadoTokenCopyButton = document.getElementById("reader-legado-token-copy");
          const sessionStatus = document.getElementById("reader-session-status");
          const adminPanel = document.getElementById("reader-admin-panel");
          const accountTabs = Array.from(document.querySelectorAll("[data-account-tab]"));
          const status = document.getElementById("reader-account-status");
          let latestBookSource = null;
          const setStatus = (message) => { if (status) status.textContent = message; };
          const roleLabel = (value) => {
            switch (value) {
              case "Administrator": return "管理员";
              case "Operator": return "运营人员";
              case "Reader": return "读者";
              default: return "读者";
            }
          };
          const hasOperationsAccess = (value) => value === "Administrator" || value === "Operator";
          const firstCharacter = (value) => Array.from(String(value || "墨").trim() || "墨")[0].toUpperCase();
          const dateLabel = (value) => {
            const parsed = new Date(value);
            return Number.isNaN(parsed.valueOf()) ? "未知时间" : parsed.toLocaleString();
          };
          const accountTabFromHash = () => {
            const candidate = window.location.hash.slice(1);
            return accountTabs.some(tab => tab.dataset.accountTab === candidate) ? candidate : "profile";
          };
          const selectAccountTab = (name, focus = false, updateHash = false) => {
            const selected = accountTabs.find(tab => tab.dataset.accountTab === name)
              || accountTabs.find(tab => tab.dataset.accountTab === "profile");
            if (!selected) return;
            const selectedName = selected.dataset.accountTab;
            for (const tab of accountTabs) {
              const active = tab === selected;
              tab.setAttribute("aria-selected", active ? "true" : "false");
              tab.tabIndex = active ? 0 : -1;
              const panel = document.getElementById(tab.getAttribute("aria-controls") || "");
              if (panel) panel.hidden = !active;
            }
            if (updateHash && window.location.hash !== `#${selectedName}`) {
              window.history.replaceState(null, "", `#${selectedName}`);
            }
            if (focus) selected.focus();
          };
          for (const [index, tab] of accountTabs.entries()) {
            tab.addEventListener("click", () => selectAccountTab(tab.dataset.accountTab, false, true));
            tab.addEventListener("keydown", (event) => {
              const offset = event.key === "ArrowRight" || event.key === "ArrowDown"
                ? 1
                : event.key === "ArrowLeft" || event.key === "ArrowUp"
                  ? -1
                  : event.key === "Home"
                    ? -index
                    : event.key === "End"
                      ? accountTabs.length - 1 - index
                      : 0;
              if (!offset || accountTabs.length < 2) return;
              event.preventDefault();
              const nextIndex = (index + offset + accountTabs.length) % accountTabs.length;
              selectAccountTab(accountTabs[nextIndex].dataset.accountTab, true, true);
            });
          }
          window.addEventListener("hashchange", () => selectAccountTab(accountTabFromHash()));
          selectAccountTab(accountTabFromHash());
          const renderProfile = (profile, fallbackEmail) => {
            const value = typeof profile?.displayName === "string" && profile.displayName.trim()
              ? profile.displayName.trim()
              : fallbackEmail.split("@")[0].trim() || "墨客";
            if (displayName) displayName.textContent = value;
            if (displayNameValue) displayNameValue.textContent = value;
            const profileInput = document.getElementById("reader-display-name");
            if (profileInput && document.activeElement !== profileInput) profileInput.value = value;
            if (avatar) avatar.textContent = firstCharacter(value);
          };
          const renderLegadoTokens = (values) => {
            if (!legadoTokenList) return;
            legadoTokenList.replaceChildren();
            const activeValues = Array.isArray(values) ? values.filter(value => !value?.revokedAt) : [];
            if (activeValues.length === 0) {
              const empty = document.createElement("li");
              empty.className = "account-token account-token--empty";
              empty.textContent = "还没有阅读 3.0 令牌。";
              legadoTokenList.append(empty);
              return;
            }

            for (const value of activeValues) {
              const row = document.createElement("li");
              row.className = "account-token";
              const copy = document.createElement("div");
              copy.className = "account-token__copy";
              const name = document.createElement("strong");
              name.textContent = typeof value?.name === "string" ? value.name : "阅读 3.0";
              const meta = document.createElement("span");
              meta.className = "account-token__meta";
              const expiresAt = new Date(value?.expiresAt);
              const state = Number.isNaN(expiresAt.valueOf()) || expiresAt <= new Date()
                ? "已过期"
                : `有效至 ${dateLabel(value.expiresAt)}`;
              meta.textContent = `${value?.prefix || "lf_lgd_"} · ${state}`;
              copy.append(name, meta);
              row.append(copy);
              if (typeof value?.id === "string") {
                const action = document.createElement("button");
                action.className = "button";
                action.type = "button";
                action.textContent = "撤销";
                action.addEventListener("click", async () => {
                  const message = "撤销后，阅读 3.0 将无法继续使用这个令牌，令牌记录也会立即删除。确定撤销吗？";
                  if (!window.confirm(message)) return;
                  action.disabled = true;
                  const response = await client.apiFetch(
                    `/api/v1/me/legado/tokens/${encodeURIComponent(value.id)}`,
                    { method: "DELETE" });
                  if (response?.ok) {
                    setStatus("阅读 3.0 令牌已撤销，记录已删除。");
                    await loadLegadoTokens();
                  } else {
                    action.disabled = false;
                    setStatus("暂时无法撤销令牌，请稍后重试。");
                  }
                });
                row.append(action);
              }
              legadoTokenList.append(row);
            }
          };
          const loadLegadoTokens = async () => {
            if (!legadoTokenList) return;
            const response = await client.apiFetch("/api/v1/me/legado/tokens");
            if (!response?.ok) {
              renderLegadoTokens([]);
              const failed = legadoTokenList.querySelector(".account-token--empty");
              if (failed) failed.textContent = "令牌列表暂时无法加载。";
              return;
            }
            renderLegadoTokens(await response.json().catch(() => []));
          };
          const copyText = async (value, button, message, resetLabel = "复制") => {
            if (!value) return;
            try {
              await navigator.clipboard.writeText(value);
              if (button) button.textContent = "已复制";
              setStatus(message);
              window.setTimeout(() => { if (button) button.textContent = resetLabel; }, 1600);
            } catch {
              setStatus("浏览器未允许自动复制，请手动复制显示的内容。");
            }
          };
          const getSafeReturnTo = () => {
            const candidate = new URLSearchParams(window.location.search).get("returnTo");
            if (!candidate || !candidate.startsWith("/") || candidate.startsWith("//")) return null;

            let target;
            try {
              target = new URL(candidate, window.location.origin);
            } catch {
              return null;
            }
            const allowedPath = target.pathname === "/reader"
              || target.pathname.startsWith("/reader/")
              || target.pathname === "/admin/operations";
            return target.origin === window.location.origin && allowedPath
              ? `${target.pathname}${target.search}${target.hash}`
              : null;
          };
          const safeReturnTo = () => getSafeReturnTo() || "/reader";
          const switchLink = document.getElementById("reader-account-switch");
          const returnTo = getSafeReturnTo();
          if (switchLink && returnTo) {
            const target = new URL(switchLink.getAttribute("href") || "/reader/account", window.location.origin);
            target.searchParams.set("returnTo", returnTo);
            switchLink.href = `${target.pathname}${target.search}`;
          }

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
              window.location.assign(safeReturnTo());
              return;
            }

            setStatus(response?.status === 409
              ? "这个邮箱已经注册，请直接登录。"
              : "暂时无法完成操作，请检查信息后重试。");
            if (button) button.disabled = false;
          };

          const showSignedIn = async () => {
            if (!client?.isSignedIn()) {
              setStatus("请登录后继续使用 InkFlow。");
              if (forms) forms.hidden = false;
              if (session) session.hidden = true;
              return;
            }

            const response = await client.apiFetch("/api/v1/auth/me");
            if (response === null) {
              setStatus("当前会话暂时无法验证，请检查网络后重试。");
              if (forms) forms.hidden = true;
              if (session) session.hidden = false;
              if (sessionStatus) sessionStatus.textContent = "暂时无法验证当前会话。";
              return;
            }
            if (!response.ok) {
              client.clearSession();
              setStatus("会话已失效，请重新登录。");
              if (forms) forms.hidden = false;
              if (session) session.hidden = true;
              return;
            }

            const payload = await response.json().catch(() => null);
            if (forms) forms.hidden = true;
            if (session) session.hidden = false;
            selectAccountTab(accountTabFromHash());
            setStatus("当前会话已验证。");
            const accountEmail = typeof payload?.email === "string" && payload.email.trim()
              ? payload.email.trim()
              : "已登录";
            const accountRole = typeof payload?.role === "string" ? payload.role : "Reader";
            const profileResponse = await client.apiFetch("/api/v1/me/profile");
            const profile = profileResponse?.ok
              ? await profileResponse.json().catch(() => null)
              : null;
            if (pageTitle) pageTitle.textContent = "我的账户";
            if (pageDescription) pageDescription.textContent = "管理你的个人资料、账户安全和阅读器访问权限。";
            renderProfile(profile, accountEmail);
            if (email) email.textContent = accountEmail;
            if (emailValue) emailValue.textContent = accountEmail;
            if (role) role.textContent = roleLabel(accountRole);
            if (roleValue) roleValue.textContent = roleLabel(accountRole);
            if (adminPanel) adminPanel.hidden = !hasOperationsAccess(accountRole);
            if (sessionStatus) sessionStatus.textContent = "当前会话有效";
            await loadLegadoTokens();
          };

          loginForm?.addEventListener("submit", (event) => {
            event.preventDefault();
            void submit(loginForm, "/api/v1/auth/login", "登录成功，正在返回书库…");
          });
          registerForm?.addEventListener("submit", (event) => {
            event.preventDefault();
            void submit(registerForm, "/api/v1/auth/register", "注册成功，正在返回书库…");
          });
          profileForm?.addEventListener("submit", async (event) => {
            event.preventDefault();
            const values = new FormData(profileForm);
            const value = String(values.get("displayName") || "").trim();
            const button = profileForm.querySelector("button[type=submit]");
            if (value.length > 64) {
              setStatus("显示名称不能超过 64 个字符。");
              return;
            }
            if (button) button.disabled = true;
            const response = await client.apiFetch("/api/v1/me/profile", {
              method: "PUT",
              body: JSON.stringify({ displayName: value })
            });
            const payload = response?.ok ? await response.json().catch(() => null) : null;
            if (response?.ok && payload) {
              renderProfile(payload, emailValue?.textContent || "已登录");
              setStatus("个人资料已保存。");
            } else {
              setStatus("暂时无法保存个人资料，请稍后重试。");
            }
            if (button) button.disabled = false;
          });
          passwordForm?.addEventListener("submit", async (event) => {
            event.preventDefault();
            const values = new FormData(passwordForm);
            const currentPassword = String(values.get("currentPassword") || "");
            const newPassword = String(values.get("newPassword") || "");
            const confirmPassword = String(values.get("confirmPassword") || "");
            const button = passwordForm.querySelector("button[type=submit]");
            if (newPassword.length < 12 || newPassword.length > 256) {
              setStatus("新密码长度需要在 12 到 256 个字符之间。");
              return;
            }
            if (newPassword !== confirmPassword) {
              setStatus("两次输入的新密码不一致。");
              return;
            }
            if (button) button.disabled = true;
            const response = await client.apiFetch("/api/v1/me/password", {
              method: "POST",
              body: JSON.stringify({ currentPassword, newPassword })
            });
            if (response?.ok) {
              client.clearSession();
              passwordForm.reset();
              if (forms) forms.hidden = false;
              if (session) session.hidden = true;
              if (adminPanel) adminPanel.hidden = true;
              setStatus("密码已修改，请使用新密码重新登录。");
            } else {
              setStatus(response?.status === 401
                ? "当前密码不正确。"
                : "暂时无法修改密码，请检查信息后重试。");
            }
            if (button) button.disabled = false;
          });
          legadoTokenForm?.addEventListener("submit", async (event) => {
            event.preventDefault();
            const values = new FormData(legadoTokenForm);
            const name = String(values.get("name") || "").trim();
            const button = legadoTokenForm.querySelector("button[type=submit]");
            if (button) button.disabled = true;
            setStatus("正在创建阅读 3.0 令牌，请稍候…");
            const response = await client.apiFetch("/api/v1/me/legado/tokens", {
              method: "POST",
              body: JSON.stringify({ name })
            });
            const payload = response?.ok ? await response.json().catch(() => null) : null;
            if (response?.ok && typeof payload?.token === "string") {
              latestBookSource = payload.bookSource || null;
              if (legadoTokenSecret) legadoTokenSecret.value = payload.token;
              if (legadoTokenReveal) legadoTokenReveal.hidden = false;
              legadoTokenForm.reset();
              setStatus("令牌已创建。出于安全原因，原始令牌只会显示这一次。");
              await loadLegadoTokens();
            } else {
              setStatus("暂时无法创建令牌，请稍后重试。");
            }
            if (button) button.disabled = false;
          });
          legadoTokenCopyButton?.addEventListener("click", () =>
            copyText(legadoTokenSecret?.value, legadoTokenCopyButton, "令牌已复制。"));
          legadoBookSourceButton?.addEventListener("click", () =>
            copyText(
              latestBookSource ? JSON.stringify(latestBookSource, null, 2) : "",
              legadoBookSourceButton,
              "阅读 3.0 书源配置已复制。",
              "复制书源配置"));
          document.getElementById("reader-logout")?.addEventListener("click", async () => {
            const button = document.getElementById("reader-logout");
            if (button) button.disabled = true;
            await client?.apiFetch("/api/v1/auth/logout", { method: "POST" });
            client?.clearSession();
            if (forms) forms.hidden = false;
            if (session) session.hidden = true;
            if (adminPanel) adminPanel.hidden = true;
            if (legadoTokenReveal) legadoTokenReveal.hidden = true;
            if (legadoTokenSecret) legadoTokenSecret.value = "";
            latestBookSource = null;
            if (pageTitle) pageTitle.textContent = initialPageTitle;
            if (pageDescription) pageDescription.textContent = initialPageDescription;
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
                    <div id="reader-session-avatar" class="account-avatar" aria-hidden="true">墨</div>
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
                  <p class="muted">你拥有运营管理权限，可以查看采集运行和平台状态。</p>
                  <a class="button button--primary" href="/admin/operations">进入运营中心</a>
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
