// IndexedDB wrapper for TourList client storage. Values are stored out-of-line under an explicit
// key (a Guid string). Blazor JS interop serializes/deserializes objects (camelCase) automatically.
//
// Stores:
//   guestRoutes  - routes owned by a guest (this device only)
//   openedRoutes - full route aggregate cached for the offline Driving View (M4)
//   listCatalog  - best-effort offline browse cache
//   meta         - misc (auth cache, migration flag)

const DB_NAME = 'tourlist';
const DB_VERSION = 1;
const STORES = ['guestRoutes', 'openedRoutes', 'listCatalog', 'meta'];

function openDb() {
    return new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, DB_VERSION);
        req.onupgradeneeded = e => {
            const db = e.target.result;
            for (const s of STORES) {
                if (!db.objectStoreNames.contains(s)) {
                    db.createObjectStore(s);
                }
            }
        };
        req.onsuccess = e => resolve(e.target.result);
        req.onerror = () => reject(req.error);
    });
}

async function run(store, mode, action) {
    const db = await openDb();
    try {
        return await new Promise((resolve, reject) => {
            const request = action(db.transaction(store, mode).objectStore(store));
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    } finally {
        db.close();
    }
}

export const getAll = (store) => run(store, 'readonly', os => os.getAll());
export const get = (store, key) => run(store, 'readonly', os => os.get(key));
export const put = (store, key, value) => run(store, 'readwrite', os => os.put(value, key));
export const del = (store, key) => run(store, 'readwrite', os => os.delete(key));
export const clear = (store) => run(store, 'readwrite', os => os.clear());
