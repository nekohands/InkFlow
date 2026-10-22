(() => {
  const toggle = document.getElementById("reader-shelf-toggle");
  const shelfStatus = document.getElementById("reader-shelf-status");
  const packageActions = document.getElementById("reader-package-actions");
  const packageButton = document.getElementById("reader-package-download");
  const packageStatus = document.getElementById("reader-package-status");
  const packageFormat = document.getElementById("reader-package-format");
  const client = window.InkFlowReader;
  const bookId = packageActions?.dataset.bookId || toggle?.dataset.bookId;
  const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
  if (!client?.isSignedIn() || !guidPattern.test(bookId || "")) return;

  if (toggle) {
    toggle.hidden = false;
    if (shelfStatus) shelfStatus.hidden = true;
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
        if (shelfStatus) {
          shelfStatus.hidden = false;
          shelfStatus.textContent = "已同步到你的书架。";
        }
        return;
      }
      toggle.disabled = false;
      if (shelfStatus) {
        shelfStatus.hidden = false;
        shelfStatus.textContent = "暂时无法同步书架，请稍后重试。";
      }
    });
  }

  if (!packageActions || !packageButton || !packageStatus || !packageFormat ||
      typeof client.downloadResponse !== "function") return;

  packageActions.hidden = false;
  packageStatus.hidden = true;
  const setPackageStatus = (message, muted = false) => {
    packageStatus.hidden = false;
    packageStatus.textContent = message;
    packageStatus.className = muted ? "book-package-status muted" : "book-package-status";
  };
  const redirectToLogin = () => {
    client.clearSession();
    window.location.assign(`/reader/account?returnTo=${encodeURIComponent(window.location.pathname)}`);
  };
  const downloadPackage = async (value, format) => {
    const packageId = value?.id;
    if (!guidPattern.test(packageId || "")) {
      setPackageStatus("下载包标识无效，请刷新页面重试。");
      return;
    }
    const response = await client.apiFetch(`/api/v1/admin/packages/${encodeURIComponent(packageId)}/download`);
    if (response?.ok) {
      const downloaded = await client.downloadResponse(
        response,
        value?.artifactFileName || `inkflow-book.${format}`);
      setPackageStatus(downloaded ? "下载已开始。" : "暂时无法准备下载文件，请稍后重试。", downloaded);
      return;
    }
    if (response?.status === 401) {
      redirectToLogin();
      return;
    }
    setPackageStatus("下载暂时失败，请稍后重试。");
  };

  packageButton.addEventListener("click", async () => {
    const format = String(packageFormat.value || "epub").toLowerCase();
    if (!["epub", "txt", "zip"].includes(format)) return;
    packageButton.disabled = true;
    setPackageStatus("正在检查已有打包…", true);
    const response = await client.apiFetch("/api/v1/admin/packages?limit=100");
    if (response?.status === 401) {
      redirectToLogin();
      return;
    }
    if (!response?.ok) {
      setPackageStatus("暂时无法读取下载任务，请稍后重试。");
      packageButton.disabled = false;
      return;
    }
    const payload = await response.json().catch(() => null);
    const values = Array.isArray(payload?.data) ? payload.data : [];
    const matches = values.filter((value) =>
      String(value?.canonicalBookId).toLowerCase() === bookId.toLowerCase() &&
      String(value?.format).toLowerCase() === format);
    const completed = matches.find((value) =>
      String(value?.status).toLowerCase() === "completed" && value?.artifactFileName);
    if (completed) {
      setPackageStatus("已有完成包，正在直接下载。", true);
      await downloadPackage(completed, format);
      packageButton.disabled = false;
      return;
    }
    if (matches.some((value) => ["queued", "running"].includes(String(value?.status).toLowerCase()))) {
      setPackageStatus("已有下载任务，正在打开下载页。", true);
      window.location.assign("/admin/operations#packages");
      return;
    }
    const createResponse = await client.apiFetch(`/api/v1/admin/books/${encodeURIComponent(bookId)}/packages`, {
      method: "POST",
      body: JSON.stringify({ format })
    });
    const created = createResponse ? await createResponse.json().catch(() => null) : null;
    if (createResponse?.status === 401) {
      redirectToLogin();
      return;
    }
    if (createResponse?.ok && created?.package?.id) {
      setPackageStatus("下载任务已创建，正在打开下载页。", true);
      window.location.assign("/admin/operations#packages");
      return;
    }
    setPackageStatus(created?.error === "package.no-chapters"
      ? "这本书还没有可打包的正文。"
      : "下载任务创建失败，请稍后重试。");
    packageButton.disabled = false;
  });
})();
