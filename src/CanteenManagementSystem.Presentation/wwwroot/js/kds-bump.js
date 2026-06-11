/* =============================================================================
   kds-bump.js — bump-bar / large-format toggle for the Kitchen Display
   -----------------------------------------------------------------------------
   Activates only when the page declares itself as a KDS page by setting
   `<body data-kds-page="true">`. Wires:
     * .kds-bump-toggle           — flips the body[data-kds-bump] attribute
     * F11                        — same as the toggle button
     * "f"                        — also toggles (one-hand quickness)
     * persists the choice in localStorage so reloads stay in mode

   The toggle button is auto-injected on KDS pages — no Razor change required.
============================================================================= */

(function () {
    'use strict';
    if (!document.body || document.body.dataset.kdsPage !== 'true') return;

    function setBump(on) {
        document.body.setAttribute('data-kds-bump', on ? 'true' : 'false');
        try { localStorage.setItem('cms-kds-bump', on ? 'true' : 'false'); } catch (e) {}
        var btn = document.querySelector('.kds-bump-toggle');
        if (btn) btn.textContent = on ? '✕ Exit large mode' : '⛶ Large mode';
    }

    function init() {
        var saved = null;
        try { saved = localStorage.getItem('cms-kds-bump'); } catch (e) {}
        setBump(saved === 'true');

        if (!document.querySelector('.kds-bump-toggle')) {
            var b = document.createElement('button');
            b.className = 'kds-bump-toggle';
            b.type = 'button';
            b.setAttribute('aria-label', 'Toggle large-format mode');
            b.textContent = '⛶ Large mode';
            b.addEventListener('click', function () {
                var on = document.body.getAttribute('data-kds-bump') !== 'true';
                setBump(on);
            });
            document.body.appendChild(b);
        }

        document.addEventListener('keydown', function (e) {
            if (e.target && /^(input|textarea|select)$/i.test(e.target.tagName || '')) return;
            if (e.key === 'F11' || e.key === 'f' || e.key === 'F') {
                e.preventDefault();
                var on = document.body.getAttribute('data-kds-bump') !== 'true';
                setBump(on);
            }
        });
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();
})();
