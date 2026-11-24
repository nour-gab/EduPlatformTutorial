/**
 * wizard.js — Instructor Course Wizard
 * Bootstrap pill tab navigation + dynamic lesson row management.
 * As described in Ch.19 §19.4.
 */
(function () {
    'use strict';

    // ── Tab navigation ────────────────────────────────────────────────────────

    var tabIds = ['#tab-basics', '#tab-lessons', '#tab-pricing', '#tab-review'];

    function getActiveTabIndex() {
        var active = document.querySelector('#wizard-tabs .nav-link.active');
        if (!active) return 0;
        var target = active.getAttribute('data-bs-target');
        return tabIds.indexOf(target);
    }

    function goToTab(index) {
        if (index < 0 || index >= tabIds.length) return;
        var tab = document.querySelector(
            '#wizard-tabs [data-bs-target="' + tabIds[index] + '"]'
        );
        if (tab) {
            var bsTab = new bootstrap.Tab(tab);
            bsTab.show();
        }
    }

    document.addEventListener('click', function (e) {
        if (e.target.classList.contains('wizard-next')) {
            goToTab(getActiveTabIndex() + 1);
        }
        if (e.target.classList.contains('wizard-prev')) {
            goToTab(getActiveTabIndex() - 1);
        }
    });

    // ── Auto-generate slug from title ─────────────────────────────────────────

    var titleInput = document.querySelector('[name="Title"]');
    var slugInput  = document.getElementById('slug-input');

    if (titleInput && slugInput && !slugInput.value) {
        titleInput.addEventListener('input', function () {
            slugInput.value = titleInput.value
                .toLowerCase()
                .trim()
                .replace(/[^a-z0-9]+/g, '-')
                .replace(/^-+|-+$/g, '');
        });
    }

    // ── Lesson rows ───────────────────────────────────────────────────────────

    var container = document.getElementById('lessons-container');
    var addBtn    = document.getElementById('add-lesson-btn');

    function getLessonCount() {
        return container ? container.querySelectorAll('.lesson-row').length : 0;
    }

    function addLessonRow() {
        if (!container) return;
        var idx = getLessonCount();
        var pfx = 'Lessons[' + idx + ']';
        var newId = crypto.randomUUID ? crypto.randomUUID() : Date.now().toString();

        var html = '<div class="lesson-row border rounded p-3 mb-2">' +
            '<input type="hidden" name="' + pfx + '.Id" value="' + newId + '" />' +
            '<div class="row g-2 align-items-center">' +
            '  <div class="col-auto">' +
            '    <label class="form-label small mb-0">Order</label>' +
            '    <input type="number" name="' + pfx + '.Order" class="form-control form-control-sm lesson-order"' +
            '           value="' + idx + '" style="width:70px;" />' +
            '  </div>' +
            '  <div class="col">' +
            '    <label class="form-label small mb-0">Title</label>' +
            '    <input type="text" name="' + pfx + '.Title" class="form-control form-control-sm"' +
            '           placeholder="Lesson title" required />' +
            '  </div>' +
            '  <div class="col-auto d-flex align-items-end">' +
            '    <div class="form-check">' +
            '      <input type="checkbox" name="' + pfx + '.IsPreview" class="form-check-input" value="true" />' +
            '      <input type="hidden" name="' + pfx + '.IsPreview" value="false" />' +
            '      <label class="form-check-label small">Free preview</label>' +
            '    </div>' +
            '  </div>' +
            '  <div class="col-auto">' +
            '    <button type="button" class="btn btn-sm btn-outline-danger remove-lesson-btn">✕</button>' +
            '  </div>' +
            '</div>' +
            '<div class="mt-2">' +
            '  <label class="form-label small mb-0">Content (Markdown)</label>' +
            '  <textarea name="' + pfx + '.ContentMarkdown" class="form-control form-control-sm"' +
            '            rows="3" placeholder="Lesson content in Markdown..."></textarea>' +
            '</div>' +
            '</div>';

        container.insertAdjacentHTML('beforeend', html);
        reindexLessons();
    }

    function reindexLessons() {
        if (!container) return;
        var rows = container.querySelectorAll('.lesson-row');
        rows.forEach(function (row, i) {
            row.querySelectorAll('[name]').forEach(function (el) {
                el.name = el.name.replace(/Lessons\[\d+\]/, 'Lessons[' + i + ']');
            });
        });
    }

    if (addBtn) {
        addBtn.addEventListener('click', addLessonRow);
    }

    if (container) {
        container.addEventListener('click', function (e) {
            if (e.target.classList.contains('remove-lesson-btn')) {
                var row = e.target.closest('.lesson-row');
                if (row) {
                    row.remove();
                    reindexLessons();
                }
            }
        });
    }
})();
