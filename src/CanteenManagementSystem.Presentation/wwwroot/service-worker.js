// Canteen Management System — offline-first service worker.
// Strategy:
//   * Static assets (css/js/fonts/images): cache-first with version-bumped cache.
//   * Navigation requests: network-first, fall back to a cached shell.
//   * /api/* and /Order/PlaceOrder writes when offline: queued in IndexedDB
//     and replayed on the next "online" event or via background-sync.

// Bumped to v2 after the Font Awesome → Boxicons migration so existing
// clients drop the stale v1 cache (which precached the removed FA stylesheet).
const CACHE_VERSION = 'canteen-v2';
const STATIC_CACHE = `${CACHE_VERSION}-static`;
const SHELL_URLS = [
  '/',
  '/css/site.css',
  '/css/app-shell.css',
  '/lib/bootstrap/dist/css/bootstrap.min.css',
  '/lib/boxicons/css/boxicons.min.css',
  '/lib/jquery/jquery.min.js',
  '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
  '/js/site.js',
  '/js/app-branding.js',
  '/js/offline-queue.js',
  '/manifest.webmanifest'
];

self.addEventListener('install', (event) => {
  event.waitUntil(
    // Precache best-effort: a single 404 must not abort the whole install
    // (cache.addAll is atomic and would reject), so map each URL individually.
    caches.open(STATIC_CACHE)
      .then((cache) => Promise.allSettled(SHELL_URLS.map((u) => cache.add(u))))
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => !k.startsWith(CACHE_VERSION)).map((k) => caches.delete(k))))
      .then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', (event) => {
  const request = event.request;
  if (request.method !== 'GET') return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;

  // Navigation: network-first, shell fallback.
  if (request.mode === 'navigate') {
    event.respondWith(
      fetch(request).catch(() => caches.match('/').then((r) => r || new Response('Offline', { status: 503 })))
    );
    return;
  }

  // Static: cache-first, populate on miss.
  event.respondWith(
    caches.match(request).then((cached) => {
      if (cached) return cached;
      return fetch(request).then((response) => {
        if (response.ok && (request.destination === 'style' || request.destination === 'script' || request.destination === 'font' || request.destination === 'image')) {
          const clone = response.clone();
          caches.open(STATIC_CACHE).then((c) => c.put(request, clone));
        }
        return response;
      }).catch(() => new Response('', { status: 504 }));
    })
  );
});

// Background sync: replay queued counter orders once the device comes back online.
self.addEventListener('sync', (event) => {
  if (event.tag !== 'canteen-offline-orders') return;
  event.waitUntil(replayQueuedOrders());
});

self.addEventListener('message', (event) => {
  if (event.data && event.data.type === 'REPLAY_QUEUE') {
    replayQueuedOrders();
  }
});

async function replayQueuedOrders() {
  const db = await openDb();
  const tx = db.transaction('orders', 'readwrite');
  const store = tx.objectStore('orders');
  const all = await promisifyRequest(store.getAll());
  for (const item of all) {
    try {
      const response = await fetch(item.url, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'X-Idempotency-Key': item.idempotencyKey },
        body: JSON.stringify(item.payload)
      });
      if (response.ok) {
        await promisifyRequest(store.delete(item.id));
      }
    } catch (e) {
      // Leave in queue; will retry on next sync.
    }
  }
}

function openDb() {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open('canteen-offline', 1);
    req.onupgradeneeded = () => {
      const db = req.result;
      if (!db.objectStoreNames.contains('orders')) {
        db.createObjectStore('orders', { keyPath: 'id', autoIncrement: true });
      }
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

function promisifyRequest(req) {
  return new Promise((resolve, reject) => {
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}
