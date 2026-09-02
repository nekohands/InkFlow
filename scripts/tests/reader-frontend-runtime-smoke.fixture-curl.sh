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
case "$url" in
  */reader)
    body='lang="zh-CN" id="main-content" role="search" class="search-bar" class="skip-link" :focus-visible @media (max-width: 640px) @media (prefers-reduced-motion: reduce) /reader/manifest.webmanifest id="reader-install" beforeinstallprompt event.preventDefault() appinstalled reader-auth-pending location.replace returnTo'
    ;;
  */reader/account)
    body='id="reader-login-form" /reader/account/register autocomplete="current-password" reader-auth-pending sessionStorage rel="manifest" /reader/sw.js aria-live="polite"'
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
printf '200'
