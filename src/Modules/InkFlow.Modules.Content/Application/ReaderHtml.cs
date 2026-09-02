using System.Net;
using System.Text;
using InkFlow.Modules.Library.Application;

namespace InkFlow.Modules.Content.Application;

/// <summary>
/// Web Reader v1 的服务端 HTML 渲染(纯函数,便于离线测试)。
/// 页面流:搜索/书目 → 书详情(含"开始阅读") → 目录 → 正文 + 上一章/下一章。
/// 阅读设置只保存在当前设备的 localStorage;正文仍只读取已落库的 Canonical Content。
/// </summary>
public static partial class ReaderHtml
{
    /// <summary>
    /// 浏览器会话客户端：只处理同源认证 API，令牌保留在当前 tab 的 sessionStorage，
    /// 不把凭据写入 URL、HTML 或长期 localStorage。除登录/注册页外，读者页面必须
    /// 先有可验证的会话；服务端 API 仍独立执行认证与授权。
    /// </summary>
    private const string ReaderClientScript =
        """
        <script>
        (() => {
          const sessionKey = "inkflow.reader.session.v1";
          const maxTokenLength = 512;
          let refreshPromise = null;

          const isToken = (value) => typeof value === "string" && value.length > 0 && value.length <= maxTokenLength;

          const readSession = () => {
            try {
              const stored = JSON.parse(sessionStorage.getItem(sessionKey) || "null");
              return isToken(stored?.accessToken) && isToken(stored?.refreshToken)
                ? { accessToken: stored.accessToken, refreshToken: stored.refreshToken }
                : null;
            } catch {
              return null;
            }
          };

          const saveSession = (payload) => {
            const accessToken = payload?.access_token;
            const refreshToken = payload?.refresh_token;
            if (!isToken(accessToken) || !isToken(refreshToken)) return false;
            try {
              sessionStorage.setItem(sessionKey, JSON.stringify({ accessToken, refreshToken }));
              return true;
            } catch {
              return false;
            }
          };

          const clearSession = () => {
            try { sessionStorage.removeItem(sessionKey); } catch { /* storage may be unavailable */ }
          };

          const authFetch = async (path, options = {}) => {
            if (typeof path !== "string" || !path.startsWith("/api/v1/auth/")) return null;
            const headers = new Headers(options.headers || {});
            headers.set("Accept", "application/json");
            if (options.body && !headers.has("Content-Type") && !(options.body instanceof FormData)) headers.set("Content-Type", "application/json");
            try {
              return await fetch(path, { ...options, headers, credentials: "same-origin", cache: "no-store" });
            } catch {
              return null;
            }
          };

          const refreshAccessToken = () => {
            if (refreshPromise) return refreshPromise;
            const session = readSession();
            if (!session?.refreshToken) return Promise.resolve(false);

            refreshPromise = (async () => {
              try {
                const response = await authFetch("/api/v1/auth/refresh", {
                  method: "POST",
                  body: JSON.stringify({ refresh_token: session.refreshToken })
                });
                const payload = response?.ok ? await response.json().catch(() => null) : null;
                if (!saveSession(payload)) {
                  clearSession();
                  return false;
                }
                return true;
              } catch {
                clearSession();
                return false;
              } finally {
                refreshPromise = null;
              }
            })();
            return refreshPromise;
          };

          const apiFetch = async (path, options = {}) => {
            if (typeof path !== "string" || !path.startsWith("/api/v1/")) return null;
            const initial = readSession();
            if (!initial?.accessToken) return null;

            const request = async (accessToken) => {
              const headers = new Headers(options.headers || {});
              headers.set("Accept", "application/json");
              headers.set("Authorization", `Bearer ${accessToken}`);
              if (options.body && !headers.has("Content-Type") && !(options.body instanceof FormData)) headers.set("Content-Type", "application/json");
              return fetch(path, { ...options, headers, credentials: "same-origin", cache: "no-store" });
            };

            let response;
            try {
              response = await request(initial.accessToken);
            } catch {
              return null;
            }

            if (response.status !== 401 || !initial.refreshToken || path === "/api/v1/auth/refresh") return response;
            if (!await refreshAccessToken()) return response;

            const refreshed = readSession();
            if (!refreshed?.accessToken) return response;
            try {
              return await request(refreshed.accessToken);
            } catch {
              return null;
            }
          };

          const isAccountPage = () => ["/reader/account", "/reader/account/register"].includes(window.location.pathname);
          const redirectToLogin = () => {
            const returnTo = `${window.location.pathname}${window.location.search}${window.location.hash}`;
            const query = new URLSearchParams({ returnTo });
            window.location.replace(`/reader/account?${query.toString()}`);
          };
          const revealPage = () => document.documentElement.classList.remove("reader-auth-pending");
          const requireAuthentication = async () => {
            if (isAccountPage()) {
              revealPage();
              return;
            }

            document.documentElement.classList.add("reader-auth-pending");
            if (!readSession()?.accessToken) {
              redirectToLogin();
              return;
            }

            const response = await apiFetch("/api/v1/auth/me");
            if (response && !response.ok) {
              clearSession();
              redirectToLogin();
              return;
            }

            // 网络暂不可用时保留已有会话，让离线壳仍可展示；受保护 API 自己继续校验。
            revealPage();
          };

          window.InkFlowReader = Object.freeze({
            isSignedIn: () => Boolean(readSession()?.accessToken),
            saveSession,
            clearSession,
            authFetch,
            apiFetch
          });
          void requireAuthentication();
        })();
        </script>
        """;

