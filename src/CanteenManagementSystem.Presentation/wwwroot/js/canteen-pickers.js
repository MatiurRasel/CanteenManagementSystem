/* =============================================================================
   canteen-pickers.js — Flatpickr date/datetime picker auto-binding
   -----------------------------------------------------------------------------
   * Every <input type="date"> becomes a Flatpickr with dd-MM-yyyy display
     format, but the underlying value POSTed to ASP.NET stays ISO (yyyy-MM-dd)
     so the model binder still parses it as DateTime.
   * <input type="datetime-local"> picks up an enableTime variant.
   * Opt OUT on a specific input with `data-cl-no-flatpickr`.
   * Re-runs on DOM mutations so SignalR / partial updates also pick up the
     pickers — no manual init needed.
   * Locale: respects <html lang> when set to a Flatpickr-supported code.
============================================================================= */
(function () {
    'use strict';

    if (typeof window.flatpickr !== 'function') {
        // Flatpickr CDN failed (offline?). Native input[type=date] still works,
        // so just degrade gracefully and bail.
        return;
    }

    var COMMON = {
        // dd-MM-yyyy on screen, but the form value posts as ISO yyyy-MM-dd so
        // ASP.NET's default DateTime binder still parses it correctly.
        dateFormat: 'Y-m-d',
        altInput: true,
        altFormat: 'd-m-Y',
        allowInput: true,
        disableMobile: true,
        // Use the ARIA-accessible weekday header (Su Mo Tu...).
        locale: { firstDayOfWeek: 1 }
    };

    function bind(root) {
        var dateInputs = (root || document).querySelectorAll(
            'input[type="date"]:not([data-cl-flatpickr-bound]):not([data-cl-no-flatpickr])');
        dateInputs.forEach(function (el) {
            el.setAttribute('data-cl-flatpickr-bound', '1');
            try {
                window.flatpickr(el, Object.assign({}, COMMON));
            } catch (e) { /* swallow */ }
        });

        var dtInputs = (root || document).querySelectorAll(
            'input[type="datetime-local"]:not([data-cl-flatpickr-bound]):not([data-cl-no-flatpickr])');
        dtInputs.forEach(function (el) {
            el.setAttribute('data-cl-flatpickr-bound', '1');
            try {
                window.flatpickr(el, Object.assign({}, COMMON, {
                    enableTime: true,
                    dateFormat: 'Y-m-d\\TH:i',
                    altFormat:  'd-m-Y H:i'
                }));
            } catch (e) {}
        });
    }

    function ready(fn) {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', fn);
        } else { fn(); }
    }

    ready(function () {
        bind(document);

        // Watch for late-rendered inputs (modals, SignalR injection, AJAX panels).
        var mo = new MutationObserver(function (mutations) {
            for (var i = 0; i < mutations.length; i++) {
                var m = mutations[i];
                for (var j = 0; j < m.addedNodes.length; j++) {
                    var n = m.addedNodes[j];
                    if (n.nodeType === 1) bind(n);
                }
            }
        });
        mo.observe(document.body, { childList: true, subtree: true });
    });
})();
