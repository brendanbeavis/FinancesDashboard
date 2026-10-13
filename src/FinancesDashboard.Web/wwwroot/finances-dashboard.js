window.financesDashboardMotion = {
    prefersReducedMotion: function () {
        return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    }
};