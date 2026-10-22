const { chromium } = require('playwright');
const { startServer } = require('./server.cjs');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const crypto = require('node:crypto');
const os = require('node:os');
const { compareScreenshots } = require('./compare-screenshots.cjs');

const input = path.resolve(process.argv[2] || '../../artifacts/reader-fixtures');
const output = path.resolve(process.argv[3] || '../../artifacts/reader-browser');
const baseline = process.argv[4] ? path.resolve(process.argv[4]) : null;
fs.mkdirSync(output, { recursive: true });
const hash = bytes => crypto.createHash('sha256').update(bytes).digest('hex');

(async () => {
  const fixture = await startServer(input);
  const browser = await chromium.launch({ headless: true });
  const report = { browser: browser.version(), platform: process.platform, environment: { os: os.release(), architecture: process.arch, cpu: os.cpus()[0].model, logicalCpus: os.cpus().length, node: process.version, playwright: require('playwright/package.json').version }, viewports: [], measurements: [], pageErrors: [], consoleMessages: [], failedRequests: [], screenshots: {}, visualComparisons: {} };
  const byName = Object.fromEntries(fixture.manifest.map(item => [item.name, item]));
  const visit = async (page, name) => {
    await page.goto(fixture.baseUrl + byName[name].Path);
    await page.locator('html:not(.reader-auth-pending)').waitFor();
    await page.evaluate(() => document.fonts.ready);
  };
  const login = async page => {
    await page.locator('#reader-login-form-email').fill('reader@example.test');
    await page.locator('#reader-login-form-password').fill('FixturePassword123!');
    await page.locator('#reader-login-form button[type=submit]').click();
    await page.waitForURL(url => url.pathname === '/reader');
    await page.locator('.book-grid').waitFor();
  };
  const screenshot = async (page, name) => {
    // Chapter progress saves after two async frames. Compare the completed UI,
    // not a race between an empty status and the visible "已同步" acknowledgement.
    if (await page.locator('#reader-sync-status').count()) {
      await page.waitForFunction(() => document.querySelector('#reader-sync-status').textContent === '已同步');
    }
    // Keep pointer hover out of the snapshot and wait for two identical frames;
    // compare pixels only after asynchronous UI state has settled.
    await page.mouse.move(0, 0);
    await page.evaluate(() => document.fonts.ready);
    let previous = null;
    let bytes;
    for (let attempt = 0; attempt < 10; attempt++) {
      await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
      bytes = await page.screenshot({ animations: 'disabled' });
      if (previous && hash(bytes) === hash(previous)) break;
      previous = bytes;
      if (attempt === 9) throw new Error(`Screenshot did not stabilize: ${name}`);
    }
    fs.writeFileSync(path.join(output, name + '.png'), bytes);
    report.screenshots[name] = hash(bytes);
    if (baseline) {
      const comparison = compareScreenshots(bytes, fs.readFileSync(path.join(baseline, name + '.png')));
      report.visualComparisons[name] = comparison;
      assert.ok(comparison.equivalent, `Screenshot changed: ${name}: ${JSON.stringify(comparison)}`);
    }
  };
  try {
    for (const viewport of [{ width: 390, height: 844 }, { width: 768, height: 1024 }, { width: 1440, height: 900 }, { width: 1920, height: 1080 }]) {
      fixture.state.preferences = null;
      fixture.state.progress = null;
      const context = await browser.newContext({ viewport, locale: 'zh-CN', timezoneId: 'UTC', reducedMotion: 'reduce' });
      const page = await context.newPage();
      page.on('pageerror', error => report.pageErrors.push(error.message));
      page.on('console', message => {
        if (['warning', 'error'].includes(message.type())) report.consoleMessages.push({ type: message.type(), text: message.text() });
      });
      page.on('requestfailed', request => {
        if (!request.failure()?.errorText.includes('ERR_ABORTED')) report.failedRequests.push({ path: new URL(request.url()).pathname, error: request.failure()?.errorText });
      });
      // Exercise the real login redirect/client; fixtures never access a real account.
      await page.goto(fixture.baseUrl + '/reader');
      await page.waitForURL(url => url.pathname === '/reader/account');
      assert.equal(new URL(page.url()).searchParams.get('returnTo'), '/reader');
      await screenshot(page, `login-${viewport.width}`);
      await login(page);
      await screenshot(page, `catalog-${viewport.width}`);
      await visit(page, 'toc-10000');
      assert.equal(await page.locator('.toc a').count(), 10000);
      await screenshot(page, `toc-${viewport.width}`);
      await visit(page, 'short');
      assert.equal(await page.locator('.reader-content__body p').count(), 3);
      assert.equal(await page.evaluate(() => window.xss), undefined);
      await screenshot(page, `short-${viewport.width}`);
      const trigger = page.locator('[data-open-reader-settings]').first();
      await trigger.focus();
      await page.keyboard.press('Enter');
      assert.equal(await page.locator('#reader-settings').evaluate(node => node.open), true);
      await page.locator('#reader-theme').selectOption('dark');
      assert.equal(await page.locator('html').getAttribute('data-reader-theme'), 'dark');
      await page.keyboard.press('Escape');
      assert.equal(await trigger.evaluate(node => node === document.activeElement), true);
      await page.reload();
      await page.waitForFunction(() => document.documentElement.dataset.readerTheme === 'dark');
      await page.locator('a[rel=next]').click();
      await page.waitForURL(fixture.baseUrl + byName.long.Path);
      assert.equal(await page.locator('.reader-content__body p').count(), 500);
      await screenshot(page, `long-${viewport.width}`);
      const layout = await page.evaluate(() => ({ width: innerWidth, scrollWidth: document.documentElement.scrollWidth, nodes: document.querySelectorAll('*').length }));
      assert.ok(layout.scrollWidth <= layout.width + 1, `Horizontal overflow: ${viewport.width}`);
      await page.locator('a[rel=prev]').click();
      await page.waitForURL(fixture.baseUrl + byName.short.Path);
      // A reflow check using CSS zoom, recorded separately from physical-device/browser chrome zoom.
      await page.evaluate(() => { document.documentElement.style.zoom = '2'; });
      await trigger.click();
      assert.equal(await page.locator('#reader-settings').evaluate(node => node.open), true);
      await screenshot(page, `zoom200-${viewport.width}`);
      await page.keyboard.press('Escape');
      await visit(page, 'empty');
      assert.ok((await page.locator('main').innerText()).includes('书库还是空的'));
      await visit(page, 'search-xss');
      assert.equal(await page.evaluate(() => window.xss), undefined);
      await visit(page, 'missing');
      assert.ok((await page.locator('main').innerText()).length > 0);
      await visit(page, 'shelf');
      await page.getByText('书架暂无书目。浏览书库，加入一本想读的书吧。', { exact: true }).waitFor();
      await screenshot(page, `shelf-${viewport.width}`);
      fixture.state.failReading = true;
      await page.reload();
      await page.waitForFunction(() => !document.querySelector('#reader-dashboard-status').textContent.includes('正在加载'));
      const errorState = await page.locator('#reader-dashboard-status').innerText();
      assert.ok(!errorState.includes('暂无书目'));
      fixture.state.failReading = false;
      await visit(page, 'account');
      await page.waitForFunction(() => document.querySelector('#account-title').textContent === '我的账户');
      await page.locator('#account-tab-profile').focus();
      await page.keyboard.press('ArrowRight');
      assert.equal(await page.locator('#account-tab-security').getAttribute('aria-selected'), 'true');
      await screenshot(page, `account-${viewport.width}`);
      await visit(page, 'operations');
      await page.waitForFunction(() => document.querySelector('#operations-auth-status').textContent.includes('已更新'));
      assert.equal(await page.locator('#operations-tab-diagnostics').isVisible(), false);
      await screenshot(page, `operations-${viewport.width}`);
      fixture.state.role = 'Administrator';
      await page.reload();
      await page.waitForFunction(() => document.querySelector('#operations-auth-status').textContent.includes('管理员'));
      await page.locator('#operations-tab-sources').click();
      await page.locator('#operations-sources-list summary').first().click();
      await page.getByRole('button', { name: '停用来源', exact: true }).click();
      assert.equal(await page.locator('#operations-action-dialog').evaluate(node => node.open), true);
      await screenshot(page, `operations-confirm-${viewport.width}`);
      await page.locator('#operations-action-reason').fill('');
      await page.locator('#operations-action-submit').click();
      assert.ok(!fixture.state.requests.some(r => r.method === 'POST' && r.path.includes('/sources/')));
      await page.locator('#operations-action-cancel').click();
      assert.equal(await page.locator('#operations-action-dialog').evaluate(node => node.open), false);
      fixture.state.role = 'Reader';
      // Expired access token refreshes once; failed refresh returns to login.
      const refreshes = fixture.state.refreshes;
      fixture.state.expire = true;
      await visit(page, 'catalog');
      assert.equal(fixture.state.refreshes, refreshes + 1);
      fixture.state.expire = true;
      fixture.state.refreshFailure = true;
      await page.reload();
      await page.waitForURL(url => url.pathname === '/reader/account');
      fixture.state.refreshFailure = false;
      await login(page);
      // The existing SW scope is /reader/, which excludes the /reader root URL.
      await visit(page, 'history');
      await page.waitForFunction(async () => Boolean((await navigator.serviceWorker.getRegistration())?.active));
      const cacheUrls = (await page.evaluate(async () => (await Promise.all((await caches.keys()).map(async key => (await (await caches.open(key)).keys()).map(r => new URL(r.url).pathname)))).flat())).sort();
      assert.ok(!cacheUrls.some(url => url.startsWith('/api/') || url.includes('account') || url.includes('shelf')));
      await context.setOffline(true);
      await page.goto(fixture.baseUrl + '/reader/offline');
      assert.ok((await page.locator('main').innerText()).includes('离线'));
      await context.setOffline(false);
      report.viewports.push({ ...viewport, layout, errorState, cacheUrls, checks: 'login, XSS, 10k TOC, settings/keyboard/focus, reload, navigation, 200% CSS zoom, long/empty/error, account tabs, operations, refresh/401, offline/cache PASS' });
      await context.close();
      console.log(`Browser behavior PASS ${viewport.width}x${viewport.height}`);
    }

    // Stable desktop measurement protocol; responsive behavior above covers all four sizes.
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: 'zh-CN', timezoneId: 'UTC', reducedMotion: 'reduce' });
    const page = await context.newPage();
    await page.goto(fixture.baseUrl + '/reader/account');
    await login(page);
    for (const name of ['short', 'long', 'toc-100', 'toc-1000', 'toc-10000']) {
      for (let round = 1; round <= 3; round++) {
        for (let warmup = 0; warmup < 3; warmup++) await visit(page, name);
        const samples = [];
        for (let sample = 0; sample < 30; sample++) {
          await visit(page, name);
          samples.push(await page.evaluate(async () => {
            await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
            const navigation = performance.getEntriesByType('navigation')[0];
            return { renderMs: performance.now(), domContentLoadedMs: navigation.domContentLoadedEventEnd, nodes: document.querySelectorAll('*').length };
          }));
        }
        const ordered = samples.map(s => s.renderMs).sort((a, b) => a - b);
        report.measurements.push({ name, round, payloadBytes: byName[name].bytes, p50: ordered[14], p95: ordered[28], samples });
        console.log(`${name} round=${round} p50=${ordered[14].toFixed(1)} p95=${ordered[28].toFixed(1)}`);
      }
    }
    await context.close();
    assert.deepEqual(report.pageErrors, [], 'Uncaught browser errors');
    assert.ok(report.consoleMessages.every(message => /^Failed to load resource: (the server responded with a status of (401|404|503) \(|net::ERR_INTERNET_DISCONNECTED)/.test(message.text)), JSON.stringify(report.consoleMessages));
    // Intentional offline navigation can fail network requests before SW fallback.
    assert.ok(report.failedRequests.every(item => item.error.includes('ERR_INTERNET_DISCONNECTED')), JSON.stringify(report.failedRequests));
    if (baseline) {
      const expected = JSON.parse(fs.readFileSync(path.join(baseline, 'browser.json')));
      assert.deepEqual(report.viewports, expected.viewports, 'Browser behavior changed');
      assert.deepEqual(report.measurements.map(m => [m.name, m.round, m.payloadBytes, m.samples[0].nodes]), expected.measurements.map(m => [m.name, m.round, m.payloadBytes, m.samples[0].nodes]), 'Payload/DOM changed');
    }
    console.log('PASS: Reader browser behavior, screenshots, and measurements');
  } finally {
    fs.writeFileSync(path.join(output, 'browser.json'), JSON.stringify(report, null, 2));
    await browser.close();
    await new Promise(resolve => fixture.server.close(resolve));
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
