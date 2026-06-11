/* =============================================================================
   tour-wizard.js — guided onboarding tour
   -----------------------------------------------------------------------------
   Dependency-free Shepherd-style step-through tour. Highlights elements via
   a pulsing border + tooltip with Prev / Next / Skip buttons. Steps are
   declared inline on the page:

       <body data-cl-tour="counter-first-time">
         <button data-cl-tour-step="1" data-cl-tour-text="Scan a card here">
           Scan
         </button>
         <div    data-cl-tour-step="2" data-cl-tour-text="Pick items by number">
           Keypad
         </div>
       </body>

   On first visit (per role, persisted in localStorage), the tour auto-starts.
   Subsequent visits show a "Replay tour" link in the topbar customizer.
============================================================================= */

(function () {
    'use strict';

    function getSteps() {
        return Array.from(document.querySelectorAll('[data-cl-tour-step]'))
            .map((el) => ({
                el,
                order: parseInt(el.getAttribute('data-cl-tour-step'), 10) || 0,
                text:  el.getAttribute('data-cl-tour-text') || ''
            }))
            .sort((a, b) => a.order - b.order);
    }

    function ensureStyles() {
        if (document.getElementById('cl-tour-styles')) return;
        const css = `
            .cl-tour-spot {
                outline: 3px solid #ffd400 !important;
                box-shadow: 0 0 0 9999px rgba(0,0,0,0.55);
                position: relative; z-index: 2050; border-radius: 4px;
                transition: outline-color .25s ease;
            }
            .cl-tour-tooltip {
                position: fixed; z-index: 2060;
                background: #fff; color: #000;
                padding: 0.85rem 1rem; border-radius: 0.5rem;
                box-shadow: 0 30px 80px rgba(0,0,0,0.35);
                max-width: 320px; font-family: Inter, Arial, sans-serif;
                font-size: 0.95rem; line-height: 1.35;
            }
            [data-bs-theme="dark"] .cl-tour-tooltip { background: #1c1c22; color: #fff; }
            .cl-tour-tooltip .cl-tour-actions {
                display: flex; justify-content: space-between; align-items: center;
                margin-top: 0.75rem; gap: 0.5rem;
            }
            .cl-tour-tooltip button {
                border: 1px solid currentColor; background: transparent;
                color: inherit; padding: 0.3rem 0.7rem; border-radius: 0.35rem;
                cursor: pointer; font-size: 0.85rem;
            }
            .cl-tour-tooltip button.primary {
                background: #ffd400; color: #000; border-color: #ffd400; font-weight: 600;
            }
            .cl-tour-tooltip .cl-tour-counter {
                font-size: 0.8rem; opacity: 0.7;
            }`;
        const style = document.createElement('style');
        style.id = 'cl-tour-styles';
        style.textContent = css;
        document.head.appendChild(style);
    }

    let tip = null, current = -1, steps = [], tourKey = '';

    function start() {
        steps = getSteps();
        if (steps.length === 0) return;
        ensureStyles();
        current = 0;
        show();
    }

    function persistDone() {
        try { localStorage.setItem('cms-tour:' + tourKey, '1'); } catch (e) {}
    }

    function cleanup() {
        if (tip) { tip.remove(); tip = null; }
        document.querySelectorAll('.cl-tour-spot').forEach((el) => el.classList.remove('cl-tour-spot'));
    }

    function show() {
        cleanup();
        if (current < 0 || current >= steps.length) { persistDone(); return; }
        const step = steps[current];
        if (!step.el || !step.el.isConnected) { current++; show(); return; }
        step.el.classList.add('cl-tour-spot');
        try { step.el.scrollIntoView({ behavior: 'smooth', block: 'center' }); } catch (e) {}

        tip = document.createElement('div');
        tip.className = 'cl-tour-tooltip';
        tip.innerHTML = `
            <div>${escapeHtml(step.text)}</div>
            <div class="cl-tour-actions">
                <span class="cl-tour-counter">${current + 1} / ${steps.length}</span>
                <span>
                    <button data-act="skip">Skip</button>
                    <button data-act="prev"${current === 0 ? ' disabled' : ''}>← Back</button>
                    <button data-act="next" class="primary">${current === steps.length - 1 ? 'Done' : 'Next →'}</button>
                </span>
            </div>`;
        document.body.appendChild(tip);

        // Position next to the spotlight, keeping inside the viewport.
        const r = step.el.getBoundingClientRect();
        const tw = tip.offsetWidth, th = tip.offsetHeight;
        let left = r.left + (r.width / 2) - (tw / 2);
        let top  = r.bottom + 12;
        if (top + th > innerHeight - 16) top = r.top - th - 12;
        left = Math.max(8, Math.min(left, innerWidth - tw - 8));
        if (top < 8) top = 8;
        tip.style.left = left + 'px';
        tip.style.top  = top  + 'px';

        tip.addEventListener('click', (e) => {
            const act = (e.target.getAttribute && e.target.getAttribute('data-act')) || '';
            if (act === 'next') { current++; show(); }
            else if (act === 'prev') { current = Math.max(0, current - 1); show(); }
            else if (act === 'skip') { persistDone(); cleanup(); }
        });
    }

    function escapeHtml(s) {
        return String(s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;')
            .replace(/"/g,'&quot;').replace(/'/g,'&#39;');
    }

    function autoStart() {
        const body = document.body;
        const key = body && body.getAttribute('data-cl-tour');
        if (!key) return;
        tourKey = key;
        let seen = null;
        try { seen = localStorage.getItem('cms-tour:' + key); } catch (e) {}
        if (seen === '1') return;
        // Defer slightly so other DOMContentLoaded handlers settle.
        setTimeout(start, 350);
    }

    // Replay button hook.
    document.addEventListener('click', (e) => {
        const t = e.target.closest('[data-cl-tour-replay]');
        if (!t) return;
        e.preventDefault();
        const key = document.body.getAttribute('data-cl-tour');
        if (!key) return;
        try { localStorage.removeItem('cms-tour:' + key); } catch (e) {}
        tourKey = key; start();
    });

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', autoStart);
    else autoStart();

    window.CanteenTour = { start, stop: cleanup };
})();
