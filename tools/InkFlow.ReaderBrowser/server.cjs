// Loopback-only disposable renderer fixture. Never connected to a real account/database.
const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');

async function startServer(directory, port = 0) {
  const manifest = JSON.parse(fs.readFileSync(path.join(directory, 'manifest.json')));
  const pages = new Map(manifest.map(item => [item.Path, { ...item, body: fs.readFileSync(path.join(directory, item.file)) }]));
  const state = { role: 'Reader', failReading: false, expire: false, refreshFailure: false, preferences: null, progress: null, requests: [], refreshes: 0 };
  const server = http.createServer(async (request, response) => {
    const url = new URL(request.url, 'http://localhost');
    const send = (status, value, contentType = 'application/json') => {
      response.writeHead(status, { 'Content-Type': contentType, 'Cache-Control': 'no-store' });
      response.end(contentType === 'application/json' ? JSON.stringify(value) : value);
    };
    if (url.pathname === '/favicon.ico') return send(204, '');
    if (!url.pathname.startsWith('/api/')) {
      const page = pages.get(url.pathname);
      return page ? send(page.Status, page.body, page.ContentType) : send(404, { error: 'fixture_not_found' });
    }
    state.requests.push({ path: url.pathname, method: request.method }); // No headers, credentials or bodies in evidence.
    let body = '';
    for await (const chunk of request) body += chunk;
    const value = body ? JSON.parse(body) : null;
    if (url.pathname === '/api/v1/auth/login' || url.pathname === '/api/v1/auth/register') {
      state.expire = false;
      return send(200, { access_token: 'fixture-access', refresh_token: 'fixture-refresh' });
    }
    if (url.pathname === '/api/v1/auth/refresh') {
      state.refreshes++;
      if (state.refreshFailure) return send(401, { error: 'fixture_expired' });
      state.expire = false;
      return send(200, { access_token: 'fixture-access-renewed', refresh_token: 'fixture-refresh-renewed' });
    }
    if (!request.headers.authorization || state.expire) return send(401, { error: 'fixture_unauthorized' });
    if (url.pathname === '/api/v1/auth/me') return send(200, { email: 'reader@example.test', role: state.role });
    if (url.pathname === '/api/v1/me/profile') return send(200, { displayName: '基线读者', email: 'reader@example.test' });
    if (url.pathname === '/api/v1/me/profile/avatar') return send(200, pages.get('/reader/icon-192.svg').body, 'image/svg+xml');
    if (url.pathname === '/api/v1/me/legado/tokens') return send(200, []);
    if (url.pathname === '/api/v1/me/reading/preferences') {
      if (request.method === 'PUT') state.preferences = value;
      return send(200, state.preferences);
    }
    if (url.pathname.startsWith('/api/v1/me/reading/progress/')) {
      if (request.method === 'PUT') state.progress = value;
      return send(200, state.progress);
    }
    if (url.pathname.startsWith('/api/v1/me/reading/')) return send(state.failReading ? 503 : 200, state.failReading ? { error: 'fixture_unavailable' } : []);
    if (url.pathname === '/api/v1/admin/operations/overview') return send(200, {
      generatedAt: '2026-10-22T00:00:00Z', status: 'ready',
      sources: { status: 'ready', data: [{ sourceId: 'fixture', displayName: '夹具来源', status: 'ready', isEnabled: true, capabilities: [] }] }, crawler: { status: 'ready', data: [] },
      consistency: { status: 'ready', report: { status: 'healthy', totalIssueCount: 0, issues: [] } }
    });
    if (url.pathname.startsWith('/api/v1/admin/')) return send(200, { data: [], items: [], hasMore: false, nextCursor: null });
    return send(200, {});
  });
  await new Promise(resolve => server.listen(port, '127.0.0.1', resolve));
  return { server, state, manifest, baseUrl: `http://127.0.0.1:${server.address().port}` };
}
module.exports = { startServer };
if (require.main === module) startServer(path.resolve(process.argv[2]), Number(process.argv[3] || 4204)).then(({ baseUrl }) => console.log(baseUrl));