    private const string Head =
        """
        <!DOCTYPE html>
        <html lang="zh-CN" data-reader-theme="system">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <meta name="theme-color" content="#f6f4ef">
          <link rel="manifest" href="/reader/manifest.webmanifest">
          <link rel="icon" href="/reader/icon-192.svg" type="image/svg+xml">
          <title>墨流 · InkFlow</title>
          <style>
            :root {
              --reader-bg: #f6f4ef;
              --reader-surface: #fffdf8;
              --reader-surface-raised: rgba(255, 253, 248, 0.92);
              --reader-text: #25231f;
              --reader-muted: #746f66;
              --reader-border: #e5dfd4;
              --reader-accent: #a65332;
              --reader-accent-strong: #813a23;
              --reader-accent-contrast: #fffaf4;
              --reader-focus: #0b6bcb;
              --reader-font-size: 100%;
              --reader-line-height: 1.8;
              --reader-content-width: 46rem;
              --reader-radius: 14px;
              --reader-shadow: 0 16px 40px rgba(68, 52, 34, 0.08);
              color-scheme: light;
              font-family: ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
            }

            html[data-reader-theme="light"] {
              --reader-bg: #f6f4ef;
              --reader-surface: #fffdf8;
              --reader-surface-raised: rgba(255, 253, 248, 0.92);
              --reader-text: #25231f;
              --reader-muted: #746f66;
              --reader-border: #e5dfd4;
              --reader-accent: #a65332;
              --reader-accent-strong: #813a23;
              color-scheme: light;
            }

            html[data-reader-theme="sepia"] {
              --reader-bg: #f0e3ca;
              --reader-surface: #f8edda;
              --reader-surface-raised: rgba(248, 237, 218, 0.94);
              --reader-text: #493a2c;
              --reader-muted: #79634c;
              --reader-border: #dfcdb0;
              --reader-accent: #8c4d2c;
              --reader-accent-strong: #6b3822;
              color-scheme: light;
            }

            html[data-reader-theme="dark"] {
              --reader-bg: #171717;
              --reader-surface: #202020;
              --reader-surface-raised: rgba(32, 32, 32, 0.94);
              --reader-text: #e8e4dc;
              --reader-muted: #aaa39a;
              --reader-border: #3c3935;
              --reader-accent: #e09a74;
              --reader-accent-strong: #f0b18e;
              --reader-accent-contrast: #21150e;
              --reader-focus: #7eb6f2;
              color-scheme: dark;
            }

            @media (prefers-color-scheme: dark) {
              html[data-reader-theme="system"] {
                --reader-bg: #171717;
                --reader-surface: #202020;
                --reader-surface-raised: rgba(32, 32, 32, 0.94);
                --reader-text: #e8e4dc;
                --reader-muted: #aaa39a;
                --reader-border: #3c3935;
                --reader-accent: #e09a74;
                --reader-accent-strong: #f0b18e;
                --reader-accent-contrast: #21150e;
                --reader-focus: #7eb6f2;
                color-scheme: dark;
              }
            }

            *, *::before, *::after { box-sizing: border-box; }
            html { min-width: 320px; scroll-behavior: smooth; }
            body {
              min-height: 100vh;
              margin: 0;
              background: var(--reader-bg);
              color: var(--reader-text);
              line-height: 1.6;
              text-rendering: optimizeLegibility;
            }

            a { color: var(--reader-accent-strong); }
            a:hover { color: var(--reader-accent); }
            button, input, select { font: inherit; }
            button, a { -webkit-tap-highlight-color: transparent; }
            :focus-visible {
              outline: 3px solid var(--reader-focus);
              outline-offset: 3px;
            }

            .skip-link {
              position: absolute;
              z-index: 10;
              left: 1rem;
              top: -4rem;
              padding: 0.65rem 0.9rem;
              border-radius: 0.55rem;
              background: var(--reader-text);
              color: var(--reader-bg);
              text-decoration: none;
            }
            .skip-link:focus { top: 1rem; }
            html.reader-auth-pending body > :not(.skip-link):not(script) { visibility: hidden; }

            .site-header {
              border-bottom: 1px solid var(--reader-border);
              background: var(--reader-surface-raised);
              backdrop-filter: blur(12px);
            }
            .site-header__inner {
              display: flex;
              align-items: center;
              justify-content: space-between;
              gap: 1rem;
              max-width: 72rem;
              margin: 0 auto;
              padding: 1rem clamp(1rem, 4vw, 2rem);
            }
            .brand {
              display: inline-flex;
              align-items: baseline;
              gap: 0.65rem;
              color: var(--reader-text);
              text-decoration: none;
            }
            .brand__name { font-size: 1.05rem; font-weight: 750; letter-spacing: 0.04em; }
            .brand__sub { color: var(--reader-muted); font-size: 0.8rem; }
            .reader-nav { display: flex; align-items: center; justify-content: flex-end; gap: 0.2rem; flex-wrap: wrap; }
            .reader-nav a, .reader-nav button {
              min-height: 2.45rem;
              padding: 0.45rem 0.7rem;
              border: 0;
              border-radius: 0.6rem;
              background: transparent;
              color: var(--reader-text);
              font-size: 0.9rem;
              text-decoration: none;
              cursor: pointer;
            }
            .reader-nav a:hover, .reader-nav button:hover { background: var(--reader-bg); color: var(--reader-accent-strong); }
            .reader-nav__install[hidden] { display: none; }

            .page-shell {
              width: min(calc(100% - 2rem), 72rem);
              margin: 0 auto;
              padding: clamp(1.5rem, 4vw, 3rem) 0 4rem;
            }
            .page-intro { max-width: 42rem; margin-bottom: 1.5rem; }
            .eyebrow {
              margin: 0 0 0.4rem;
              color: var(--reader-accent);
              font-size: 0.78rem;
              font-weight: 750;
              letter-spacing: 0.12em;
              text-transform: uppercase;
            }
            h1, h2, p { margin-top: 0; }
            h1 { margin-bottom: 0.65rem; font-size: clamp(1.65rem, 4vw, 2.35rem); line-height: 1.2; }
            h2 { font-size: 1.25rem; line-height: 1.3; }
            .muted { color: var(--reader-muted); }
            .notice {
              margin: 1rem 0;
              padding: 0.85rem 1rem;
              border: 1px solid var(--reader-border);
              border-radius: 0.75rem;
              color: var(--reader-muted);
              background: var(--reader-surface);
            }

            .search-bar {
              display: flex;
              gap: 0.65rem;
              width: min(100%, 44rem);
              margin: 1.35rem 0 2rem;
            }
            .search-bar input {
              min-width: 0;
              flex: 1;
              min-height: 3rem;
              padding: 0.7rem 0.95rem;
              border: 1px solid var(--reader-border);
              border-radius: 0.75rem;
              background: var(--reader-surface);
              color: var(--reader-text);
            }
            .button, .search-bar button {
              display: inline-flex;
              min-height: 2.85rem;
              align-items: center;
              justify-content: center;
              gap: 0.4rem;
              padding: 0.65rem 1rem;
              border: 1px solid var(--reader-border);
              border-radius: 0.7rem;
              background: var(--reader-surface);
              color: var(--reader-text);
              cursor: pointer;
              text-decoration: none;
              white-space: nowrap;
            }
            .button:hover, .search-bar button:hover { border-color: var(--reader-accent); }
            .button--primary {
              border-color: var(--reader-accent);
              background: var(--reader-accent);
              color: var(--reader-accent-contrast);
              font-weight: 700;
            }
            .button--primary:hover { background: var(--reader-accent-strong); color: var(--reader-accent-contrast); }

            .book-grid {
              display: grid;
              grid-template-columns: repeat(auto-fit, minmax(min(100%, 18rem), 1fr));
              gap: 0.9rem;
              padding: 0;
              margin: 0;
              list-style: none;
            }
            .book-card {
              min-width: 0;
              min-height: 9rem;
              border: 1px solid var(--reader-border);
              border-radius: var(--reader-radius);
              background: var(--reader-surface);
              box-shadow: 0 4px 16px rgba(68, 52, 34, 0.04);
              transition: transform 160ms ease, box-shadow 160ms ease, border-color 160ms ease;
            }
            .book-card:hover { transform: translateY(-2px); border-color: var(--reader-accent); box-shadow: var(--reader-shadow); }
            .book-card__link {
              display: flex;
              min-width: 0;
              min-height: 9rem;
              flex-direction: column;
              justify-content: space-between;
              gap: 0.65rem;
              padding: 1.15rem;
              color: var(--reader-text);
              text-decoration: none;
            }
            .book-card__title { font-size: 1.1rem; font-weight: 700; line-height: 1.35; }
            .book-card__author { color: var(--reader-muted); }
            .book-card__meta { color: var(--reader-muted); font-size: 0.85rem; }
            .result-count { margin: -0.8rem 0 1rem; color: var(--reader-muted); }

            .breadcrumbs {
              display: flex;
              flex-wrap: wrap;
              gap: 0.5rem;
              margin-bottom: 1.25rem;
              color: var(--reader-muted);
              font-size: 0.9rem;
            }
            .book-hero, .content-panel {
              border: 1px solid var(--reader-border);
              border-radius: var(--reader-radius);
              background: var(--reader-surface);
              box-shadow: var(--reader-shadow);
            }
            .book-hero { padding: clamp(1.25rem, 4vw, 2.5rem); }
            .book-author { margin-bottom: 1.25rem; color: var(--reader-muted); font-size: 1.05rem; }
            .book-actions { display: flex; flex-wrap: wrap; gap: 0.7rem; }
            .content-panel { margin-top: 1.25rem; padding: clamp(1.1rem, 3vw, 1.8rem); }
            .toc {
              display: grid;
              grid-template-columns: repeat(auto-fit, minmax(min(100%, 20rem), 1fr));
              gap: 0.25rem 1.25rem;
              padding: 0;
              margin: 0;
              list-style: none;
            }
            .toc li { min-width: 0; border-bottom: 1px solid var(--reader-border); }
            .toc a { display: block; padding: 0.75rem 0.2rem; text-decoration: none; }
            .toc a:hover { padding-left: 0.45rem; }
            .book-card__title,
            .book-card__author,
            .book-author,
            .book-hero h1,
            .toc a,
            .breadcrumbs > * { overflow-wrap: anywhere; }

            .reader-page { background: var(--reader-bg); }
            .reader-shell {
              width: min(calc(100% - 1.5rem), var(--reader-content-width));
              margin: 0 auto;
              padding: 0.85rem 0 3rem;
            }
            .reader-progress {
              position: fixed;
              z-index: 4;
              top: 0;
              left: 0;
              width: 0;
              height: 3px;
              background: var(--reader-accent);
              transition: width 120ms linear;
            }
            .reader-toolbar {
              position: sticky;
              z-index: 3;
              top: 0.75rem;
              display: flex;
              align-items: center;
              gap: 0.45rem;
              min-height: 3.1rem;
              margin-bottom: 1.4rem;
              padding: 0.4rem;
              border: 1px solid var(--reader-border);
              border-radius: 0.9rem;
              background: var(--reader-surface-raised);
              box-shadow: 0 8px 24px rgba(68, 52, 34, 0.08);
              backdrop-filter: blur(12px);
            }
            .toolbar-button {
              display: inline-flex;
              min-height: 2.35rem;
              align-items: center;
              gap: 0.35rem;
              padding: 0.45rem 0.7rem;
              border: 0;
              border-radius: 0.6rem;
              background: transparent;
              color: var(--reader-text);
              cursor: pointer;
              text-decoration: none;
              white-space: nowrap;
            }
            .toolbar-button:hover { background: var(--reader-bg); }
            .reader-chapter { overflow: hidden; flex: 1; color: var(--reader-muted); font-size: 0.86rem; text-align: center; text-overflow: ellipsis; white-space: nowrap; }
            .reader-content {
              font-size: var(--reader-font-size);
              line-height: var(--reader-line-height);
              overflow-wrap: anywhere;
            }
            .reader-content__title { margin-bottom: 0.4rem; font-size: clamp(1.65rem, 5vw, 2.45rem); }
            .reader-content__book { margin-bottom: 2.1rem; color: var(--reader-muted); }
            .reader-content__body { margin-top: 1.5rem; }
            .reader-content__body p { margin: 0 0 1.15em; text-indent: 2em; }
            .reader-content__body p:last-child { margin-bottom: 0; }
            .reader-end { margin: 2rem 0; color: var(--reader-muted); font-size: 0.88rem; text-align: center; }
            .chapter-nav { display: flex; justify-content: space-between; gap: 0.7rem; margin-top: 2.5rem; padding-top: 1.15rem; border-top: 1px solid var(--reader-border); }
            .chapter-nav .button { min-width: 7rem; }
            .reader-sync-status { margin-left: 0.15rem; color: var(--reader-muted); font-size: 0.78rem; white-space: nowrap; }

            .dashboard-list { display: grid; gap: 0.8rem; padding: 0; margin: 0; list-style: none; }
            .dashboard-item {
              display: flex;
              align-items: center;
              justify-content: space-between;
              gap: 1rem;
              padding: 1rem 1.1rem;
              border: 1px solid var(--reader-border);
              border-radius: var(--reader-radius);
              background: var(--reader-surface);
            }
            .dashboard-item__main { min-width: 0; }
            .dashboard-item__title { display: block; margin-bottom: 0.25rem; font-size: 1.05rem; font-weight: 700; overflow-wrap: anywhere; }
            .dashboard-item__meta { color: var(--reader-muted); font-size: 0.88rem; overflow-wrap: anywhere; }
            .dashboard-item__actions { display: flex; align-items: center; flex-wrap: wrap; justify-content: flex-end; gap: 0.45rem; }
            .dashboard-item__actions .button { min-height: 2.55rem; }
            .form-card { max-width: 32rem; padding: clamp(1.1rem, 3vw, 1.6rem); border: 1px solid var(--reader-border); border-radius: var(--reader-radius); background: var(--reader-surface); box-shadow: var(--reader-shadow); }
            .form-card + .form-card { margin-top: 1rem; }
            .form-card h2 { margin-bottom: 1rem; }
            .account-page { max-width: 48rem; }
            .account-page .page-intro { margin-inline: auto; text-align: center; }
            .account-layout { display: grid; justify-items: center; }
            .account-form-card { width: min(100%, 32rem); }
            .account-switch { margin: 1rem 0 0; color: var(--reader-muted); text-align: center; }
            .account-switch a { color: var(--reader-accent-strong); font-weight: 700; }
            .form-field { display: grid; gap: 0.4rem; margin: 1rem 0; }
            .form-field label { font-weight: 650; }
            .form-field input, .form-field textarea { min-height: 2.8rem; padding: 0.65rem 0.75rem; border: 1px solid var(--reader-border); border-radius: 0.65rem; background: var(--reader-bg); color: var(--reader-text); }
            .form-actions { display: flex; align-items: center; justify-content: space-between; gap: 0.7rem; flex-wrap: wrap; }
            .account-session { display: grid; gap: 1rem; width: 100%; margin-inline: auto; }
            .account-profile-card { display: block; max-width: none; padding: clamp(1.25rem, 4vw, 2rem); }
            .account-profile__identity { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; min-width: 0; }
            .account-avatar-wrap { position: relative; flex: 0 0 4rem; width: 4rem; height: 4rem; }
            .account-avatar { display: grid; width: 100%; height: 100%; place-items: center; border-radius: 50%; background: var(--reader-accent); color: var(--reader-accent-contrast); font-size: 1.65rem; font-weight: 800; }
            .account-avatar--image { position: absolute; inset: 0; object-fit: cover; }
            .account-profile__copy { min-width: 0; }
            .account-profile__copy .eyebrow { margin-bottom: 0.25rem; }
            .account-profile__copy h2 { margin: 0 0 0.35rem; }
            .account-profile__name { margin: 0 0 0.35rem; color: var(--reader-text); font-size: 1.05rem; font-weight: 700; overflow-wrap: anywhere; }
            .account-profile__email { margin: 0 0 0.65rem; color: var(--reader-muted); overflow-wrap: anywhere; }
            .account-role { display: inline-flex; align-items: center; min-height: 1.7rem; padding: 0.2rem 0.65rem; border-radius: 999px; background: var(--reader-bg); color: var(--reader-accent-strong); font-size: 0.82rem; font-weight: 700; }
            .account-profile__status { display: flex; align-items: center; gap: 0.45rem; margin: 1.25rem 0 0; padding-top: 0.9rem; border-top: 1px solid var(--reader-border); color: var(--reader-muted); font-size: 0.85rem; }
            .account-status-dot { width: 0.55rem; height: 0.55rem; border-radius: 50%; background: #2f8f5b; box-shadow: 0 0 0 0.25rem rgba(47, 143, 91, 0.14); }
            .account-tabs { display: flex; gap: 0.25rem; overflow-x: auto; padding: 0.25rem; border: 1px solid var(--reader-border); border-radius: 0.8rem; background: var(--reader-surface); }
            .account-tab { flex: 1 0 auto; min-height: 2.8rem; padding: 0.65rem 1rem; border: 0; border-radius: 0.6rem; background: transparent; color: var(--reader-muted); font: inherit; font-weight: 700; cursor: pointer; }
            .account-tab:hover { color: var(--reader-text); background: var(--reader-bg); }
            .account-tab[aria-selected="true"] { background: var(--reader-accent); color: var(--reader-accent-contrast); }
            .account-tab-panels, .account-tab-panel { display: grid; gap: 1rem; }
            .account-tab-panel .form-card + .form-card { margin-top: 0; }
            .account-tab-panel:focus { outline: 2px solid var(--reader-accent); outline-offset: 0.25rem; }
            .account-panel { max-width: none; }
            .account-panel h2 { margin-bottom: 1rem; }
            .account-avatar-settings { margin: 1.25rem 0 1.5rem; padding-top: 1.1rem; border-top: 1px solid var(--reader-border); }
            .account-avatar-settings h3 { margin: 0 0 0.45rem; }
            .account-avatar-settings p { margin-bottom: 0.8rem; }
            .account-details { display: grid; gap: 0.8rem; margin: 0; }
            .account-detail { display: flex; align-items: baseline; justify-content: space-between; gap: 1rem; padding-bottom: 0.8rem; border-bottom: 1px solid var(--reader-border); }
            .account-detail:last-child { padding-bottom: 0; border-bottom: 0; }
            .account-detail dt { color: var(--reader-muted); font-size: 0.9rem; }
            .account-detail dd { margin: 0; color: var(--reader-text); font-weight: 650; text-align: right; overflow-wrap: anywhere; }
            .account-panel__heading { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; }
            .account-panel__heading h2 { margin-bottom: 0.45rem; }
            .account-panel__heading p { margin-bottom: 0; }
            .account-token-form { display: grid; grid-template-columns: minmax(0, 1fr) auto; align-items: end; gap: 0.8rem; margin-top: 1.1rem; }
            .account-token-form .form-field { min-width: 0; margin: 0; }
            .account-token-form > .button { margin-bottom: 0; }
            .account-token-reveal { display: grid; gap: 0.65rem; margin-top: 1rem; padding: 0.9rem; border: 1px solid var(--reader-accent); border-radius: 0.75rem; background: var(--reader-bg); }
            .account-token-reveal textarea { width: 100%; resize: vertical; font-family: ui-monospace, SFMono-Regular, Consolas, monospace; overflow-wrap: anywhere; }
            .account-token-list { display: grid; gap: 0.65rem; padding: 0; margin: 1rem 0 0; list-style: none; }
            .account-token { display: flex; align-items: center; justify-content: space-between; gap: 1rem; padding: 0.8rem 0.9rem; border: 1px solid var(--reader-border); border-radius: 0.7rem; }
            .account-token__copy { display: grid; min-width: 0; gap: 0.2rem; }
            .account-token__copy strong { overflow-wrap: anywhere; }
            .account-token__meta { color: var(--reader-muted); font-size: 0.82rem; overflow-wrap: anywhere; }
            .account-token--empty { color: var(--reader-muted); }
            .account-actions { display: flex; justify-content: flex-end; }
            [hidden] { display: none !important; }

            dialog {
              width: min(calc(100% - 2rem), 28rem);
              padding: 0;
              border: 1px solid var(--reader-border);
              border-radius: 1rem;
              background: var(--reader-surface);
              color: var(--reader-text);
              box-shadow: 0 24px 80px rgba(0, 0, 0, 0.28);
            }
            dialog::backdrop { background: rgba(20, 18, 15, 0.46); backdrop-filter: blur(3px); }
            .settings-dialog__inner { padding: 1.2rem; }
            .settings-dialog__header { display: flex; align-items: center; justify-content: space-between; gap: 1rem; margin-bottom: 1rem; }
            .settings-dialog__header h2 { margin: 0; }
            .icon-button { min-width: 2.5rem; min-height: 2.5rem; border: 1px solid var(--reader-border); border-radius: 0.65rem; background: transparent; color: var(--reader-text); cursor: pointer; }
            .setting { display: grid; gap: 0.45rem; margin: 1rem 0; }
            .setting label { font-weight: 650; }
            .setting select, .setting input[type=range] { width: 100%; }
            .setting select { min-height: 2.7rem; padding: 0.45rem; border: 1px solid var(--reader-border); border-radius: 0.6rem; background: var(--reader-bg); color: var(--reader-text); }
            .setting__range { display: flex; align-items: center; gap: 0.7rem; }
            .setting__range output { min-width: 3.7rem; color: var(--reader-muted); font-size: 0.9rem; text-align: right; }
            .sr-only { position: absolute; width: 1px; height: 1px; padding: 0; margin: -1px; overflow: hidden; clip: rect(0, 0, 0, 0); white-space: nowrap; border: 0; }

            @media (max-width: 640px) {
              .site-header__inner { padding-block: 0.8rem; }
              .brand__sub { display: none; }
              .reader-nav { gap: 0; }
              .reader-nav a, .reader-nav button { padding-inline: 0.45rem; font-size: 0.8rem; }
              .page-shell { width: min(calc(100% - 1rem), 72rem); padding-top: 1.25rem; }
              .search-bar { margin-bottom: 1.35rem; }
              .search-bar button { padding-inline: 0.85rem; }
              .book-hero { box-shadow: none; }
              .reader-shell { width: min(calc(100% - 1rem), var(--reader-content-width)); padding-top: 0.35rem; }
              .reader-toolbar { top: 0.35rem; margin-bottom: 1rem; }
              .toolbar-button { min-width: 2.55rem; justify-content: center; padding-inline: 0.5rem; }
              .toolbar-button span:last-child { display: none; }
              .reader-content__body p { text-indent: 1.5em; }
              .chapter-nav .button { min-width: 0; flex: 1; }
              .dashboard-item { align-items: flex-start; flex-direction: column; }
              .dashboard-item__actions { width: 100%; justify-content: flex-start; }
              .reader-sync-status { display: none; }
              .account-profile__identity { gap: 0.75rem; }
              .account-panel__heading { flex-direction: column; }
              .account-token-form { grid-template-columns: 1fr; }
              .account-token-form > .button { width: 100%; }
              .account-token { align-items: flex-start; flex-direction: column; }
              .account-token .button { width: 100%; }
              .account-actions .button { width: 100%; }
            }

            @media (prefers-reduced-motion: reduce) {
              *, *::before, *::after { scroll-behavior: auto !important; transition-duration: 0.001ms !important; animation-duration: 0.001ms !important; animation-iteration-count: 1 !important; }
            }
          </style>
        </head>
        <body>
        <a class="skip-link" href="#main-content">跳到主要内容</a>
        """ + ReaderClientScript;

