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

  const downloadResponse = async (response, fallbackName) => {
    if (!response?.ok) return false;
    const blob = await response.blob().catch(() => null);
    if (!blob) return false;
    const filename = typeof fallbackName === "string" && fallbackName.trim()
      ? fallbackName.trim().replaceAll("\\", "_").replaceAll("/", "_").replaceAll("\0", "_")
      : "inkflow-download";
    const objectUrl = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = objectUrl;
    link.download = filename;
    link.hidden = true;
    document.body.append(link);
    link.click();
    link.remove();
    window.setTimeout(() => URL.revokeObjectURL(objectUrl), 1000);
    return true;
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
    apiFetch,
    downloadResponse
  });
  void requireAuthentication();
})();
