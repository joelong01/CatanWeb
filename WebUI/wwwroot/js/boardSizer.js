/**
 * Board Sizer - Calculates and applies correct dimensions to the board container
 * based on available width and the board's aspect ratio.
 */

window.boardSizer = {
    _resizeHandler: null,
    _containerSelector: null,
    _aspectRatio: 1,
    _dotNetRef: null,

    /**
     * Initializes the board sizer with the container and aspect ratio.
     * @param {string} containerSelector - CSS selector for the board container
     * @param {number} aspectRatio - Width / Height ratio of the board
     * @param {object} dotNetRef - Optional .NET reference for callbacks
     * @param {string} constraintSelector - CSS selector for the constraining container (optional)
     */
    initialize: function(containerSelector, aspectRatio, dotNetRef, constraintSelector) {
        this._containerSelector = containerSelector;
        this._aspectRatio = aspectRatio;
        this._dotNetRef = dotNetRef;
        this._constraintSelector = constraintSelector || '.center-panel';

        console.log('[boardSizer] Initialized:', { aspectRatio, containerSelector, constraintSelector: this._constraintSelector });

        // Apply initial sizing
        this.updateSize();

        // Set up resize listener
        this._setupResizeHandler();
    },

    /**
     * Updates the board container size based on available width.
     * NOTE: With viewport scaling (viewportScaler.js), we no longer set explicit pixel
     * dimensions. The board uses CSS width/height: 100% and the viewport transform
     * scales everything uniformly. This method now just reports current dimensions.
     */
    updateSize: function() {
        const container = document.querySelector(this._containerSelector);
        if (!container) {
            console.error('[boardSizer] Container not found:', this._containerSelector);
            return { width: 0, height: 0 };
        }

        // Try to get dimensions from any visible panel (in portrait mode, inactive panels are hidden)
        const panelSelectors = ['.center-panel', '.left-panel', '.right-panel'];
        let width = 0;
        let height = 0;

        for (const selector of panelSelectors) {
            const panel = document.querySelector(selector);
            if (panel) {
                const rect = panel.getBoundingClientRect();
                if (rect.width > 0 && rect.height > 0) {
                    width = rect.width;
                    height = rect.height;
                    break;
                }
            }
        }

        // Fallback to constraint container if no panel found
        if (width === 0 || height === 0) {
            const constraintContainer = document.querySelector(this._constraintSelector);
            if (constraintContainer) {
                const constraintRect = constraintContainer.getBoundingClientRect();
                width = constraintRect.width;
                height = constraintRect.height;
            }
        }

        // Don't set explicit pixel dimensions - let CSS handle sizing
        // The viewport transform (viewportScaler.js) scales everything uniformly
        console.log('[boardSizer] Container size:', { width: Math.round(width), height: Math.round(height) });

        return { width, height };
    },

    /**
     * Updates the aspect ratio and recalculates size.
     * @param {number} aspectRatio - New aspect ratio
     */
    setAspectRatio: function(aspectRatio) {
        this._aspectRatio = aspectRatio;
        this.updateSize();
    },

    /**
     * Sets up the window resize handler.
     */
    _setupResizeHandler: function() {
        if (this._resizeHandler) {
            window.removeEventListener('resize', this._resizeHandler);
        }

        let debounceTimer = null;

        this._resizeHandler = () => {
            if (debounceTimer) {
                clearTimeout(debounceTimer);
            }

            debounceTimer = setTimeout(() => {
                const size = this.updateSize();

                // Notify .NET if callback registered
                if (this._dotNetRef) {
                    this._dotNetRef.invokeMethodAsync('OnBoardResized', size.width, size.height);
                }
            }, 100);
        };

        window.addEventListener('resize', this._resizeHandler);
    },

    /**
     * Cleans up the resize handler.
     */
    dispose: function() {
        if (this._resizeHandler) {
            window.removeEventListener('resize', this._resizeHandler);
            this._resizeHandler = null;
        }
        this._dotNetRef = null;
    },

    /**
     * Gets the current bounds of the board container.
     * @returns {object} - { width, height, left, top } or null if not found
     */
    getBounds: function() {
        const container = document.querySelector(this._containerSelector);
        if (!container) {
            return null;
        }
        const rect = container.getBoundingClientRect();
        return {
            width: rect.width,
            height: rect.height,
            left: rect.left,
            top: rect.top
        };
    }
};
