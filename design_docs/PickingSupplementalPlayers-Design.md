# PickSupplementalPlayers State Transition Design

## Overview

This document analyzes the flow when transitioning into and out of the `PickSupplementalPlayers` state, focusing on a bug where player pictures randomly don't render when returning to the normal template.

## State Machine Flow

### Entry into PickSupplementalPlayers

**Trigger**: `WaitingForNext` → Next button pressed when `HasSupplementalBuildPhase` is true

**File**: `Catan3.Shared/GameLogic/GameStateMachine.cs:1108-1121`

```csharp
case Shared.Models.GameState.WaitingForNext:
    if (gameModel.HasSupplementalBuildPhase)
    {
        gameModel.GameState = Shared.Models.GameState.PickSupplementalPlayers;
        gameModel.Players.ForEach(p => p.ParticipatingInSupplemental = false);
        gameModel.NextPlayerToRollAfterSupplemental = gameModel.NextPlayerId(gameModel.CurrentPlayerId, 1);
    }
```

### Exit from PickSupplementalPlayers

**Trigger**: Next button pressed in `PickSupplementalPlayers` state

**File**: `Catan3.Shared/GameLogic/GameStateMachine.cs:1128-1158`

Transitions to either:
- `Supplemental` state (if players opted in)
- `WaitingForRoll` state (if no players opted in)

## MVVM Message Flow

### GameModel Update Sequence

