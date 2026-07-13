// Touch-friendly drag-reorder via SortableJS (loaded globally from index.html).
// Reports the moved indices back to the owning Blazor component, which performs the reorder.

const instances = new WeakMap();

export function init(element, dotnetRef) {
    if (typeof Sortable === 'undefined' || !element) {
        return false;
    }
    const sortable = Sortable.create(element, {
        animation: 150,
        handle: '.drag',
        onEnd: evt => {
            if (evt.oldIndex !== evt.newIndex) {
                dotnetRef.invokeMethodAsync('OnDragReorder', evt.oldIndex, evt.newIndex);
            }
        },
    });
    instances.set(element, sortable);
    return true;
}

export function dispose(element) {
    const sortable = instances.get(element);
    if (sortable) {
        sortable.destroy();
        instances.delete(element);
    }
}
