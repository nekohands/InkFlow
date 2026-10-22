using static InkFlow.Api.Presentation.ReaderAssets;
using System.Text;

namespace InkFlow.Api.Presentation;

public static partial class ReaderHtml
{
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
                <p class="muted">Reader 账户可以创建和查看采集、打包并下载书籍；来源状态仅供查看。运营人员和管理员还可以处理死信、治理内容和恢复平台告警。页面只消费受保护的有限数据，不展示凭据、任务变量或正文载荷。</p>
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
                  <dl id="operations-summary-crawler-item" class="operations-summary__item" data-operations-roles="Operator,Administrator"><dt>采集死信</dt><dd id="operations-summary-crawler"></dd></dl>
                   <dl id="operations-summary-consistency-item" class="operations-summary__item" data-operations-roles="Operator,Administrator"><dt>一致性</dt><dd id="operations-summary-consistency"></dd></dl>
                   <dl class="operations-summary__item"><dt>快照时间</dt><dd id="operations-generated-at">—</dd></dl>
                 </section>
                <nav id="operations-tabs" class="operations-tabs" aria-label="运维功能分类" role="tablist">
                  <button id="operations-tab-collection" class="operations-tab" type="button" role="tab" aria-selected="true" aria-controls="operations-panel-collection" data-operations-tab="collection" data-operations-roles="Reader,Operator,Administrator">采集任务</button>
                  <button id="operations-tab-packages" class="operations-tab" type="button" role="tab" aria-selected="false" aria-controls="operations-panel-packages" data-operations-tab="packages" data-operations-roles="Reader,Operator,Administrator" tabindex="-1">打包下载</button>
                  <button id="operations-tab-sources" class="operations-tab" type="button" role="tab" aria-selected="false" aria-controls="operations-panel-sources" data-operations-tab="sources" data-operations-roles="Reader,Operator,Administrator" tabindex="-1">来源与死信</button>
                  <button id="operations-tab-governance" class="operations-tab" type="button" role="tab" aria-selected="false" aria-controls="operations-panel-governance" data-operations-tab="governance" data-operations-roles="Operator,Administrator" tabindex="-1">内容治理</button>
                  <button id="operations-tab-diagnostics" class="operations-tab" type="button" role="tab" aria-selected="false" aria-controls="operations-panel-diagnostics" data-operations-tab="diagnostics" data-operations-roles="Operator,Administrator" tabindex="-1">告警与一致性</button>
                </nav>
                <div class="operations-tab-panels">
                  <section id="operations-panel-collection" class="operations-tab-panel" role="tabpanel" aria-labelledby="operations-tab-collection" data-operations-panel="collection" data-operations-roles="Reader,Operator,Administrator">
                    <section class="operations-panel" aria-labelledby="operations-collection-title">
                  <header class="operations-panel__header">
                    <h2 id="operations-collection-title">书籍采集</h2>
                     <span class="muted">地址入口 · 异步执行 · 按状态分类</span>
                  </header>
                  <form id="operations-collection-form" class="operations-form">
                    <div class="operations-form__field">
                      <label for="operations-collection-url">已登记公共来源的书籍地址</label>
                      <input id="operations-collection-url" type="url" maxlength="2048" required placeholder="https://example.com/book/…" autocomplete="off">
                    </div>
                    <div class="operations-form__actions"><button id="operations-collection-submit" class="button button--primary" type="submit">开始采集</button></div>
                    <p class="operations-form__hint">只接受已登记来源的精确书籍页面；不做代理、不绕过登录、付费、VIP、验证码或访问控制。</p>
                  </form>
                   <div id="operations-collection-status" class="operations-panel__status" role="status" aria-live="polite"></div>
                   <div id="operations-collection-list" class="operations-run-status-view" aria-label="采集运行列表"></div>
                    </section>
                  </section>
                  <section id="operations-panel-packages" class="operations-tab-panel" role="tabpanel" aria-labelledby="operations-tab-packages" data-operations-panel="packages" data-operations-roles="Reader,Operator,Administrator" hidden>
                    <section class="operations-panel" aria-labelledby="operations-package-title">
                  <header class="operations-panel__header">
                    <h2 id="operations-package-title">书籍打包</h2>
                    <span class="muted">完整快照 · 不覆盖旧包</span>
                  </header>
                  <form id="operations-package-form" class="operations-form">
                    <div class="operations-form__field">
                      <label for="operations-package-book-id">正典书 ID</label>
                      <input id="operations-package-book-id" type="text" maxlength="36" required placeholder="从采集运行卡片复制正典书 ID" autocomplete="off">
                    </div>
                    <div class="operations-form__field">
                      <label for="operations-package-format">格式</label>
                      <select id="operations-package-format">
                        <option value="epub">EPUB 3</option>
                        <option value="txt">单文件 TXT</option>
                        <option value="zip">ZIP</option>
                      </select>
                    </div>
                    <div class="operations-form__actions"><button id="operations-package-submit" class="button button--primary" type="submit">创建打包任务</button></div>
                    <p class="operations-form__hint">只有全部必需章节存在当前已发布正文时，任务才会生成可下载文件。</p>
                  </form>
                   <div id="operations-package-status" class="operations-panel__status" role="status" aria-live="polite"></div>
                   <ul id="operations-package-list" class="operations-package-list" aria-label="书籍打包任务列表"></ul>
                    </section>
                  </section>
                  <section id="operations-panel-governance" class="operations-tab-panel" role="tabpanel" aria-labelledby="operations-tab-governance" data-operations-panel="governance" data-operations-roles="Operator,Administrator" hidden>
                    <section class="operations-panel" aria-labelledby="operations-policy-title">
                  <header class="operations-panel__header">
                    <h2 id="operations-policy-title">内容政策</h2>
                    <span class="muted">管理员下架 / 恢复</span>
                  </header>
                  <form id="operations-policy-form" class="operations-form">
                    <div class="operations-form__field">
                      <label for="operations-policy-book-id">正典书 ID</label>
                      <input id="operations-policy-book-id" type="text" maxlength="36" required placeholder="输入需要下架的正典书 ID" autocomplete="off">
                    </div>
                    <div class="operations-form__actions"><button id="operations-policy-submit" class="button button--primary" type="submit">发起下架</button></div>
                    <p class="operations-form__hint">下架是追加式政策决定；不会删除历史数据，恢复操作需要再次填写理由。</p>
                  </form>
                   <div id="operations-policy-status" class="operations-panel__status" role="status" aria-live="polite"></div>
                   <ul id="operations-policy-list" class="operations-policy-list" aria-label="当前下架书籍列表"></ul>
                    </section>
                  </section>
                  <section id="operations-panel-sources" class="operations-tab-panel" role="tabpanel" aria-labelledby="operations-tab-sources" data-operations-panel="sources" data-operations-roles="Reader,Operator,Administrator" hidden>
                    <section class="operations-panel" aria-labelledby="operations-sources-title">
                  <header class="operations-panel__header">
                    <h2 id="operations-sources-title">来源健康</h2>
                    <span class="muted">按来源能力分组 · Reader 只读</span>
                  </header>
                  <div id="operations-sources-status" class="operations-panel__status" role="status" aria-live="polite"></div>
                  <div id="operations-sources-list" class="operations-grid"></div>
                </section>
                <section class="operations-panel" aria-labelledby="operations-crawler-title" data-operations-roles="Operator,Administrator">
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
                  </section>
                  <section id="operations-panel-diagnostics" class="operations-tab-panel" role="tabpanel" aria-labelledby="operations-tab-diagnostics" data-operations-panel="diagnostics" data-operations-roles="Operator,Administrator" hidden>
                    <section class="operations-panel" id="operations-history" aria-labelledby="operations-history-title">
                  <header class="operations-panel__header">
                    <h2 id="operations-history-title">告警历史</h2>
                    <span class="muted">触发与恢复转折</span>
                  </header>
                  <div id="operations-history-status" class="operations-panel__status" role="status" aria-live="polite"></div>
                  <div id="operations-history-table" class="operations-table-wrap" hidden>
                    <table class="operations-table">
                      <caption class="sr-only">告警历史记录</caption>
                      <thead><tr><th scope="col">转折</th><th scope="col">告警</th><th scope="col">资源</th><th scope="col">发生时间</th><th scope="col">出现次数</th></tr></thead>
                      <tbody id="operations-history-body"></tbody>
                    </table>
                  </div>
                  <div class="operations-history-controls">
                    <span class="operations-history__meta">仅管理员可查看平台级历史。</span>
                    <div class="operations-history-controls__actions">
                      <button id="operations-history-refresh" class="button" type="button" hidden>刷新历史</button>
                      <button id="operations-history-more" class="button" type="button" hidden>加载更早记录</button>
                    </div>
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
                  </section>
                </div>
              </div>
              <dialog id="operations-action-dialog" aria-labelledby="operations-action-title">
                <form id="operations-action-form" class="operations-dialog__inner">
                  <header class="operations-dialog__header">
                    <h2 id="operations-action-title">确认运维操作</h2>
                    <button id="operations-action-close" class="icon-button" type="button" aria-label="关闭确认对话框">×</button>
                  </header>
                  <p id="operations-action-description" class="operations-dialog__description"></p>
                  <fieldset id="operations-action-suggestions" class="operations-dialog__suggestions" hidden></fieldset>
                  <p class="operations-dialog__hint">可直接选择、修改或自行填写理由。</p>
                  <div class="form-field">
                    <label for="operations-action-reason">操作理由</label>
                    <textarea id="operations-action-reason" maxlength="512" required placeholder="可选择上方常用理由，也可以自行填写或修改"></textarea>
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
