// Day/night theme persistence. The authoritative theme class is applied by Blazor on the .app
// wrapper; this just stores the choice and reports the OS preference.
window.tourTheme = {
    get() {
        try { return localStorage.getItem('tourlist-theme'); } catch { return null; }
    },
    set(mode) {
        try { localStorage.setItem('tourlist-theme', mode); } catch { /* private mode */ }
        document.documentElement.dataset.theme = mode;
    },
    preferred() {
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: light)').matches
            ? 'light'
            : 'dark';
    },
};
