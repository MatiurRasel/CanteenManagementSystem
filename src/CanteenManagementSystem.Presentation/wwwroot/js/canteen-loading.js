/* =============================================================================
   canteen-loading.js  — companion to /css/canteen-loading.css
   -----------------------------------------------------------------------------
   Exposes window.CanteenLoading with a tiny, dependency-free API:

     show(el)               // sets data-loaded="false" on the wrapper
     hide(el)               // sets data-loaded="true"
     wrap(el, asyncFn)      // show → run → hide; returns the promise/value
     button(btn, asyncFn)   // disables button + swaps in spinner during async
     overlay({ text, target, fixed })  // create a curtain; returns { hide() }

   Auto-bound conveniences:
     - Any <form data-cl-form> shows a button-spinner on submit until response.
     - Any <button data-cl-async="url"> POSTs to url with a button-spinner.
     - Any container with [data-cl-overlay-on-submit] gets a scoped overlay
       while a form inside it submits.
============================================================================= */

(function (global) {
    'use strict';

    /** Resolve `el` to an actual DOM Element. Accepts string selector or Element. */
    function $(el) {
        if (!el) return null;
        if (typeof el === 'string') return document.querySelector(el);
        if (el.nodeType === 1)      return el;
        return null;
    }

    function show(el) {
        const node = $(el);
        if (node) node.setAttribute('data-loaded', 'false');
    }

    function hide(el) {
        const node = $(el);
        if (node) node.setAttribute('data-loaded', 'true');
    }

    /**
     * Wrap an async operation: shows skeleton, awaits, hides on settle.
     * Re-throws the original error so callers can still catch.
     */
    async function wrap(el, asyncFn) {
        show(el);
        try {
            return await (typeof asyncFn === 'function' ? asyncFn() : asyncFn);
        } finally {
            hide(el);
        }
    }

    /**
     * Toggle a button into its loading state and back. While loading:
     *   - data-loading="true" set on the element (CSS picks it up)
     *   - aria-busy="true" set
     *   - if a .cl-btn-spinner child does not exist, one is injected
     *   - .cl-btn-text wraps the original child nodes
     *
     * On finally:
     *   - reverted to data-loading="false"
     *   - aria-busy removed
     */
    async function button(btn, asyncFn) {
        const node = $(btn);
        if (!node) return asyncFn?.();

        ensureButtonStructure(node);
        node.setAttribute('data-loading', 'true');
        node.setAttribute('aria-busy', 'true');
        node.disabled = true;
        try {
            return await asyncFn?.();
        } finally {
            node.setAttribute('data-loading', 'false');
            node.removeAttribute('aria-busy');
            node.disabled = false;
        }
    }

    /** Make sure the button has the .cl-btn-text + .cl-btn-spinner siblings. */
    function ensureButtonStructure(node) {
        if (node.querySelector(':scope > .cl-btn-text')) return;

        const textWrap = document.createElement('span');
        textWrap.className = 'cl-btn-text';
        while (node.firstChild) textWrap.appendChild(node.firstChild);
        node.appendChild(textWrap);

        const spin = document.createElement('span');
        spin.className = 'cl-btn-spinner';
        spin.innerHTML = '<span class="cl-spinner cl-spinner-sm" aria-hidden="true"></span>';
        const label = node.getAttribute('data-loading-text');
        if (label) {
            const lbl = document.createElement('span');
            lbl.textContent = label;
            spin.appendChild(lbl);
        }
        node.appendChild(spin);
    }

    /**
     * Show a curtain. By default it covers the viewport (fixed). Pass
     * { target } to scope it to a container — the container is given
     * .cl-overlay-host so the overlay can fill it.
     *
     * Returns { hide(), node } so callers can dismiss programmatically.
     */
    function overlay(opts) {
        const o = opts || {};
        const target = $(o.target) || document.body;
        const fixed = o.target ? false : true;

        // Avoid stacking duplicate overlays in the same container.
        const existing = target.querySelector(':scope > .cl-overlay');
        if (existing) {
            existing.setAttribute('data-active', 'true');
            return overlayHandle(existing);
        }

        if (!fixed) target.classList.add('cl-overlay-host');

        const node = document.createElement('div');
        node.className = 'cl-overlay' + (fixed ? ' cl-overlay-fixed' : '');
        node.setAttribute('role', 'status');
        node.setAttribute('aria-live', 'polite');
        node.innerHTML = [
            '<span class="cl-spinner" aria-hidden="true"></span>',
            o.text ? `<span class="cl-overlay-text">${escapeHtml(o.text)}</span>` : ''
        ].join('');
        target.appendChild(node);

        // Force reflow so the data-active transition runs.
        // eslint-disable-next-line no-unused-expressions
        node.offsetHeight;
        node.setAttribute('data-active', 'true');

        return overlayHandle(node);
    }

    function overlayHandle(node) {
        return {
            node,
            hide() {
                node.setAttribute('data-active', 'false');
                setTimeout(() => node.remove(), 200);
            }
        };
    }

    function escapeHtml(s) {
        return String(s)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;')
            .replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    /* ─── Auto-binding (DOMContentLoaded) ───────────────────────────────── */

    function autoBind() {
        // Forms with data-cl-form: button spinner during submit.
        document.querySelectorAll('form[data-cl-form]').forEach((form) => {
            if (form.dataset.clBound === '1') return;
            form.dataset.clBound = '1';
            form.addEventListener('submit', () => {
                const btn = form.querySelector('button[type="submit"], input[type="submit"]');
                if (btn && btn.tagName === 'BUTTON') {
                    ensureButtonStructure(btn);
                    btn.setAttribute('data-loading', 'true');
                    btn.setAttribute('aria-busy', 'true');
                    btn.disabled = true;
                }
            });
        });

        // Buttons with data-cl-async="url": POST the URL with spinner.
        document.querySelectorAll('button[data-cl-async]').forEach((btn) => {
            if (btn.dataset.clBound === '1') return;
            btn.dataset.clBound = '1';
            btn.addEventListener('click', async (ev) => {
                ev.preventDefault();
                const url = btn.getAttribute('data-cl-async');
                await button(btn, () => fetch(url, { method: 'POST', headers: { 'Accept': 'application/json' } }));
            });
        });

        // Containers with data-cl-overlay-on-submit: scope an overlay to them
        // while any inner form submits.
        document.querySelectorAll('[data-cl-overlay-on-submit]').forEach((host) => {
            if (host.dataset.clBound === '1') return;
            host.dataset.clBound = '1';
            host.addEventListener('submit', () => {
                const text = host.getAttribute('data-cl-overlay-text') || undefined;
                const handle = overlay({ target: host, text });
                // Hide after navigation or 30s safety.
                setTimeout(() => handle.hide(), 30000);
            }, true);
        });
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', autoBind);
    } else {
        autoBind();
    }

    // Re-bind after dynamic DOM injection (SignalR / partial render hooks).
    if (typeof MutationObserver !== 'undefined') {
        const obs = new MutationObserver((muts) => {
            for (const m of muts) {
                for (const n of m.addedNodes) {
                    if (n.nodeType === 1) { autoBind(); return; }
                }
            }
        });
        obs.observe(document.body, { childList: true, subtree: true });
    }

    /* ─── Export ────────────────────────────────────────────────────────── */
    global.CanteenLoading = { show, hide, wrap, button, overlay };
})(window);
