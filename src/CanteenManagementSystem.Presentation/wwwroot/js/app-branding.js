(function () {
    const body = document.body;

    if (!body) {
        return;
    }

    const root = document.documentElement;
    const mappings = [
        ['brandAccent', '--app-accent'],
        ['brandSidebar', '--app-sidebar-bg'],
        ['brandTopbar', '--app-topbar-bg']
    ];

    mappings.forEach(([dataKey, cssVar]) => {
        const value = body.dataset[dataKey];
        if (value) {
            root.style.setProperty(cssVar, value);
        }
    });
})();
