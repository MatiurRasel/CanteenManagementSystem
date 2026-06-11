/* =============================================================================
   menu-dragreorder.js — drag-reorder daily menu rows
   -----------------------------------------------------------------------------
   Hooks on any element with [data-cl-sortable-menu]. Uses HTML5 native drag
   API (no SortableJS dependency — staying OSS-pure per ADR 0003).

   USAGE
       <tbody data-cl-sortable-menu data-cl-reorder-url="/admin/menu/reorder"
              data-cl-token="@Antiforgery.GetTokens(Context).RequestToken">
         <tr data-id="123" draggable="true">…</tr>
       </tbody>

   On drop, the script reads the new order of [data-id] attributes and POSTs
       { items: [{ dailyMenuId, displayOrder }, …] }
   to the reorder URL with the antiforgery token.
============================================================================= */

(function () {
    'use strict';

    function bind(container) {
        if (container.dataset.bound === '1') return;
        container.dataset.bound = '1';
        let dragged = null;

        container.addEventListener('dragstart', function (e) {
            const row = e.target.closest('[data-id]');
            if (!row || !container.contains(row)) return;
            dragged = row;
            row.style.opacity = '0.5';
            try { e.dataTransfer.effectAllowed = 'move'; } catch (e) {}
        });
        container.addEventListener('dragend', function () {
            if (dragged) dragged.style.opacity = '';
            dragged = null;
        });
        container.addEventListener('dragover', function (e) {
            const over = e.target.closest('[data-id]');
            if (!over || !container.contains(over) || over === dragged) return;
            e.preventDefault();
            const rect = over.getBoundingClientRect();
            const after = (e.clientY - rect.top) > (rect.height / 2);
            if (after) over.parentNode.insertBefore(dragged, over.nextSibling);
            else       over.parentNode.insertBefore(dragged, over);
        });
        container.addEventListener('drop', function (e) { e.preventDefault(); persist(container); });
    }

    function persist(container) {
        const url = container.getAttribute('data-cl-reorder-url') || '/admin/menu/reorder';
        const token = container.getAttribute('data-cl-token') || '';
        const items = Array.from(container.querySelectorAll('[data-id]')).map((row, i) => ({
            dailyMenuId: parseInt(row.getAttribute('data-id'), 10),
            displayOrder: i + 1
        })).filter((i) => Number.isFinite(i.dailyMenuId));

        fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify({ items })
        }).then((r) => {
            if (window.showToast) window.showToast(r.ok ? 'Order saved.' : 'Reorder failed.', r.ok ? 'success' : 'danger');
        }).catch(() => {
            if (window.showToast) window.showToast('Reorder failed.', 'danger');
        });
    }

    function init() {
        document.querySelectorAll('[data-cl-sortable-menu]').forEach(bind);
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', init);
    else init();

    if (typeof MutationObserver !== 'undefined') {
        new MutationObserver(init).observe(document.body, { childList: true, subtree: true });
    }
})();
