/* =============================================================================
   sidebar-collapse.js — desktop sidebar collapse/expand (icon-only mode)
   -----------------------------------------------------------------------------
   * Toggles `body.sidebar-collapsed` via any [data-sidebar-collapse] button.
   * Persists state in localStorage (key: cms-sidebar-collapsed = "1" | "0").
   * Restores state on load BEFORE first paint (this script is in <head> via
     defer-after-init pattern, but the early init also happens in the inline
     head script — see _Layout.cshtml).
   * Ctrl+B / Cmd+B keyboard shortcut.
   * Wires up mobile search overlay [data-topbar-mobile-search] / [-close].
   * Wires up Ctrl+K to focus the topbar search input.
   * Wires up [data-customizer-open] → opens the existing template customizer
     (delegates to whatever toggle the customizer partial exposes).
============================================================================= */
(function () {
    'use strict';

    var KEY = 'cms-sidebar-collapsed';

    function applyInitialState() {
        try {
            var saved = localStorage.getItem(KEY);
            if (saved === '1') document.body.classList.add('sidebar-collapsed');
        } catch (e) {}
    }

    function bindCollapse() {
        document.querySelectorAll('[data-sidebar-collapse]').forEach(function (btn) {
            if (btn.dataset.boundCollapse === '1') return;
            btn.dataset.boundCollapse = '1';
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                var on = document.body.classList.toggle('sidebar-collapsed');
                try { localStorage.setItem(KEY, on ? '1' : '0'); } catch (err) {}
            });
        });
    }

    function bindMobileSearch() {
        var overlay = document.getElementById('cms-mobile-search');
        if (!overlay) return;
        document.querySelectorAll('[data-topbar-mobile-search]').forEach(function (btn) {
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                overlay.hidden = false;
                var input = overlay.querySelector('input');
                if (input) input.focus();
            });
        });
        document.querySelectorAll('[data-topbar-mobile-search-close]').forEach(function (btn) {
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                overlay.hidden = true;
            });
        });
    }

    function bindKeyboard() {
        document.addEventListener('keydown', function (e) {
            // Ctrl+B (or Cmd+B) toggles sidebar
            if ((e.ctrlKey || e.metaKey) && (e.key === 'b' || e.key === 'B')) {
                e.preventDefault();
                var on = document.body.classList.toggle('sidebar-collapsed');
                try { localStorage.setItem(KEY, on ? '1' : '0'); } catch (err) {}
                return;
            }
            // Ctrl+K (or Cmd+K) focuses topbar search
            if ((e.ctrlKey || e.metaKey) && (e.key === 'k' || e.key === 'K')) {
                var input = document.getElementById('cms-topbar-search');
                if (input) { e.preventDefault(); input.focus(); input.select(); }
            }
        });
    }

    function bindCustomizerLink() {
        // The existing _TemplateCustomizer partial exposes its toggle via
        // .template-customizer-open or [data-template-customizer-toggle]; we
        // just click whichever exists so the avatar-menu shortcut works.
        document.querySelectorAll('[data-customizer-open]').forEach(function (btn) {
            btn.addEventListener('click', function (e) {
                e.preventDefault();
                var target =
                    document.querySelector('[data-template-customizer-toggle]') ||
                    document.querySelector('.template-customizer-open') ||
                    document.querySelector('#template-customizer-toggle');
                if (target) target.click();
            });
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', function () {
            applyInitialState();
            bindCollapse();
            bindMobileSearch();
            bindKeyboard();
            bindCustomizerLink();
        });
    } else {
        applyInitialState();
        bindCollapse();
        bindMobileSearch();
        bindKeyboard();
        bindCustomizerLink();
    }
})();