    private const string ReaderScript =
        """
        <script>
        (() => {
          const storageKey = "inkflow.reader.preferences.v1";
          const defaults = { theme: "system", fontSize: 100, lineHeight: 180 };
          const root = document.documentElement;
          const dialog = document.getElementById("reader-settings");
          const theme = document.getElementById("reader-theme");
          const fontSize = document.getElementById("reader-font-size");
          const lineHeight = document.getElementById("reader-line-height");
          const fontSizeOutput = document.getElementById("reader-font-size-output");
          const lineHeightOutput = document.getElementById("reader-line-height-output");
          const status = document.getElementById("reader-settings-status");
          let lastTrigger = null;
          let preferenceSyncTimer = null;

          const clamp = (value, min, max, fallback) => {
            const number = Number(value);
            return Number.isFinite(number) ? Math.min(max, Math.max(min, number)) : fallback;
          };

          const readPreferences = () => {
            try {
              const stored = JSON.parse(localStorage.getItem(storageKey) || "null");
              return {
                theme: ["system", "light", "sepia", "dark"].includes(stored?.theme) ? stored.theme : defaults.theme,
                fontSize: clamp(stored?.fontSize, 90, 140, defaults.fontSize),
                lineHeight: clamp(stored?.lineHeight, 150, 230, defaults.lineHeight)
              };
            } catch {
              return { ...defaults };
            }
          };

          let preferences = readPreferences();
          const normaliseTheme = (value) => {
            const candidate = String(value || defaults.theme).toLowerCase();
            return ["system", "light", "sepia", "dark"].includes(candidate) ? candidate : defaults.theme;
          };

          const applyPreferences = (announce) => {
            root.dataset.readerTheme = preferences.theme;
            root.style.setProperty("--reader-font-size", `${preferences.fontSize}%`);
            root.style.setProperty("--reader-line-height", `${preferences.lineHeight / 100}`);
            if (theme) theme.value = preferences.theme;
            if (fontSize) fontSize.value = preferences.fontSize;
            if (lineHeight) lineHeight.value = preferences.lineHeight;
            if (fontSizeOutput) fontSizeOutput.value = `${preferences.fontSize}%`;
            if (lineHeightOutput) lineHeightOutput.value = `${(preferences.lineHeight / 100).toFixed(2)}×`;
            if (announce && status) status.textContent = "阅读设置已保存到本设备";
          };

          const savePreferences = () => {
            try { localStorage.setItem(storageKey, JSON.stringify(preferences)); } catch { /* private mode may deny storage */ }
          };

          const syncPreferencesFromServer = async () => {
            const client = window.InkFlowReader;
            if (!client?.isSignedIn()) return;
            const response = await client.apiFetch("/api/v1/me/reading/preferences");
            if (!response?.ok) return;
            const remote = await response.json().catch(() => null);
            if (!remote) return;
            preferences = {
              theme: normaliseTheme(remote.theme),
              fontSize: clamp(remote.fontSizePercent, 90, 140, defaults.fontSize),
              lineHeight: clamp(remote.lineHeightPercent, 150, 230, defaults.lineHeight)
            };
            savePreferences();
            applyPreferences(false);
          };

          const syncPreferencesToServer = async () => {
            const client = window.InkFlowReader;
            if (!client?.isSignedIn()) return;
            const response = await client.apiFetch("/api/v1/me/reading/preferences", {
              method: "PUT",
              body: JSON.stringify({
                fontSizePercent: preferences.fontSize,
                lineHeightPercent: preferences.lineHeight,
                theme: preferences.theme
              })
            });
            if (response?.ok && status) status.textContent = "阅读设置已同步";
          };

          const schedulePreferencesSync = () => {
            window.clearTimeout(preferenceSyncTimer);
            preferenceSyncTimer = window.setTimeout(() => void syncPreferencesToServer(), 350);
          };

          const openDialog = (trigger) => {
            lastTrigger = trigger;
            applyPreferences(false);
            if (dialog && typeof dialog.showModal === "function") dialog.showModal();
            else if (dialog) dialog.setAttribute("open", "");
          };

          const closeDialog = () => {
            if (!dialog) return;
            if (typeof dialog.close === "function" && dialog.open) dialog.close();
            else dialog.removeAttribute("open");
            lastTrigger?.focus();
          };

          document.querySelectorAll("[data-open-reader-settings]").forEach((trigger) => {
            trigger.addEventListener("click", () => openDialog(trigger));
          });
          document.getElementById("reader-settings-close")?.addEventListener("click", closeDialog);
          dialog?.addEventListener("close", () => lastTrigger?.focus());

          const updateFromControls = () => {
            preferences = {
              theme: theme?.value || defaults.theme,
              fontSize: clamp(fontSize?.value, 90, 140, defaults.fontSize),
              lineHeight: clamp(lineHeight?.value, 150, 230, defaults.lineHeight)
            };
            savePreferences();
            applyPreferences(true);
            schedulePreferencesSync();
          };
          document.getElementById("reader-settings-form")?.addEventListener("input", updateFromControls);
          document.getElementById("reader-settings-form")?.addEventListener("change", updateFromControls);

          const progress = document.getElementById("reading-progress");
          const updateProgress = () => {
            if (!progress) return;
            const maximum = document.documentElement.scrollHeight - window.innerHeight;
            const percentage = maximum <= 0 ? 100 : Math.round((window.scrollY / maximum) * 100);
            progress.style.width = `${Math.min(100, Math.max(0, percentage))}%`;
            progress.setAttribute("aria-valuenow", percentage);
          };
          window.addEventListener("scroll", updateProgress, { passive: true });
          window.addEventListener("resize", updateProgress);

          applyPreferences(false);
          updateProgress();
          void syncPreferencesFromServer();
        })();
        </script>
        """;

