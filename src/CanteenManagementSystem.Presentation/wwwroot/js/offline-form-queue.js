/* =============================================================================
   offline-form-queue.js — generic IndexedDB write-buffer for any form
   -----------------------------------------------------------------------------
   Complements the order-specific /js/offline-queue.js. Marks ANY form with
   [data-cl-offline] for offline-tolerant submit:

     1. Form submit goes through normally when navigator.onLine is true.
     2. When offline → form data serialised into IndexedDB ('cms-form-queue'
        store) and a toast announces "Saved offline".
     3. On `online` event + every page load while online, the queue drains
        FIFO; each successful POST removes the row.

   The order-specific queue at /js/offline-queue.js handles the kiosk/counter
   submit path. This script handles everything else: void / refund forms,
   admin CRUD forms with [data-cl-offline] opt-in, etc.
============================================================================= */

(function () {
    'use strict';
    if (!('indexedDB' in window) || !('fetch' in window)) return;

    const DB = 'cms-form-queue';
    const STORE = 'queue';

    function openDb() {
        return new Promise((resolve, reject) => {
            const req = indexedDB.open(DB, 1);
            req.onupgradeneeded = () => req.result.createObjectStore(STORE, { keyPath: 'id', autoIncrement: true });
            req.onsuccess = () => resolve(req.result);
            req.onerror   = () => reject(req.error);
        });
    }

    async function enqueue(entry) {
        const db = await openDb();
        await new Promise((res, rej) => {
            const tx = db.transaction(STORE, 'readwrite');
            tx.objectStore(STORE).add(entry);
            tx.oncomplete = res; tx.onerror = () => rej(tx.error);
        });
        updateBadge(+1);
    }

    async function listQueued() {
        const db = await openDb();
        return await new Promise((res, rej) => {
            const tx = db.transaction(STORE, 'readonly');
            const req = tx.objectStore(STORE).getAll();
            req.onsuccess = () => res(req.result || []);
            req.onerror   = () => rej(req.error);
        });
    }

    async function remove(id) {
        const db = await openDb();
        await new Promise((res, rej) => {
            const tx = db.transaction(STORE, 'readwrite');
            tx.objectStore(STORE).delete(id);
            tx.oncomplete = res; tx.onerror = () => rej(tx.error);
        });
        updateBadge(-1);
    }

    let counter = 0;
    function updateBadge(delta) {
        counter = Math.max(0, counter + delta);
        document.querySelectorAll('[data-cl-offline-badge]').forEach((el) => {
            el.textContent = counter > 0 ? counter.toString() : '';
            el.style.display = counter > 0 ? 'inline-block' : 'none';
        });
    }

    async function refreshBadge() {
        const all = await listQueued().catch(() => []);
        counter = all.length;
        updateBadge(0);
    }

    function toast(msg, variant) {
        if (window.showToast) window.showToast(msg, variant || 'info');
        else console.info('[offline-form]', msg);
    }

    async function drain() {
        if (!navigator.onLine) return;
        const items = await listQueued().catch(() => []);
        for (const e of items) {
            try {
                const r = await fetch(e.url, {
                    method: e.method || 'POST',
                    credentials: 'same-origin',
                    headers: e.headers || { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: e.body
                });
                if (r.ok) {
                    await remove(e.id);
                    toast('Synced queued submission.', 'success');
                } else {
                    console.warn('[offline-form] sync got', r.status, 'for', e.url);
                    break;
                }
            } catch (err) {
                console.warn('[offline-form] sync failed', err);
                break;
            }
        }
    }

    function attachForm(form) {
        if (form.dataset.clOfflineBound === '1') return;
        form.dataset.clOfflineBound = '1';
        form.addEventListener('submit', async (ev) => {
            if (navigator.onLine) return;
            ev.preventDefault();
            const data = new FormData(form);
            const body = new URLSearchParams();
            for (const [k, v] of data.entries()) body.append(k, typeof v === 'string' ? v : '(file)');
            await enqueue({
                url: form.action || location.href,
                method: (form.method || 'POST').toUpperCase(),
                body: body.toString(),
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                queuedAt: new Date().toISOString()
            });
            toast('Saved offline — will sync when you reconnect.', 'warning');
            form.reset();
        });
    }

    function init() {
        document.querySelectorAll('form[data-cl-offline]').forEach(attachForm);
        refreshBadge();
        window.addEventListener('online',  () => drain());
        window.addEventListener('offline', () => toast('Offline — submissions will queue locally.', 'warning'));
        if (navigator.onLine) drain();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();

    window.CanteenOfflineFormQueue = { drain, list: listQueued };
})();
