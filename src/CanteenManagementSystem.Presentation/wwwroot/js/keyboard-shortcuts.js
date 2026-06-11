/* =============================================================================
   keyboard-shortcuts.js — global keyboard shortcuts
   -----------------------------------------------------------------------------
   Listens at document level and reacts to:
     * Esc          — closes any open Bootstrap modal / dismiss any visible toast
                      / clears focus on the active input
                      / if a form with [data-cancel-href] is focused, navigate
                        to the cancel URL
     * Enter        — when focus is inside a form WITHOUT a textarea, submit the
                      form via the first <button type="submit">
     * "?"          — opens the keyboard shortcut help dialog (mounted lazily)
     * "/"          — focuses the topbar search input if present
     * Ctrl+P       — handled by the browser (we deliberately don't intercept;
                      receipt-print.css provides the print stylesheet)

   The script is dependency-free and idempotent — safe to load twice.
============================================================================= */

(function () {
    'use strict';
    if (window.__cmsKbsBound) return;
    window.__cmsKbsBound = true;

    function isEditable(el) {
        if (!el) return false;
        var tag = (el.tagName || '').toUpperCase();
        if (tag === 'TEXTAREA') return true;
        if (tag === 'INPUT') {
            var t = (el.type || '').toLowerCase();
            return ['text','search','password','email','number','tel','url','date','time','datetime-local','month','week'].indexOf(t) !== -1;
        }
        return el.isContentEditable;
    }

    function activeModal() {
        var m = document.querySelector('.modal.show');
        if (!m || !window.bootstrap) return null;
        return window.bootstrap.Modal.getInstance(m);
    }

    function visibleToast() {
        var t = document.querySelector('.toast.show, .toast.showing');
        if (!t || !window.bootstrap) return null;
        return window.bootstrap.Toast.getInstance(t);
    }

    function findOwningForm(el) {
        while (el && el !== document.body) {
            if (el.tagName === 'FORM') return el;
            el = el.parentNode;
        }
        return null;
    }

    document.addEventListener('keydown', function (ev) {
        // Don't hijack keys when the user is typing in another component's
        // editor (Monaco, CodeMirror, etc.). Crude but effective: only fire
        // shortcuts when no modifier (except Shift for Enter-submit) is held.
        if (ev.metaKey || ev.ctrlKey || ev.altKey) return;

        var key = ev.key;

        if (key === 'Escape') {
            var m = activeModal();
            if (m) { m.hide(); ev.preventDefault(); return; }
            var t = visibleToast();
            if (t) { t.hide(); ev.preventDefault(); return; }
            if (document.activeElement && document.activeElement !== document.body) {
                document.activeElement.blur();
            }
            var form = findOwningForm(document.activeElement);
            var cancel = form && form.getAttribute('data-cancel-href');
            if (cancel) { window.location.href = cancel; ev.preventDefault(); }
            return;
        }

        if (key === 'Enter' && !ev.shiftKey) {
            // Don't submit when focus is on a textarea or a real button — let
            // the platform behaviour handle it.
            var ae = document.activeElement;
            if (!ae || ae.tagName === 'TEXTAREA' || ae.tagName === 'BUTTON') return;
            if (!isEditable(ae)) return;
            var form = findOwningForm(ae);
            if (!form) return;
            // Skip if the form explicitly opts out.
            if (form.hasAttribute('data-no-kbs-submit')) return;
            var submit = form.querySelector('button[type="submit"], input[type="submit"]');
            if (submit) { submit.click(); ev.preventDefault(); }
            return;
        }

        // "/" focuses topbar search (Twitter-style).
        if (key === '/' && !isEditable(document.activeElement)) {
            var search = document.querySelector('[data-role="topbar-search"], input[type="search"]');
            if (search) { search.focus(); ev.preventDefault(); }
            return;
        }

        // "?" opens the help overlay (Shift+/ on most layouts).
        if (key === '?' && !isEditable(document.activeElement)) {
            showHelp();
            ev.preventDefault();
            return;
        }
    }, false);

    var helpOverlay = null;
    function showHelp() {
        if (helpOverlay) { helpOverlay.style.display = 'flex'; return; }
        helpOverlay = document.createElement('div');
        helpOverlay.setAttribute('role', 'dialog');
        helpOverlay.setAttribute('aria-label', 'Keyboard shortcuts');
        helpOverlay.style.cssText = [
            'position:fixed', 'inset:0',
            'background:rgba(0,0,0,0.55)',
            'display:flex', 'align-items:center', 'justify-content:center',
            'z-index:2000'
        ].join(';');
        helpOverlay.addEventListener('click', function (e) {
            if (e.target === helpOverlay) helpOverlay.style.display = 'none';
        });
        var card = document.createElement('div');
        card.style.cssText = [
            'background:var(--bs-body-bg,#fff)', 'color:var(--bs-body-color,#000)',
            'padding:1.5rem 2rem', 'border-radius:.75rem', 'max-width:480px', 'width:90%',
            'box-shadow:0 30px 80px rgba(0,0,0,0.35)', 'font-family:Inter,sans-serif'
        ].join(';');
        card.innerHTML = [
            '<h5 style="margin:0 0 .75rem">Keyboard shortcuts</h5>',
            '<table style="width:100%;border-collapse:collapse;font-size:.9rem">',
            '<tr><td style="padding:.25rem 0"><kbd>Esc</kbd></td><td>Close modal / dismiss toast / blur input</td></tr>',
            '<tr><td style="padding:.25rem 0"><kbd>Enter</kbd></td><td>Submit form (when focus is in a text field)</td></tr>',
            '<tr><td style="padding:.25rem 0"><kbd>/</kbd></td><td>Focus topbar search</td></tr>',
            '<tr><td style="padding:.25rem 0"><kbd>?</kbd></td><td>Show this help</td></tr>',
            '<tr><td style="padding:.25rem 0"><kbd>Ctrl</kbd>+<kbd>P</kbd></td><td>Print (browser uses the receipt stylesheet)</td></tr>',
            '</table>',
            '<div style="margin-top:1rem;text-align:right;font-size:.8rem;color:var(--bs-secondary-color,#666)">Press <kbd>Esc</kbd> to close.</div>'
        ].join('');
        helpOverlay.appendChild(card);
        document.body.appendChild(helpOverlay);
    }
})();