    private const string ReaderHeader =
        """
        <header class="site-header">
          <div class="site-header__inner">
            <a class="brand" href="/reader" aria-label="返回 InkFlow 书库"><span class="brand__name">墨流</span><span class="brand__sub">InkFlow · 阅读</span></a>
            <nav class="reader-nav" aria-label="读者导航">
              <a href="/reader">书库</a>
              <a href="/reader/shelf">书架</a>
              <a href="/reader/history">历史</a>
              <a data-reader-account-link href="/reader/account">账户</a>
              <button id="reader-install" class="reader-nav__install" type="button" hidden>安装应用</button>
            </nav>
          </div>
        </header>
        """;

    private const string ReaderPwaScript =
        """
        <script>
        (() => {
          const installButton = document.getElementById("reader-install");
          let deferredPrompt = null;
          const standalone = window.matchMedia?.("(display-mode: standalone)")?.matches || window.navigator.standalone === true;

          if (installButton && !standalone) {
            window.addEventListener("beforeinstallprompt", (event) => {
              event.preventDefault();
              deferredPrompt = event;
              installButton.hidden = false;
            });
            installButton.addEventListener("click", async () => {
              if (!deferredPrompt) return;
              await deferredPrompt.prompt();
              deferredPrompt = null;
              installButton.hidden = true;
            });
            window.addEventListener("appinstalled", () => {
              deferredPrompt = null;
              installButton.hidden = true;
            });
          }

          if ("serviceWorker" in navigator) {
            navigator.serviceWorker.register("/reader/sw.js", { scope: "/reader/" }).catch(() => { /* offline enhancement is optional */ });
          }
        })();
        </script>
        """;

    private const string Tail = ReaderAccountScript + ReaderDashboardScript + ReaderPwaScript + "</body></html>";

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
