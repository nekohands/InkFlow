(() => {
  const client = window.InkFlowReader;
  const authStatus = document.getElementById("operations-auth-status");
  const refreshButton = document.getElementById("operations-refresh");
  const content = document.getElementById("operations-content");
  const summary = document.getElementById("operations-summary");
  const actionDialog = document.getElementById("operations-action-dialog");
  const actionForm = document.getElementById("operations-action-form");
  const actionTitle = document.getElementById("operations-action-title");
  const actionDescription = document.getElementById("operations-action-description");
  const actionStatus = document.getElementById("operations-action-status");
  const actionSuggestions = document.getElementById("operations-action-suggestions");
  const actionReason = document.getElementById("operations-action-reason");
  const actionSubmit = document.getElementById("operations-action-submit");
  const collectionForm = document.getElementById("operations-collection-form");
  const collectionUrl = document.getElementById("operations-collection-url");
  const collectionSubmit = document.getElementById("operations-collection-submit");
  const collectionStatus = document.getElementById("operations-collection-status");
  const collectionList = document.getElementById("operations-collection-list");
  const packageForm = document.getElementById("operations-package-form");
  const packageBookId = document.getElementById("operations-package-book-id");
  const packageFormat = document.getElementById("operations-package-format");
  const packageSubmit = document.getElementById("operations-package-submit");
  const packageStatus = document.getElementById("operations-package-status");
  const packageList = document.getElementById("operations-package-list");
  const policyForm = document.getElementById("operations-policy-form");
  const policyBookId = document.getElementById("operations-policy-book-id");
  const policySubmit = document.getElementById("operations-policy-submit");
  const policyStatus = document.getElementById("operations-policy-status");
  const policyList = document.getElementById("operations-policy-list");
  const historyPanel = document.getElementById("operations-history");
  const historyStatus = document.getElementById("operations-history-status");
  const historyTable = document.getElementById("operations-history-table");
  const historyBody = document.getElementById("operations-history-body");
  const historyRefresh = document.getElementById("operations-history-refresh");
  const historyMore = document.getElementById("operations-history-more");
  const operationsTabs = Array.from(document.querySelectorAll("[data-operations-tab]"));
  const operationsRoleElements = Array.from(document.querySelectorAll("[data-operations-roles]"));
  const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
  const operationRoles = new Set(["Reader", "Operator", "Administrator"]);
  const capabilities = new Set(["Search", "BookInfo", "Toc", "Content", "Update"]);
  const runStatusOrder = ["pending", "running", "paused", "stopping", "completed", "failed", "stopped", "cancelled"];
  const stableErrors = {
    sources_unavailable: "来源清单暂时不可用。",
    source_health_unavailable: "部分来源健康记录暂时不可用。",
    crawler_unavailable: "死信队列暂时不可用。",
    consistency_unavailable: "一致性检查暂时不可用。"
  };
  let currentRole = null;
  let pendingAction = null;
  let loading = false;
  let historyLoading = false;
  let historyCursor = null;
  let collectionLoading = false;
  let packageLoading = false;
  let policyLoading = false;
  let collectionHasActive = false;
  let packageHasActive = false;
  let taskPollTimer = null;
  let activeCollectionStatus = null;
  const packageValues = new Map();
  const detailsOpenState = new Map();

  const captureDetailsOpenState = () => {
    for (const element of document.querySelectorAll("[data-operations-details-key]")) {
      detailsOpenState.set(element.dataset.operationsDetailsKey, element.open);
    }
  };
  const restoreDetailsOpenState = (element, key, fallback = false) => {
    element.dataset.operationsDetailsKey = key;
    element.open = detailsOpenState.has(key) ? detailsOpenState.get(key) : fallback;
  };

  const text = (value, fallback = "") => {
    if (value === null || value === undefined) return fallback;
    const normalized = String(value).trim();
    return normalized ? normalized : fallback;
  };
  const asGuid = (value) => typeof value === "string" && guidPattern.test(value) ? value : null;
  const asBoolean = (value) => value === true || value === "true";
  const isSourceEnabled = (value) => value !== false && String(value).toLowerCase() !== "false";
  const asNumber = (value, fallback = 0) => {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : fallback;
  };
  const dateLabel = (value) => {
    const parsed = new Date(value);
    return Number.isNaN(parsed.valueOf()) ? "时间未知" : parsed.toLocaleString();
  };
  const sizeLabel = (value) => {
    const bytes = Math.max(0, Math.trunc(asNumber(value)));
    if (bytes < 1024) return bytes + " B";
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + " KiB";
    return (bytes / (1024 * 1024)).toFixed(1) + " MiB";
  };
  const roleLabel = (role) => ({ Operator: "运营", Administrator: "管理员", Reader: "读者" }[role] || "未知角色");
  const sectionLabel = (status) => ({ ready: "正常", partial: "部分可用", unavailable: "不可用" }[String(status).toLowerCase()] || "未知");
  const healthLabel = (status) => ({ Unknown: "待探测", Healthy: "健康", Degraded: "降级", Unhealthy: "不健康", Disabled: "已停用" }[status] || text(status, "未知"));
  const runStatusLabel = (status) => ({ pending: "等待中", running: "采集中", paused: "已暂停", stopping: "停止中", completed: "已完成", failed: "失败", stopped: "已停止", cancelled: "已取消" }[String(status).toLowerCase()] || text(status, "未知"));
  const runStageLabel = (stage) => ({ bookinfo: "书籍信息", toc: "目录", content: "正文" }[String(stage).toLowerCase()] || text(stage, "未知阶段"));
  const packageStatusLabel = (status) => ({ queued: "排队中", running: "打包中", completed: "已完成", failed: "失败", expired: "已过期" }[String(status).toLowerCase()] || text(status, "未知"));
  const controlLabel = (action) => ({ pause: "暂停", resume: "恢复", stop: "停止", cancel: "取消" }[String(action).toLowerCase()] || "执行");
  const policyActionLabel = (action) => ({ takedown: "下架", restore: "恢复" }[String(action).toLowerCase()] || "执行");
  const reasonSuggestionsFor = (action) => {
    const actionName = String(action?.action || "");
    if (actionName === "run-control") {
      return ({
        pause: ["临时暂停采集，稍后继续", "需要核查来源或任务状态", "暂时释放采集资源"],
        resume: ["已确认来源状态，继续采集", "维护完成，恢复任务执行", "补充资源后继续执行"],
        stop: ["本次采集暂不再继续", "采集内容已改用其他来源", "需要重新安排采集任务"],
        cancel: ["任务已不再需要继续执行", "已确认本次采集结果无需保留", "任务重复，保留其他运行即可"]
      }[String(action?.controlAction || "").toLowerCase()] || []);
    }
    if (actionName === "content-policy") {
      return action?.policyAction === "takedown"
        ? ["依据内容治理要求暂时下架", "收到版权或合规处理请求", "待复核期间暂时隐藏内容"]
        : ["复核完成，恢复内容展示", "下架原因已处理，恢复公开访问", "确认内容符合展示要求"];
    }
    return ({
      replay: ["上游临时失败，人工确认后重放", "网络或服务短暂异常，重新尝试", "来源问题已修复，重新投递任务"],
      "cancelled-cleanup": ["按计划清理已取消任务", "已确认取消任务无需保留", "清理历史任务以释放运维空间"],
      "run-delete": ["按计划清理失败任务", "已确认失败任务无需保留", "清理失败任务及其残留记录"],
      "source-disable": ["来源当前不可用，暂时停用", "来源维护中，暂时停止使用", "发现来源异常，先暂停调度"],
      "source-enable": ["来源已恢复，重新启用", "维护完成，恢复来源调度", "已确认来源可以继续使用"],
      disable: ["能力当前异常，暂时停用", "能力维护中，暂时停止调用", "连续失败，先暂停该能力"],
      enable: ["能力已恢复，重新启用", "维护完成，恢复能力调用", "已确认可以继续探测该能力"]
    }[actionName] || ["按运维计划执行该操作", "已确认当前状态后执行", "由管理员手动发起"]).slice(0, 3);
  };
  const renderReasonSuggestions = (action) => {
    if (!actionSuggestions) return;
    const suggestions = reasonSuggestionsFor(action);
    actionSuggestions.replaceChildren();
    actionSuggestions.hidden = suggestions.length === 0;
    if (!suggestions.length) return;
    actionSuggestions.append(node("legend", null, "常用理由"));
    for (const suggestion of suggestions) {
      const button = node("button", "button operations-dialog__suggestion", suggestion);
      button.type = "button";
      button.dataset.reasonSuggestion = suggestion;
      button.setAttribute("data-reason-suggestion", suggestion);
      button.addEventListener("click", () => {
        actionReason.value = suggestion;
        actionReason.focus();
      });
      actionSuggestions.append(button);
    }
    actionReason.value = suggestions[0];
  };
  const operationsTabFromHash = () => {
    const candidate = window.location.hash.slice(1);
    return operationsTabs.some((tab) => !tab.hidden && tab.dataset.operationsTab === candidate)
      ? candidate
      : operationsTabs.find((tab) => !tab.hidden)?.dataset.operationsTab || "collection";
  };
  const selectOperationsTab = (name, focus = false, updateHash = false) => {
    const selected = operationsTabs.find((tab) => !tab.hidden && tab.dataset.operationsTab === name)
      || operationsTabs.find((tab) => !tab.hidden);
    if (!selected) return;
    const selectedName = selected.dataset.operationsTab;
    for (const tab of operationsTabs) {
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
  const statusTone = (value) => {
    const normalized = String(value || "").toLowerCase();
    if (["ready", "healthy", "replayed", "resolved", "completed"].includes(normalized)) return "ready";
    if (["partial", "degraded", "unknown", "opened", "pending", "running", "paused", "stopping", "queued"].includes(normalized)) return "partial";
    if (["unavailable", "unhealthy", "disabled", "issues_found", "error", "failed", "cancelled"].includes(normalized)) return "danger";
    return "neutral";
  };
  const badge = (value, label = value) => {
    const node = document.createElement("span");
    node.className = "operations-badge operations-badge--" + statusTone(value);
    node.textContent = text(label, "未知");
    return node;
  };
  const node = (tag, className, value) => {
    const element = document.createElement(tag);
    if (className) element.className = className;
    if (value !== undefined) element.textContent = value;
    return element;
  };
  const setNotice = (element, message, tone = "neutral") => {
    if (!element) return;
    element.className = "notice operations-auth operations-auth--" + tone;
    element.replaceChildren(document.createTextNode(message));
  };
  const appendLink = (element, href, label) => {
    const link = document.createElement("a");
    link.href = href;
    link.textContent = label;
    element.append(" ", link);
  };
  const hideContent = () => {
    if (content) content.hidden = true;
    if (refreshButton) refreshButton.disabled = true;
  };
  const showLogin = (message) => {
    hideContent();
    setNotice(authStatus, message, "neutral");
    appendLink(authStatus, "/reader/account", "前往登录");
  };
  const showForbidden = () => {
    hideContent();
    setNotice(authStatus, "当前账户没有采集与下载中心权限。", "danger");
  };
  const applyRoleSurface = () => {
    const isReader = currentRole === "Reader";
    for (const element of operationsRoleElements) {
      const allowedRoles = String(element.dataset.operationsRoles || "")
        .split(",")
        .map((value) => value.trim())
        .filter(Boolean);
      element.hidden = !allowedRoles.includes(currentRole);
    }
    const title = document.getElementById("operations-title");
    if (title) title.textContent = isReader ? "采集与下载" : "运维中心";
    const sourceTab = document.getElementById("operations-tab-sources");
    if (sourceTab) sourceTab.textContent = isReader ? "来源状态" : "来源与死信";
    selectOperationsTab(operationsTabFromHash());
  };
  const canOperateSources = () => currentRole !== "Reader";
  const errorMessage = (status, payload) => {
    if (status === 400) return "请求理由无效，请填写 1–512 个字符。";
    if (status === 401) return "会话已失效，请重新登录。";
    if (status === 403) return "当前账户没有执行该操作的权限。";
    if (status === 404) return "目标记录不存在，可能已被移除。";
    if (status === 409) return "目标状态已经改变或已经重放，请刷新快照。";
    return text(payload?.error, "操作暂时失败，请稍后重试。");
  };
  const renderSectionStatus = (element, section, hasData, emptyMessage) => {
    if (!element) return;
    element.replaceChildren();
    const status = text(section?.status, "unavailable").toLowerCase();
    let message = "";
    if (status === "ready" && !hasData) message = emptyMessage;
    if (status !== "ready") {
      message = sectionLabel(status) + "：" + (stableErrors[section?.error] || "暂时无法读取，请刷新后重试。");
    }
    if (message) {
      const tone = status === "ready" ? "ready" : statusTone(status);
      element.append(node("p", "operations-state operations-state--" + tone, message));
    }
  };
  const updateSummaryItem = (id, value, status) => {
    const target = document.getElementById(id);
    if (!target) return;
    target.replaceChildren();
    target.append(badge(status, value));
  };
  const renderSummary = (snapshot) => {
    if (!summary) return;
    summary.hidden = false;
    updateSummaryItem("operations-summary-status", sectionLabel(snapshot?.status), snapshot?.status);
    updateSummaryItem("operations-summary-sources", sectionLabel(snapshot?.sources?.status), snapshot?.sources?.status);
    updateSummaryItem("operations-summary-crawler", sectionLabel(snapshot?.crawler?.status), snapshot?.crawler?.status);
    updateSummaryItem("operations-summary-consistency", sectionLabel(snapshot?.consistency?.status), snapshot?.consistency?.status);
    const generated = document.getElementById("operations-generated-at");
    if (generated) generated.textContent = dateLabel(snapshot?.generatedAt);
  };
  const createActionButton = (label, action, sourceId, capability, ariaLabel) => {
    const button = node("button", "button", label);
    button.type = "button";
    button.dataset.action = action;
    button.dataset.sourceId = sourceId;
    button.dataset.capability = capability;
    button.setAttribute("aria-label", ariaLabel);
    button.addEventListener("click", () => openActionDialog({
      action,
      sourceId,
      capability,
      title: sourceId + " / " + capability,
      detail: action === "disable"
        ? "停用后，该来源的这项能力不会再被候选流程使用。"
        : "恢复只会回到待探测状态，不会伪造健康；系统需等待下一次真实探针。",
    }));
    return button;
  };
  const renderSources = (section) => {
    const list = document.getElementById("operations-sources-list");
    const status = document.getElementById("operations-sources-status");
    const values = Array.isArray(section?.data) ? section.data : [];
    renderSectionStatus(status, section, values.length > 0, "暂无已登记来源或能力健康记录。");
    captureDetailsOpenState();
    list?.replaceChildren();
    for (const source of values) {
      const sourceId = text(source?.sourceId, "unknown-source");
      const card = node("details", "operations-card");
      restoreDetailsOpenState(card, "source:" + sourceId);
      const header = node("summary", "operations-card__header");
      const headingWrap = node("div");
      headingWrap.append(node("h3", null, text(source?.displayName, "未命名来源")));
      headingWrap.append(node("p", "operations-card__id", sourceId));
      const sourceEnabled = isSourceEnabled(source?.isEnabled);
      const badges = node("div", "operations-card__badges");
      badges.append(
        badge(source?.status, sectionLabel(source?.status)),
        badge(sourceEnabled ? "ready" : "disabled", sourceEnabled ? "已启用" : "已停用"));
      header.append(headingWrap, badges);
      card.append(header);
      if (source?.error) card.append(node("p", "operations-card__error", stableErrors[source.error] || "来源健康记录暂时不可用。"));
      const sourceActions = node("div", "operations-source__actions");
      if (canOperateSources() && sourceId !== "unknown-source") {
        const sourceAction = sourceEnabled ? "source-disable" : "source-enable";
        const sourceButton = node("button", "button", sourceEnabled ? "停用来源" : "恢复来源");
        sourceButton.type = "button";
        sourceButton.addEventListener("click", () => openActionDialog({
          action: sourceAction,
          sourceId,
          title: (sourceEnabled ? "停用来源 · " : "恢复来源 · ") + text(source?.displayName, sourceId),
          detail: sourceEnabled
            ? "停用后，该来源不会再参与地址解析、搜索、追更调度或采集执行；已有来源数据会保留。"
            : "恢复后，该来源重新具备执行资格；各项能力健康状态不会被重置。",
        }));
        sourceActions.append(sourceButton);
      }
      card.append(sourceActions);
      const healthList = node("ul", "operations-health-list");
      const healthValues = Array.isArray(source?.capabilities) ? source.capabilities : [];
      if (!healthValues.length) {
        healthList.append(node("li", "operations-health-row__meta", "暂无能力健康记录。"));
      }
      for (const health of healthValues) {
        const capability = text(health?.capability, "Unknown");
        const row = node("li", "operations-health-row");
        const top = node("div", "operations-health-row__top");
        top.append(node("span", "operations-health-row__name", capability), badge(health?.status, healthLabel(health?.status)));
        row.append(top);
        const failureCount = Math.max(0, Math.trunc(asNumber(health?.consecutiveFailures)));
        row.append(node("div", "operations-health-row__meta", failureCount + " 次连续失败 · 更新于 " + dateLabel(health?.updatedAt)));
        if (health?.lastFailureReason) {
          row.append(node("div", "operations-health-row__reason", "最近失败： " + text(health.lastFailureReason)));
        }
        const actions = node("div", "operations-health-row__actions");
        if (canOperateSources() && capabilities.has(capability) && sourceId !== "unknown-source") {
          const available = asBoolean(health?.isAvailable);
          actions.append(createActionButton(
            available ? "停用能力" : "恢复能力",
            available ? "disable" : "enable",
            sourceId,
            capability,
            (available ? "停用" : "恢复") + sourceId + "的" + capability + "能力"));
        }
        row.append(actions);
        healthList.append(row);
      }
      card.append(healthList);
      list?.append(card);
    }
  };
  const renderCrawler = (section) => {
    const status = document.getElementById("operations-crawler-status");
    const table = document.getElementById("operations-crawler-table");
    const body = document.getElementById("operations-crawler-body");
    const data = section?.data;
    const values = Array.isArray(data?.deadLetters) ? data.deadLetters : [];
    renderSectionStatus(status, section, values.length > 0, "当前没有待处理死信。");
    body?.replaceChildren();
    if (data && asBoolean(data.hasMoreDeadLetters)) {
      status?.append(node("p", "operations-state operations-state--partial", "本次显示 " + values.length + " 条，仍有更多死信未展示。"));
    }
    if (table) table.hidden = values.length === 0;
    for (const deadLetter of values) {
      const row = node("tr");
      const source = node("td");
      source.append(node("strong", null, text(deadLetter?.sourceId, "未知来源")));
      source.append(node("div", "operations-table__meta", asGuid(deadLetter?.id) || "无有效死信 ID"));
      const reason = node("td", "operations-table__reason", text(deadLetter?.reason, "未提供失败原因"));
      const attempts = node("td", null, String(Math.max(0, Math.trunc(asNumber(deadLetter?.attemptCount)))));
      const time = node("td", "operations-table__meta", dateLabel(deadLetter?.deadLetteredAt));
      const state = node("td");
      const replayed = asBoolean(deadLetter?.isReplayed);
      state.append(badge(replayed ? "replayed" : "unknown", replayed ? "已重放" : "待处理"));
      if (replayed && asGuid(deadLetter?.replayTaskId)) {
        state.append(node("div", "operations-table__meta", "任务 " + asGuid(deadLetter.replayTaskId)));
      }
      const action = node("td");
      const deadLetterId = asGuid(deadLetter?.id);
      if (!replayed && deadLetterId) {
        const button = node("button", "button", "填写理由并重放");
        button.type = "button";
        button.addEventListener("click", () => openActionDialog({
          action: "replay",
          deadLetterId,
          title: "重放死信 " + deadLetterId,
          detail: "原死信保持不变，系统只会创建一条新的 Pending 重放任务。失败原因：" + text(deadLetter?.reason, "未提供") + "；尝试次数：" + Math.max(0, Math.trunc(asNumber(deadLetter?.attemptCount))) + "。",
        }));
        action.append(button);
      } else if (replayed) {
        action.append(node("span", "operations-table__meta", "无需重复操作"));
      } else {
        action.append(node("span", "operations-table__meta", "缺少有效 ID"));
      }
      row.append(source, reason, attempts, time, state, action);
      body?.append(row);
    }
  };
  const setPanelStatus = (element, message, tone = "neutral") => {
    if (!element) return;
    element.replaceChildren();
    if (message) element.append(node("p", "operations-state operations-state--" + tone, message));
  };
  const downloadPackageArtifact = async (packageValue, trigger) => {
    const packageId = asGuid(packageValue?.id);
    if (!packageId || !client?.isSignedIn()) return;
    if (trigger) trigger.disabled = true;
    setPanelStatus(packageStatus, "正在准备下载…");
    const response = await client.apiFetch("/api/v1/admin/packages/" + packageId + "/download");
    if (response?.ok && await client.downloadResponse(response, text(packageValue?.artifactFileName, "inkflow-download"))) {
      setPanelStatus(packageStatus, "下载已开始。", "ready");
    } else if (response?.status === 401) {
      client.clearSession();
      showLogin("会话已失效，请重新登录后访问运维中心。");
    } else {
      setPanelStatus(packageStatus, "下载暂时失败，请稍后重试。", "danger");
    }
    if (trigger) trigger.disabled = false;
  };
  const scheduleTaskPoll = () => {
    if (taskPollTimer !== null) return;
    if (!collectionHasActive && !packageHasActive) return;
    if (!operationRoles.has(currentRole) || !client?.isSignedIn()) return;
    taskPollTimer = window.setTimeout(async () => {
      taskPollTimer = null;
      await Promise.all([loadCollectionRuns(), loadPackages()]);
      scheduleTaskPoll();
    }, 4000);
  };
  const openPackageForBook = (bookId) => {
    if (!packageBookId || !asGuid(bookId)) return;
    packageBookId.value = bookId;
    packageBookId.focus();
    packageBookId.scrollIntoView({ behavior: "smooth", block: "center" });
  };
  const rerunCollection = (run) => {
    const value = text(run?.inputUrl, "");
    if (!value || !collectionUrl) return;
    collectionUrl.value = value;
    collectionUrl.focus();
    collectionUrl.scrollIntoView({ behavior: "smooth", block: "center" });
  };
  const createRunControlButton = (run, action, label) => {
    const button = node("button", "button", label);
    button.type = "button";
    button.addEventListener("click", () => openActionDialog({
      action: "run-control",
      runId: asGuid(run?.id),
      controlAction: action,
      title: label + " · " + text(run?.sourceId, "未知来源"),
      detail: "该命令会记录操作理由。暂停可恢复；停止和取消完成后不可继续。",
    }));
    return button;
  };
  const renderCollectionRuns = (values) => {
    const runs = Array.isArray(values) ? values : [];
    const validRuns = runs.filter((run) => asGuid(run?.id));
    collectionHasActive = validRuns.some((run) => ["pending", "running", "stopping"].includes(String(run?.status || "").toLowerCase()));
    captureDetailsOpenState();
    collectionList?.replaceChildren();
    if (validRuns.length === 0) {
      activeCollectionStatus = null;
      setPanelStatus(collectionStatus, "暂无采集运行。请输入一本已登记公共来源的书籍地址开始。", "ready");
      return;
    }
    const grouped = new Map();
    for (const run of validRuns) {
      const status = text(run?.status, "unknown").toLowerCase();
      if (!grouped.has(status)) grouped.set(status, []);
      grouped.get(status).push(run);
    }
    const statuses = [
      ...runStatusOrder.filter((status) => grouped.has(status)),
      ...Array.from(grouped.keys()).filter((status) => !runStatusOrder.includes(status)),
    ];
    setPanelStatus(collectionStatus, "已加载 " + validRuns.length + " 个采集任务，按状态分类。", "ready");
    const runTabs = node("nav", "operations-run-tabs");
    runTabs.setAttribute("role", "tablist");
    runTabs.setAttribute("aria-label", "采集任务状态");
    const runPanels = node("div", "operations-run-tab-panels");
    const statusTabs = [];
    collectionList?.append(runTabs, runPanels);
    for (const [index, status] of statuses.entries()) {
      const groupRuns = grouped.get(status) || [];
      const tabId = "operations-collection-tab-" + index;
      const panelId = "operations-collection-panel-" + index;
      const tab = node("button", "operations-run-tab");
      tab.type = "button";
      tab.id = tabId;
      tab.dataset.collectionStatus = status;
      tab.setAttribute("data-collection-status", status);
      tab.setAttribute("role", "tab");
      tab.setAttribute("aria-controls", panelId);
      tab.setAttribute("aria-selected", "false");
      tab.append(node("span", null, runStatusLabel(status)), badge(status, groupRuns.length + " 个"));
      runTabs.append(tab);
      statusTabs.push(tab);
      const group = node("section", "operations-run-tab-panel");
      group.id = panelId;
      group.setAttribute("role", "tabpanel");
      group.setAttribute("aria-labelledby", tabId);
      group.hidden = true;
      const groupHeader = node("header", "operations-run-group__header");
      const groupActions = node("div", "operations-run-group__actions");
      groupActions.append(badge(status, groupRuns.length + " 个"));
      if (status === "cancelled" && currentRole !== "Reader") {
        const cleanupButton = node("button", "button", "清理已取消任务");
        cleanupButton.type = "button";
        cleanupButton.setAttribute("aria-label", "清理所有已取消采集任务");
        cleanupButton.addEventListener("click", () => openActionDialog({
          action: "cancelled-cleanup",
          title: "清理已取消采集任务",
          detail: "该操作不可恢复，将删除列表中的所有已取消运行及其采集子任务、死信记录；书籍、正文和审计记录会保留。请填写理由。",
        }));
        groupActions.append(cleanupButton);
      }
      groupHeader.append(node("h3", null, runStatusLabel(status)), groupActions);
      const groupList = node("ul", "operations-run-list operations-run-list--group");
      group.append(groupHeader, groupList);
      for (const run of groupRuns) {
        const runId = asGuid(run?.id);
        const item = node("li");
        const card = node("details", "operations-run-card");
        restoreDetailsOpenState(card, "run:" + runId);
        const header = node("summary", "operations-run-card__summary");
        const titleWrap = node("span", "operations-run-card__summary-copy");
        const bookTitle = text(run?.bookTitle, "");
        const sourceId = text(run?.sourceId, "未知来源");
        const externalBookId = text(run?.externalBookId, "未知书籍");
        titleWrap.append(node("span", "operations-run-card__summary-title", bookTitle ? "书名：" + bookTitle : sourceId + " · " + externalBookId));
        titleWrap.append(node("span", "operations-run-card__summary-meta", "采集地址：" + text(run?.inputUrl, "未记录")));
        header.append(titleWrap, badge(run?.status, runStatusLabel(run?.status)));
        card.append(header);
        card.append(node("p", "operations-run-card__meta", "来源：" + sourceId + " · 外部书籍 ID：" + externalBookId + " · 运行 " + runId + " · 阶段：" + runStageLabel(run?.stage)));
        const progressWrap = node("div", "operations-run-card__progress");
        const progress = document.createElement("progress");
        progress.max = 100;
        progress.setAttribute("aria-valuemin", "0");
        progress.setAttribute("aria-valuemax", "100");
        const knownProgress = run?.progressPercent !== null && run?.progressPercent !== undefined;
        if (knownProgress) {
          const percent = Math.max(0, Math.min(100, Math.trunc(asNumber(run?.progressPercent))));
          progress.value = percent;
          progress.setAttribute("aria-valuenow", String(percent));
        } else {
          progress.removeAttribute("value");
          progress.removeAttribute("aria-valuenow");
        }
        progress.setAttribute("aria-label", "采集进度");
        progressWrap.append(progress);
        const progressLabel = knownProgress
          ? Math.max(0, Math.min(100, Math.trunc(asNumber(run?.progressPercent)))) + "%"
          : "正在发现总量";
        progressWrap.append(node("p", "operations-run-card__meta", runStatusLabel(run?.status) + " · " + progressLabel + " · 已完成 " + Math.max(0, Math.trunc(asNumber(run?.completedTaskCount))) + " / " + Math.max(0, Math.trunc(asNumber(run?.totalTaskCount))) + " 个任务"));
        progressWrap.append(node(
          "p",
          "operations-run-card__meta",
          "进行中 " + Math.max(0, Math.trunc(asNumber(run?.inFlightTaskCount))) +
          " · 待处理 " + Math.max(0, Math.trunc(asNumber(run?.pendingTaskCount))) +
          " · 失败 " + Math.max(0, Math.trunc(asNumber(run?.failedTaskCount))) +
          " · 取消 " + Math.max(0, Math.trunc(asNumber(run?.cancelledTaskCount))) +
          " · 剩余 " + Math.max(0, Math.trunc(asNumber(run?.remainingTaskCount)))));
        card.append(progressWrap);
        if (run?.lastError) card.append(node("p", "operations-run-card__error", "最近错误：" + text(run.lastError)));
        const canonicalId = asGuid(run?.canonicalBookId);
        if (canonicalId) card.append(node("p", "operations-run-card__meta", "正典书 ID：" + canonicalId));
        const actions = node("div", "operations-run-card__actions");
        if (currentRole !== "Reader" && ["pending", "running"].includes(status)) {
          actions.append(createRunControlButton(run, "pause", "暂停"));
          actions.append(createRunControlButton(run, "stop", "停止"));
          actions.append(createRunControlButton(run, "cancel", "取消"));
        } else if (currentRole !== "Reader" && status === "paused") {
          actions.append(createRunControlButton(run, "resume", "恢复"));
          actions.append(createRunControlButton(run, "stop", "停止"));
          actions.append(createRunControlButton(run, "cancel", "取消"));
        } else if (currentRole !== "Reader" && status === "stopping") {
          actions.append(createRunControlButton(run, "cancel", "立即取消"));
        } else if (currentRole !== "Reader" && status === "stopped") {
          actions.append(createRunControlButton(run, "cancel", "取消"));
        }
        if (currentRole !== "Reader" && status === "failed") {
          const deleteButton = node("button", "button", "删除失败任务");
          deleteButton.type = "button";
          deleteButton.addEventListener("click", () => openActionDialog({
            action: "run-delete",
            runId,
            title: "删除失败采集任务 · " + runId,
            detail: "该操作不可恢复，只删除这次失败运行及其采集子任务、死信记录，不会删除书籍、正文或审计记录。请填写理由。",
          }));
          actions.append(deleteButton);
        }
        if (["failed", "stopped", "cancelled"].includes(status)) {
          const rerunButton = node("button", "button", status === "stopped" ? "重试" : "重新开始");
          rerunButton.type = "button";
          rerunButton.addEventListener("click", () => rerunCollection(run));
          actions.append(rerunButton);
        }
        if (canonicalId) {
          const packageButton = node("button", "button", "为此书打包");
          packageButton.type = "button";
          packageButton.addEventListener("click", () => openPackageForBook(canonicalId));
          actions.append(packageButton);
        }
        card.append(actions);
        item.append(card);
        groupList.append(item);
      }
      runPanels.append(group);
    }
    const selectCollectionStatus = (name, focus = false) => {
      const selected = statusTabs.find((tab) => tab.dataset.collectionStatus === name) || statusTabs[0];
      if (!selected) return;
      activeCollectionStatus = selected.dataset.collectionStatus;
      for (const tab of statusTabs) {
        const active = tab === selected;
        tab.setAttribute("aria-selected", active ? "true" : "false");
        tab.tabIndex = active ? 0 : -1;
        const panel = document.getElementById(tab.getAttribute("aria-controls") || "");
        if (panel) panel.hidden = !active;
      }
      if (focus) selected.focus();
    };
    for (const [index, tab] of statusTabs.entries()) {
      tab.addEventListener("click", () => selectCollectionStatus(tab.dataset.collectionStatus, false));
      tab.addEventListener("keydown", (event) => {
        const offset = event.key === "ArrowRight" || event.key === "ArrowDown"
          ? 1
          : event.key === "ArrowLeft" || event.key === "ArrowUp"
            ? -1
            : event.key === "Home"
              ? -index
              : event.key === "End"
                ? statusTabs.length - 1 - index
                : 0;
        if (!offset || statusTabs.length < 2) return;
        event.preventDefault();
        const nextIndex = (index + offset + statusTabs.length) % statusTabs.length;
        selectCollectionStatus(statusTabs[nextIndex].dataset.collectionStatus, true);
      });
    }
    selectCollectionStatus(activeCollectionStatus || statuses[0]);
  };
  const loadCollectionRuns = async () => {
    if (collectionLoading || !client?.isSignedIn() || !operationRoles.has(currentRole)) return;
    collectionLoading = true;
    const allRuns = [];
    const seenCursors = new Set();
    let cursor = null;
    while (true) {
      const query = new URLSearchParams({ limit: "100" });
      if (cursor) query.set("cursor", cursor);
      const response = await client.apiFetch("/api/v1/admin/collection-runs?" + query.toString());
      if (response === null) {
        setPanelStatus(collectionStatus, "采集运行暂时不可用，请稍后刷新。", "danger");
        collectionLoading = false;
        return;
      }
      if (response.status === 401) {
        client.clearSession();
        showLogin("会话已失效，请重新登录后访问运维中心。");
        collectionLoading = false;
        return;
      }
      if (response.status === 403) {
        setPanelStatus(collectionStatus, "当前账户没有读取采集运行的权限。", "danger");
        collectionLoading = false;
        return;
      }
      const payload = await response.json().catch(() => null);
      if (!response.ok) {
        setPanelStatus(collectionStatus, errorMessage(response.status, payload), "danger");
        collectionLoading = false;
        return;
      }
      if (Array.isArray(payload?.data)) allRuns.push(...payload.data);
      const nextCursor = text(payload?.nextCursor, "") || null;
      if (!nextCursor) break;
      if (seenCursors.has(nextCursor)) {
        setPanelStatus(collectionStatus, "采集运行分页游标异常，请稍后刷新。", "danger");
        collectionLoading = false;
        return;
      }
      seenCursors.add(nextCursor);
      cursor = nextCursor;
    }
    renderCollectionRuns(allRuns);
    collectionLoading = false;
    scheduleTaskPoll();
  };
  const renderPackages = () => {
    packageHasActive = Array.from(packageValues.values()).some((value) => ["queued", "running"].includes(String(value?.status || "").toLowerCase()));
    captureDetailsOpenState();
    packageList?.replaceChildren();
    if (!packageValues.size) {
      setPanelStatus(packageStatus, "暂无打包任务。可填写采集运行卡片中的正典书 ID。", "ready");
      return;
    }
    setPanelStatus(packageStatus, "已加载 " + packageValues.size + " 个打包任务。", "ready");
    for (const packageValue of packageValues.values()) {
      const packageId = asGuid(packageValue?.id);
      if (!packageId) continue;
      const item = node("li");
      const card = node("details", "operations-package-card");
      const status = String(packageValue?.status || "").toLowerCase();
      restoreDetailsOpenState(card, "package:" + packageId, ["queued", "running"].includes(status));
      const header = node("summary", "operations-package-card__header");
      const titleWrap = node("span", "operations-package-card__summary-copy");
      titleWrap.append(node("span", "operations-package-card__summary-title", text(packageValue?.format, "未知格式").toUpperCase() + " · " + packageStatusLabel(packageValue?.status)));
      titleWrap.append(node("span", "operations-package-card__summary-meta", "任务 " + packageId + " · 书籍 " + (asGuid(packageValue?.canonicalBookId) || "无效 ID")));
      header.append(titleWrap, badge(packageValue?.status, packageStatusLabel(packageValue?.status)));
      card.append(header);
      const totalChapters = Math.max(0, Math.trunc(asNumber(packageValue?.totalChapterCount)));
      const progressKnown = totalChapters > 0;
      const progressLabel = progressKnown
        ? Math.max(0, Math.min(100, Math.trunc(asNumber(packageValue?.progressPercent)))) + "%"
        : "正在读取章节总量";
      const progressWrap = node("div", "operations-package-card__progress");
      const progress = document.createElement("progress");
      progress.max = 100;
      progress.setAttribute("aria-valuemin", "0");
      progress.setAttribute("aria-valuemax", "100");
      if (progressKnown) {
        const percent = Math.max(0, Math.min(100, Math.trunc(asNumber(packageValue?.progressPercent))));
        progress.value = percent;
        progress.setAttribute("aria-valuenow", String(percent));
      } else {
        progress.removeAttribute("value");
        progress.removeAttribute("aria-valuenow");
      }
      progress.setAttribute("aria-label", "打包进度");
      progressWrap.append(progress);
      progressWrap.append(node("p", "operations-package-card__meta", progressLabel + " · 已完成 " + Math.max(0, Math.trunc(asNumber(packageValue?.completedChapterCount))) + " / " + totalChapters + " 章"));
      card.append(progressWrap);
      if (packageValue?.failureReason) card.append(node("p", "operations-package-card__error", "最近错误：" + text(packageValue.failureReason)));
      const actions = node("div", "operations-package-card__actions");
      if (status === "completed") {
        card.append(node("p", "operations-package-card__meta", "文件 " + text(packageValue?.artifactFileName, "未记录") + " · " + sizeLabel(packageValue?.artifactLength) + " · 有效至 " + dateLabel(packageValue?.expiresAt)));
        const button = node("button", "button", "下载 " + text(packageValue?.format, "书籍包").toUpperCase());
        button.type = "button";
        button.addEventListener("click", () => { void downloadPackageArtifact(packageValue, button); });
        actions.append(button);
      } else if (status === "expired") {
        card.append(node("p", "operations-package-card__meta", "下载文件已过期，无法继续下载。"));
      }
      card.append(actions);
      item.append(card);
      packageList?.append(item);
    }
    scheduleTaskPoll();
  };
  const renderPolicyRestricted = (message = "内容政策管理仅管理员可用。") => {
    policyList?.replaceChildren();
    setPanelStatus(policyStatus, message, "danger");
    if (policyBookId) policyBookId.disabled = true;
    if (policySubmit) policySubmit.disabled = true;
  };
  const renderPolicy = (values) => {
    if (policyBookId) policyBookId.disabled = false;
    if (policySubmit) policySubmit.disabled = false;
    captureDetailsOpenState();
    policyList?.replaceChildren();
    if (!Array.isArray(values) || values.length === 0) {
      setPanelStatus(policyStatus, "当前没有下架书籍。", "ready");
      return;
    }
    setPanelStatus(policyStatus, "已加载 " + values.length + " 条下架记录。", "ready");
    for (const value of values) {
      const bookId = asGuid(value?.canonicalBookId);
      if (!bookId || value?.isTakedown !== true) continue;
      const decision = value?.latestDecision;
      const item = node("li");
      const card = node("details", "operations-policy-card");
      restoreDetailsOpenState(card, "policy:" + bookId);
      const header = node("summary", "operations-policy-card__header");
      const titleWrap = node("div");
      titleWrap.append(node("p", "operations-policy-card__title", "正典书 " + bookId));
      titleWrap.append(node(
        "p",
        "operations-policy-card__meta",
        "下架于 " + dateLabel(decision?.createdAt)));
      header.append(titleWrap, badge("disabled", "已下架"));
      card.append(header);
      card.append(node(
        "p",
        "operations-policy-card__reason",
        "理由：" + text(decision?.reason, "未提供理由")));
      const actions = node("div", "operations-policy-card__actions");
      const restoreButton = node("button", "button", "恢复内容");
      restoreButton.type = "button";
      restoreButton.addEventListener("click", () => openActionDialog({
        action: "content-policy",
        policyAction: "restore",
        bookId,
        title: "恢复内容 · " + bookId,
        detail: "该命令会追加一条恢复决定并写入审计，不会删除历史下架记录。"
      }));
      actions.append(restoreButton);
      card.append(actions);
      item.append(card);
      policyList?.append(item);
    }
  };
  const loadPolicy = async () => {
    if (policyLoading || currentRole !== "Administrator" || !client?.isSignedIn()) return;
    policyLoading = true;
    setPanelStatus(policyStatus, "正在加载下架内容…");
    const response = await client.apiFetch("/api/v1/admin/content/takedowns?limit=50");
    if (response === null) {
      setPanelStatus(policyStatus, "下架记录暂时不可用，请稍后刷新。", "danger");
      policyLoading = false;
      return;
    }
    if (response.status === 401) {
      client.clearSession();
      showLogin("会话已失效，请重新登录后访问运维中心。");
      policyLoading = false;
      return;
    }
    if (response.status === 403) {
      renderPolicyRestricted("当前账户没有内容政策管理权限。");
      policyLoading = false;
      return;
    }
    const payload = await response.json().catch(() => null);
    if (!response.ok) {
      setPanelStatus(policyStatus, "下架记录请求失败，请刷新后重试。", "danger");
      policyLoading = false;
      return;
    }
    renderPolicy(payload);
    policyLoading = false;
  };
  const startPolicyTakedown = () => {
    const bookId = asGuid(String(policyBookId?.value || "").trim());
    if (!bookId) {
      setPanelStatus(policyStatus, "请输入有效的正典书 ID。", "danger");
      policyBookId?.focus();
      return;
    }
    openActionDialog({
      action: "content-policy",
      policyAction: "takedown",
      bookId,
      title: "下架内容 · " + bookId,
      detail: "该命令会使书目、目录、正文、Web Reader、公共搜索和 Legado 暂时不可见，并追加审计记录。"
    });
  };
  const loadPackages = async () => {
    if (packageLoading || !client?.isSignedIn() || !operationRoles.has(currentRole)) return;
    packageLoading = true;
    const response = await client.apiFetch("/api/v1/admin/packages?limit=50");
    if (response === null) {
      setPanelStatus(packageStatus, "打包任务暂时不可用，请稍后刷新。", "danger");
      packageLoading = false;
      return;
    }
    if (response.status === 401) {
      client.clearSession();
      showLogin("会话已失效，请重新登录后访问运维中心。");
      packageLoading = false;
      return;
    }
    if (response.status === 403) {
      setPanelStatus(packageStatus, "当前账户没有读取打包任务的权限。", "danger");
      packageLoading = false;
      return;
    }
    const payload = await response.json().catch(() => null);
    if (!response.ok) {
      setPanelStatus(packageStatus, "打包任务请求失败，请刷新后重试。", "danger");
      packageLoading = false;
      return;
    }
    packageValues.clear();
    for (const value of Array.isArray(payload?.data) ? payload.data : []) {
      const packageId = asGuid(value?.id);
      if (packageId) packageValues.set(packageId, value);
    }
    renderPackages();
    packageLoading = false;
  };
  const startCollection = async () => {
    const value = String(collectionUrl?.value || "").trim();
    if (!value || value.length > 2048) {
      setPanelStatus(collectionStatus, "请输入 1–2048 个字符的书籍地址。", "danger");
      collectionUrl?.focus();
      return;
    }
    if (collectionSubmit) collectionSubmit.disabled = true;
    setPanelStatus(collectionStatus, "正在创建采集运行…");
    const response = await client.apiFetch("/api/v1/admin/collection-runs", {
      method: "POST",
      body: JSON.stringify({ url: value })
    });
    const payload = response ? await response.json().catch(() => null) : null;
    if (response?.ok) {
      if (collectionUrl) collectionUrl.value = "";
      setPanelStatus(collectionStatus, payload?.status === "reused" ? "已复用同一来源书籍的进行中采集运行。" : "采集运行已创建，Worker 将异步执行。", "ready");
      void loadCollectionRuns();
    } else if (response?.status === 401) {
      client.clearSession();
      showLogin("会话已失效，请重新登录后访问运维中心。");
    } else {
      setPanelStatus(collectionStatus, "地址不受支持或采集运行创建失败，请确认来源已登记。", "danger");
    }
    if (collectionSubmit) collectionSubmit.disabled = false;
  };
  const startPackage = async () => {
    const bookId = asGuid(String(packageBookId?.value || "").trim());
    const format = text(packageFormat?.value, "").toLowerCase();
    if (!bookId || !["zip", "epub", "txt"].includes(format)) {
      setPanelStatus(packageStatus, "请输入有效的正典书 ID，并选择 ZIP、EPUB 或 TXT。", "danger");
      packageBookId?.focus();
      return;
    }
    if (packageSubmit) packageSubmit.disabled = true;
    setPanelStatus(packageStatus, "正在创建 " + format.toUpperCase() + " 打包任务…");
    const response = await client.apiFetch("/api/v1/admin/books/" + encodeURIComponent(bookId) + "/packages", {
      method: "POST",
      body: JSON.stringify({ format })
    });
    const payload = response ? await response.json().catch(() => null) : null;
    const packageId = asGuid(payload?.package?.id);
    if (response?.ok && packageId) {
      packageValues.set(packageId, payload.package);
      if (packageBookId) packageBookId.value = "";
      renderPackages();
      setPanelStatus(packageStatus, "打包任务已创建，完成后可下载。", "ready");
      void loadPackages();
    } else if (response?.status === 401) {
      client.clearSession();
      showLogin("会话已失效，请重新登录后访问运维中心。");
    } else {
      setPanelStatus(packageStatus, "打包任务创建失败，请确认书籍存在且没有被下架。", "danger");
    }
    if (packageSubmit) packageSubmit.disabled = false;
  };
  const renderConsistency = (section) => {
    const status = document.getElementById("operations-consistency-status");
    const meta = document.getElementById("operations-consistency-meta");
    const list = document.getElementById("operations-consistency-list");
    const data = section?.data;
    const issues = Array.isArray(data?.issues) ? data.issues : [];
    renderSectionStatus(status, section, issues.length > 0, "当前没有发现一致性问题。");
    list?.replaceChildren();
    if (meta) {
      const total = Math.max(0, Math.trunc(asNumber(data?.totalIssueCount)));
      const returned = Math.max(0, Math.trunc(asNumber(data?.returnedIssueCount)));
      meta.textContent = data
        ? (data.status === "healthy" ? "健康 · " : "发现问题 · ") + total + " 个问题，返回 " + returned + " 个"
        : "";
    }
    if (data?.truncated) {
      status?.append(node("p", "operations-state operations-state--partial", "问题列表已截断，只展示后端允许的有限条目。"));
    }
    for (const issue of issues) {
      const item = node("li", "operations-issue");
      const header = node("div", "operations-issue__header");
      header.append(node("span", "operations-issue__code", text(issue?.code, "unknown_issue")), badge(issue?.severity, text(issue?.severity, "未知")));
      item.append(header);
      item.append(node("div", "operations-issue__resource", text(issue?.resourceType, "资源") + " · " + text(issue?.resourceId, "无资源 ID")));
      item.append(node("div", "operations-issue__message", text(issue?.message, "暂无说明")));
      list?.append(item);
    }
  };
  const transitionLabel = (transition) => ({
    opened: "已触发",
    resolved: "已恢复"
  }[String(transition || "").toLowerCase()] || "未知转折");
  const setHistoryStatus = (message, tone = "neutral") => {
    if (!historyStatus) return;
    historyStatus.replaceChildren(node(
      "p",
      "operations-state operations-state--" + tone,
      message));
  };
  const renderHistoryRestricted = () => {
    historyCursor = null;
    historyBody?.replaceChildren();
    if (historyTable) historyTable.hidden = true;
    if (historyRefresh) {
      historyRefresh.hidden = true;
      historyRefresh.disabled = true;
    }
    if (historyMore) {
      historyMore.hidden = true;
      historyMore.disabled = true;
    }
    setHistoryStatus("平台告警历史仅管理员可查看。", "partial");
  };
  const renderHistory = (payload, append) => {
    const entries = Array.isArray(payload?.entries) ? payload.entries : [];
    if (!append) historyBody?.replaceChildren();
    for (const entry of entries) {
      const row = node("tr");
      const transition = text(entry?.transition, "unknown");
      const transitionCell = node("td");
      transitionCell.append(badge(transition, transitionLabel(transition)));
      transitionCell.append(node(
        "div",
        "operations-history__meta",
        "事件 " + (asGuid(entry?.id) || "无有效 ID")));
      const alertCell = node("td");
      alertCell.append(node("strong", null, text(entry?.code, "unknown_alert")));
      alertCell.append(node(
        "div",
        "operations-history__meta",
        text(entry?.severity, "未知级别")));
      const resourceCell = node("td", "operations-history__meta");
      resourceCell.textContent = text(entry?.resourceType, "资源") + " · " +
        text(entry?.resourceId, "无资源 ID");
      const timeCell = node("td", "operations-history__meta", dateLabel(entry?.occurredAt));
      const countCell = node(
        "td",
        "operations-history__meta",
        String(Math.max(0, Math.trunc(asNumber(entry?.occurrenceCount)))));
      row.append(transitionCell, alertCell, resourceCell, timeCell, countCell);
      historyBody?.append(row);
    }
    historyCursor = text(payload?.nextCursor, "") || null;
    const visibleCount = historyBody?.children.length || 0;
    if (historyTable) historyTable.hidden = visibleCount === 0;
    if (historyMore) {
      historyMore.hidden = !historyCursor;
      historyMore.disabled = !historyCursor || historyLoading;
    }
    if (visibleCount === 0) {
      setHistoryStatus(
        append ? "没有更多历史记录。" : "暂无告警转折记录。",
        "ready");
    } else {
      setHistoryStatus(
        "已加载 " + visibleCount + " 条告警转折记录。" +
        (historyCursor ? "可继续加载更早记录。" : ""),
        "ready");
    }
  };
  const historyErrorMessage = (status) => {
    if (status === 401) return "会话已失效，请重新登录。";
    if (status === 403) return "平台告警历史仅管理员可查看。";
    if (status === 503) return "告警历史暂时不可用，请稍后重试。";
    return "告警历史请求失败，请刷新后重试。";
  };
  const loadHistory = async (reset = false) => {
    if (currentRole !== "Administrator" || historyLoading || !client?.isSignedIn()) return;
    const cursor = reset ? null : historyCursor;
    historyLoading = true;
    if (reset) {
      historyCursor = null;
      historyBody?.replaceChildren();
      if (historyTable) historyTable.hidden = true;
    }
    if (historyRefresh) historyRefresh.disabled = true;
    if (historyMore) historyMore.disabled = true;
    setHistoryStatus(reset ? "正在加载告警历史…" : "正在加载更早历史记录…");
    const query = new URLSearchParams({ limit: "50" });
    if (cursor) query.set("cursor", cursor);
    let response = null;
    try {
      response = await client.apiFetch(
        "/api/v1/admin/operations/alerts/history?" + query.toString());
    } catch {
      response = null;
    }
    if (response === null) {
      setHistoryStatus("告警历史请求失败，请检查网络后重试。", "danger");
      historyLoading = false;
      if (historyRefresh) historyRefresh.disabled = false;
      return;
    }
    if (response.status === 401) {
      client.clearSession();
      historyLoading = false;
      showLogin("会话已失效，请重新登录后访问运维中心。");
      return;
    }
    if (response.status === 403) {
      historyLoading = false;
      renderHistoryRestricted();
      return;
    }
    const payload = await response.json().catch(() => null);
    if (!response.ok) {
      setHistoryStatus(historyErrorMessage(response.status), "danger");
      historyLoading = false;
      if (historyRefresh) historyRefresh.disabled = false;
      if (historyMore) historyMore.disabled = !historyCursor;
      return;
    }
    renderHistory(payload, !reset);
    historyLoading = false;
    if (historyRefresh) historyRefresh.disabled = false;
    if (historyMore) historyMore.disabled = !historyCursor;
  };
  const renderSnapshot = (snapshot) => {
    if (!snapshot || typeof snapshot !== "object") {
      setNotice(authStatus, "运维快照格式异常，请稍后重试。", "danger");
      return;
    }
    if (content) content.hidden = false;
    renderSummary(snapshot);
    renderSources(snapshot.sources);
    if (currentRole !== "Reader") {
      renderCrawler(snapshot.crawler);
      renderConsistency(snapshot.consistency);
    }
  };
  const showAuthorized = (identity) => {
    currentRole = text(identity?.role, "");
    if (!operationRoles.has(currentRole)) {
      showForbidden();
      return false;
    }
    applyRoleSurface();
    if (historyPanel) historyPanel.hidden = false;
    if (currentRole === "Administrator") {
      if (historyRefresh) {
        historyRefresh.hidden = false;
        historyRefresh.disabled = false;
      }
      setHistoryStatus("管理员可查看平台告警历史。", "neutral");
      if (policyBookId) policyBookId.disabled = false;
      if (policySubmit) policySubmit.disabled = false;
    } else {
      renderHistoryRestricted();
      renderPolicyRestricted();
    }
    if (refreshButton) refreshButton.disabled = false;
    setNotice(
      authStatus,
      currentRole === "Reader"
        ? "已验证读者身份，可以创建和查看采集、打包并下载书籍；来源状态仅供查看。"
        : "已验证 " + roleLabel(currentRole) + " 身份，可以读取运维快照。",
      "ready");
    return true;
  };
  const ensureAuthorized = async () => {
    if (!client?.isSignedIn()) {
      showLogin("采集与下载中心需要登录后的账户。");
      return false;
    }
    const response = await client.apiFetch("/api/v1/auth/me");
    if (response === null) {
      setNotice(authStatus, "暂时无法验证当前会话，请检查网络后重试。", "danger");
      hideContent();
      return false;
    }
    if (response.status === 401) {
      client.clearSession();
      showLogin("会话已失效，请重新登录后访问运维中心。");
      return false;
    }
    if (!response.ok) {
      setNotice(authStatus, "当前会话暂时无法验证，请稍后重试。", "danger");
      hideContent();
      return false;
    }
    return showAuthorized(await response.json().catch(() => null));
  };
  const loadSnapshot = async () => {
    if (loading) return;
    if (!await ensureAuthorized()) return;
    loading = true;
    if (refreshButton) refreshButton.disabled = true;
    setNotice(authStatus, "正在加载运维快照…", "neutral");
    const response = await client.apiFetch("/api/v1/admin/operations/overview?limit=50");
    if (response === null) {
      setNotice(authStatus, "运维快照请求失败，请检查网络后重试。", "danger");
      loading = false;
      if (refreshButton) refreshButton.disabled = false;
      return;
    }
    if (response.status === 401) {
      client.clearSession();
      showLogin("会话已失效，请重新登录后访问运维中心。");
      loading = false;
      return;
    }
    if (response.status === 403) {
      showForbidden();
      loading = false;
      return;
    }
    const payload = await response.json().catch(() => null);
    if (!response.ok) {
      setNotice(authStatus, errorMessage(response.status, payload), "danger");
      loading = false;
      if (refreshButton) refreshButton.disabled = false;
      return;
    }
    renderSnapshot(payload);
    setNotice(authStatus, "已更新 · " + dateLabel(payload?.generatedAt) + " · " + roleLabel(currentRole), "ready");
    void loadCollectionRuns();
    void loadPackages();
    void loadPolicy();
    void loadHistory(true);
    loading = false;
    if (refreshButton) refreshButton.disabled = false;
  };
  const openActionDialog = (action) => {
    if (!actionDialog || !actionForm) return;
    pendingAction = action;
    actionTitle.textContent = action.title || "确认运维操作";
    actionDescription.textContent = action.detail || "请填写本次操作理由。";
    renderReasonSuggestions(action);
    actionStatus.textContent = "";
    actionStatus.className = "operations-dialog__status";
    actionSubmit.textContent = action.action === "replay"
      ? "确认重放"
      : action.action === "run-control"
        ? "确认" + controlLabel(action.controlAction)
        : action.action === "content-policy"
          ? "确认" + policyActionLabel(action.policyAction)
        : action.action === "run-delete"
            ? "确认删除"
            : action.action === "cancelled-cleanup"
              ? "确认清理"
            : action.action === "source-disable"
              ? "确认停用来源"
              : action.action === "source-enable"
                ? "确认恢复来源"
        : (action.action === "disable" ? "确认停用" : "确认恢复");
    actionSubmit.disabled = false;
    actionDialog.showModal();
    actionReason.focus();
  };
  const closeActionDialog = () => {
    if (actionDialog?.open) actionDialog.close();
    pendingAction = null;
  };
  const submitAction = async () => {
    if (!pendingAction || !client?.isSignedIn()) return;
    const reason = String(actionReason.value || "").trim();
    if (!reason || reason.length > 512) {
      actionStatus.textContent = "请填写 1–512 个字符的理由。";
      actionStatus.className = "operations-dialog__status operations-dialog__status--danger";
      actionReason.focus();
      return;
    }
    actionSubmit.disabled = true;
    actionStatus.textContent = "正在提交，请稍候…";
    actionStatus.className = "operations-dialog__status";
    let path = "";
    if (pendingAction.action === "replay") {
      path = "/api/v1/admin/crawler/dead-letters/" + encodeURIComponent(pendingAction.deadLetterId) + "/replay";
    } else if (pendingAction.action === "run-control") {
      path = "/api/v1/admin/collection-runs/" + encodeURIComponent(pendingAction.runId) + "/control";
    } else if (pendingAction.action === "run-delete") {
      path = "/api/v1/admin/collection-runs/" + encodeURIComponent(pendingAction.runId) + "/delete";
    } else if (pendingAction.action === "cancelled-cleanup") {
      path = "/api/v1/admin/collection-runs/cancelled/cleanup";
    } else if (pendingAction.action === "content-policy") {
      path = pendingAction.policyAction === "takedown"
        ? "/api/v1/admin/content/takedowns"
        : "/api/v1/admin/content/takedowns/" + encodeURIComponent(pendingAction.bookId) + "/restore";
    } else if (["source-disable", "source-enable"].includes(pendingAction.action)) {
      path = "/api/v1/admin/sources/" + encodeURIComponent(pendingAction.sourceId) + "/" +
        (pendingAction.action === "source-disable" ? "disable" : "enable");
    } else {
      path = "/api/v1/admin/sources/" + encodeURIComponent(pendingAction.sourceId) + "/health/" + encodeURIComponent(pendingAction.capability) + "/" + pendingAction.action;
    }
    const requestBody = pendingAction.action === "run-control"
      ? { action: pendingAction.controlAction, reason }
      : pendingAction.action === "content-policy" && pendingAction.policyAction === "takedown"
        ? { bookId: pendingAction.bookId, reason }
      : { reason };
    const response = await client.apiFetch(path, {
      method: "POST",
      body: JSON.stringify(requestBody)
    });
    const payload = response ? await response.json().catch(() => null) : null;
    if (response?.ok) {
      const taskId = asGuid(payload?.replayTaskId);
      actionStatus.textContent = pendingAction.action === "replay"
        ? (payload?.status === "AlreadyReplayed" ? "该死信已经重放过。" : "重放任务已创建。") + (taskId ? " 新任务：" + taskId : "")
        : pendingAction.action === "run-control"
          ? "采集运行控制命令已提交。"
          : pendingAction.action === "run-delete"
            ? "失败采集任务已删除。"
            : pendingAction.action === "cancelled-cleanup"
              ? "已取消采集任务已清理，共删除 " + Math.max(0, Math.trunc(asNumber(payload?.deletedCount))) + " 条。"
            : pendingAction.action === "source-disable"
              ? "来源已停用。"
              : pendingAction.action === "source-enable"
                ? "来源已恢复。"
          : pendingAction.action === "content-policy"
            ? "内容政策已" + policyActionLabel(pendingAction.policyAction) + "，并已记录审计。"
          : (pendingAction.action === "disable" ? "能力已停用。" : "能力已恢复，等待真实探针确认。");
      actionStatus.className = "operations-dialog__status operations-dialog__status--ready";
      actionSubmit.disabled = true;
      window.setTimeout(() => {
        closeActionDialog();
        void loadSnapshot();
        void loadCollectionRuns();
      }, 700);
      return;
    }
    if (response?.status === 401) {
      client.clearSession();
      closeActionDialog();
      showLogin("会话已失效，请重新登录后访问运维中心。");
      return;
    }
    actionStatus.textContent = errorMessage(response?.status || 0, payload);
    actionStatus.className = "operations-dialog__status operations-dialog__status--danger";
    actionSubmit.disabled = false;
  };

  for (const [index, tab] of operationsTabs.entries()) {
    tab.addEventListener("click", () => selectOperationsTab(tab.dataset.operationsTab, false, true));
    tab.addEventListener("keydown", (event) => {
      const offset = event.key === "ArrowRight" || event.key === "ArrowDown"
        ? 1
        : event.key === "ArrowLeft" || event.key === "ArrowUp"
          ? -1
          : event.key === "Home"
            ? -index
            : event.key === "End"
              ? operationsTabs.length - 1 - index
              : 0;
      if (!offset || operationsTabs.length < 2) return;
      event.preventDefault();
      const nextIndex = (index + offset + operationsTabs.length) % operationsTabs.length;
      selectOperationsTab(operationsTabs[nextIndex].dataset.operationsTab, true, true);
    });
  }
  window.addEventListener("hashchange", () => selectOperationsTab(operationsTabFromHash()));
  selectOperationsTab(operationsTabFromHash());
  refreshButton?.addEventListener("click", () => { void loadSnapshot(); });
  collectionForm?.addEventListener("submit", (event) => {
    event.preventDefault();
    void startCollection();
  });
  packageForm?.addEventListener("submit", (event) => {
    event.preventDefault();
    void startPackage();
  });
  policyForm?.addEventListener("submit", (event) => {
    event.preventDefault();
    startPolicyTakedown();
  });
  historyRefresh?.addEventListener("click", () => { void loadHistory(true); });
  historyMore?.addEventListener("click", () => { void loadHistory(false); });
  document.getElementById("operations-action-close")?.addEventListener("click", closeActionDialog);
  document.getElementById("operations-action-cancel")?.addEventListener("click", closeActionDialog);
  actionForm?.addEventListener("submit", (event) => {
    event.preventDefault();
    void submitAction();
  });
  actionDialog?.addEventListener("close", () => { pendingAction = null; });
  void loadSnapshot();
})();
