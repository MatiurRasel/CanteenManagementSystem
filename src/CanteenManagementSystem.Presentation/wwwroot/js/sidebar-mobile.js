/* =============================================================================
   sidebar-mobile.js — sidebar off-canvas + high-contrast toggle
   -----------------------------------------------------------------------------
   Companion to /css/app-mobile.css. Adds:
     * [data-toggle="sidebar"]    — flips the sidebar's data-mobile-open attr
     * Backdrop tap               — closes the sidebar
     * [data-toggle="contrast"]   — flips <html data-contrast="high|normal">,
                                    persists choice in localStorage,
                                    falls back to (prefers-contrast: more)
     * Auto-close on viewport > 991.98 px so users who rotate to landscape
       don't get a stale open-state.
============================================================================= */

(function () {
    'use strict';

    function $$(sel) { return Array.prototype.slice.call(document.querySelectorAll(sel)); }

    function ensureBackdrop(sidebar) {
        var next = sidebar.nextElementSibling;
        if (next && next.classList.contains('app-shell-sidebar-backdrop')) return next;
        var bd = document.createElement('div');
        bd.className = 'app-shell-sidebar-backdrop';
        sidebar.parentNode.insertBefore(bd, sidebar.nextSibling);
        bd.addEventListener('click', function () {
            sidebar.setAttribute('data-mobile-open', 'false');
        });
        return bd;
    }

    function bindSidebar() {
        var sidebar = document.querySelector('.app-shell-sidebar');
        if (!sidebar) return;
        ensureBackdrop(sidebar);

        $$('[data-toggle="sidebar"]').forEach(function (btn) {
            if (btn.dataset.boundSidebar === '1') return;
            btn.dataset.boundSidebar = '1';
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                var open = sidebar.getAttribute('data-mobile-open') === 'true';
                sidebar.setAttribute('data-mobile-open', open ? 'false' : 'true');
            });
        });

        // Auto-close when widening past the breakpoint.
        var mq = window.matchMedia('(min-width: 992px)');
        var handler = function (e) { if (e.matches) sidebar.setAttribute('data-mobile-open', 'false'); };
        if (mq.addEventListener) mq.addEventListener('change', handler);
        else mq.addListener(handler);
    }

    function applyContrast(mode) {
        document.documentElement.setAttribute('data-contrast', mode === 'high' ? 'high' : 'normal');
        try { localStorage.setItem('cms-contrast', mode === 'high' ? 'high' : 'normal'); } catch (e) {}
    }

    function bindContrast() {
        // Initial state: localStorage wins, else OS hint.
        var saved = null;
        try { saved = localStorage.getItem('cms-contrast'); } catch (e) {}
        if (saved === 'high') {
            applyContrast('high');
        } else if (saved !== 'normal' && window.matchMedia && window.matchMedia('(prefers-contrast: more)').matches) {
            applyContrast('high');
        }

        $$('[data-toggle="contrast"]').forEach(function (btn) {
            if (btn.dataset.boundContrast === '1') return;
            btn.dataset.boundContrast = '1';
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                var current = document.documentElement.getAttribute('data-contrast');
                applyContrast(current === 'high' ? 'normal' : 'high');
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () { bindSidebar(); bindContrast(); });
    } else {
        bindSidebar(); bindContrast();
    }
})();
