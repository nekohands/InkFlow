using System.Text;

namespace InkFlow.Modules.Content.Application;

public static partial class ReaderHtml
{
    private const string OperationsHeader =
        """
        <header class="site-header">
          <div class="site-header__inner">
            <a class="brand" href="/reader" aria-label="返回 InkFlow 书库"><span class="brand__name">墨流</span><span class="brand__sub">InkFlow · 运维</span></a>
            <nav class="reader-nav" aria-label="运维导航">
              <a href="/admin/operations" aria-current="page">运维中心</a>
              <a href="/reader">返回书库</a>
            </nav>
          </div>
        </header>
        """;

    private const string OperationsStyles =
        """
        <style>
          .operations-page .page-intro { max-width: 56rem; }
          .operations-toolbar {
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 1rem;
            margin-bottom: 1rem;
          }
          .operations-toolbar .notice { flex: 1; margin: 0; }
          .operations-content { display: grid; gap: 1rem; }
          .operations-summary {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(min(100%, 11rem), 1fr));
            gap: 0.75rem;
          }
          .operations-summary__item {
            display: grid;
            gap: 0.35rem;
            min-height: 6.2rem;
            padding: 1rem;
            border: 1px solid var(--reader-border);
            border-radius: var(--reader-radius);
            background: var(--reader-surface);
            box-shadow: var(--reader-shadow);
          }
          .operations-summary__item dt { color: var(--reader-muted); font-size: 0.82rem; }
          .operations-summary__item dd { margin: 0; font-size: 1.15rem; font-weight: 750; }
          .operations-panel {
            min-width: 0;
            padding: clamp(1rem, 3vw, 1.5rem);
            border: 1px solid var(--reader-border);
            border-radius: var(--reader-radius);
            background: var(--reader-surface);
            box-shadow: var(--reader-shadow);
          }
          .operations-panel__header {
            display: flex;
            align-items: baseline;
            justify-content: space-between;
            gap: 1rem;
            margin-bottom: 1rem;
          }
          .operations-panel__header h2 { margin: 0; }
          .operations-panel__status { margin: 0 0 0.9rem; }
          .operations-panel__status:empty { display: none; }
          .operations-state {
            margin: 0;
            padding: 0.7rem 0.85rem;
            border-radius: 0.65rem;
            background: var(--reader-bg);
            color: var(--reader-muted);
          }
          .operations-state--danger { color: #8e321f; }
          .operations-state--ready { color: #27643d; }
          .operations-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(min(100%, 22rem), 1fr));
            gap: 0.8rem;
          }
          .operations-card {
            min-width: 0;
            padding: 1rem;
            border: 1px solid var(--reader-border);
            border-radius: 0.85rem;
            background: var(--reader-bg);
          }
          .operations-card__header {
            display: flex;
            align-items: flex-start;
            justify-content: space-between;
            gap: 0.8rem;
            margin-bottom: 0.8rem;
          }
          .operations-card__header h3 { margin: 0; font-size: 1.05rem; overflow-wrap: anywhere; }
          .operations-card__id { margin: 0.2rem 0 0; color: var(--reader-muted); font-size: 0.78rem; overflow-wrap: anywhere; }
          .operations-card__error { margin: 0 0 0.8rem; color: #8e321f; font-size: 0.88rem; overflow-wrap: anywhere; }
          .operations-health-list { display: grid; gap: 0.55rem; padding: 0; margin: 0; list-style: none; }
          .operations-health-row {
            display: grid;
            gap: 0.55rem;
            padding-top: 0.7rem;
            border-top: 1px solid var(--reader-border);
          }
          .operations-health-row__top {
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 0.7rem;
          }
          .operations-health-row__name { font-weight: 700; overflow-wrap: anywhere; }
          .operations-health-row__meta,
          .operations-health-row__reason { color: var(--reader-muted); font-size: 0.82rem; overflow-wrap: anywhere; }
          .operations-health-row__actions { display: flex; flex-wrap: wrap; gap: 0.45rem; }
          .operations-health-row__actions .button { min-height: 2.45rem; padding-block: 0.45rem; font-size: 0.86rem; }
          .operations-badge {
            display: inline-flex;
            align-items: center;
            justify-content: center;
            flex: none;
            min-height: 1.6rem;
            padding: 0.15rem 0.5rem;
            border: 1px solid var(--reader-border);
            border-radius: 999px;
            background: var(--reader-bg);
            color: var(--reader-muted);
            font-size: 0.76rem;
            font-weight: 700;
            white-space: nowrap;
          }
          .operations-badge--ready { border-color: #8bc69e; background: #edf8ef; color: #27643d; }
          .operations-badge--partial { border-color: #d9b76c; background: #fff8df; color: #725516; }
          .operations-badge--danger { border-color: #dc9b8c; background: #fff0ec; color: #8e321f; }
          .operations-badge--neutral { color: var(--reader-muted); }
          .operations-table-wrap { overflow-x: auto; }
          .operations-table { width: 100%; min-width: 48rem; border-collapse: collapse; }
          .operations-table caption { margin-bottom: 0.65rem; text-align: left; }
          .operations-table th,
          .operations-table td {
            padding: 0.75rem 0.65rem;
            border-bottom: 1px solid var(--reader-border);
            vertical-align: top;
            text-align: left;
          }
          .operations-table th { color: var(--reader-muted); font-size: 0.78rem; font-weight: 700; }
          .operations-table td { overflow-wrap: anywhere; }
          .operations-table__reason { max-width: 24rem; color: var(--reader-muted); }
          .operations-table__meta { color: var(--reader-muted); font-size: 0.8rem; }
          .operations-table .button { min-height: 2.4rem; padding-block: 0.45rem; font-size: 0.84rem; }
          .operations-issue-list { display: grid; gap: 0.7rem; padding: 0; margin: 0; list-style: none; }
          .operations-issue {
            display: grid;
            gap: 0.45rem;
            padding: 0.85rem 1rem;
            border: 1px solid var(--reader-border);
            border-radius: 0.75rem;
            background: var(--reader-bg);
          }
          .operations-issue__header { display: flex; align-items: flex-start; justify-content: space-between; gap: 0.7rem; }
          .operations-issue__code { font-weight: 750; overflow-wrap: anywhere; }
          .operations-issue__resource,
          .operations-issue__message { color: var(--reader-muted); font-size: 0.86rem; overflow-wrap: anywhere; }
          .operations-dialog__inner { padding: 1.2rem; }
          .operations-dialog__header { display: flex; align-items: flex-start; justify-content: space-between; gap: 1rem; margin-bottom: 1rem; }
          .operations-dialog__header h2 { margin: 0; }
          .operations-dialog__description { color: var(--reader-muted); }
          .operations-dialog textarea {
            width: 100%;
            min-height: 7rem;
            resize: vertical;
            padding: 0.65rem 0.75rem;
            border: 1px solid var(--reader-border);
            border-radius: 0.65rem;
            background: var(--reader-bg);
            color: var(--reader-text);
          }
          .operations-dialog__status { min-height: 1.4rem; margin: 0.8rem 0 0; color: var(--reader-muted); font-size: 0.86rem; }
          .operations-dialog__status--danger { color: #8e321f; }
          .operations-dialog__status--ready { color: #27643d; }
          @media (max-width: 640px) {
            .operations-toolbar { align-items: stretch; flex-direction: column; }
            .operations-toolbar .button { width: 100%; }
            .operations-panel__header { align-items: flex-start; flex-direction: column; }
            .operations-card__header { flex-direction: column; }
          }
        </style>
        """;

