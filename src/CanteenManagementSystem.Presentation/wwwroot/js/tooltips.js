// =============================================================================
// tooltips.js  (wwwroot)
// -----------------------------------------------------------------------------
// Initialises Bootstrap 5 tooltips for every element that opts in via
//   data-bs-toggle="tooltip" title="…"
// Picks up dynamically inserted nodes too via a MutationObserver, so the
// kitchen display / live order list still get tooltips after SignalR pushes.
// =============================================================================

(function () {
    'use strict';

    function ensureTooltip (el) {
        if (!el || el._tt) return;
        if (typeof bootstrap === 'undefined' || !bootstrap.Tooltip) return;
        el._tt = new bootstrap.Tooltip(el, { container: 'body', trigger: 'hover focus' });
    }

    function initAll (root) {
        if (!root || !root.querySelectorAll) return;
        root.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(ensureTooltip);
    }

    document.addEventListener('DOMContentLoaded', () => initAll(document));

    new MutationObserver(muts => {
        muts.forEach(m => m.addedNodes && m.addedNodes.forEach(n => {
            if (n.nodeType !== 1) return;
            if (n.matches && n.matches('[data-bs-toggle="tooltip"]')) ensureTooltip(n);
            initAll(n);
        }));
    }).observe(document.body, { childList: true, subtree: true });
})();
