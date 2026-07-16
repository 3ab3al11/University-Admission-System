// Shared UI behavior lives in this local script so CSP can block inline event
// handler attributes throughout the application.
(function () {
    "use strict";

    var themeButton = document.getElementById("themeToggle");
    if (themeButton) {
        themeButton.addEventListener("click", function () {
            var current = document.documentElement.getAttribute("data-bs-theme") || "light";
            var next = current === "dark" ? "light" : "dark";
            document.documentElement.setAttribute("data-bs-theme", next);

            try {
                localStorage.setItem("anu-theme", next);
            } catch (_) {
                // The selected theme still applies for this page when storage is blocked.
            }
        });
    }

    document.querySelectorAll("[data-print-page]").forEach(function (button) {
        button.addEventListener("click", function () {
            window.print();
        });
    });

    document.querySelectorAll("form[data-confirm-message]").forEach(function (form) {
        form.addEventListener("submit", function (event) {
            if (!window.confirm(form.dataset.confirmMessage || "")) {
                event.preventDefault();
            }
        });
    });
})();
