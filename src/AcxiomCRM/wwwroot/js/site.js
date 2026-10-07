// Confirmation for destructive actions: <form data-confirm="Are you sure?">
document.addEventListener("submit", function (e) {
    var form = e.target;
    if (form.dataset && form.dataset.confirm && !window.confirm(form.dataset.confirm)) {
        e.preventDefault();
    }
});

// Opportunity form: closing stages fix the probability (mirrors server-side rule).
document.addEventListener("change", function (e) {
    var el = e.target;
    if (el.name !== "Stage" || !el.form) return;
    var prob = el.form.querySelector("[name='Probability']");
    if (!prob) return;
    if (el.value === "Won") prob.value = 100;
    else if (el.value === "Lost") prob.value = 0;
});

// Enable Bootstrap tooltips.
document.querySelectorAll('[data-bs-toggle="tooltip"]').forEach(function (el) {
    new bootstrap.Tooltip(el);
});
