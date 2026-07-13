// Device geolocation for the Driving View. Live position stays on-device and is pushed to the
// Blazor component via [JSInvokable] callbacks — it is NEVER sent to the server (privacy by design).

let watchId = null;

export function platform() {
    const ua = navigator.userAgent || '';
    if (/iPhone|iPad|iPod/i.test(ua)) return 'ios';
    if (/Android/i.test(ua)) return 'android';
    return 'other';
}

export function startWatch(dotnetRef) {
    if (!('geolocation' in navigator)) {
        dotnetRef.invokeMethodAsync('OnPositionError', 'unavailable');
        return;
    }
    if (watchId !== null) return;

    watchId = navigator.geolocation.watchPosition(
        p => dotnetRef.invokeMethodAsync('OnPositionChanged', p.coords.latitude, p.coords.longitude, p.coords.accuracy),
        e => dotnetRef.invokeMethodAsync('OnPositionError', e.code === 1 ? 'denied' : 'unavailable'),
        { enableHighAccuracy: true, maximumAge: 5000, timeout: 15000 });
}

export function stopWatch() {
    if (watchId !== null && 'geolocation' in navigator) {
        navigator.geolocation.clearWatch(watchId);
        watchId = null;
    }
}

export function openExternal(url) {
    window.open(url, '_blank', 'noopener');
}
