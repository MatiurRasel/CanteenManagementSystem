/* =============================================================================
   sidebar-resize.js — click-and-drag sidebar width on desktop
   -----------------------------------------------------------------------------
   * Mouse / touch drag on [data-sidebar-resize] adjusts CSS var
     `--sidebar-w` on the root element. The aside + main pane both read this
     var, so the layout reflows in real time.
   * Width is constrained to [MIN, MAX]; persisted in localStorage.
   * Double-click on the handle resets to the default.
   * Keyboard support: ArrowLeft / ArrowRight nudges by 16 px when the handle
     is focused; Home resets to default.
   * Disabled when sidebar is collapsed (the handle is hidden via CSS).
   * Auto-applies the saved width before any user interaction.
============================================================================= */
(function () {
    'use strict';

    var KEY = 'cms-sidebar-width';
    var MIN = 200;          // px — narrowest readable width
    var MAX = 380;          // px — keeps content pane usable
    var DEFAULT = 260;      // px — matches --sidebar-w default

    function clamp(n) { return Math.max(MIN, Math.min(MAX, Math.round(n))); }

    function applyWidth(px, persist) {
        document.documentElement.style.setProperty('--sidebar-w', px + 'px');
        if (persist) {
            try { localStorage.setItem(KEY, String(px)); } catch (e) {}
        }
    }

    function applyInitialState() {
        try {
            var saved = parseInt(localStorage.getItem(KEY) || '', 10);
            if (Number.isFinite(saved) && saved >= MIN && saved <= MAX) {
                applyWidth(saved, false);
            }
        } catch (e) {}
    }

    function bindResize() {
        var handle = document.querySelector('[data-sidebar-resize]');
        if (!handle) return;

        var dragging = false;
        var startX = 0;
        var startW = DEFAULT;

        function beginDrag(clientX) {
            if (document.body.classList.contains('sidebar-collapsed')) return;
            dragging = true;
            startX = clientX;
            var menu = document.getElementById('layout-menu');
            startW = menu ? menu.getBoundingClientRect().width : DEFAULT;
            document.body.classList.add('sidebar-resizing');
        }

        function moveDrag(clientX) {
            if (!dragging) return;
            var next = clamp(startW + (clientX - startX));
            applyWidth(next, false);
        }

        function endDrag() {
            if (!dragging) return;
            dragging = false;
            document.body.classList.remove('sidebar-resizing');
            var current = parseFloat(getComputedStyle(document.documentElement).getPropertyValue('--sidebar-w'));
            if (Number.isFinite(current)) {
                try { localStorage.setItem(KEY, String(Math.round(current))); } catch (e) {}
            }
        }

        // Mouse
        handle.addEventListener('mousedown', function (e) {
            if (e.button !== 0) return;
            e.preventDefault();
            beginDrag(e.clientX);
        });
        document.addEventListener('mousemove', function (e) {
            if (dragging) moveDrag(e.clientX);
        });
        document.addEventListener('mouseup', endDrag);

        // Touch
        handle.addEventListener('touchstart', function (e) {
            if (e.touches.length !== 1) return;
            beginDrag(e.touches[0].clientX);
        }, { passive: true });
        document.addEventListener('touchmove', function (e) {
            if (dragging && e.touches.length === 1) {
                moveDrag(e.touches[0].clientX);
            }
        }, { passive: true });
        document.addEventListener('touchend', endDrag);
        document.addEventListener('touchcancel', endDrag);

        // Double-click: reset
        handle.addEventListener('dblclick', function (e) {
            e.preventDefault();
            applyWidth(DEFAULT, true);
        });

        // Keyboard (when focused)
        handle.addEventListener('keydown', function (e) {
            var menu = document.getElementById('layout-menu');
            if (!menu) return;
            var w = Math.round(menu.getBoundingClientRect().width);
            if (e.key === 'ArrowRight') { e.preventDefault(); applyWidth(clamp(w + 16), true); }
            else if (e.key === 'ArrowLeft') { e.preventDefault(); applyWidth(clamp(w - 16), true); }
            else if (e.key === 'Home') { e.preventDefault(); applyWidth(DEFAULT, true); }
        });
    }

    // Apply saved width as early as possible (script loaded with defer, so
    // DOM is parsed but body may not yet have all children — that's fine,
    // we're just setting a CSS variable on the root).
    applyInitialState();

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', bindResize);
    } else {
        bindResize();
    }
})();
