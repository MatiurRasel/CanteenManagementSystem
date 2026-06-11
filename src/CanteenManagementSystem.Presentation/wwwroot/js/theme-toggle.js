// =============================================================================
// theme-toggle.js  (wwwroot)
// -----------------------------------------------------------------------------
// Persists the user's light/dark choice in localStorage as 'canteen.theme'
// and flips data-bs-theme on <html> for Bootstrap + our app CSS tokens.
//
// The pre-render snippet in _Layout already applies the saved theme before
// first paint to avoid FOUC. This script only wires the toggle button +
// reacts to OS-level changes when no explicit preference is stored.
// =============================================================================

(function () {
    'use strict';

    const KEY = 'canteen.theme';
    const root = document.documentElement;

    function setTheme(t, persist) {
        if (t !== 'dark' && t !== 'light') t = 'light';
        root.setAttribute('data-bs-theme', t);
        const icon = document.querySelector('[data-theme-icon]');
        if (icon) {
            icon.classList.toggle('fa-moon', t === 'light');
            icon.classList.toggle('fa-sun',  t === 'dark');
        }
        if (persist) {
            try { localStorage.setItem(KEY, t); } catch (e) {}
        }
    }

    function currentTheme() {
        const persisted = (() => { try { return localStorage.getItem(KEY); } catch (e) { return null; } })();
        if (persisted === 'dark' || persisted === 'light') return persisted;
        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    }

    document.addEventListener('DOMContentLoaded', () => {
        // sync the icon state after first paint
        setTheme(currentTheme(), false);

        const btn = document.querySelector('[data-theme-toggle]');
        if (btn) {
            btn.addEventListener('click', () => {
                const next = currentTheme() === 'dark' ? 'light' : 'dark';
                setTheme(next, true);
            });
        }
    });

    // React to OS-level theme changes only when the user hasn't explicitly chosen.
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', e => {
        try {
            if (localStorage.getItem(KEY)) return; // explicit pref wins
        } catch (e) { /* ignore */ }
        setTheme(e.matches ? 'dark' : 'light', false);
    });
})();
