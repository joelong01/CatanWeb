# WebUI Scaling Architecture Design

**Date:** December 5, 2025  
**Purpose:** Replace fragile percentage-based scaling with robust fixed-dimension approach  
**Based on:** Desktop XAML Viewbox pattern analysis  

## Executive Summary

The current WebUI scaling system is fragile due to mixed pixel/percentage approaches and complex JavaScript retrofitting. This document outlines a complete architectural redesign based on the proven XAML Viewbox pattern used in the Desktop app, supporting 4K development environments and mobile devices with consistent, predictable scaling.

## Problem Analysis

### Current WebUI Issues (Fragility)

1. **Mixed Scaling Approaches**
   - Fixed pixels: `PlayerTile: 553px width`, `PurchaseButton: 70x90px`
   - Percentages: `width: 100%`, `height: 100%`
   - JavaScript scaling: `panelsScaler.js` retrofits scaling after layout
   - Media queries: Different layouts for different breakpoints

2. **Fragile Dependencies**
   - Components break when viewport changes unexpectedly
   - Portrait mode requires complex JavaScript transforms
   - Scaling inconsistencies across different screen sizes
   - Debugging difficulties due to mixed coordinate systems

3. **Performance Issues**
   - Multiple reflows during resize events
   - Complex media query evaluations
   - JavaScript-driven layout changes cause jank

### Desktop XAML Success Pattern

The Desktop app achieves robust scaling through:

1. **Uniform Scaling Containers**

   ```xml
   <Viewbox Stretch="Uniform" Grid.Column="0">
     <!-- All content scales together as single unit -->
   </Viewbox>
   ```

2. **Fixed Grid Proportions**

   ```xml
   <ColumnDefinition Width="25*" />  <!-- Left panel -->
   <ColumnDefinition Width="60*" />  <!-- Board -->
   <ColumnDefinition Width="26*" />  <!-- Right panel -->
   ```

3. **Consistent Coordinate System**
   - All components use fixed dimensions within their containers
   - Viewbox handles all scaling uniformly
   - No mixed pixel/percentage approaches

## Proposed Architecture

### Core Principle: Fixed Internal Dimensions + Uniform Scaling

Create a fixed-dimension coordinate system that scales uniformly to fit any viewport, just like XAML Viewbox.

### Target Resolutions

**Single Base Resolution Per Orientation:**

- **Landscape**: 1920x1080 (design coordinate system)
- **Portrait**: 1080x1920 (rotated coordinate system)
- **All devices**: Scale these bases to fit viewport, capped at 1.0x
- **4K displays**: Get scale=1.0 (capped), 1366x768 laptops get scale=0.71
- **Benefit**: Identical content across all devices, simple scaling logic

### HTML Structure

```html
<div class="game-viewport">
  <!-- Fixed aspect ratio container - equivalent to XAML Page -->
  <div class="game-container" data-layout-mode="landscape">
    
    <!-- Portrait mode tab bar (hidden in landscape) -->
    <div class="portrait-tabs">
      <button class="portrait-tab" data-tab="board">Board</button>
      <button class="portrait-tab" data-tab="controls">Controls</button>
      <button class="portrait-tab" data-tab="players">Players</button>
    </div>
    
    <!-- Three scaling zones - equivalent to XAML Grid.ColumnDefinitions -->
    <div class="game-grid">
      
      <!-- Left Panel Viewbox equivalent -->
      <div class="panel-viewbox left-viewbox left-panel">
        <div class="left-panel-content">
          <!-- Game controls, purchase buttons, roll grid -->
          <!-- ALL content uses fixed pixel dimensions -->
        </div>
      </div>
      
      <!-- Center Board Viewbox equivalent -->
      <div class="panel-viewbox center-viewbox center-panel">
        <div class="board-content">
          <!-- Game board - fixed dimensions, scales as unit -->
        </div>
      </div>
      
      <!-- Right Panel Viewbox equivalent -->
      <div class="panel-viewbox right-viewbox right-panel">
        <div class="right-panel-content">
          <!-- Player panels, resource tracking -->
          <!-- ALL content uses fixed pixel dimensions -->
        </div>
      </div>
      
    </div>
  </div>
  
  <!-- Fixed hamburger menu and side nav (outside scaled container) -->
  <button class="hamburger-btn">☰</button>
  <div class="side-nav-overlay">
    <div class="side-nav-panel">
      <!-- Navigation menu items -->
    </div>
  </div>
</div>
```

