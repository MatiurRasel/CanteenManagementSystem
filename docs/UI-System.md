# UI System

## Tokens

`wwwroot/css/app-shell.css` defines the design tokens (colors, spacing,
shadows, radii). Tenants override accent + chrome via `BrandingConfiguration`.
The layout injects them at render time so a tenant can rebrand without a
deploy.

## Skeleton + loading patterns

Choose the right tool for the job:

| Use case | Markup |
|---|---|
| Inline text shimmer while a row loads | `<span class="skeleton-text" style="width:60%"></span>` |
| Avatar placeholder | `<div class="skeleton-avatar"></div>` |
| Card placeholder | `<div class="skeleton-card"></div>` |
| Table row placeholder | `<div class="skeleton-row"></div>` |
| Full-screen overlay during a route change | `window.canteenLoader.show()` / `.hide()` |
| Top progress bar during fetch | `window.canteenLoader.bar.start()` / `.bar.done()` |
| Inline button spinner | `<button><span class="loader-inline"></span> Saving…</button>` |
| Card-level overlay (in-place) | `<div class="position-relative"><div class="loader-partial"></div>…</div>` |

## Lazy-loaded images

Use `data-src` + `class="lazy"`. `site.js` registers an IntersectionObserver
to swap `src` once the image is near the viewport. Native `loading="lazy"` is
still applied so the browser can prioritise too.

```html
<img class="lazy" data-src="/menu/foo.webp" alt="" loading="lazy" decoding="async" />
```

## Server-side rendering (SSR)

Razor is SSR by default — every page comes back as HTML with content
in-place. Hot patterns to keep first paint fast:

1. Use `@RenderSection` blocks for above-the-fold critical content.
2. Lazy-load below-the-fold libraries (`<script defer src=...>`).
3. Output-cache GET endpoints that don't depend on per-user state.
4. Use `app-shell.css` skeletons as placeholders while client data fetches.

## Mobile-first

* Sidebar slides off-canvas below 992px (`is-open` class flips it back).
* Tap targets ≥ 44px on coarse pointers (`@media (hover: none) and (pointer: coarse)`).
* The default form-control-kiosk variant uses a 1.4rem font so kiosk taps land.
