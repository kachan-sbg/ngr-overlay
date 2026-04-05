# Phase 5 — Configuration UI `[x]`

> [← Index](INDEX.md)

---

**TASK-501** `[x]`
- **Title**: Settings window scaffold (WPF)
- **Description**: Create WPF `SettingsWindow` in `SimOverlay.App`. Tab/sidebar with sections: "Overlays" (list with enable/disable checkboxes, properties panel on selection), "Global Settings". "Apply" and "Close" buttons. Created lazily; `ShowInTaskbar = false`.
- **Acceptance Criteria**: Window opens from tray icon. Contains correct sections. Closing and reopening shows previously set values. Does not appear in taskbar.
- **Dependencies**: TASK-301, TASK-102.

---

**TASK-502** `[x]`
- **Title**: Per-overlay settings panel — color pickers, numeric inputs, stream override
- **Description**: Two tabs per overlay: **Screen** (background/text/player-highlight colors, opacity, width/height, font size, overlay-specific toggles) and **Stream Override** ("Enable override" toggle, per-field "Custom" checkboxes — unchecked = inherit base as greyed placeholder). Bind to `OverlayConfigViewModel` + `StreamOverrideViewModel`. On Apply: push to live `OverlayConfig` and call `ConfigStore.Save()`. LostFocus on any field triggers preview (no save); Apply button saves.
- **Acceptance Criteria**: Screen tab edits base config. Stream Override tab controls override values. Unchecked fields show inherited value as placeholder. Applying immediately updates a live overlay. Min/max enforced. Switching tabs doesn't lose unsaved changes.
- **Dependencies**: TASK-501, TASK-303.

---

**TASK-503** `[x]`
- **Title**: Global settings panel — sim priority, startup behavior, stream mode
- **Description**: Controls: (1) "Start with Windows" — adds/removes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`. (2) "Edit Mode" toggle (mirrors tray, applies immediately via `OverlayManager.SetEditMode`). (3) "Stream Mode" toggle — activates/deactivates stream overrides, saves `globalSettings.streamModeActive`, broadcasts `StreamModeChangedEvent` via `OverlayManager.SetStreamMode`.
- **Acceptance Criteria**: "Start with Windows" correctly manages registry entry. Stream Mode toggle switches all overlays with enabled override to stream profile. Identical effect to tray menu. State persists across restarts.
- **Dependencies**: TASK-501, TASK-107.

---

**TASK-504** `[x]`
- **Title**: Tray icon and context menu
- **Description**: `TrayIconController` using `System.Windows.Forms.NotifyIcon` (`UseWindowsForms=true` in csproj). Menu items: "Settings…", separator, "Edit mode" (CheckOnClick toggle), "Stream mode" (CheckOnClick toggle), separator, "Exit". Double-click opens Settings. Checkbox states synced from live `OverlayManager` each time the menu opens. `_syncingMenu` flag prevents feedback loops during programmatic sync.
- **Acceptance Criteria**: Tray icon visible. All menu actions work. Labels reflect current state. Stream Mode toggle identical in effect to Settings window.
- **Dependencies**: TASK-501, TASK-107, TASK-304.
