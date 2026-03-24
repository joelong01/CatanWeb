/**
 * Keyboard shortcuts for game purchase actions.
 * Supports s=Settlement, c=City, k=Soldier, r=Road, d=DevCard.
 * Calls back into Blazor via DotNet object reference.
 */
window.gameKeyboardShortcuts = {
    _dotNetRef: null,
    _handler: null,

    /**
     * Register global keyboard shortcut listener.
     * @param {object} dotNetRef - DotNet object reference for callbacks
     */
    initialize: function (dotNetRef) {
        this._dotNetRef = dotNetRef;
        if (this._handler) {
            window.removeEventListener('keydown', this._handler);
        }
        this._handler = (e) => this._onKeyDown(e);
        window.addEventListener('keydown', this._handler);
    },

    _onKeyDown: function (e) {
        // Ignore if typing in an input or editable element
        const target = e.target;
        if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.isContentEditable) {
            return;
        }

        const key = e.key.toLowerCase();
        if (key === 's' || key === 'c' || key === 'k' || key === 'r' || key === 'd') {
            if (this._dotNetRef) {
                this._dotNetRef.invokeMethodAsync('OnPurchaseKeyPressed', key)
                    .catch(() => {}); // Ignore errors (component may be disposed)
            }
        }
    },

    /**
     * Remove keyboard listener and release resources.
     */
    dispose: function () {
        if (this._handler) {
            window.removeEventListener('keydown', this._handler);
            this._handler = null;
        }
        this._dotNetRef = null;
    }
};