### Layout Diagrams

**Landscape Mode (1920x1080 base):**

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│ [☰]                    Game Viewport (100vw × 100vh)                       │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │              Game Container (1920×1080, scaled)                     │   │
│   │  ┌─────────┐  ┌───────────────────────┐  ┌─────────────────────┐   │   │
│   │  │  Left   │  │                       │  │       Right         │   │   │
│   │  │ Panel   │  │        Board          │  │      Panel          │   │   │
│   │  │         │  │      (Center)         │  │                     │   │   │
│   │  │ • Game  │  │                       │  │ • Resource Cards    │   │   │
│   │  │   Name  │  │   ┌─────────────┐     │  │ • Player Tiles      │   │   │
│   │  │ • Btns  │  │   │    Hex      │     │  │   ┌─────────────┐   │   │   │
│   │  │ • Roll  │  │   │   Board     │     │  │   │   Player    │   │   │   │
│   │  │   Grid  │  │   │             │     │  │   │    Card     │   │   │   │
│   │  │ • Buy   │  │   └─────────────┘     │  │   └─────────────┘   │   │   │
│   │  │   Btns  │  │                       │  │   ┌─────────────┐   │   │   │
│   │  └─────────┘  └───────────────────────┘  │   │   Player    │   │   │   │
│   │   480px          ~910px                  │   │    Card     │   │   │   │
│   │   (25fr)         (60fr)                  │   └─────────────┘   │   │   │
│   │                                          │       530px         │   │   │
│   │                                          │       (26fr)        │   │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
```

**Portrait Mode (1080x1920 base) - Tabbed Interface:**

```text
┌─────────────────────────────────────────┐
│          Game Viewport                  │
│ ┌─────────────────────────────────────┐ │
│ │        Game Container               │ │
│ │ ┌─────┬─────────┬─────────────────┐ │ │
│ │ │Board│Controls │    Players      │ │ │  ← Tab Bar
│ │ └─────┴─────────┴─────────────────┘ │ │     (60px)
│ │ ┌─────────────────────────────────┐ │ │
│ │ │                                 │ │ │
│ │ │          Active Tab             │ │ │
│ │ │          Content                │ │ │
│ │ │                                 │ │ │
│ │ │  Board Tab:                     │ │ │
│ │ │  ┌─────────────────────────┐    │ │ │
│ │ │  │        Hex Board        │    │ │ │
│ │ │  │      (Full Width)       │    │ │ │
│ │ │  └─────────────────────────┘    │ │ │
│ │ │                                 │ │ │
│ │ │  Controls Tab:                  │ │ │
│ │ │  • Game Name                    │ │ │
│ │ │  • Action Buttons               │ │ │
│ │ │  • Roll Grid                    │ │ │
│ │ │  • Purchase Buttons             │ │ │
│ │ │                                 │ │ │
│ │ │  Players Tab:                   │ │ │
│ │ │  • Resource Tracking            │ │ │
│ │ │  • Player Cards Stack           │ │ │
│ │ │                                 │ │ │
│ │ └─────────────────────────────────┘ │ │
│ │           1080×1860                 │ │
│ └─────────────────────────────────────┘ │
└─────────────────────────────────────────┘
```

### CSS Scaling System

```css
:root {
  /* Single base resolution per orientation - no complexity */
  --landscape-width: 1920px;
  --landscape-height: 1080px;
  --portrait-width: 1080px;
  --portrait-height: 1920px;
  
  /* Layout proportions - match XAML Grid.ColumnDefinitions */
  --left-panel-width: 25fr;   /* 25* in XAML */
  --center-panel-width: 60fr; /* 60* in XAML */
  --right-panel-width: 26fr;  /* 26* in XAML */
}

