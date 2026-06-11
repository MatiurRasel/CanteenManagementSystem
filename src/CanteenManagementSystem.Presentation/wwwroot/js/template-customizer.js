// =============================================================================
// template-customizer.js  (Phase-2 extended)
// -----------------------------------------------------------------------------
// Slide-in customizer panel. Layered configuration model:
//
//   Effective = USER override (localStorage) > TENANT default (data-* on <body>) > FALLBACK
//
//   User overrides       — set by the panel itself, persisted to localStorage.
//   Tenant defaults      — written by an admin at /admin/branding, served by the
//                          server as `data-tenant-accent` / `data-tenant-app-name`
//                          on <body>.
//   Fallback             — hard-coded constants here (used when neither layer set).
//
// When the user clicks "Reset to tenant defaults" we wipe localStorage and
// fall back to the data-* values — exactly what a fresh user would see.
// =============================================================================

(function () {
    'use strict';

    var KEYS = {
        theme:     'canteen.theme',
        accent:    'canteen.accent',
        density:   'canteen.density',
        dir:       'canteen.dir',
        contained: 'canteen.contained',
        menu:      'canteen.menuCollapsed',
    };

    var root = document.documentElement;
    var body = document.body;

    // ── Tenant defaults (from server) ────────────────────────────────────
    var TENANT = {
        accent:    body.dataset.tenantAccent || '#10B981',
    };
    // ── Fallback defaults ────────────────────────────────────────────────
    var FALLBACK = {
        theme:     'system',
        density:   'comfortable',
        dir:       'ltr',
        contained: 'false',
        menu:      'false',
    };

    function ls(k, v) {
        try { if (v === undefined) return localStorage.getItem(k); localStorage.setItem(k, v); }
        catch (_) { return null; }
    }
    function effective(key) {
        var v = ls(KEYS[key]);
        if (v !== null && v !== '') return v;
        if (key === 'accent') return TENANT.accent;
        return FALLBACK[key];
    }

    function hexToRgb(hex) {
        if (!hex) return '16, 185, 129';
        hex = hex.replace('#', '');
        if (hex.length === 3) hex = hex.split('').map(c => c + c).join('');
        if (hex.length !== 6) return '16, 185, 129';
        var r = parseInt(hex.slice(0, 2), 16);
        var g = parseInt(hex.slice(2, 4), 16);
        var b = parseInt(hex.slice(4, 6), 16);
        return r + ', ' + g + ', ' + b;
    }
    function rgbToHex(r, g, b) {
        var to = n => Math.max(0, Math.min(255, Math.round(n))).toString(16).padStart(2, '0');
        return '#' + to(r) + to(g) + to(b);
    }
    function hslToHex(h, s, l) {
        s /= 100; l /= 100;
        var k = n => (n + h / 30) % 12;
        var a = s * Math.min(l, 1 - l);
        var f = n => l - a * Math.max(-1, Math.min(k(n) - 3, Math.min(9 - k(n), 1)));
        return rgbToHex(f(0) * 255, f(8) * 255, f(4) * 255);
    }
    function hexToHsl(hex) {
        hex = hex.replace('#', '');
        if (hex.length === 3) hex = hex.split('').map(c=>c+c).join('');
        var r = parseInt(hex.slice(0,2),16)/255, g = parseInt(hex.slice(2,4),16)/255, b = parseInt(hex.slice(4,6),16)/255;
        var max = Math.max(r,g,b), min = Math.min(r,g,b);
        var h, s, l = (max+min)/2;
        if (max === min) { h = s = 0; }
        else {
            var d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            switch (max) {
                case r: h = (g - b) / d + (g < b ? 6 : 0); break;
                case g: h = (b - r) / d + 2; break;
                default: h = (r - g) / d + 4;
            }
            h *= 60;
        }
        return { h: Math.round(h), s: Math.round(s * 100), l: Math.round(l * 100) };
    }

    function applyTheme(t) {
        if (t === 'system') t = window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
        root.setAttribute('data-bs-theme', t);
    }
    function applyAccent(hex) {
        root.style.setProperty('--app-accent', hex);
        root.style.setProperty('--app-accent-rgb', hexToRgb(hex));
        // Reflect in the custom picker UI
        var ci = document.getElementById('cz-color-input'); if (ci) ci.value = hex;
        var ch = document.getElementById('cz-color-hex');   if (ch) ch.value = hex;
        var hsl = hexToHsl(hex);
        var H = document.getElementById('cz-hue'); if (H) H.value = hsl.h;
        var S = document.getElementById('cz-sat'); if (S) S.value = hsl.s;
        var L = document.getElementById('cz-lit'); if (L) L.value = hsl.l;
    }
    function applyDir(d) { root.setAttribute('dir', d === 'rtl' ? 'rtl' : 'ltr'); }
    function applyContained(c) { body.setAttribute('data-contained', c === 'true' ? 'true' : 'false'); }
    function applyMenu(c) { body.classList.toggle('layout-collapsed', c === 'true'); }
    function applyDensity(d) {
        if (d !== 'compact' && d !== 'spacious') d = 'comfortable';
        body.setAttribute('data-density', d);
    }

    function reflectPills() {
        document.querySelectorAll('.cz-pill[data-theme]').forEach(b => b.setAttribute('aria-checked', String(b.dataset.theme === effective('theme'))));
        document.querySelectorAll('.cz-pill[data-dir]').forEach(b => b.setAttribute('aria-checked', String(b.dataset.dir === effective('dir'))));
        document.querySelectorAll('.cz-pill[data-contained]').forEach(b => b.setAttribute('aria-checked', String(b.dataset.contained === effective('contained'))));
        document.querySelectorAll('.cz-pill[data-density]').forEach(b => b.setAttribute('aria-checked', String(b.dataset.density === effective('density'))));
        document.querySelectorAll('.cz-pill[data-menu]').forEach(b => {
            var want = b.dataset.menu === 'collapsed' ? 'true' : 'false';
            b.setAttribute('aria-checked', String(want === effective('menu')));
        });
        var current = (effective('accent') || '').toLowerCase();
        document.querySelectorAll('.cz-swatch').forEach(s => s.setAttribute('aria-checked', String(s.dataset.accent.toLowerCase() === current)));
    }

    function applyAll() {
        applyTheme(effective('theme'));
        applyAccent(effective('accent'));
        applyDensity(effective('density'));
        applyDir(effective('dir'));
        applyContained(effective('contained'));
        applyMenu(effective('menu'));
        reflectPills();
    }

    function bind() {
        var trigger  = document.getElementById('cz-trigger');
        var panel    = document.getElementById('cz-panel');
        var backdrop = document.getElementById('cz-backdrop');
        var close    = document.getElementById('cz-close');
        var reset    = document.getElementById('cz-reset');
        if (!trigger || !panel) return;

        function open() { panel.classList.add('is-open'); backdrop.classList.add('is-open'); trigger.setAttribute('aria-expanded', 'true'); panel.setAttribute('aria-hidden', 'false'); }
        function shut() { panel.classList.remove('is-open'); backdrop.classList.remove('is-open'); trigger.setAttribute('aria-expanded', 'false'); panel.setAttribute('aria-hidden', 'true'); }
        trigger.addEventListener('click', open);
        close.addEventListener('click', shut);
        backdrop.addEventListener('click', shut);
        document.addEventListener('keydown', e => { if (e.key === 'Escape') shut(); });

        // ── Presets (apply bundle of choices) ────────────────────────────
        document.querySelectorAll('.cz-preset').forEach(b => {
            b.addEventListener('click', () => {
                ls(KEYS.theme,   b.dataset.theme);
                ls(KEYS.accent,  b.dataset.accent);
                ls(KEYS.density, b.dataset.density);
                applyAll();
            });
        });

        // ── Single-axis pills ────────────────────────────────────────────
        document.querySelectorAll('.cz-pill[data-theme]').forEach(b =>
            b.addEventListener('click', () => { ls(KEYS.theme, b.dataset.theme); applyTheme(b.dataset.theme); reflectPills(); }));
        document.querySelectorAll('.cz-pill[data-dir]').forEach(b =>
            b.addEventListener('click', () => { ls(KEYS.dir, b.dataset.dir); applyDir(b.dataset.dir); reflectPills(); }));
        document.querySelectorAll('.cz-pill[data-contained]').forEach(b =>
            b.addEventListener('click', () => { ls(KEYS.contained, b.dataset.contained); applyContained(b.dataset.contained); reflectPills(); }));
        document.querySelectorAll('.cz-pill[data-density]').forEach(b =>
            b.addEventListener('click', () => { ls(KEYS.density, b.dataset.density); applyDensity(b.dataset.density); reflectPills(); }));
        document.querySelectorAll('.cz-pill[data-menu]').forEach(b =>
            b.addEventListener('click', () => {
                var v = b.dataset.menu === 'collapsed' ? 'true' : 'false';
                ls(KEYS.menu, v); applyMenu(v); reflectPills();
            }));

        // ── Swatches ─────────────────────────────────────────────────────
        document.querySelectorAll('.cz-swatch').forEach(s =>
            s.addEventListener('click', () => { ls(KEYS.accent, s.dataset.accent); applyAccent(s.dataset.accent); reflectPills(); }));

        // ── Custom color picker ─────────────────────────────────────────
        var ci = document.getElementById('cz-color-input');
        var ch = document.getElementById('cz-color-hex');
        var H  = document.getElementById('cz-hue');
        var S  = document.getElementById('cz-sat');
        var L  = document.getElementById('cz-lit');
        function setAccentSafe(c) {
            if (!/^#?[0-9a-fA-F]{6}$/.test(c.replace('#',''))) return;
            if (!c.startsWith('#')) c = '#' + c;
            ls(KEYS.accent, c); applyAccent(c); reflectPills();
        }
        ci && ci.addEventListener('input',  e => setAccentSafe(e.target.value));
        ch && ch.addEventListener('change', e => setAccentSafe(e.target.value));
        function fromHsl() {
            if (!H || !S || !L) return;
            var hex = hslToHex(+H.value, +S.value, +L.value);
            ls(KEYS.accent, hex); applyAccent(hex); reflectPills();
        }
        H && H.addEventListener('input', fromHsl);
        S && S.addEventListener('input', fromHsl);
        L && L.addEventListener('input', fromHsl);

        // ── Reset to tenant default ─────────────────────────────────────
        reset.addEventListener('click', () => {
            Object.values(KEYS).forEach(k => { try { localStorage.removeItem(k); } catch (_) {} });
            applyAll();
        });
    }

    if (window.matchMedia) {
        window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', () => {
            if (effective('theme') === 'system') applyTheme('system');
        });
    }

    document.addEventListener('DOMContentLoaded', () => {
        bind();
        applyAll();
    });
})();