1. **GameStateMachine** processes state change → returns new GameModel
2. **GameMessageService** sends `UpdateGameModel` message
3. **MainPageViewModel** receives message → updates `GameViewModel.GameModel`
4. **GameViewModel.OnGameModelChanged** calls `MergePlayers()`
5. **MergePlayers** updates existing PlayerViewModels (doesn't recreate them)

**File**: `DesktopApp/Game/GameView/GameViewModel.cs:304-321`

```csharp
private void MergePlayers(GameModel gameModel)
{
    for (int i = 0; i < Players.Count; i++)
    {
        Players[i].Player = gameModel.Players[i]; // triggers PlayerViewModel.PlayerChanged
        Players[i].IsCurrentPlayer = (gameModel.Players[i].Id == gameModel.CurrentPlayerId);
        Players[i].ParticipatingInSupplemental = gameModel.Players[i].ParticipatingInSupplemental;
    }
}
```

**Key Insight**: PlayerViewModels are REUSED, not recreated. The same instances persist across state transitions.

### ParticipatingInSupplemental Message

When user checks/unchecks the supplemental checkbox:

**File**: `DesktopApp/Player/PlayerViewModel.cs:59-62`

```csharp
partial void OnParticipatingInSupplementalChanged(bool oldValue, bool newValue)
{
    WeakReferenceMessenger.Default.Send(new ParticipatingInSupplementalMessage(this.Id, newValue));
}
```

## UI Template Architecture

### Template Selection System

**File**: `DesktopApp/MainPage/MainPage.xaml:189-194`

```xml
<ListView x:Name="ListView_Players"
    ItemsSource="{x:Bind MainPageModel.GameViewModel.Players, Mode=OneWay}"
    ItemTemplate="{x:Bind StateToItemTemplate(MainPageModel.GameViewModel.GameModel.GameState), Mode=OneWay}">
```

**File**: `DesktopApp/MainPage/MainPage.xaml.cs:72-98`

```csharp
public DataTemplate? StateToItemTemplate(Shared.Models.GameState gameState)
{
    switch (gameState)
    {
        case Shared.Models.GameState.FinishedRollOrder:
            return "RollOrderTemplate";
        case Shared.Models.GameState.PickSupplementalPlayers:
            return "PickSupplementalPlayersTemplate";
        default:
            return "PlayerStatsTemplate";
    }
}
```

### Template Definitions

#### PlayerStatsTemplate (Normal Play)

**File**: `DesktopApp/MainPage/MainPage.xaml:26-30`

Uses a **UserControl**:
```xml
<DataTemplate x:Key="PlayerStatsTemplate" x:DataType="models:PlayerViewModel">
    <c:PlayerCtrl PlayerViewModel="{x:Bind Mode=OneWay}"
        GameState="{Binding ElementName=PAGE_MainPage, Path=MainPageModel.GameViewModel.GameModel.GameState, Mode=OneWay}" />
</DataTemplate>
```

**Image Binding in PlayerCtrl.xaml:66-72**:
```xml
<Grid CornerRadius="25" Width="50" Height="50">
    <Grid.Background>
        <ImageBrush ImageSource="{x:Bind PlayerViewModel.CroppedImageUri, Mode=OneWay,
            FallbackValue='ms-appx:///Assets/DefaultPlayers/guest.png'}" />
    </Grid.Background>
</Grid>
```

#### PickSupplementalPlayersTemplate

**File**: `DesktopApp/MainPage/MainPage.xaml:53-75`

Uses **inline XAML**:
```xml
<DataTemplate x:Key="PickSupplementalPlayersTemplate" x:DataType="models:PlayerViewModel">
    <StackPanel>
        <CheckBox Content="Doing Supplemental"
            IsChecked="{x:Bind ParticipatingInSupplemental, Mode=TwoWay}" />
        <Ellipse Width="75" Height="75">
            <Ellipse.Fill>
                <ImageBrush ImageSource="{x:Bind CroppedImageUri, Mode=OneWay}" />
            </Ellipse.Fill>
        </Ellipse>
        <TextBlock Text="{x:Bind Name, Mode=OneWay}" />
    </StackPanel>
</DataTemplate>
```

## Key Architectural Differences

| Aspect | PlayerStatsTemplate | PickSupplementalPlayersTemplate |
|--------|--------------------|---------------------------------|
| Structure | UserControl (PlayerCtrl) | Inline XAML |
| Image Container | Grid.Background | Ellipse.Fill |
| Binding Path | `PlayerViewModel.CroppedImageUri` | `CroppedImageUri` |
| FallbackValue | Yes | No |

## Bug Analysis

### Symptom
When transitioning FROM `PickSupplementalPlayers` BACK to normal play, player pictures randomly don't render. All other properties (name, colors, stats) render correctly.

### Root Cause: x:Bind with Null Initial DependencyProperty

**File**: `DesktopApp/Player/PlayerCtrl.xaml.cs:34`

```csharp
public static readonly DependencyProperty PlayerViewModelProperty =
    DependencyProperty.Register("PlayerViewModel", typeof(PlayerViewModel), typeof(PlayerCtrl),
        new PropertyMetadata(null));  // Default is null, no PropertyChangedCallback
```

**The Problem**:

1. When ListView switches templates, it creates new `PlayerCtrl` instances
2. During `InitializeComponent()`, x:Bind evaluates `PlayerViewModel.CroppedImageUri`
3. At this moment, `PlayerViewModel` is still `null` (default value)
4. The binding fails silently (null reference on nested path)
5. Then the binding system sets `PlayerViewModel` from the template
6. **x:Bind does NOT re-evaluate** because:
   - The root property (`PlayerViewModel`) changed from null to a value
   - But the nested property (`CroppedImageUri`) didn't "change" - it's the same string
   - x:Bind optimizes by not re-evaluating unchanged values

**Why "randomly"?**
- Depends on timing of control initialization vs binding evaluation
- ListView virtualization and recycling affects when controls are created
- Some controls might get their PlayerViewModel set before x:Bind evaluates

### Evidence Supporting This Theory

1. **Only images fail** - Other bindings work because they're direct properties, not nested paths through a null object
2. **Text and colors work** - These bind to properties that either:
   - Are direct on the DataTemplate context
   - Use `{Binding}` instead of `{x:Bind}`
3. **FallbackValue doesn't help** - It's for when the target value is null, not when the source path fails

## Proposed Fixes

### Fix Option 1: Add PropertyChangedCallback to Force Binding Update (Recommended)

**File**: `DesktopApp/Player/PlayerCtrl.xaml.cs`

```csharp
public static readonly DependencyProperty PlayerViewModelProperty =
    DependencyProperty.Register("PlayerViewModel", typeof(PlayerViewModel), typeof(PlayerCtrl),
        new PropertyMetadata(null, OnPlayerViewModelChanged));

private static void OnPlayerViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
{
    if (d is PlayerCtrl ctrl && e.NewValue != null)
    {
        // Force x:Bind to re-evaluate by triggering Bindings.Update()
        ctrl.Bindings.Update();
    }
}
```

**Pros**:
- Directly addresses the root cause
- Forces all x:Bind expressions to re-evaluate
- Minimal code change

**Cons**:
- Re-evaluates ALL bindings, not just the image

### Fix Option 2: Bind to CroppedBitmapImage Instead of CroppedImageUri

**File**: `DesktopApp/Player/PlayerCtrl.xaml:71`

Change from:
```xml
<ImageBrush ImageSource="{x:Bind PlayerViewModel.CroppedImageUri, Mode=OneWay, ...}" />
```

To:
```xml
<ImageBrush ImageSource="{x:Bind PlayerViewModel.CroppedBitmapImage, Mode=OneWay, ...}" />
```

**Pros**:
- `CroppedBitmapImage` is already created and ready
- BitmapImage handles its own loading

**Cons**:
- Still has the null initial value issue
- Inconsistent with other templates

### Fix Option 3: Initialize with Non-Null Default

**File**: `DesktopApp/Player/PlayerCtrl.xaml.cs`

```csharp
public static readonly DependencyProperty PlayerViewModelProperty =
    DependencyProperty.Register("PlayerViewModel", typeof(PlayerViewModel), typeof(PlayerCtrl),
        new PropertyMetadata(PlayerViewModel.Default));  // Use default instance
```

**Pros**:
- Prevents null reference during initial binding

**Cons**:
- Default instance might have incorrect data briefly
- Could cause flickering

### Fix Option 4: Use Traditional Binding Instead of x:Bind

**File**: `DesktopApp/Player/PlayerCtrl.xaml:71`

Change from:
```xml
<ImageBrush ImageSource="{x:Bind PlayerViewModel.CroppedImageUri, Mode=OneWay, ...}" />
```

To:
```xml
<ImageBrush ImageSource="{Binding PlayerViewModel.CroppedImageUri, Mode=OneWay, ...}" />
```

**Pros**:
- `{Binding}` handles null sources more gracefully
- Re-evaluates when any part of the path changes

**Cons**:
- Loses compile-time binding validation
- Slightly less performant

## Recommended Solution

**Fix Option 1** is recommended because it:
1. Addresses the root cause directly
2. Is a minimal, targeted change
3. Ensures all bindings work correctly after PlayerViewModel is set
4. Doesn't change binding syntax or template structure

## Files to Modify

1. `DesktopApp/Player/PlayerCtrl.xaml.cs` - Add PropertyChangedCallback

## Testing Plan

1. Start a game with supplemental build phase enabled
2. Progress to `WaitingForNext` state
3. Press Next to enter `PickSupplementalPlayers`
4. Verify all player pictures display in the checkbox template
5. Select some players for supplemental
6. Press Next to exit `PickSupplementalPlayers`
7. **Verify all player pictures display correctly in the normal template**
8. Repeat steps 2-7 multiple times to catch intermittent issues