    private const string OperationsScript =
        """
        <script>
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
          const actionReason = document.getElementById("operations-action-reason");
          const actionSubmit = document.getElementById("operations-action-submit");
          const guidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
          const operationRoles = new Set(["Operator", "Administrator"]);
          const capabilities = new Set(["Search", "BookInfo", "Toc", "Content", "Update"]);
          const stableErrors = {
            sources_unavailable: "来源清单暂时不可用。",
            source_health_unavailable: "部分来源健康记录暂时不可用。",
            crawler_unavailable: "死信队列暂时不可用。",
            consistency_unavailable: "一致性检查暂时不可用。"
          };
          let currentRole = null;
          let pendingAction = null;
          let loading = false;

          const text = (value, fallback = "") => {
            if (value === null || value === undefined) return fallback;
            const normalized = String(value).trim();
            return normalized ? normalized : fallback;
          };
          const asGuid = (value) => typeof value === "string" && guidPattern.test(value) ? value : null;
          const asBoolean = (value) => value === true || value === "true";
          const asNumber = (value, fallback = 0) => {
            const parsed = Number(value);
            return Number.isFinite(parsed) ? parsed : fallback;
          };
          const dateLabel = (value) => {
            const parsed = new Date(value);
            return Number.isNaN(parsed.valueOf()) ? "时间未知" : parsed.toLocaleString();
          };
          const roleLabel = (role) => ({ Operator: "运营", Administrator: "管理员", Reader: "读者" }[role] || "未知角色");
          const sectionLabel = (status) => ({ ready: "正常", partial: "部分可用", unavailable: "不可用" }[String(status).toLowerCase()] || "未知");
          const healthLabel = (status) => ({ Unknown: "待探测", Healthy: "健康", Degraded: "降级", Unhealthy: "不健康", Disabled: "已停用" }[status] || text(status, "未知"));
          const statusTone = (value) => {
            const normalized = String(value || "").toLowerCase();
            if (["ready", "healthy", "replayed"].includes(normalized)) return "ready";
            if (["partial", "degraded", "unknown"].includes(normalized)) return "partial";
            if (["unavailable", "unhealthy", "disabled", "issues_found", "error"].includes(normalized)) return "danger";
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
            setNotice(authStatus, "当前账户没有运维中心权限。", "danger");
          };
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
            list?.replaceChildren();
            for (const source of values) {
              const sourceId = text(source?.sourceId, "unknown-source");
              const card = node("article", "operations-card");
              const header = node("header", "operations-card__header");
              const headingWrap = node("div");
              headingWrap.append(node("h3", null, text(source?.displayName, "未命名来源")));
              headingWrap.append(node("p", "operations-card__id", sourceId));
              header.append(headingWrap, badge(source?.status, sectionLabel(source?.status)));
              card.append(header);
              if (source?.error) card.append(node("p", "operations-card__error", stableErrors[source.error] || "来源健康记录暂时不可用。"));
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
                if (capabilities.has(capability) && sourceId !== "unknown-source") {
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
          const renderSnapshot = (snapshot) => {
            if (!snapshot || typeof snapshot !== "object") {
              setNotice(authStatus, "运维快照格式异常，请稍后重试。", "danger");
              return;
            }
            if (content) content.hidden = false;
            renderSummary(snapshot);
            renderSources(snapshot.sources);
            renderCrawler(snapshot.crawler);
            renderConsistency(snapshot.consistency);
          };
          const showAuthorized = (identity) => {
            currentRole = text(identity?.role, "");
            if (!operationRoles.has(currentRole)) {
              showForbidden();
              return false;
            }
            if (refreshButton) refreshButton.disabled = false;
            setNotice(authStatus, "已验证 " + roleLabel(currentRole) + " 身份，可以读取运维快照。", "ready");
            return true;
          };
          const ensureAuthorized = async () => {
            if (!client?.isSignedIn()) {
              showLogin("运维中心需要登录后的 Operator 或 Administrator 账户。");
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
            loading = false;
            if (refreshButton) refreshButton.disabled = false;
          };
          const openActionDialog = (action) => {
            if (!actionDialog || !actionForm) return;
            pendingAction = action;
            actionTitle.textContent = action.title || "确认运维操作";
            actionDescription.textContent = action.detail || "请填写本次操作理由。";
            actionReason.value = "";
            actionStatus.textContent = "";
            actionStatus.className = "operations-dialog__status";
            actionSubmit.textContent = action.action === "replay" ? "确认重放" : (action.action === "disable" ? "确认停用" : "确认恢复");
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
            } else {
              path = "/api/v1/admin/sources/" + encodeURIComponent(pendingAction.sourceId) + "/health/" + encodeURIComponent(pendingAction.capability) + "/" + pendingAction.action;
            }
            const response = await client.apiFetch(path, {
              method: "POST",
              body: JSON.stringify({ reason })
            });
            const payload = response ? await response.json().catch(() => null) : null;
            if (response?.ok) {
              const taskId = asGuid(payload?.replayTaskId);
              actionStatus.textContent = pendingAction.action === "replay"
                ? (payload?.status === "AlreadyReplayed" ? "该死信已经重放过。" : "重放任务已创建。") + (taskId ? " 新任务：" + taskId : "")
                : (pendingAction.action === "disable" ? "能力已停用。" : "能力已恢复，等待真实探针确认。");
              actionStatus.className = "operations-dialog__status operations-dialog__status--ready";
              actionSubmit.disabled = true;
              window.setTimeout(() => {
                closeActionDialog();
                void loadSnapshot();
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

          refreshButton?.addEventListener("click", () => { void loadSnapshot(); });
          document.getElementById("operations-action-close")?.addEventListener("click", closeActionDialog);
          document.getElementById("operations-action-cancel")?.addEventListener("click", closeActionDialog);
          actionForm?.addEventListener("submit", (event) => {
            event.preventDefault();
            void submitAction();
          });
          actionDialog?.addEventListener("close", () => { pendingAction = null; });
          void loadSnapshot();
        })();
        </script>
        """;

