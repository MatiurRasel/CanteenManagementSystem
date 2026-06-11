// =============================================================================
// audio-cues.js  (wwwroot)
// -----------------------------------------------------------------------------
// Universal success/error beeps + haptic feedback for the operator counter.
//
// FIRES AUTOMATICALLY ON
//   * Any Bootstrap toast that renders with a known variant class
//       (.text-bg-success → ding, .text-bg-danger → error tone)
//   * Any element that adds class "kiosk-scan-success" / "kiosk-scan-error"
//   * Programmatic   : window.canteenCues.success() / .error() / .info() / .warn()
//
// CUSTOMISE
//   Auto-disabled when the audio context can't unlock (e.g. iOS Safari before
//   first user gesture). The first touchstart/click anywhere on the page primes
//   it so subsequent beeps are silent failures rather than blocking errors.
// =============================================================================

(function () {
    'use strict';

    let ctx = null;
    let unlocked = false;

    function ensureCtx () {
        if (ctx) return ctx;
        try {
            ctx = new (window.AudioContext || window.webkitAudioContext)();
        } catch { ctx = null; }
        return ctx;
    }

    function unlock () {
        const c = ensureCtx();
        if (!c) return;
        if (c.state === 'suspended') c.resume().catch(() => {});
        unlocked = true;
    }

    // Try to unlock on first user gesture.
    ['touchstart', 'click', 'keydown'].forEach(ev =>
        document.addEventListener(ev, unlock, { once: true, capture: true, passive: true }));

    function beep (freq, durationMs, volume, type) {
        const c = ensureCtx();
        if (!c) return;
        if (!unlocked && c.state === 'suspended') return;
        const osc = c.createOscillator();
        const gain = c.createGain();
        osc.type = type || 'sine';
        osc.frequency.value = freq;
        gain.gain.value = volume;
        osc.connect(gain).connect(c.destination);
        osc.start();
        gain.gain.exponentialRampToValueAtTime(0.0001, c.currentTime + durationMs / 1000);
        osc.stop(c.currentTime + durationMs / 1000);
    }

    function vibrate (pattern) {
        if (navigator.vibrate) navigator.vibrate(pattern);
    }

    const cues = {
        success () { beep(880, 90, 0.18, 'sine'); setTimeout(() => beep(1318, 110, 0.18, 'sine'), 80); vibrate(40); },
        error   () { beep(180, 220, 0.22, 'square'); setTimeout(() => beep(140, 260, 0.22, 'square'), 230); vibrate([60, 60, 60]); },
        info    () { beep(660, 90, 0.14, 'triangle'); vibrate(20); },
        warn    () { beep(440, 170, 0.18, 'sawtooth'); vibrate(50); }
    };

    window.canteenCues = cues;

    // Auto-bind: Bootstrap toast shown event.
    document.addEventListener('shown.bs.toast', function (evt) {
        const el = evt.target;
        if (!el) return;
        if (el.classList.contains('text-bg-success')) cues.success();
        else if (el.classList.contains('text-bg-danger')) cues.error();
        else if (el.classList.contains('text-bg-warning')) cues.warn();
        else if (el.classList.contains('text-bg-info')) cues.info();
    });

    // Auto-bind: legacy alert markers (counter screen).
    new MutationObserver(muts => {
        muts.forEach(m => m.addedNodes && m.addedNodes.forEach(n => {
            if (n.nodeType !== 1) return;
            const cls = n.classList;
            if (!cls) return;
            if (cls.contains('kiosk-scan-success')) cues.success();
            else if (cls.contains('kiosk-scan-error')) cues.error();
        }));
    }).observe(document.body, { childList: true, subtree: true });
})();
