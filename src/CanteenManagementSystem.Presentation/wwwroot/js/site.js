// =============================================================
// Canteen Management System — site.js
// Sidebar toggle, theme persistence, toast helper, fetch wrapper.
// =============================================================
(function () {
  'use strict';

  const STORAGE_KEY_THEME = 'canteen.theme';
  const root = document.documentElement;

  // ---------- 1. Theme (light / dark) ---------------------------
  function applyTheme(theme) {
    root.setAttribute('data-bs-theme', theme);
    try { localStorage.setItem(STORAGE_KEY_THEME, theme); } catch (e) {}
    document.querySelectorAll('[data-theme-icon]').forEach((el) => {
      el.classList.toggle('fa-moon', theme === 'light');
      el.classList.toggle('fa-sun',  theme === 'dark');
    });
  }
  function initTheme() {
    let saved;
    try { saved = localStorage.getItem(STORAGE_KEY_THEME); } catch (e) { saved = null; }
    const prefersDark = window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    applyTheme(saved || (prefersDark ? 'dark' : 'light'));
  }
  document.addEventListener('click', (e) => {
    const trigger = e.target.closest('[data-theme-toggle]');
    if (!trigger) return;
    e.preventDefault();
    const next = root.getAttribute('data-bs-theme') === 'dark' ? 'light' : 'dark';
    applyTheme(next);
  });

  // ---------- 2. Sidebar (mobile) -------------------------------
  const sidebar = () => document.getElementById('layout-menu');
  function ensureBackdrop() {
    let bd = document.querySelector('.menu-backdrop');
    if (!bd) {
      bd = document.createElement('div');
      bd.className = 'menu-backdrop';
      bd.addEventListener('click', closeSidebar);
      document.body.appendChild(bd);
    }
    return bd;
  }
  function openSidebar()  { sidebar()?.classList.add('is-open');    ensureBackdrop().classList.add('is-visible'); document.body.style.overflow = 'hidden'; }
  function closeSidebar() { sidebar()?.classList.remove('is-open'); document.querySelector('.menu-backdrop')?.classList.remove('is-visible'); document.body.style.overflow = ''; }
  document.addEventListener('click', (e) => {
    if (e.target.closest('[data-menu-toggle]')) {
      e.preventDefault();
      sidebar()?.classList.contains('is-open') ? closeSidebar() : openSidebar();
    }
    if (e.target.closest('[data-menu-close]')) {
      e.preventDefault();
      closeSidebar();
    }
  });
  window.addEventListener('resize', () => { if (window.innerWidth >= 992) closeSidebar(); });

  // ---------- 3. Toast helper -----------------------------------
  function ensureToastStack() {
    let stack = document.querySelector('.toast-stack');
    if (!stack) {
      stack = document.createElement('div');
      stack.className = 'toast-stack';
      stack.setAttribute('role', 'status');
      stack.setAttribute('aria-live', 'polite');
      document.body.appendChild(stack);
    }
    return stack;
  }
  function iconForVariant(v) {
    return v === 'success' ? 'fa-circle-check'
         : v === 'warning' ? 'fa-triangle-exclamation'
         : v === 'danger'  ? 'fa-circle-xmark'
         : 'fa-circle-info';
  }
  function escapeHtml(s) {
    return String(s).replace(/[&<>"']/g, (c) => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  }
  function toast(message, variant, timeout) {
    if (!message) return;
    variant = variant || 'info';
    timeout = timeout || 4000;
    const stack = ensureToastStack();
    const card = document.createElement('div');
    card.className = 'toast-card toast-' + variant;
    card.innerHTML =
      '<div class="d-flex align-items-start gap-2">' +
        '<i class="fa-solid ' + iconForVariant(variant) + ' mt-1"></i>' +
        '<div class="flex-grow-1">' + escapeHtml(message) + '</div>' +
        '<button type="button" class="btn-close btn-sm" aria-label="Close"></button>' +
      '</div>';
    card.querySelector('.btn-close').addEventListener('click', () => card.remove());
    stack.appendChild(card);
    setTimeout(() => card.remove(), timeout);
  }
  window.canteenToast = toast;

  // ---------- 4. Async fetch wrapper ----------------------------
  window.canteenFetch = async function (url, options) {
    options = Object.assign({ method: 'GET', headers: {} }, options || {});
    options.headers = Object.assign({
      'Accept': 'application/json',
      'X-Requested-With': 'XMLHttpRequest'
    }, options.headers || {});
    if (options.body && typeof options.body === 'object' && !(options.body instanceof FormData)) {
      options.headers['Content-Type'] = 'application/json';
      options.body = JSON.stringify(options.body);
    }
    const response = await fetch(url, options);
    const contentType = response.headers.get('content-type') || '';
    const payload = contentType.includes('json') ? await response.json().catch(() => null) : null;
    if (!response.ok) {
      const msg = (payload && (payload.detail || payload.message)) || ('Request failed (' + response.status + ')');
      toast(msg, 'danger');
      const error = new Error(msg);
      error.payload = payload;
      error.status = response.status;
      throw error;
    }
    return payload;
  };

  // ---------- 5. Loader API ------------------------------------
  let overlayEl = null;
  function ensureOverlay() {
    if (!overlayEl) { overlayEl = document.createElement('div'); overlayEl.className = 'loader-overlay'; overlayEl.setAttribute('aria-busy','true'); }
    return overlayEl;
  }
  let barEl = null;
  function ensureBar() {
    if (!barEl) { barEl = document.createElement('div'); barEl.className = 'loader-bar'; document.body.appendChild(barEl); }
    return barEl;
  }
  window.canteenLoader = {
    show: () => document.body.appendChild(ensureOverlay()),
    hide: () => overlayEl?.remove(),
    bar: {
      start: () => { const b = ensureBar(); b.classList.remove('is-done'); requestAnimationFrame(() => b.classList.add('is-active')); },
      done:  () => { const b = ensureBar(); b.classList.add('is-done'); setTimeout(() => b.classList.remove('is-active','is-done'), 500); }
    }
  };

  // ---------- 6. Lazy-load images -----------------------------
  function activateLazyImages() {
    if (!('IntersectionObserver' in window)) {
      document.querySelectorAll('img.lazy').forEach((img) => { if (img.dataset.src) img.src = img.dataset.src; img.classList.add('is-loaded'); });
      return;
    }
    const io = new IntersectionObserver((entries) => {
      for (const e of entries) {
        if (!e.isIntersecting) continue;
        const img = e.target;
        if (img.dataset.src) img.src = img.dataset.src;
        img.addEventListener('load', () => img.classList.add('is-loaded'), { once: true });
        io.unobserve(img);
      }
    }, { rootMargin: '200px' });
    document.querySelectorAll('img.lazy[data-src]').forEach((img) => io.observe(img));
  }

  // ---------- 7. Init -------------------------------------------
  document.addEventListener('DOMContentLoaded', () => {
    initTheme();
    activateLazyImages();
    const params = new URLSearchParams(window.location.search);
    if (params.has('flash')) toast(params.get('flash'), params.get('flashType') || 'info');
    document.querySelector('[data-autofocus]')?.focus();
  });
})();
