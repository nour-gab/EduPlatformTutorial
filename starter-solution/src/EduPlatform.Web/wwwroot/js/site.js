// site.js — Ch.14: HTMX active chip tracking
// HTMX hx-push-url updates the URL; we sync the .active class on the chips.

document.addEventListener('htmx:afterRequest', function (e) {
    var url = e.detail.pathInfo ? e.detail.pathInfo.requestPath : '';
    var params = new URLSearchParams(url.split('?')[1] || '');
    var activeSlug = params.get('categorySlug') || '';

    document.querySelectorAll('.category-chip').forEach(function (chip) {
        var chipSlug = new URLSearchParams((chip.getAttribute('href') || '').split('?')[1]).get('categorySlug') || '';
        chip.classList.toggle('active', chipSlug === activeSlug);
    });
});

// Ch.14 §14.6.1 — popstate: re-render grid when user presses back/forward
window.addEventListener('popstate', function () {
    var params = new URLSearchParams(window.location.search);
    var categorySlug = params.get('categorySlug') || '';
    var grid = document.getElementById('popular-grid');
    if (grid) {
        htmx.ajax('GET', '/Home/PopularGrid?categorySlug=' + encodeURIComponent(categorySlug), { target: grid, swap: 'innerHTML' });
    }
});

