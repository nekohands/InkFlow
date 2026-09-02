#!/usr/bin/env bash
set -Eeuo pipefail

output=""
url=""

while (($# > 0)); do
  case "$1" in
    --output)
      output="$2"
      shift 2
      ;;
    --write-out)
      shift 2
      ;;
    *)
      url="$1"
      shift
      ;;
  esac
done

if [[ -z "$output" || -z "$url" ]]; then
  exit 2
fi

body=""
status=200
case "$url" in
  */reader)
    body='lang="zh-CN" id="main-content" role="search" class="search-bar" class="skip-link" :focus-visible @media (max-width: 640px) @media (prefers-reduced-motion: reduce) /reader/manifest.webmanifest id="reader-install" beforeinstallprompt event.preventDefault() appinstalled reader-auth-pending location.replace returnTo'
    ;;
  */reader/account)
    body='id="reader-login-form" /reader/account/register autocomplete="current-password" reader-session-profile reader-session-role reader-session-avatar-image account-panel account-tabs role="tablist" data-account-tab="profile" account-panel-profile account-panel-security account-panel-reader reader-admin-panel 进入运营中心 reader-profile-form /api/v1/me/profile reader-avatar-form reader-avatar-file accept="image/jpeg,image/png,image/webp" /api/v1/me/profile/avatar 2 MiB reader-password-form /api/v1/me/password reader-legado-token-form /api/v1/me/legado/tokens 记录也会立即删除 撤销会立即删除记录 reader-legado-token-reveal 仅显示一次 reader-auth-pending sessionStorage rel="manifest" /reader/sw.js aria-live="polite"'
    ;;
  */reader/account/register)
    body='id="reader-register-form" autocomplete="new-password" minlength="12" /reader/account reader-auth-pending sessionStorage rel="manifest" /reader/sw.js aria-live="polite"'
    ;;
  */reader/shelf)
    body='data-reader-dashboard="shelf" reader-dashboard-list aria-live="polite" rel="manifest" /reader/sw.js'
    ;;
  */reader/history)
    body='data-reader-dashboard="history" reader-dashboard-list aria-live="polite" rel="manifest" /reader/sw.js'
    ;;
  */reader/offline)
    body='当前处于离线状态 返回书库 rel="manifest" /reader/sw.js'
    ;;
  */reader/read/22222222-2222-4222-8222-222222222222)
    body='该章节尚未发布内容 reader-auth-pending location.replace'
    status=404
    ;;
  */reader/manifest.webmanifest)
    body='{
      "start_url": "/reader",
      "scope": "/reader/",
      "display": "standalone",
      "icons": [
        {"src": "/reader/icon-192.svg", "sizes": "192x192"},
        {"src": "/reader/icon-512.svg", "sizes": "512x512"}
      ]
    }'
    ;;
  */reader/sw.js)
    body='inkflow-reader-shell-v1 /reader/offline request.mode === "navigate" self.clients.claim()'
    ;;
  */reader/icon-192.svg|*/reader/icon-512.svg)
    body='<svg viewBox="0 0 512 512"></svg>'
    ;;
  */admin/operations)
    body='id="operations-content" id="operations-collection-form" id="operations-collection-url" /api/v1/admin/operations/overview /api/v1/admin/collection-runs /api/v1/admin/books/ id="operations-package-form" EPUB 3 单文件 TXT id="operations-policy-form" id="operations-policy-book-id" 当前下架书籍列表 /api/v1/admin/content/takedowns?limit=50 bookId: pendingAction.bookId /api/v1/admin/operations/alerts/history? operations-history-more operations-action-reason aria-live="polite" reader-auth-pending'
    ;;
  *)
    exit 1
    ;;
esac

printf '%s' "$body" > "$output"
printf '%s' "$status"
