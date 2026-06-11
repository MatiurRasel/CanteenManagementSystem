// =============================================================================
// sw.js — service worker for Web Push notifications + offline shell hint
// -----------------------------------------------------------------------------
// Registered by /js/push-client.js on user opt-in. The push event surfaces a
// system notification with the payload JSON sent by IWebPushService.
//
// SCOPE
//   Registration scope is "/" so the worker can receive notifications from
//   anywhere in the app. The offline shell is intentionally minimal — only
//   stale-while-revalidate for static assets to avoid serving stale dynamic
//   pages. Full offline-queue is a future expansion.
// =============================================================================

const CACHE = 'canteen-shell-v1';
const STATIC_ASSETS = [
    '/css/site.css',
    '/css/app-shell.css',
    '/css/canteen-loading.css',
    '/js/canteen-loading.js',
    '/js/tooltips.js',
    '/lib/bootstrap/dist/css/bootstrap.min.css'
];

self.addEventListener('install', (e) => {
    self.skipWaiting();
    e.waitUntil(caches.open(CACHE).then((c) => c.addAll(STATIC_ASSETS).catch(() => null)));
});

self.addEventListener('activate', (e) => {
    e.waitUntil(self.clients.claim());
});

self.addEventListener('push', (event) => {
    let data = {};
    try { data = event.data ? event.data.json() : {}; }
    catch (e) { data = { title: 'Notification', body: event.data ? event.data.text() : '' }; }

    const title = data.Title || data.title || 'Canteen';
    const options = {
        body: data.Body || data.body || '',
        icon: data.Icon || data.icon || '/favicon.ico',
        badge: '/favicon.ico',
        tag: data.Tag || data.tag,
        data: { url: data.Url || data.url || '/' },
        renotify: !!(data.Tag || data.tag)
    };
    event.waitUntil(self.registration.showNotification(title, options));
});

self.addEventListener('notificationclick', (event) => {
    event.notification.close();
    const target = (event.notification.data && event.notification.data.url) || '/';
    event.waitUntil(
        clients.matchAll({ type: 'window' }).then((wins) => {
            for (const w of wins) {
                if ('focus' in w) { w.navigate(target); w.focus(); return; }
            }
            if (clients.openWindow) return clients.openWindow(target);
        })
    );
});

self.addEventListener('fetch', (event) => {
    const req = event.request;
    if (req.method !== 'GET') return;
    const url = new URL(req.url);
    if (!STATIC_ASSETS.includes(url.pathname)) return;
    event.respondWith((async () => {
        try {
            const fresh = await fetch(req);
            const cache = await caches.open(CACHE);
            cache.put(req, fresh.clone());
            return fresh;
        } catch (e) {
            const cached = await caches.match(req);
            if (cached) return cached;
            throw e;
        }
    })());
});
