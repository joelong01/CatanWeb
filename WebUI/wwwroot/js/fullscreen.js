/**
 * Fullscreen toggle with cross-browser support.
 * Handles standard API plus webkit/moz/ms prefixes for older browsers.
 */

// Debounce to prevent double-triggering from UI frameworks
let lastToggleTime = 0;
const DEBOUNCE_MS = 1000;

/**
 * Toggle fullscreen mode for the document.
 * Uses cross-browser APIs with appropriate prefixes.
 */
window.toggleFullScreen = () => {
    // Debounce check - prevent rapid toggling
    const now = Date.now();
    if (now - lastToggleTime < DEBOUNCE_MS) {
        return;
    }
    lastToggleTime = now;

    // Check if currently in fullscreen (cross-browser)
    const isFullscreen = document.fullscreenElement ||
                         document.webkitFullscreenElement ||
                         document.mozFullScreenElement ||
                         document.msFullscreenElement;

    if (!isFullscreen) {
        // Enter fullscreen (cross-browser)
        const elem = document.documentElement;
        if (elem.requestFullscreen) {
            elem.requestFullscreen().catch(() => {});
        } else if (elem.webkitRequestFullscreen) {
            elem.webkitRequestFullscreen();
        } else if (elem.mozRequestFullScreen) {
            elem.mozRequestFullScreen();
        } else if (elem.msRequestFullscreen) {
            elem.msRequestFullscreen();
        }
    } else {
        // Exit fullscreen (cross-browser)
        if (document.exitFullscreen) {
            document.exitFullscreen();
        } else if (document.webkitExitFullscreen) {
            document.webkitExitFullscreen();
        } else if (document.mozCancelFullScreen) {
            document.mozCancelFullScreen();
        } else if (document.msExitFullscreen) {
            document.msExitFullscreen();
        }
    }
};
