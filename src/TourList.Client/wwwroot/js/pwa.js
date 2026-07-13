// Registers the service worker and surfaces an "update available" signal when a newer app version
// has been fetched (handles PWA version skew — cached clients running old code after a deploy).

(function registerServiceWorker() {
    if (!('serviceWorker' in navigator)) {
        return;
    }

    let reloading = false;
    navigator.serviceWorker.addEventListener('controllerchange', () => {
        if (!reloading) {
            reloading = true;
            location.reload();
        }
    });

    navigator.serviceWorker.register('service-worker.js', { updateViaCache: 'none' }).then(registration => {
        registration.addEventListener('updatefound', () => {
            const incoming = registration.installing;
            if (!incoming) {
                return;
            }
            incoming.addEventListener('statechange', () => {
                // A new worker is installed AND a controller exists ⇒ this is an update, not first install.
                if (incoming.state === 'installed' && navigator.serviceWorker.controller) {
                    window.__tourlistUpdateReg = registration;
                    window.dispatchEvent(new CustomEvent('tourlist:update-available'));
                }
            });
        });
    });
})();

window.tourlistPwa = {
    onUpdateAvailable(dotnetRef) {
        if (window.__tourlistUpdateReg) {
            dotnetRef.invokeMethodAsync('OnUpdateAvailable');
            return;
        }
        window.addEventListener('tourlist:update-available',
            () => dotnetRef.invokeMethodAsync('OnUpdateAvailable'), { once: true });
    },
    applyUpdate() {
        const registration = window.__tourlistUpdateReg;
        if (registration && registration.waiting) {
            registration.waiting.postMessage('SKIP_WAITING'); // → activate → controllerchange → reload
        } else {
            location.reload();
        }
    },
};