.game-viewport {
  width: 100vw;
  height: 100vh;
  display: flex;
  align-items: flex-start;
  justify-content: flex-start;
  background: #000;
  overflow: hidden;
}

.game-container {
  /* Fixed internal coordinate system - set by JavaScript based on orientation */
  width: var(--base-width);
  height: var(--base-height);
  
  /* Scale to fit viewport - this is the "Viewbox Stretch=Uniform" equivalent */
  transform-origin: top left;
  transform: scale(var(--viewport-scale));
}

.game-grid {
  display: grid;
  width: 100%;
  height: 100%;
  gap: 10px;
}

/* Landscape Mode - Default (matches XAML layout) */
.game-container[data-layout-mode="landscape"] .game-grid {
  grid-template-columns: var(--left-panel-width) var(--center-panel-width) var(--right-panel-width);
  grid-template-rows: 1fr;
}

/* Landscape Mode - Tab bar hidden */
.game-container[data-layout-mode="landscape"] .portrait-tabs {
  display: none;
}

/* Portrait Mode - Tabbed interface (Board, Controls, Players tabs) */
.game-container[data-layout-mode="portrait"] .portrait-tabs {
  display: flex;
  background: #1a1a2e;
  border-bottom: 2px solid #333;
  height: 60px;
  flex-shrink: 0;
}

.game-container[data-layout-mode="portrait"] .portrait-tab {
  flex: 1;
  padding: 12px 8px;
  text-align: center;
  font-size: 16px;
  font-weight: 600;
  color: #888;
  background: transparent;
  border: none;
  cursor: pointer;
  transition: all 0.2s ease;
  border-bottom: 3px solid transparent;
}

.game-container[data-layout-mode="portrait"] .portrait-tab.active {
  color: #fff;
  border-bottom-color: #4a9eff;
  background: rgba(74, 158, 255, 0.1);
}

/* Portrait Mode - Game grid becomes a stacking container */
.game-container[data-layout-mode="portrait"] .game-grid {
  display: flex;
  flex-direction: column;
  height: calc(100% - 60px);  /* 1920 - 60 = 1860px in reference coords */
}

/* Portrait Mode - Panel Visibility: all panels hidden by default */
.game-container[data-layout-mode="portrait"] .left-panel,
.game-container[data-layout-mode="portrait"] .center-panel,
.game-container[data-layout-mode="portrait"] .right-panel {
  display: none;
  width: 100%;
  height: 100%;
}

/* Portrait Mode - Active panel fills the tab content area */
.game-container[data-layout-mode="portrait"] .portrait-active {
  display: flex !important;
  flex: 1;
  align-items: flex-start;
  justify-content: center;
}

.panel-viewbox {
  /* Each panel acts like a XAML Viewbox - content inside maintains proportions */
  overflow: hidden;
  display: flex;
  align-items: center;
  justify-content: center;
  min-width: 0;
  min-height: 0;
}

/* Fixed overlays that don't scale */
.hamburger-btn {
  position: fixed;
  top: 10px;
  left: 10px;
  z-index: 9999;
  /* Not affected by transform: scale() */
}
```

### JavaScript Viewport Scaler

```javascript
/**
 * ViewportScaler - Handles uniform scaling of fixed-dimension game container
 * Equivalent to XAML Viewbox Stretch="Uniform" behavior
 */
class ViewportScaler {
  constructor() {
    this.container = document.querySelector('.game-container');
    this.viewport = document.querySelector('.game-viewport');
    
    // Debounce resize events
    this.updateScale = this.debounce(this.updateScale.bind(this), 150);
    
    window.addEventListener('resize', this.updateScale);
    window.addEventListener('orientationchange', this.updateScale);
    
    this.updateScale(); // Initial scaling
  }
  