    public static string OperationsPage()
    {
        var sb = new StringBuilder(Head);
        sb.Append(OperationsHeader);
        sb.Append(OperationsStyles);
        sb.Append(
            """
            <main id="main-content" class="page-shell operations-page">
              <section class="page-intro" aria-labelledby="operations-title">
                <p class="eyebrow">InkFlow Operations</p>
                <h1 id="operations-title">运维中心</h1>
                <p class="muted">集中查看来源健康、采集死信和跨模块一致性。页面只消费受保护的有限快照，不展示凭据、任务变量或正文载荷。</p>
              </section>
              <section class="operations-toolbar" aria-label="运维中心控制">
                <p id="operations-auth-status" class="notice" role="status" aria-live="polite">正在检查运维权限…</p>
                <button id="operations-refresh" class="button button--primary" type="button" disabled>刷新快照</button>
              </section>
              <noscript><p class="notice" role="status">运维中心需要启用 JavaScript 以读取受保护快照；请使用 API 客户端访问管理接口。</p></noscript>
              <div id="operations-content" class="operations-content" hidden>
                <section class="operations-summary" id="operations-summary" aria-label="快照摘要" hidden>
                  <dl class="operations-summary__item"><dt>整体状态</dt><dd id="operations-summary-status"></dd></dl>
                  <dl class="operations-summary__item"><dt>来源健康</dt><dd id="operations-summary-sources"></dd></dl>
                  <dl class="operations-summary__item"><dt>采集死信</dt><dd id="operations-summary-crawler"></dd></dl>
                  <dl class="operations-summary__item"><dt>一致性</dt><dd id="operations-summary-consistency"></dd></dl>
                  <dl class="operations-summary__item"><dt>快照时间</dt><dd id="operations-generated-at">—</dd></dl>
                </section>
                <section class="operations-panel" aria-labelledby="operations-sources-title">
                  <header class="operations-panel__header">
                    <h2 id="operations-sources-title">来源健康</h2>
                    <span class="muted">按来源能力分组</span>
                  </header>
                  <div id="operations-sources-status" class="operations-panel__status" role="status" aria-live="polite"></div>
                  <div id="operations-sources-list" class="operations-grid"></div>
                </section>
                <section class="operations-panel" aria-labelledby="operations-crawler-title">
                  <header class="operations-panel__header">
                    <h2 id="operations-crawler-title">采集死信</h2>
                    <span class="muted">受控重放</span>
                  </header>
                  <div id="operations-crawler-status" class="operations-panel__status" role="status" aria-live="polite"></div>
                  <div id="operations-crawler-table" class="operations-table-wrap" hidden>
                    <table class="operations-table">
                      <caption class="sr-only">采集死信列表</caption>
                      <thead><tr><th scope="col">来源</th><th scope="col">失败原因</th><th scope="col">尝试次数</th><th scope="col">进入时间</th><th scope="col">状态</th><th scope="col">操作</th></tr></thead>
                      <tbody id="operations-crawler-body"></tbody>
                    </table>
                  </div>
                </section>
                <section class="operations-panel" aria-labelledby="operations-consistency-title">
                  <header class="operations-panel__header">
                    <h2 id="operations-consistency-title">跨模块一致性</h2>
                    <span id="operations-consistency-meta" class="muted"></span>
                  </header>
                  <div id="operations-consistency-status" class="operations-panel__status" role="status" aria-live="polite"></div>
                  <ul id="operations-consistency-list" class="operations-issue-list" aria-label="一致性问题列表"></ul>
                </section>
              </div>
              <dialog id="operations-action-dialog" aria-labelledby="operations-action-title">
                <form id="operations-action-form" class="operations-dialog__inner">
                  <header class="operations-dialog__header">
                    <h2 id="operations-action-title">确认运维操作</h2>
                    <button id="operations-action-close" class="icon-button" type="button" aria-label="关闭确认对话框">×</button>
                  </header>
                  <p id="operations-action-description" class="operations-dialog__description"></p>
                  <div class="form-field">
                    <label for="operations-action-reason">操作理由</label>
                    <textarea id="operations-action-reason" maxlength="512" required placeholder="说明本次操作的原因"></textarea>
                  </div>
                  <p id="operations-action-status" class="operations-dialog__status" role="status" aria-live="polite"></p>
                  <div class="form-actions">
                    <button id="operations-action-cancel" class="button" type="button">取消</button>
                    <button id="operations-action-submit" class="button button--primary" type="submit">确认操作</button>
                  </div>
                </form>
              </dialog>
            </main>
            """);
        sb.Append(OperationsScript);
        sb.Append("</body></html>");
        return sb.ToString();
    }
}
