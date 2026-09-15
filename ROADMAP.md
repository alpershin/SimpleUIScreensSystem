# Roadmap

Goal: a package for building uGUI interfaces by hand in scenes and prefabs, with reliable navigation, reusable behaviour components, and on-demand loading. Stages build on each other; later stages assume the earlier ones are done.

## Stage 1 — Release housekeeping

- [x] English README and repository description
- [x] `package.json` description matches the package
- [ ] Translate `Documentation~/addressable-screen-cache.md` to English
- [ ] Tags `v1.0.2` and `v2.0.0`
- [ ] Verify on the declared minimum: Unity 6000.0 with Addressables 2.7.6
- [ ] CI: EditMode and PlayMode tests on the minimum Unity version (needs a small test project in the repository or a generated one)

## Stage 2 — Screen lifecycle

- [x] Explicit `ScreenState`: Hidden, Opening, Open, Closing
- [x] `OnOpening` / `OnOpened` / `OnClosing` / `OnClosed`, raised once per transition, also for external (de)activation
- [x] Interrupting a transition reverses it from the current alpha and scale
- [x] Transitions run on the screen itself; the shared `Coroutines` runner is obsolete
- [x] `Hide()` for instant deactivation; startup hiding does not animate
- [x] Static state (`UINavigator`, `UIInitializer`) resets for Enter Play Mode without domain reload
- [ ] Transition profile asset (durations, curves, reduce-motion) instead of constants

## Stage 3 — Navigation

- [ ] One facade over scene screens and Addressable screens, keyed by `ScreenId`
- [ ] History for main screens: open, replace, back
- [ ] Modal stack; input blocked under the top modal; focus returns to the previous screen
- [ ] Layers: main, modal, notifications, HUD, with sorting rules
- [ ] Escape / Android Back handling: top modal first, then history
- [ ] Keyboard and gamepad focus: initial selection, focus restore, no navigation into covered screens

## Stage 4 — Builder components

- [ ] Button action: open, close, back
- [ ] Modal backdrop: dim, block, close on click
- [ ] Tab switcher
- [ ] State group: content / loading / empty / error
- [ ] Loading button
- [ ] Field with label, hint and validation error
- [ ] Tooltip and notification with a queue
- [ ] Safe Area container
- [ ] Optional theming: colors, TMP styles, sprites, interactive states

## Stage 5 — Performance and data

- [ ] Persist popularity statistics between sessions
- [ ] Virtualized list and grid with fixed-size cells; variable height later
- [ ] Measure real screen memory in a player build and compare with catalog estimates
- [ ] Diagnostics for redundant raycast targets and deep layout chains

## Stage 6 — Editor tools

- [ ] Create UI Root / Create Screen commands with Undo support
- [ ] Catalog validation: missing prefabs, duplicate IDs and code names
- [ ] Runtime debug window: open screens, cache contents, prefetch ranking
- [ ] Transition preview in the editor with value restore