  updateScale() {
    if (!this.container || !this.viewport) return;
    
    const viewportWidth = this.viewport.offsetWidth;
    const viewportHeight = this.viewport.offsetHeight;
    const viewportAspect = viewportWidth / viewportHeight;
    
    // Portrait detection: aspect ratio < 4:3 (1.333)
    // - 16:9 (1.78) = landscape
    // - 4:3 (1.33) = landscape (boundary case - treat as landscape)
    // - 3:4 (0.75) = portrait
    // Using < (not <=) so exactly 4:3 is landscape
    const isPortrait = viewportAspect < (4/3);
    const ref = isPortrait
      ? { width: 1080, height: 1920 }
      : { width: 1920, height: 1080 };
    
    // Calculate scale factor - cap at 1.0 to prevent UI oversizing
    const scaleX = viewportWidth / ref.width;
    const scaleY = viewportHeight / ref.height;
    const scale = Math.min(scaleX, scaleY, 1.0);
    
    // Apply scale and update container dimensions
    this.container.style.setProperty('--viewport-scale', scale);
    this.container.style.setProperty('--base-width', `${ref.width}px`);
    this.container.style.setProperty('--base-height', `${ref.height}px`);
    
    // Set layout mode
    this.container.dataset.layoutMode = isPortrait ? 'portrait' : 'landscape';
    
    this.debugLog(viewportWidth, viewportHeight, ref.width, ref.height, scale);
  }
  
  debugLog(viewportWidth, viewportHeight, targetWidth, targetHeight, scale) {
    console.log(`Viewport: ${viewportWidth}x${viewportHeight}, ` + 
                `Target: ${targetWidth}x${targetHeight}, ` + 
                `Scale: ${scale.toFixed(3)} (capped at 1.0), ` +
                `Layout: ${this.container.dataset.layoutMode}`);
  }
  
  debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
      const later = () => {
        clearTimeout(timeout);
        func(...args);
      };
      clearTimeout(timeout);
      timeout = setTimeout(later, wait);
    };
  }
}

// Initialize scaling system
document.addEventListener('DOMContentLoaded', () => {
  window.viewportScaler = new ViewportScaler();
});
```

## Migration Strategy

### Phase 1: Foundation Infrastructure

**Objectives:**

- Establish the fixed-dimension container system
- Implement viewport scaling logic
- Test basic scaling on 1080p and 4K displays

**Implementation Steps:**

1. Create new `ViewportScaler` JavaScript class
2. Add CSS container and grid system
3. Test viewport scaling without content
4. Validate scaling on 4K displays

**Success Criteria:**

- Container scales smoothly on resize
- Scale cap at 1.0x prevents UI oversizing
- No layout jank during transitions

### Phase 2: Layout Migration

**Objectives:**

- Convert Game.razor to use fixed positioning
- Remove existing responsive CSS and JavaScript
- Implement landscape/portrait tabbed interface

**Implementation Steps:**

1. Replace `Game.razor.css` grid system with fixed positioning
2. Remove `panelsScaler.js` and all media queries
3. Implement tabbed interface for portrait mode
4. Test layout mode switching

**Success Criteria:**

- Game page renders correctly in both orientations
- Portrait mode shows tabbed interface (Board, Controls, Players)
- No JavaScript scaling conflicts

### Phase 3: Component Conversion

**Objectives:**

- Update all components to use fixed dimensions
- Remove percentage-based sizing
- Eliminate CSS media queries

**Component Priority:**

1. **High Priority**: PlayerTile, PurchaseButton, Board components
2. **Medium Priority**: Resource cards, navigation elements
3. **Low Priority**: Settings pages, modals

**Implementation per Component:**

1. Replace percentage widths/heights with fixed pixel values
2. Remove component-specific media queries
3. Update scoped CSS to use absolute positioning
4. Test scaling behavior

### Phase 4: Mobile & Polish

**Objectives:**

- Test on actual mobile devices
- Add touch-friendly interactions
- Optimize performance

**Implementation Steps:**

1. Test on actual mobile devices
2. Fine-tune portrait mode proportions
3. Add touch gesture support
4. Performance optimization

## Technical Details

### Coordinate System

**Landscape Mode (1920x1080 base):**

- Left panel: 0-480px (25% of 1920)
- Center panel: 490-1400px (60% with gaps)
- Right panel: 1410-1920px (26% remaining)

**Portrait Mode (1080x1920 base) - Tabbed Interface:**

- Tab bar: 0-60px (fixed height)
- Active tab content: 60-1920px (1860px available height)
- Each tab fills full width: 0-1080px

### Tab State Management

**Blazor Implementation:**

```csharp
private string _portraitTab = "board"; // Default tab

