# Simple UI Screens System

A lightweight screen manager for hand-built uGUI interfaces in Unity. Screens are regular prefabs or scene objects with a `UIScreen` component; the package handles opening, closing, transitions, and optional on-demand loading through Addressables with predictive prefetching.

*Note: parts of this documentation were written with the help of an AI assistant.*

## Features

- **Scene screens** — register screens placed in a scene and open them by a typed key.
- **Addressable screens** — load screen prefabs on demand, keep a bounded cache, and prefetch the screens the player is most likely to open next.
- **Typed keys** — `ScreenId` replaces enums; a `Screens` class is generated from the catalog, so adding a screen never requires editing the package.
- **Transitions** — fade and modal scale animations driven by unscaled time, so menus animate while the game is paused.
- **Inspector bindings** — hook buttons to open or close a screen without writing code.
- **Tests** — EditMode and PlayMode test suites cover keys, ranking, code generation, loading, and navigation.

## Requirements

| Dependency | Version |
|---|---|
| Unity | 6000.0 or newer |
| uGUI (`com.unity.ugui`) | 2.0.0 |
| Addressables (`com.unity.addressables`) | 2.7.6 |

## Installation

1. Open **Window → Package Manager**.
2. Click **+** and choose **Add package from git URL...**
3. Paste:
   ```
   https://github.com/alpershin/SimpleUIScreensSystem.git
   ```
4. Click **Add**. The **Demo** sample can be imported from the package page.

## Quick start: screens in a scene

1. Build a screen under a Canvas and add `UIScreen` to its root (a `CanvasGroup` is added automatically).
2. Fill in the **ID** field, assign close buttons, an optional modal container, and enable the animation if you want it.
3. Declare the key in your project and open the screen through the navigator:

```csharp
using SimpleUIScreensSystem;

public static class Screens
{
    public static readonly ScreenId Settings = new ScreenId("settings");
}

UINavigator.Instance.Open(Screens.Settings);
UINavigator.Instance.Close(Screens.Settings);
```

`UIInitializer` finds every `UIScreen` on startup, registers it under its ID, and hides it.

## Addressable screens with predictive loading

`AddressableUIRoot` loads screen prefabs on demand and prefetches likely next screens in the background. Ranking combines a designer priority, decaying open frequency, learned screen-to-screen transitions, and recent user actions reported through `ReportAction()`. User requests always get a reserved load slot; the cache is bounded by screen count and an estimated memory budget.

1. Mark the screen prefab as Addressable.
2. Create **Create → Simple UI → Addressable Screen Catalog** and add an entry per screen: a **Code Name** (for example `Settings`), the prefab reference, priority, memory estimate, and whether it may be prefetched. A stable ID is generated automatically and survives renames and prefab replacement.
3. Click **Generate Screen Keys...** in the catalog inspector and save the file inside your game's `Assets` folder.
4. Add `AddressableUIRoot` to an active object, assign the catalog and a parent container under your Canvas.

```csharp
using SimpleUIScreensSystem.AddressableUI;
using Game.UI; // namespace of the generated Screens class

[SerializeField] private AddressableUIRoot _navigator;

_navigator.ReportAction("inventory.tab_selected");
_navigator.Open(Screens.Inventory);
_navigator.Close(Screens.Inventory);
```

For buttons, add `AddressableScreenActions`, pick the screen from the dropdown, and bind its `Open` or `Close` to the button's `onClick`.

Setup details, the ranking formula, defaults, lifecycle contract, and limitations are described in [Documentation~/addressable-screen-cache.md](Documentation~/addressable-screen-cache.md) (currently in Russian).

## API overview

### `UIScreen`
Base component for every screen. Can be used as is or subclassed.

- `Open()` / `Close()` — start the opening or closing transition; calling the opposite mid-transition reverses it from the current values.
- `Hide()` — deactivates immediately without a transition.
- `State` — `Hidden`, `Opening`, `Open` or `Closing`; `IsOpen` is true in every state but `Hidden`.
- `OnOpening`, `OnOpened`, `OnClosing`, `OnClosed` — `UnityEvent`s raised at the start and end of each transition. Activating or deactivating the object from outside raises them too.
- `Init()` — binds the transition; runs automatically in `Awake` when nobody called it earlier.
- `Id` — the typed key from the **ID** field.

### `UINavigator`
Registry for scene screens, available as `UINavigator.Instance` or as a standalone instance.

- `Add(UIScreen)` / `Remove(UIScreen)`
- `Open(ScreenId, Action closeCallback = null)` — the callback fires once when that opening closes.
- `Close(ScreenId)`, `CloseAll()`
- `TryGetScreen(ScreenId, out UIScreen)`

### `AddressableUIRoot`
Scene-owned cache for Addressable screens.

- `Open(ScreenId)`, `Close(ScreenId)` — closing also cancels a pending open.
- `ReportAction(string)` — feeds the popularity model with a stable action name.
- `TryGetScreen(ScreenId, out UIScreen)`, `TrimCache()`
- `ScreenOpened`, `LoadFailed` — events for activation and load errors.

### `ScreenId`
Immutable value type with ordinal comparison. Keys are case-sensitive; `default(ScreenId)` is invalid and is rejected by both navigators.

## Migration from 1.x

- `EScreenType` was removed. Set the **ID** field on each scene `UIScreen` and declare `ScreenId` constants in your project.
- `UINavigator.Open(EScreenType.X)` → `Open(Screens.X)`; `GetScreen` → `TryGetScreen`.
- `OnOpened` now fires when the opening transition has finished, not when the object is activated; subscribe to `OnOpening` for the old timing.
- The shared `Coroutines` runner is obsolete; transitions run on the screen itself.
- The minimum Unity version is now 6000.0, and Addressables and uGUI are declared dependencies.

## Tests

Open **Window → General → Test Runner**. EditMode suites cover `ScreenId`, the popularity policy, and key generation. PlayMode suites cover `AddressableUIRoot` with an in-memory resource provider and `UINavigator`; they do not require Addressables settings or a content build.

## License

MIT. See [LICENSE](LICENSE).

## Support

Open an issue in this repository if you run into problems or have questions.
