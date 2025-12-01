/**
 * SVG Rasterizer - Converts SVG elements to canvas for performance optimization.
 *
 * The browser spends significant time re-rasterizing complex SVG patterns on every
 * repaint. By pre-rasterizing the static layer to a canvas, we eliminate this cost
 * for subsequent interactions.
 */

window.svgRasterizer = {
    /**
     * Rasterizes an SVG element to a canvas element.
     * @param {string} svgSelector - CSS selector for the SVG element to rasterize
     * @param {string} canvasSelector - CSS selector for the target canvas element
     * @returns {Promise<boolean>} - True if successful
     */
    rasterize: async function (svgSelector, canvasSelector) {
        const svg = document.querySelector(svgSelector);
        const canvas = document.querySelector(canvasSelector);

        if (!svg || !canvas) {
            console.error('[svgRasterizer] Could not find elements:', { svgSelector, canvasSelector });
            return false;
        }

        try {
            // Get the SVG dimensions from viewBox
            const viewBox = svg.getAttribute('viewBox');
            if (!viewBox) {
                console.error('[svgRasterizer] SVG has no viewBox');
                return false;
            }

            const [minX, minY, vbWidth, vbHeight] = viewBox.split(' ').map(Number);

            // Get the actual rendered size of the SVG
            const rect = svg.getBoundingClientRect();
            const dpr = window.devicePixelRatio || 1;

            // Use rendered size, scaled by device pixel ratio for crisp display
            const displayWidth = rect.width;
            const displayHeight = rect.height;

            if (displayWidth === 0 || displayHeight === 0) {
                console.error('[svgRasterizer] SVG has zero dimensions:', { displayWidth, displayHeight });
                return false;
            }

            const canvasWidth = Math.round(displayWidth * dpr);
            const canvasHeight = Math.round(displayHeight * dpr);

            console.log('[svgRasterizer] Sizing:', { displayWidth, displayHeight, canvasWidth, canvasHeight, dpr, vbWidth, vbHeight });

            // Set canvas backing store size (actual pixels)
            canvas.width = canvasWidth;
            canvas.height = canvasHeight;

            // Set explicit CSS dimensions to match SVG's rendered size exactly
            // This prevents CSS Grid from stretching the canvas and causing object-fit issues
            canvas.style.width = displayWidth + 'px';
            canvas.style.height = displayHeight + 'px';

            const ctx = canvas.getContext('2d');

            // Scale context to match device pixel ratio
            ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

            // Serialize SVG to string
            const serializer = new XMLSerializer();
            let svgString = serializer.serializeToString(svg);

            // Ensure SVG has xmlns
            if (!svgString.includes('xmlns="http://www.w3.org/2000/svg"')) {
                svgString = svgString.replace('<svg', '<svg xmlns="http://www.w3.org/2000/svg"');
            }

            // Convert external image hrefs to data URLs for CORS compliance
            svgString = await this._inlineImages(svgString, svg);

            // Create blob and image
            const blob = new Blob([svgString], { type: 'image/svg+xml;charset=utf-8' });
            const url = URL.createObjectURL(blob);

            const img = new Image();
            img.crossOrigin = 'anonymous';

            return new Promise((resolve, reject) => {
                img.onload = () => {
                    // Clear and draw at display size (context is already scaled)
                    ctx.clearRect(0, 0, displayWidth, displayHeight);
                    ctx.drawImage(img, 0, 0, displayWidth, displayHeight);
                    URL.revokeObjectURL(url);

                    // Swap visibility: hide SVG, show canvas
                    svg.style.display = 'none';
                    canvas.style.display = 'block';

                    console.log('[svgRasterizer] Rasterization complete:', canvasWidth, 'x', canvasHeight, 'backing pixels');
                    resolve(true);
                };

                img.onerror = (e) => {
                    console.error('[svgRasterizer] Failed to load SVG as image:', e);
                    URL.revokeObjectURL(url);
                    resolve(false);
                };

                img.src = url;
            });
        } catch (error) {
            console.error('[svgRasterizer] Error during rasterization:', error);
            return false;
        }
    },

    /**
     * Inlines external images as data URLs to avoid CORS issues.
     * @param {string} svgString - The serialized SVG string
     * @param {SVGElement} svgElement - The original SVG element for resolving relative URLs
     * @returns {Promise<string>} - SVG string with inlined images
     */
    _inlineImages: async function (svgString, svgElement) {
        // Find all image elements in the SVG
        const images = svgElement.querySelectorAll('image');
        const imagePromises = [];

        for (const img of images) {
            const href = img.getAttribute('href') || img.getAttribute('xlink:href');
            if (href && !href.startsWith('data:')) {
                imagePromises.push(this._fetchAsDataUrl(href));
            }
        }

        if (imagePromises.length === 0) {
            return svgString;
        }

        const dataUrls = await Promise.all(imagePromises);

        // Replace hrefs in the SVG string
        let index = 0;
        for (const img of images) {
            const href = img.getAttribute('href') || img.getAttribute('xlink:href');
            if (href && !href.startsWith('data:') && dataUrls[index]) {
                // Replace both href and xlink:href variants
                svgString = svgString.replace(`href="${href}"`, `href="${dataUrls[index]}"`);
                svgString = svgString.replace(`xlink:href="${href}"`, `xlink:href="${dataUrls[index]}"`);
                index++;
            }
        }

        return svgString;
    },

    /**
     * Fetches an image URL and converts it to a data URL.
     * @param {string} url - The image URL
     * @returns {Promise<string|null>} - Data URL or null on failure
     */
    _fetchAsDataUrl: async function (url) {
        try {
            const response = await fetch(url);
            const blob = await response.blob();
            return new Promise((resolve) => {
                const reader = new FileReader();
                reader.onloadend = () => resolve(reader.result);
                reader.onerror = () => resolve(null);
                reader.readAsDataURL(blob);
            });
        } catch (error) {
            console.warn('[svgRasterizer] Failed to fetch image:', url, error);
            return null;
        }
    },

    /**
     * Shows the canvas and hides the SVG after rasterization.
     * @param {string} svgSelector - CSS selector for the SVG to hide
     * @param {string} canvasSelector - CSS selector for the canvas to show
     */
    swapToCanvas: function (svgSelector, canvasSelector) {
        const svg = document.querySelector(svgSelector);
        const canvas = document.querySelector(canvasSelector);

        if (svg) svg.style.display = 'none';
        if (canvas) canvas.style.display = 'block';
    },

    /**
     * Shows the SVG and hides the canvas (for re-rasterization after shuffle).
     * @param {string} svgSelector - CSS selector for the SVG to show
     * @param {string} canvasSelector - CSS selector for the canvas to hide
     */
    reset: function (svgSelector, canvasSelector) {
        const svg = document.querySelector(svgSelector);
        const canvas = document.querySelector(canvasSelector);

        console.log('[svgRasterizer] Resetting for re-rasterization');
        if (svg) svg.style.display = '';
        if (canvas) canvas.style.display = 'none';
    }
};
