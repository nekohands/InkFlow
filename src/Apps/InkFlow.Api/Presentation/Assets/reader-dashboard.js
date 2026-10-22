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
