// Browser-side helper used by the counter / kiosk pages.
// When the device is offline, queue order POSTs to IndexedDB; otherwise send
// immediately. Reuses the service worker's background-sync registration so
// queued orders flush automatically once connectivity returns.
(function () {
  'use strict';

  const DB_NAME = 'canteen-offline';
  const STORE = 'orders';
  const SYNC_TAG = 'canteen-offline-orders';

  if ('serviceWorker' in navigator) {
    window.addEventListener('load', () => {
      navigator.serviceWorker.register('/service-worker.js')
        .catch((err) => console.warn('Service worker registration failed:', err));
    });
  }

  window.canteenOfflineQueue = {
    enqueue: enqueue,
    flush: flush
  };

  async function enqueue(url, payload) {
    const idempotencyKey = (crypto && crypto.randomUUID) ? crypto.randomUUID() : (Date.now() + '-' + Math.random());
    const db = await openDb();
    await promisify(db.transaction(STORE, 'readwrite').objectStore(STORE).add({
      url,
      payload,
      idempotencyKey,
      createdAt: Date.now()
    }));
    if ('serviceWorker' in navigator && navigator.serviceWorker.controller) {
      try {
        const registration = await navigator.serviceWorker.ready;
        if ('sync' in registration) {
          await registration.sync.register(SYNC_TAG);
        } else {
          navigator.serviceWorker.controller.postMessage({ type: 'REPLAY_QUEUE' });
        }
      } catch (e) { /* swallow — replay still happens via online event */ }
    }
    return { idempotencyKey, queued: true };
  }

  async function flush() {
    if (navigator.serviceWorker && navigator.serviceWorker.controller) {
      navigator.serviceWorker.controller.postMessage({ type: 'REPLAY_QUEUE' });
    }
  }

  window.addEventListener('online', () => flush().catch(() => {}));

  function openDb() {
    return new Promise((resolve, reject) => {
      const req = indexedDB.open(DB_NAME, 1);
      req.onupgradeneeded = () => {
        const db = req.result;
        if (!db.objectStoreNames.contains(STORE)) {
          db.createObjectStore(STORE, { keyPath: 'id', autoIncrement: true });
        }
      };
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
  }

  function promisify(req) {
    return new Promise((resolve, reject) => {
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
  }
})();
