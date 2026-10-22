const CACHE_NAME = "inkflow-reader-shell-v1";
const SHELL = [
  "/reader/offline",
  "/reader/manifest.webmanifest",
  "/reader/icon-192.svg",
  "/reader/icon-512.svg"
];

self.addEventListener("install", (event) => {
  event.waitUntil((async () => {
    const cache = await caches.open(CACHE_NAME);
    await Promise.all(SHELL.map(async (resource) => {
      try {
        const response = await fetch(resource, { cache: "no-store" });
        if (response.ok) await cache.put(resource, response);
      } catch { /* a later visit can populate the shell */ }
    }));
    await self.skipWaiting();
  })());
});

self.addEventListener("activate", (event) => {
  event.waitUntil((async () => {
    const keys = await caches.keys();
    await Promise.all(keys
      .filter((key) => key.startsWith("inkflow-reader-shell-") && key !== CACHE_NAME)
      .map((key) => caches.delete(key)));
    await self.clients.claim();
  })());
});

self.addEventListener("fetch", (event) => {
  const request = event.request;
  const url = new URL(request.url);
  if (request.method !== "GET" || url.origin !== self.location.origin || !url.pathname.startsWith("/reader/")) return;

  if (SHELL.includes(url.pathname)) {
    event.respondWith(caches.match(request).then((cached) => cached || fetch(request)));
    return;
  }

  if (request.mode === "navigate") {
    event.respondWith(fetch(request).catch(() => caches.match("/reader/offline")));
  }
});
