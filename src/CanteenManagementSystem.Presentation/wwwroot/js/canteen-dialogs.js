/* =============================================================================
   canteen-dialogs.js — SweetAlert2 confirm + toast wrappers
   -----------------------------------------------------------------------------
   1. AUTO-BIND `[data-cl-confirm]` on any form / button. The native
      `onsubmit="return confirm('…')"` pattern blocks the UI thread and looks
      like a 1995 popup. This middleware intercepts the submit / click, fires
      a themed SweetAlert2 modal, and only proceeds on confirm.
        <form … data-cl-confirm="Delete this card?">
        <a    … data-cl-confirm="Stop dispatcher?" data-cl-confirm-variant="danger">

   2. Programmatic API:
        window.cmsConfirm({ title, body, confirmText, cancelText, variant }) → Promise<bool>
        window.cmsToast(message, variant='success', timeout=3500)
        window.cmsAlert(message, variant='info')
      Variants: 'success' | 'warning' | 'danger' | 'info' | 'question'

   3. Falls back to native confirm()/alert() if SweetAlert2 didn't load
      (offline CDN). Tests + a11y screen-readers still get a real dialog.
============================================================================= */
(function () {
    'use strict';

    var hasSwal = typeof window.Swal === 'function' || (typeof window.Swal === 'object' && window.Swal !== null);
    var Swal    = window.Swal;

    function isDark() {
        return document.documentElement.getAttribute('data-bs-theme') === 'dark';
    }

    function swalDefaults(variant) {
        // Map variant → SweetAlert2 `icon` + `confirmButtonColor`.
        var v = (variant || 'question').toLowerCase();
        var icon = { success:'success', warning:'warning', danger:'error', info:'info' }[v] || 'question';
        var colour = { success:'#16a34a', warning:'#d97706', danger:'#dc2626', info:'#0891b2' }[v] || '#4f46e5';
        return {
            icon: icon,
            confirmButtonColor: colour,
            cancelButtonColor: '#71717a',
            background: isDark() ? '#18181b' : '#ffffff',
            color: isDark() ? '#fafafa' : '#18181b',
            customClass: { popup: 'cms-swal-popup' }
        };
    }

    // ── Programmatic helpers ─────────────────────────────────────────────
    window.cmsConfirm = function (opts) {
        opts = opts || {};
        if (!hasSwal) {
            var ok = window.confirm(opts.body || opts.title || 'Are you sure?');
            return Promise.resolve(ok);
        }
        return Swal.fire(Object.assign(swalDefaults(opts.variant), {
            title: opts.title || 'Are you sure?',
            text:  opts.body  || null,
            showCancelButton: true,
            confirmButtonText: opts.confirmText || 'Yes',
            cancelButtonText:  opts.cancelText  || 'Cancel'
        })).then(function (r) { return r.isConfirmed; });
    };

    window.cmsToast = function (message, variant, timeout) {
        if (!hasSwal) { try { console.log('[toast]', message); } catch (e) {} return; }
        Swal.fire(Object.assign(swalDefaults(variant || 'success'), {
            title: message,
            toast: true,
            position: 'top-end',
            showConfirmButton: false,
            timer: timeout || 3500,
            timerProgressBar: true
        }));
    };

    window.cmsAlert = function (message, variant) {
        if (!hasSwal) { window.alert(message); return; }
        Swal.fire(Object.assign(swalDefaults(variant || 'info'), {
            title: message,
            confirmButtonText: 'OK'
        }));
    };

    // ── Auto-bind on data-cl-confirm ─────────────────────────────────────
    function pickVariant(el) {
        return el.getAttribute('data-cl-confirm-variant') || 'question';
    }
    function pickTexts(el) {
        return {
            title:       el.getAttribute('data-cl-confirm-title')   || el.getAttribute('data-cl-confirm') || 'Confirm',
            body:        el.getAttribute('data-cl-confirm-body')    || null,
            confirmText: el.getAttribute('data-cl-confirm-yes')     || 'Yes',
            cancelText:  el.getAttribute('data-cl-confirm-cancel')  || 'Cancel'
        };
    }

    function handleConfirm(e, el) {
        if (el.dataset.clConfirmed === '1') {
            el.dataset.clConfirmed = ''; // allow re-use later
            return; // proceed
        }
        e.preventDefault();
        e.stopPropagation();
        var texts = pickTexts(el);
        window.cmsConfirm({
            variant: pickVariant(el),
            title:   texts.title,
            body:    texts.body,
            confirmText: texts.confirmText,
            cancelText:  texts.cancelText
        }).then(function (ok) {
            if (!ok) return;
            el.dataset.clConfirmed = '1';
            // Re-trigger the original action — submit for forms, click otherwise.
            if (el.tagName === 'FORM') {
                if (typeof el.requestSubmit === 'function') el.requestSubmit();
                else el.submit();
            } else {
                el.click();
            }
        });
    }

    document.addEventListener('submit', function (e) {
        var f = e.target;
        if (f && f.hasAttribute && f.hasAttribute('data-cl-confirm')) handleConfirm(e, f);
    }, true);

    document.addEventListener('click', function (e) {
        var t = e.target;
        var el = t && t.closest ? t.closest('[data-cl-confirm]') : null;
        if (!el) return;
        if (el.tagName === 'FORM') return;      // forms handled by the submit listener
        if (el.tagName === 'BUTTON' && el.type === 'submit' && el.form
            && el.form.hasAttribute('data-cl-confirm')) return; // duplicate
        handleConfirm(e, el);
    }, true);

    // ── Bridge: pick up the existing TempData flash toasts (success / error)
    // and route them through cmsToast so the existing controller code emits
    // SweetAlert2-styled toasts automatically.
    document.addEventListener('DOMContentLoaded', function () {
        var hooks = document.querySelectorAll('[data-cl-flash-success], [data-cl-flash-error], [data-cl-flash-warning]');
        hooks.forEach(function (el) {
            var msg = el.textContent.trim();
            if (!msg) return;
            var variant = el.hasAttribute('data-cl-flash-success') ? 'success'
                        : el.hasAttribute('data-cl-flash-warning') ? 'warning'
                        : 'danger';
            window.cmsToast(msg, variant);
            el.style.display = 'none';
        });
    });
})();
