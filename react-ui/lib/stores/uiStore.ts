/**
 * UI state store - manages client-side UI preferences and layout state.
 *
 * Uses persist middleware to save preferences to localStorage.
 * This state is not shared with the server.
 */

import { create } from 'zustand';
import { persist } from 'zustand/middleware';

/** Portrait mode tab options */
export type PortraitTab = 'board' | 'controls' | 'players';

interface UIState {
  /** Whether the viewport is in portrait orientation */
  isPortrait: boolean;

  /** Whether the device has coarse pointer (touch) */
  isMobile: boolean;

  /** Current viewport scale factor */
  viewportScale: number;

  /** Active tab in portrait mode */
  activePortraitTab: PortraitTab;

  /** Whether the nav menu (hamburger) is open */
  isNavMenuOpen: boolean;

  /** Whether winner celebration is showing */
  isShowingCelebration: boolean;

  /** Whether winner dialog is open */
  isWinnerDialogOpen: boolean;

  /** Whether the robber target menu is open */
  isRobberMenuOpen: boolean;

  /** Coordinates for context menus (robber target, etc.) */
  menuPosition: { x: number; y: number } | null;
}

interface UIActions {
  /** Update orientation state (typically from resize observer) */
  setOrientation: (isPortrait: boolean, isMobile: boolean) => void;

  /** Update viewport scale */
  setViewportScale: (scale: number) => void;

  /** Set active portrait tab */
  setActivePortraitTab: (tab: PortraitTab) => void;

  /** Toggle nav menu open/closed */
  toggleNavMenu: () => void;

  /** Set nav menu state directly */
  setNavMenuOpen: (open: boolean) => void;

  /** Show/hide winner celebration */
  setShowingCelebration: (showing: boolean) => void;

  /** Show/hide winner dialog */
  setWinnerDialogOpen: (open: boolean) => void;

  /** Open robber menu at position */
  openRobberMenu: (x: number, y: number) => void;

  /** Close robber menu */
  closeRobberMenu: () => void;

  /** Close all menus/dialogs */
  closeAllOverlays: () => void;
}

type UIStore = UIState & UIActions;

const initialState: UIState = {
  isPortrait: false,
  isMobile: false,
  viewportScale: 1,
  activePortraitTab: 'board',
  isNavMenuOpen: false,
  isShowingCelebration: false,
  isWinnerDialogOpen: false,
  isRobberMenuOpen: false,
  menuPosition: null,
};

/**
 * Zustand store for UI state.
 * Uses persist middleware to save user preferences to localStorage.
 */
export const useUIStore = create<UIStore>()(
  persist(
    (set) => ({
      ...initialState,

      setOrientation: (isPortrait, isMobile) => set({ isPortrait, isMobile }),

      setViewportScale: (viewportScale) => set({ viewportScale }),

      setActivePortraitTab: (activePortraitTab) => set({ activePortraitTab }),

      toggleNavMenu: () =>
        set((state) => ({ isNavMenuOpen: !state.isNavMenuOpen })),

      setNavMenuOpen: (isNavMenuOpen) => set({ isNavMenuOpen }),

      setShowingCelebration: (isShowingCelebration) =>
        set({ isShowingCelebration }),

      setWinnerDialogOpen: (isWinnerDialogOpen) => set({ isWinnerDialogOpen }),

      openRobberMenu: (x, y) =>
        set({ isRobberMenuOpen: true, menuPosition: { x, y } }),

      closeRobberMenu: () =>
        set({ isRobberMenuOpen: false, menuPosition: null }),

      closeAllOverlays: () =>
        set({
          isNavMenuOpen: false,
          isShowingCelebration: false,
          isWinnerDialogOpen: false,
          isRobberMenuOpen: false,
          menuPosition: null,
        }),
    }),
    {
      name: 'catan-ui-preferences',
      // Only persist user preferences, not transient state
      partialize: (state) => ({
        activePortraitTab: state.activePortraitTab,
      }),
    }
  )
);

// ============================================================================
// Selectors
// ============================================================================

/** Select whether we're in portrait layout mode */
export const selectIsPortrait = (state: UIStore) => state.isPortrait;

/** Select whether device is mobile (touch) */
export const selectIsMobile = (state: UIStore) => state.isMobile;

/** Select current portrait tab */
export const selectActivePortraitTab = (state: UIStore) =>
  state.activePortraitTab;

/** Select whether any overlay is open (for backdrop) */
export const selectHasOpenOverlay = (state: UIStore) =>
  state.isNavMenuOpen ||
  state.isWinnerDialogOpen ||
  state.isRobberMenuOpen ||
  state.isShowingCelebration;
