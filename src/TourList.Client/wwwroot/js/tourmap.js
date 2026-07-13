// Azure Maps (azure-maps-control v3) interop. Coarse API; JSON strings in, no live objects out.
// The map control is loaded globally as `atlas` (see index.html). All geocoding is server-side —
// this module never pulls in the deprecated azure-maps-rest services module.
//
// Auth uses authType:'anonymous' with a getToken callback that fetches a short-lived token from the
// server (managed identity). If the token endpoint is not configured (503), initMap returns false
// and the caller shows a placeholder.

let map = null;
let datasource = null;
let routeSource = null;
let userMarker = null;
let ready = false;

// Data-driven colors: dark and light basemap styles per theme.
const STYLE = { dark: 'grayscale_dark', light: 'grayscale_light' };

export async function initMap(elementId, tokenUrl, theme) {
    if (ready) return true;
    if (typeof atlas === 'undefined') return false;

    // Probe configuration first so a 503 cleanly yields the placeholder.
    let clientId;
    try {
        const probe = await fetch(tokenUrl, { headers: { Accept: 'application/json' } });
        if (!probe.ok) return false;
        const body = await probe.json();
        clientId = body.clientId;
    } catch {
        return false;
    }

    map = new atlas.Map(elementId, {
        view: 'Auto',
        style: STYLE[theme] || STYLE.dark,
        showLogo: false,
        showFeedbackLink: false,
        authOptions: {
            authType: 'anonymous',
            clientId,
            getToken: async (resolve, reject) => {
                try {
                    const r = await fetch(tokenUrl, { headers: { Accept: 'application/json' } });
                    if (!r.ok) throw new Error('token ' + r.status);
                    resolve((await r.json()).token);
                } catch (e) { reject(e); }
            },
        },
    });

    await new Promise(res => map.events.add('ready', res));

    datasource = new atlas.source.DataSource(null, { cluster: true, clusterRadius: 45 });
    routeSource = new atlas.source.DataSource();
    map.sources.add([datasource, routeSource]);

    // Route line (below points).
    map.layers.add(new atlas.layer.LineLayer(routeSource, null, {
        strokeColor: '#5eead4', strokeWidth: 3, lineJoin: 'round', lineCap: 'round',
    }));

    // Clustered bubbles + counts + individual colored points.
    map.layers.add(new atlas.layer.BubbleLayer(datasource, null, {
        radius: 18, color: '#5eead4', strokeColor: '#04120e', strokeWidth: 2,
        filter: ['has', 'point_count'],
    }));
    map.layers.add(new atlas.layer.SymbolLayer(datasource, null, {
        iconOptions: { image: 'none' },
        textOptions: {
            textField: ['get', 'point_count_abbreviated'], offset: [0, 0.1],
            color: '#04120e', font: ['SegoeUi-Bold'],
        },
        filter: ['has', 'point_count'],
    }));
    map.layers.add(new atlas.layer.BubbleLayer(datasource, null, {
        radius: 8,
        color: ['case', ['has', 'color'], ['get', 'color'], '#5eead4'],
        strokeColor: '#ffffff', strokeWidth: 2,
        filter: ['!', ['has', 'point_count']],
    }));

    ready = true;
    return true;
}

export function setPoints(pointsJson) {
    if (!datasource) return;
    const points = JSON.parse(pointsJson || '[]');
    datasource.clear();
    datasource.add(points.map(p => new atlas.data.Feature(
        new atlas.data.Point([p.lng, p.lat]),
        { id: p.id, listId: p.listId, color: p.color, title: p.title, subtitle: p.subtitle })));
    zoomToData();
}

export function setFilter(visibleListIdsJson) {
    if (!map || !datasource) return;
    const ids = new Set(JSON.parse(visibleListIdsJson || '[]'));
    // Re-filtering by rebuilding is simplest for the data volumes here.
    const shapes = datasource.toJson().features;
    datasource.clear();
    datasource.add(shapes.filter(f => ids.size === 0 || ids.has(f.properties.listId)));
}

export function setRoute(routeGeoJson) {
    if (!routeSource) return;
    routeSource.clear();
    if (routeGeoJson) routeSource.add(JSON.parse(routeGeoJson));
}

export function setUserPosition(lat, lng) {
    if (!map) return;
    if (!userMarker) {
        userMarker = new atlas.HtmlMarker({ htmlContent: '<div class="user-dot"></div>', position: [lng, lat] });
        map.markers.add(userMarker);
    } else {
        userMarker.setOptions({ position: [lng, lat] });
    }
}

export function setTheme(theme) {
    if (map) map.setStyle({ style: STYLE[theme] || STYLE.dark });
}

export function zoomToData() {
    if (!map || !datasource) return;
    const data = datasource.toJson();
    if (!data.features.length) return;
    map.setCamera({ bounds: atlas.data.BoundingBox.fromData(data), padding: 80 });
}

export function dispose() {
    if (map) { map.dispose(); map = null; }
    datasource = routeSource = userMarker = null;
    ready = false;
}
