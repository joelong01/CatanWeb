/**
 * ViewportScaler - Handles uniform scaling of fixed-dimension game container
 * Equivalent to XAML Viewbox Stretch="Uniform" behavior
 *
 * Design: Fixed internal coordinates (1920x1080 landscape, 1080x1920 portrait)
 * scaled uniformly to fit any viewport, capped at 1.0x to prevent oversizing.
 *
 * v3 - Mobile: scale to fill width, allow vertical scroll
 */

window.viewportScaler = {
    container: null,
    viewport: null,
    _resizeHandler: null,
    _orientationHandler: null,
    _visibilityHandler: null,
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
        this._visibilityHandler = () => this._onVisibilityChange();

        window.addEventListener('resize', this._resizeHandler);
        window.addEventListener('orientationchange', this._orientationHandler);
        document.addEventListener('visibilitychange', this._visibilityHandler);

        this._initialized = true;

        // Set environment indicator (local vs web)
        const isLocal = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1' || window.location.hostname === '0.0.0.0';
        this.container.dataset.env = isLocal ? 'local' : 'web';

        // Initial scaling
        this.updateScale();

        console.log('[viewportScaler] Initialized, env:', this.container.dataset.env);
        return true;
    },

    /**
     * Detect if running on a mobile/touch device
     */
    isMobileDevice: function () {
        return (window.matchMedia('(pointer: coarse)').matches || window.innerWidth <= 1024);
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

        // Use window dimensions for more reliable measurement on mobile
        // offsetWidth/offsetHeight can be affected by safe area padding
        const viewportWidth = window.innerWidth;
        const viewportHeight = window.innerHeight;
        const viewportAspect = viewportWidth / viewportHeight;

        // Determine orientation
        // Portrait: aspect ratio < 4:3 (1.333)
        // Landscape: aspect ratio >= 4:3
        const isPortrait = viewportAspect < this.PORTRAIT_THRESHOLD;
        const ref = isPortrait
            ? { width: this.PORTRAIT_WIDTH, height: this.PORTRAIT_HEIGHT }
            : { width: this.LANDSCAPE_WIDTH, height: this.LANDSCAPE_HEIGHT };

        const isMobile = this.isMobileDevice();

        // Calculate scale factor - uniform scaling to fit viewport
        const scaleX = viewportWidth / ref.width;
        const scaleY = viewportHeight / ref.height;

        // Desktop: use min to fit entirely within viewport (no scrolling)
        // Mobile: use scaleX to fill width (user can scroll vertically if needed)
        const scale = isMobile ? scaleX : Math.min(scaleX, scaleY);

        // Apply scale and dimensions via CSS custom properties
        this.container.style.setProperty('--viewport-scale', scale);
        this.container.style.setProperty('--base-width', `${ref.width}px`);
        this.container.style.setProperty('--base-height', `${ref.height}px`);

        // Set layout mode attribute for CSS selectors
        this.container.dataset.layoutMode = isPortrait ? 'portrait' : 'landscape';
        this.container.dataset.isMobile = isMobile ? 'true' : 'false';

        // Also set on viewport for consistent access
        this.viewport.dataset.layoutMode = isPortrait ? 'portrait' : 'landscape';

        // Notify Blazor if orientation changed
        if (this._lastIsPortrait !== isPortrait) {
            this._lastIsPortrait = isPortrait;
            if (this._dotNetRef) {
                this._dotNetRef.invokeMethodAsync('OnOrientationChanged', isPortrait);
            }
        }

        this._debugLog(viewportWidth, viewportHeight, ref.width, ref.height, scale, isPortrait, isMobile);

        // Safari fix: manually size the board container since Safari can't handle
        // height: 100% + aspect-ratio CSS calculation
        if (this.isSafari() && !isPortrait) {
            this._fixSafariBoardSize();
        }
    },

    /**
     * Detect if running in Safari (any version - desktop or mobile)
     */
    isSafari: function () {
        const ua = navigator.userAgent;
        // Safari but not Chrome (Chrome includes "Safari" in UA)
        return ua.includes('Safari') && !ua.includes('Chrome') && !ua.includes('Chromium');
    },

    /**
     * Safari workaround: Calculate and set explicit board dimensions.
     * Safari can't properly calculate width from height + aspect-ratio.
     * @param {number} retryCount - Current retry attempt (default 0)
     */
    _fixSafariBoardSize: function (retryCount = 0) {
        // Guard against running after dispose() was called
        if (!this._initialized) return;

        const maxRetries = 50; // 5 seconds max at 100ms intervals
        const centerPanel = document.querySelector('.center-panel');
        const portraitBoardArea = document.querySelector('.portrait-board-area');
        const gameBoard = document.querySelector('.game-board');
        const boardContainer = document.querySelector('.board-container');
        const boardSvgContainer = document.querySelector('.board-svg-container');

        if (!centerPanel || !boardContainer || !boardSvgContainer) {
            if (retryCount >= maxRetries) return;
            // Containers not rendered yet - retry after Blazor finishes rendering
            setTimeout(() => this._fixSafariBoardSize(retryCount + 1), 100);
            return;
        }

        // Get the available space from center panel using clientWidth/Height (content area, excludes borders)
        const containerWidth = centerPanel.clientWidth;
        const containerHeight = centerPanel.clientHeight;
        if (containerWidth <= 0 || containerHeight <= 0) return;

        // Get the board's aspect ratio from the CSS variable
        const style = getComputedStyle(boardSvgContainer);
        const aspectRatioStr = style.getPropertyValue('--board-aspect-ratio');
        const aspectRatio = parseFloat(aspectRatioStr) || (1080 / 936); // Default Catan board ratio

        // Calculate board size to fit container while maintaining aspect ratio
        let boardWidth, boardHeight;

        // Try fitting by height first (landscape typically constrained by height)
        boardHeight = containerHeight;
        boardWidth = boardHeight * aspectRatio;

        // If width exceeds container, fit by width instead
        if (boardWidth > containerWidth) {
            boardWidth = containerWidth;
            boardHeight = boardWidth / aspectRatio;
        }

        // Apply explicit dimensions to ALL containers in the chain
        // Use max-width/max-height to prevent overflow, and ensure centering

        // Intermediate containers fill the center panel
        const containerStyle = `width: ${containerWidth}px; height: ${containerHeight}px; display: flex; align-items: center; justify-content: center;`;
        if (portraitBoardArea) portraitBoardArea.style.cssText = containerStyle;
        if (gameBoard) gameBoard.style.cssText = containerStyle;
        if (boardContainer) boardContainer.style.cssText = containerStyle;

        // Board container gets exact calculated dimensions, centered by parent flexbox
        boardSvgContainer.style.cssText = `width: ${boardWidth}px; height: ${boardHeight}px; max-width: ${containerWidth}px; max-height: ${containerHeight}px;`;
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
        if (this._visibilityHandler) {
            document.removeEventListener('visibilitychange', this._visibilityHandler);
        }
        this._initialized = false;
        this.container = null;
        this.viewport = null;
    },

    /**
     * Handle page visibility changes (phone wake from sleep, tab switch, etc.)
     * Notifies Blazor to re-sync game state
     */
    _onVisibilityChange: function () {
        if (document.visibilityState === 'visible') {
            console.log('[viewportScaler] Page became visible - notifying Blazor for state sync');
            if (this._dotNetRef) {
                this._dotNetRef.invokeMethodAsync('OnPageBecameVisible')
                    .catch(err => console.warn('[viewportScaler] Failed to notify Blazor:', err));
            }
            // Also refresh the scale in case viewport changed while hidden
            this.updateScale();
        } else {
            console.log('[viewportScaler] Page became hidden');
        }
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
    _debugLog: function (viewportW, viewportH, targetW, targetH, scale, isPortrait, isMobile) {
        console.log(
            `[viewportScaler] Viewport: ${viewportW}x${viewportH}, ` +
            `Target: ${targetW}x${targetH}, ` +
            `Scale: ${scale.toFixed(3)}, ` +
            `Layout: ${isPortrait ? 'portrait' : 'landscape'}, ` +
            `Mobile: ${isMobile}, ` +
            `window.innerWidth: ${window.innerWidth}, window.innerHeight: ${window.innerHeight}, ` +
            `devicePixelRatio: ${window.devicePixelRatio}`
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

/**
 * Converts client (screen) coordinates to SVG viewBox coordinates.
 * Used for accurate hit-testing on hex tiles.
 * @param {number} clientX - Client X coordinate (from mouse event)
 * @param {number} clientY - Client Y coordinate (from mouse event)
 * @returns {number[]} - [svgX, svgY] in viewBox coordinate space
 */
window.clientToSvgCoords = function(clientX, clientY) {
    // Find the interactive SVG layer
    const svg = document.querySelector('.board-interactive-layer');
    if (!svg) {
        console.error('[clientToSvgCoords] SVG element not found');
        return null;
    }

    // Create an SVGPoint and set to client coordinates
    const point = svg.createSVGPoint();
    point.x = clientX;
    point.y = clientY;

    // Get the transformation matrix from screen to SVG coordinates
    const ctm = svg.getScreenCTM();
    if (!ctm) {
        console.error('[clientToSvgCoords] Could not get screen CTM');
        return null;
    }

    // Transform point from screen to SVG coordinates
    const svgPoint = point.matrixTransform(ctm.inverse());
    return [svgPoint.x, svgPoint.y];
};
