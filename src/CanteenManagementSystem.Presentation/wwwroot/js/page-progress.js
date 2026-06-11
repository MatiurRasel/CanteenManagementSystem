// =============================================================================
// page-progress.js
// -----------------------------------------------------------------------------
// Lightweight NProgress-style top progress bar. Zero dependencies, ~80 LOC.
//
// FIRES ON
//   * Any form submit (POST, PUT, DELETE, etc.) until the response navigates.
//   * Any in-page navigation click (<a href> to same-origin, non-_blank).
//   * Any fetch() / XMLHttpRequest the page issues.
//
// VISUAL
//   3px bar pinned to the top, painted in the app accent color via
//   CSS var --app-accent. Animates smoothly to ~85% during the request and
//   completes to 100% on load/response, then fades out.
// =============================================================================

(function () {
    var bar = document.querySelector('#page-progress > .page-progress-bar');
    if (!bar) return;
    var root = document.getElementById('page-progress');

    var active = 0;          // count of in-flight requests / pending navigations
    var pct = 0;
    var timer = null;

    function set(p) {
        pct = Math.max(0, Math.min(100, p));
        bar.style.width = pct + '%';
        root.style.opacity = pct >= 100 ? '0' : '1';
        root.setAttribute('aria-hidden', pct >= 100 ? 'true' : 'false');
    }

    function start() {
        active++;
        if (active === 1) {
            set(8);
            clearInterval(timer);
            timer = setInterval(function () {
                // approach 85% asymptotically
                if (pct < 85) set(pct + Math.max(0.2, (85 - pct) * 0.08));
            }, 150);
        }
    }

    function done() {
        active = Math.max(0, active - 1);
        if (active === 0) {
            clearInterval(timer);
            set(100);
            setTimeout(function () { set(0); }, 280);
        }
    }

    // ─── form submits ─────────────────────────────────────────────────────
    document.addEventListener('submit', function (e) {
        if (!e.target || e.defaultPrevented) return;
        // skip forms targeting a new window / explicit ignore
        if (e.target.target === '_blank') return;
        if (e.target.hasAttribute('data-no-progress')) return;
        start();
    }, true);

    // ─── link clicks (in-page navigations) ────────────────────────────────
    document.addEventListener('click', function (e) {
        var a = e.target && e.target.closest ? e.target.closest('a[href]') : null;
        if (!a) return;
        if (a.target === '_blank') return;
        if (e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
        if (a.hasAttribute('data-no-progress') || a.getAttribute('href').startsWith('#')) return;
        // same-origin only
        try {
            var u = new URL(a.href, window.location.href);
            if (u.origin !== window.location.origin) return;
        } catch (_) { return; }
        start();
    }, true);

    // Hide on back/forward navigation
    window.addEventListener('pageshow', function () { active = 0; clearInterval(timer); set(0); });
    window.addEventListener('beforeunload', function () { /* keep the bar painted */ });

    // ─── fetch() wrapper ──────────────────────────────────────────────────
    if (window.fetch) {
        var origFetch = window.fetch;
        window.fetch = function () {
            start();
            return origFetch.apply(this, arguments).finally(done);
        };
    }

    // ─── XHR wrapper ──────────────────────────────────────────────────────
    if (window.XMLHttpRequest) {
        var open = XMLHttpRequest.prototype.open;
        XMLHttpRequest.prototype.open = function () {
            this.addEventListener('loadstart', start);
            this.addEventListener('loadend', done);
            return open.apply(this, arguments);
        };
    }
})();
