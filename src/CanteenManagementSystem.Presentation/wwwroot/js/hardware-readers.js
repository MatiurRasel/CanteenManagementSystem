/* =============================================================================
   hardware-readers.js — WebHID NFC reader + Web Serial barcode scanner
   -----------------------------------------------------------------------------
   Modern Chrome / Edge expose two browser-native paths to USB peripherals:

     * WebHID    — for HID-class NFC readers (ACR122U, ACR1252U, etc.)
     * Web Serial — for COM-port barcode scanners + USB HID printers

   No native library required. The user grants device permission on first use.
   On scan, the script dispatches a CustomEvent on the target input element so
   existing keyboard-style flows work unchanged.

   USAGE
       <input type="text" data-cl-hid-uid       autofocus />  → fills with NFC UID
       <input type="text" data-cl-serial-barcode autofocus /> → fills with barcode

   Buttons:
       <button data-cl-hid-pair>Pair NFC reader</button>
       <button data-cl-serial-pair>Pair barcode scanner</button>

   Persisted pairings: the browser remembers the user's grant; subsequent
   visits re-attach without prompting.
============================================================================= */

(function () {
    'use strict';

    /* ─── WebHID: NFC card UID ─────────────────────────────────────────── */
    async function pairHid() {
        if (!('hid' in navigator)) { alert('WebHID is not supported. Use Chrome or Edge.'); return; }
        try {
            // PC/SC HID class — many NFC readers identify as 0xFFCA (CCID-USB).
            const devices = await navigator.hid.requestDevice({
                filters: [{ usagePage: 0xFFCA }, { vendorId: 0x072F /* ACS */ }]
            });
            const dev = devices[0];
            if (!dev) return;
            if (!dev.opened) await dev.open();
            dev.addEventListener('inputreport', onHidReport);
            window.showToast && window.showToast(`Paired: ${dev.productName}`, 'success');
        } catch (e) {
            console.warn('HID pair failed', e);
            window.showToast && window.showToast('Pairing failed: ' + e.message, 'danger');
        }
    }

    function onHidReport(ev) {
        // Convert the raw HID report data to a card UID string.
        const data = new Uint8Array(ev.data.buffer);
        if (data.length < 5) return;
        // Skip a 3-byte prefix common to ACS readers; trim trailing 0x00 padding.
        const trimmed = data.slice(3).filter((b, i, arr) => !(i === arr.length - 1 && b === 0));
        const uid = Array.from(trimmed).map((b) => b.toString(16).padStart(2, '0')).join(':').toUpperCase();
        deliver('cl-hid-uid', uid);
    }

    async function reattachHid() {
        if (!('hid' in navigator)) return;
        try {
            const known = await navigator.hid.getDevices();
            for (const dev of known) {
                if (!dev.opened) await dev.open();
                dev.addEventListener('inputreport', onHidReport);
            }
        } catch (e) { /* permissions revoked */ }
    }

    /* ─── Web Serial: barcode scanner (text-line per scan) ─────────────── */
    let serialReader = null;
    async function pairSerial() {
        if (!('serial' in navigator)) { alert('Web Serial is not supported. Use Chrome or Edge.'); return; }
        try {
            const port = await navigator.serial.requestPort({});
            await port.open({ baudRate: 9600 });
            const decoder = new TextDecoderStream();
            port.readable.pipeTo(decoder.writable);
            serialReader = decoder.readable.getReader();
            window.showToast && window.showToast('Barcode scanner paired.', 'success');
            pumpSerial();
        } catch (e) {
            console.warn('Serial pair failed', e);
            window.showToast && window.showToast('Pairing failed: ' + e.message, 'danger');
        }
    }

    async function pumpSerial() {
        let buf = '';
        while (serialReader) {
            try {
                const { value, done } = await serialReader.read();
                if (done) break;
                if (!value) continue;
                buf += value;
                let nl;
                while ((nl = buf.indexOf('\n')) >= 0) {
                    const line = buf.substring(0, nl).trim();
                    buf = buf.substring(nl + 1);
                    if (line.length > 0) deliver('cl-serial-barcode', line);
                }
            } catch (e) {
                console.warn('Serial read failed', e); break;
            }
        }
    }

    /* ─── Common: dispatch the scanned value to a focused/target input ── */
    function deliver(role, value) {
        // First: an element marked with the role attribute.
        let el = document.querySelector('[data-' + role + ']');
        if (!el) el = document.activeElement;
        if (!el || (el.tagName !== 'INPUT' && el.tagName !== 'TEXTAREA')) return;
        el.value = value;
        el.dispatchEvent(new Event('input',  { bubbles: true }));
        el.dispatchEvent(new Event('change', { bubbles: true }));
        // If the input lives in a form with [data-cl-submit-on-scan], submit it.
        const form = el.closest('form[data-cl-submit-on-scan]');
        if (form) form.submit();
    }

    function init() {
        document.addEventListener('click', (e) => {
            if (e.target.closest('[data-cl-hid-pair]'))    { e.preventDefault(); pairHid(); }
            if (e.target.closest('[data-cl-serial-pair]')) { e.preventDefault(); pairSerial(); }
        });
        reattachHid();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
