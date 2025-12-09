/**
 * ViewportScaler - Handles uniform scaling of fixed-dimension game container
 * Equivalent to XAML Viewbox Stretch="Uniform" behavior
 *
 * Design: Fixed internal coordinates (1920x1080 landscape, 1080x1920 portrait)
 * scaled uniformly to fit any viewport, capped at 1.0x to prevent oversizing.
 */

window.viewportScaler = {
    container: null,
    viewport: null,
    _resizeHandler: null,
    _orientationHandler: null,
    _initialized: false,
    _dotNetRef: null,
    _lastIsPortrait: null,

    // Reference dimensions
    LANDSCAPE_WIDTH: 1920,
    LANDSCAPE_HEIGHT: 1080,
    PORTRAIT_WIDTH: 1080,
    PORTRAIT_HEIGHT: 1920,

    // Portrait threshold: aspect ratio < 4:3 (1.333)
    PORTRAIT_THRESHOLD: 4 / 3,

    /**
     * Initialize the viewport scaler
     * @param {string} containerSelector - CSS selector for game container (default: '.game-container')
     * @param {string} viewportSelector - CSS selector for viewport wrapper (default: '.game-viewport')
     * @param {object} dotNetRef - Optional DotNet object reference for callbacks
     */
    initialize: function (containerSelector, viewportSelector, dotNetRef) {
        this.container = document.querySelector(containerSelector || '.game-container');
        this.viewport = document.querySelector(viewportSelector || '.game-viewport');
        this._dotNetRef = dotNetRef || null;

        if (!this.container || !this.viewport) {
            console.warn('[viewportScaler] Container or viewport not found, will retry on next call');
            return false;
        }

        if (this._initialized) {
            // Already initialized, just update
            this._dotNetRef = dotNetRef || this._dotNetRef;
            this.updateScale();
            return true;
        }

        // Set up event listeners - immediate update, no debounce for responsiveness
        this._resizeHandler = () => this.updateScale();
        this._orientationHandler = () => this.updateScale();

        window.addEventListener('resize', this._resizeHandler);
        window.addEventListener('orientationchange', this._orientationHandler);

        this._initialized = true;

        // Set environment indicator (local vs web)
        const isLocal = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1';
        this.container.dataset.env = isLocal ? 'local' : 'web';

        // Initial scaling
        this.updateScale();

        console.log('[viewportScaler] Initialized, env:', this.container.dataset.env);
        return true;
    },

    /**
     * Update the scale factor based on current viewport dimensions
     */
    updateScale: function () {
        if (!this.container || !this.viewport) {
            // Try to find elements again (may have been re-rendered by Blazor)
            this.container = document.querySelector('.game-container');
            this.viewport = document.querySelector('.game-viewport');
            if (!this.container || !this.viewport) return;
        }

        const viewportWidth = this.viewport.offsetWidth;
        const viewportHeight = this.viewport.offsetHeight;
        const viewportAspect = viewportWidth / viewportHeight;

        // Determine orientation
        // Portrait: aspect ratio < 4:3 (1.333)
        // Landscape: aspect ratio >= 4:3
        const isPortrait = viewportAspect < this.PORTRAIT_THRESHOLD;
        const ref = isPortrait
            ? { width: this.PORTRAIT_WIDTH, height: this.PORTRAIT_HEIGHT }
            : { width: this.LANDSCAPE_WIDTH, height: this.LANDSCAPE_HEIGHT };

        // Calculate scale factor - uniform scaling to fit viewport
        const scaleX = viewportWidth / ref.width;
        const scaleY = viewportHeight / ref.height;
        const scale = Math.min(scaleX, scaleY);

        // Apply scale and dimensions via CSS custom properties
        this.container.style.setProperty('--viewport-scale', scale);
        this.container.style.setProperty('--base-width', `${ref.width}px`);
        this.container.style.setProperty('--base-height', `${ref.height}px`);

        // Set layout mode attribute for CSS selectors
        this.container.dataset.layoutMode = isPortrait ? 'portrait' : 'landscape';

        // Also set on viewport for consistent access
        this.viewport.dataset.layoutMode = isPortrait ? 'portrait' : 'landscape';

        // Notify Blazor if orientation changed
        if (this._lastIsPortrait !== isPortrait) {
            this._lastIsPortrait = isPortrait;
            if (this._dotNetRef) {
                this._dotNetRef.invokeMethodAsync('OnOrientationChanged', isPortrait);
            }
        }

        this._debugLog(viewportWidth, viewportHeight, ref.width, ref.height, scale, isPortrait);
    },

    /**
     * Get current layout information
     * @returns {object} - { scale, isPortrait, baseWidth, baseHeight }
     */
    getLayoutInfo: function () {
        if (!this.container) return null;

        return {
            scale: parseFloat(this.container.style.getPropertyValue('--viewport-scale')) || 1,
            isPortrait: this.container.dataset.layoutMode === 'portrait',
            baseWidth: parseInt(this.container.style.getPropertyValue('--base-width')) || this.LANDSCAPE_WIDTH,
            baseHeight: parseInt(this.container.style.getPropertyValue('--base-height')) || this.LANDSCAPE_HEIGHT
        };
    },

    /**
     * Force a scale update (call after Blazor re-renders)
     */
    refresh: function () {
        this.container = document.querySelector('.game-container');
        this.viewport = document.querySelector('.game-viewport');
        this.updateScale();
    },

    /**
     * Clean up event listeners
     */
    dispose: function () {
        if (this._resizeHandler) {
            window.removeEventListener('resize', this._resizeHandler);
        }
        if (this._orientationHandler) {
            window.removeEventListener('orientationchange', this._orientationHandler);
        }
        this._initialized = false;
        this.container = null;
        this.viewport = null;
    },

    /**
     * Debounce helper
     */
    _debounce: function (func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    },

    /**
     * Debug logging
     */
    _debugLog: function (viewportW, viewportH, targetW, targetH, scale, isPortrait) {
        console.log(
            `[viewportScaler] Viewport: ${viewportW}x${viewportH}, ` +
            `Target: ${targetW}x${targetH}, ` +
            `Scale: ${scale.toFixed(3)}, ` +
            `Layout: ${isPortrait ? 'portrait' : 'landscape'}`
        );
    }
};

// Auto-initialize when DOM is ready (for non-Blazor pages or initial load)
// Blazor pages should call viewportScaler.initialize() after render
document.addEventListener('DOMContentLoaded', function () {
    // Delay slightly to let Blazor render first
    setTimeout(function () {
        if (document.querySelector('.game-container')) {
            window.viewportScaler.initialize();
        }
    }, 100);
});