private void SetPortraitTab(string tab)
{
    _portraitTab = tab;
    
    // Persist tab selection
    JSRuntime.InvokeVoidAsync("sessionStorage.setItem", "portraitTab", tab);
    
    // Update CSS classes for panel visibility
    StateHasChanged();
}

private string GetPanelClass(string panelName)
{
    bool isPortrait = /* viewport aspect < 4/3 check */;
    if (!isPortrait) return ""; // Landscape mode - all panels visible
    
    return panelName == _portraitTab ? "portrait-active" : "";
}
```

**JavaScript Tab Switching:**

```javascript
// Initialize tab state from session storage - Blazor owns tab switching after this
document.addEventListener('DOMContentLoaded', () => {
  const savedTab = sessionStorage.getItem('portraitTab') || 'board';
  setActiveTab(savedTab);
});

function setActiveTab(tabName) {
  // Tab-to-panel mapping (explicit mapping required)
  const tabToPanelMap = {
    'board': 'center-panel',
    'controls': 'left-panel', 
    'players': 'right-panel'
  };
  
  // Update tab button states
  document.querySelectorAll('.portrait-tab').forEach(tab => {
    tab.classList.toggle('active', tab.dataset.tab === tabName);
  });
  
  // Update panel visibility using correct mapping
  document.querySelectorAll('.left-panel, .center-panel, .right-panel').forEach(panel => {
    panel.classList.remove('portrait-active');
  });
  
  const targetPanelClass = tabToPanelMap[tabName];
  if (targetPanelClass) {
    const activePanel = document.querySelector(`.${targetPanelClass}`);
    if (activePanel) {
      activePanel.classList.add('portrait-active');
    }
  }
  
  // Persist selection
  sessionStorage.setItem('portraitTab', tabName);
}

// Note: Blazor handles tab clicks after initialization - no click handlers needed
```

### JavaScript Migration - What Changes

**Replaces `panelsScaler.js`:**

- Remove: Complex panel-by-panel transform scaling
- Remove: Media query-based scaling logic
- Keep: None - entire file replaced by ViewportScaler

**Relationship to `boardSizer.js`:**

- Keep: `getBounds()` function for coordinate conversion
- Keep: Click-to-board-coordinate mapping
- Remove: Any scaling-related functions
- Purpose: ViewportScaler handles scaling, boardSizer handles coordinate conversion

**Updated `boardSizer.js` integration:**

```javascript
// boardSizer.js - coordinate conversion only
class BoardSizer {
  getBounds() {
    const board = document.querySelector('.board-content');
    const container = document.querySelector('.game-container');
    const scale = parseFloat(getComputedStyle(container).getPropertyValue('--viewport-scale')) || 1;
    
    const boardRect = board.getBoundingClientRect();
    const containerRect = container.getBoundingClientRect();
    
    // Return bounds in reference coordinate space (1920x1080 or 1080x1920)
    return {
      left: (boardRect.left - containerRect.left) / scale,
      top: (boardRect.top - containerRect.top) / scale,
      width: boardRect.width / scale,
      height: boardRect.height / scale
    };
  }
}
```

### Fixed Overlay Management

**Hamburger Menu and Side Navigation:**

```css
/* Fixed overlays that don't scale */
.hamburger-btn,
.side-nav-overlay {
  position: fixed;
  z-index: 9999;
  /* Not affected by transform: scale() */
}

.side-nav-overlay {
  top: 0;
  left: 0;
  width: 100vw;
  height: 100vh;
  background: rgba(0, 0, 0, 0.5);
  display: none; /* Hidden by default */
}

