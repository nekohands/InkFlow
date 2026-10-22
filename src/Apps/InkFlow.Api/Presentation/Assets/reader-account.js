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
  const avatarImage = document.getElementById("reader-session-avatar-image");
  const email = document.getElementById("reader-session-email");
  const emailValue = document.getElementById("reader-session-email-value");
  const role = document.getElementById("reader-session-role");
  const roleValue = document.getElementById("reader-session-role-value");
  const displayName = document.getElementById("reader-session-display-name");
  const displayNameValue = document.getElementById("reader-session-display-name-value");
  const profileForm = document.getElementById("reader-profile-form");
  const avatarForm = document.getElementById("reader-avatar-form");
  const avatarFileInput = document.getElementById("reader-avatar-file");
  const avatarFileName = document.getElementById("reader-avatar-file-name");
  const passwordForm = document.getElementById("reader-password-form");
  const legadoTokenForm = document.getElementById("reader-legado-token-form");
  const legadoTokenList = document.getElementById("reader-legado-token-list");
  const legadoTokenReveal = document.getElementById("reader-legado-token-reveal");
  const legadoTokenSecret = document.getElementById("reader-legado-token-secret");
  const legadoBookSourceButton = document.getElementById("reader-legado-book-source-copy");
  const legadoTokenCopyButton = document.getElementById("reader-legado-token-copy");
  const sessionStatus = document.getElementById("reader-session-status");
  const adminPanel = document.getElementById("reader-admin-panel");
  const adminPanelTitle = document.getElementById("account-admin-title");
  const adminPanelDescription = document.getElementById("account-admin-description");
  const adminPanelLink = document.getElementById("account-admin-link");
  const accountTabs = Array.from(document.querySelectorAll("[data-account-tab]"));
  const status = document.getElementById("reader-account-status");
  let latestBookSource = null;
  let avatarObjectUrl = null;
  const setStatus = (message) => { if (status) status.textContent = message; };
  const roleLabel = (value) => {
    switch (value) {
      case "Administrator": return "管理员";
      case "Operator": return "运营人员";
      case "Reader": return "读者";
      default: return "读者";
    }
  };
  const hasOperationsAccess = (value) => ["Reader", "Administrator", "Operator"].includes(value);
  const firstCharacter = (value) => Array.from(String(value || "墨").trim() || "墨")[0].toUpperCase();
  const showAvatarFallback = () => {
    if (avatarObjectUrl) {
      URL.revokeObjectURL(avatarObjectUrl);
      avatarObjectUrl = null;
    }
    if (avatarImage) {
      avatarImage.hidden = true;
      avatarImage.removeAttribute("src");
    }
    if (avatar) avatar.hidden = false;
  };
  const loadAvatar = async () => {
    if (!avatarImage) return;
    const response = await client.apiFetch("/api/v1/me/profile/avatar");
    if (!response?.ok) {
      showAvatarFallback();
      return;
    }
    try {
      const blob = await response.blob();
      if (!blob.size) throw new Error("empty avatar");
      const objectUrl = URL.createObjectURL(blob);
      if (avatarObjectUrl) URL.revokeObjectURL(avatarObjectUrl);
      avatarObjectUrl = objectUrl;
      avatarImage.src = objectUrl;
      avatarImage.hidden = false;
      if (avatar) avatar.hidden = true;
    } catch {
      showAvatarFallback();
    }
  };
  avatarImage?.addEventListener("error", showAvatarFallback);
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
    if (avatarImage) avatarImage.alt = `${value}的头像`;
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
    if (accountRole === "Reader") {
      if (adminPanelTitle) adminPanelTitle.textContent = "采集与下载";
      if (adminPanelDescription) adminPanelDescription.textContent = "你可以发起书籍采集、查看进度，并生成 EPUB、TXT 或 ZIP 书籍包；来源状态仅供查看。";
      if (adminPanelLink) adminPanelLink.textContent = "进入采集与下载";
    } else {
      if (adminPanelTitle) adminPanelTitle.textContent = "管理入口";
      if (adminPanelDescription) adminPanelDescription.textContent = "你拥有运营管理权限，可以查看采集运行和平台状态。";
      if (adminPanelLink) adminPanelLink.textContent = "进入运营中心";
    }
    if (sessionStatus) sessionStatus.textContent = "当前会话有效";
    await loadAvatar();
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
  avatarFileInput?.addEventListener("change", () => {
    const file = avatarFileInput.files?.[0];
    if (avatarFileName) avatarFileName.textContent = file?.name || "尚未选择图片";
  });
  avatarForm?.addEventListener("submit", async (event) => {
    event.preventDefault();
    const file = avatarFileInput?.files?.[0];
    const button = avatarForm.querySelector("button[type=submit]");
    if (!file) {
      setStatus("请选择一张头像图片。");
      return;
    }
    if (file.size > 2 * 1024 * 1024) {
      setStatus("头像图片不能超过 2 MiB。");
      return;
    }
    const allowedTypes = new Set(["image/jpeg", "image/png", "image/webp"]);
    if (file.type && !allowedTypes.has(file.type)) {
      setStatus("头像只支持 JPG、PNG 或 WebP 图片。");
      return;
    }
    if (button) button.disabled = true;
    setStatus("正在上传头像，请稍候…");
    const body = new FormData();
    body.append("file", file);
    const response = await client.apiFetch("/api/v1/me/profile/avatar", {
      method: "PUT",
      body
    });
    if (response?.ok) {
      await loadAvatar();
      avatarForm.reset();
      if (avatarFileName) avatarFileName.textContent = "尚未选择图片";
      setStatus("头像已更新。");
    } else {
      setStatus(response?.status === 413
        ? "头像图片不能超过 2 MiB。"
        : "暂时无法上传头像，请检查图片格式后重试。");
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
      showAvatarFallback();
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
    showAvatarFallback();
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
