/* =============================================================================
   push-client.js — opt-in browser-push registration
   -----------------------------------------------------------------------------
   Wire on any button with [data-cl-push-enable]:
       <button data-cl-push-enable>Enable notifications</button>

   On click:
       1. Asks for Notification permission.
       2. Registers /js/sw.js as a service worker.
       3. Fetches /push/vapid to get the public key.
       4. Subscribes via PushManager.subscribe(applicationServerKey).
       5. POSTs the resulting JSON to /push/subscribe with antiforgery token.

   Status surfaces via button text + toast (uses window toast helper if present).
============================================================================= */

(function () {
    'use strict';
    if (!('serviceWorker' in navigator) || !('PushManager' in window)) return;

    function urlBase64ToUint8Array(base64String) {
        const padding = '='.repeat((4 - (base64String.length % 4)) % 4);
        const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
        const raw = atob(base64);
        const bytes = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
        return bytes;
    }

    function antiforgeryToken() {
        const el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    }

    async function enable(btn) {
        try {
            btn && btn.setAttribute('disabled', 'disabled');
            const perm = await Notification.requestPermission();
            if (perm !== 'granted') throw new Error('Notification permission denied.');

            const reg = await navigator.serviceWorker.register('/js/sw.js', { scope: '/' });
            await navigator.serviceWorker.ready;

            const vapid = await fetch('/push/vapid', { credentials: 'same-origin' }).then((r) => r.json());
            if (!vapid.configured) throw new Error('Web Push is not configured for this tenant. Ask your admin to set WebPush.VapidPublicKey + WebPush.VapidPrivateKey.');

            const sub = await reg.pushManager.subscribe({
                userVisibleOnly: true,
                applicationServerKey: urlBase64ToUint8Array(vapid.publicKey)
            });

            const res = await fetch('/push/subscribe', {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': antiforgeryToken()
                },
                body: JSON.stringify({
                    endpoint: sub.endpoint,
                    keys: {
                        p256dh: arrayBufferToBase64(sub.getKey('p256dh')),
                        auth:   arrayBufferToBase64(sub.getKey('auth'))
                    }
                })
            });
            if (!res.ok) throw new Error(`Server rejected the subscription (${res.status}).`);
            if (btn) { btn.textContent = '✓ Notifications enabled'; btn.removeAttribute('disabled'); }
            if (window.showToast) window.showToast('Notifications enabled.', 'success');
        }
        catch (e) {
            console.warn('Push enable failed', e);
            if (btn) { btn.removeAttribute('disabled'); btn.textContent = 'Enable notifications'; }
            if (window.showToast) window.showToast('Push setup failed: ' + e.message, 'danger');
            else alert('Push setup failed: ' + e.message);
        }
    }

    function arrayBufferToBase64(buf) {
        const bytes = new Uint8Array(buf);
        let bin = '';
        for (let i = 0; i < bytes.byteLength; i++) bin += String.fromCharCode(bytes[i]);
        return btoa(bin);
    }

    document.addEventListener('click', function (ev) {
        const btn = ev.target.closest('[data-cl-push-enable]');
        if (btn) { ev.preventDefault(); enable(btn); }
    }, false);
})();