.side-nav-overlay.open {
  display: flex;
}

.side-nav-panel {
  width: 300px;
  height: 100%;
  background: #1a1a2e;
  transform: translateX(-100%);
  transition: transform 0.3s ease;
}

.side-nav-overlay.open .side-nav-panel {
  transform: translateX(0);
}
```

**Side Navigation Handling:**

```javascript
// Side navigation management (outside scaled container)
class SideNavigation {
  constructor() {
    this.overlay = document.querySelector('.side-nav-overlay');
    this.hamburger = document.querySelector('.hamburger-btn');
    
    this.hamburger.addEventListener('click', () => this.toggle());
    this.overlay.addEventListener('click', (e) => {
      if (e.target === this.overlay) this.close();
    });
  }
  
  toggle() {
    this.overlay.classList.toggle('open');
  }
  
  close() {
    this.overlay.classList.remove('open');
  }
}
```

### Performance Considerations

1. **GPU Acceleration**: Use `transform: scale()` for hardware acceleration
2. **Paint Optimization**: Fixed positioning reduces reflow calculations
3. **Memory Efficiency**: Single coordinate system reduces complexity
4. **Smooth Rendering**: Browser handles subpixel rendering automatically

### Browser Compatibility

- **Modern Browsers**: Full CSS Grid and transform support
- **Safari**: Requires `-webkit-` prefixes for some properties
- **Mobile Browsers**: Hardware acceleration varies by device
- **Fallback**: Graceful degradation to flexbox on older browsers

## Testing Strategy

### Development Testing

1. **4K Display Validation**
   - Test 2x scaling sharpness
   - Validate pixel-perfect rendering
   - Check performance on high-DPI displays

2. **Mobile Device Testing**
   - iPhone Pro Max, standard iPhone
   - Android flagship devices
   - Tablet orientations

3. **Regression Testing**
   - Ensure all existing functionality works
   - Validate game interactions remain smooth
   - Check component animations

### Performance Benchmarks

**Target Metrics:**

- Resize response time: < 100ms
- Layout shift during scaling: 0
- Memory usage: No increase from current implementation
- Frame rate: Maintain 60fps during interactions

## Implementation Phases

The migration follows concrete implementation steps without time estimates, as per project guidelines:

### Phase 1: Foundation

- Implement ViewportScaler class
- Create CSS container system
- Basic scaling validation

### Phase 2: Core Layout

- Convert Game.razor structure
- Remove existing scaling systems
- Layout mode switching

### Phase 3: Component Migration

- High-priority components (PlayerTile, Board, PurchaseButton)
- Remove percentage-based sizing
- Fixed coordinate positioning

### Phase 4: Testing & Finalization

- Mobile device testing and optimization
- Performance tuning
- Documentation and cleanup

## Benefits of New Architecture

### Robustness

- **Predictable**: Fixed coordinate system like XAML
- **Maintainable**: Single scaling algorithm
- **Debuggable**: Clear separation of concerns

### Performance  

- **GPU Accelerated**: Hardware-accelerated transforms
- **Efficient**: Minimal reflows and repaints
- **Responsive**: Smooth scaling transitions

### Cross-Platform

- **4K Ready**: Optimal scaling with black space padding (scale capped at 1.0)
- **Mobile Optimized**: Dedicated mobile resolutions and tabbed interface
- **Future-Proof**: Easy to add new target resolutions

### Development Experience

- **XAML Familiarity**: Same patterns as Desktop app
- **Simple Logic**: No complex media query management
- **Consistent**: Uniform behavior across all devices

## Conclusion

This architecture eliminates the fragility of the current mixed-scaling approach by adopting the proven XAML Viewbox pattern. The fixed-dimension container with uniform scaling provides robust, predictable behavior across all target platforms while maintaining the sophisticated game UI functionality.

The migration can be completed incrementally, with each phase building on the previous foundation, minimizing risk and ensuring continuous functionality throughout the transition.
