# Manual Test вЂ” Phase 5: Settings UI

> Run after building. Launch app (or use `test-run.ps1`).
> F9 = open Settings, F10 = quit.

---

## TASK-501 вЂ” Settings Window Scaffold

- [ ] **Open via F9 hotkey** вЂ” Settings window appears, centred on screen.
- [ ] **Open via tray double-click** вЂ” same result.
- [ ] **Does not appear in taskbar** while open.
- [ ] **Sidebar shows** three overlays (Relative, Session Info, Delta Bar), each with an enable/disable checkbox.
- [ ] **Sidebar shows** "Global Settings" item below a separator.
- [ ] **Close button** hides the window; reopening it shows the same selected item and values.
- [ ] **Г— button** (title bar) also hides (not destroys) the window.
- [ ] **Apply button** has keyboard focus by default (Enter key triggers it).

---

## TASK-502 вЂ” Per-Overlay Settings Panel

### Screen tab

- [ ] Select "Relative" вЂ” Screen tab is visible with Position & Size, Appearance, and **Relative Options** sections. Session Info and Delta Bar sections are **not shown**.
- [ ] Select "Session Info" вЂ” **Session Info Options** shown, Relative and Delta Bar sections hidden.
- [ ] Select "Delta Bar" вЂ” **Delta Bar Options** shown, others hidden.
- [ ] Edit **X / Y / Width / Height** fields, tab away вЂ” overlay moves/resizes immediately (preview), no disk save yet.
- [ ] Edit **Font Size**, blur вЂ” overlay font updates immediately.
- [ ] Move **Opacity slider** вЂ” overlay opacity updates live.
- [ ] Edit a **Background color** channel (e.g. A from 217 to 100), blur вЂ” overlay background alpha updates.
- [ ] Color **swatch** updates when R/G/B/A values change.
- [ ] Toggle **"Show iRating column"** checkbox вЂ” overlay updates (no save).
- [ ] Click **Apply** вЂ” changes are now persisted. Restart app; changes survive.
- [ ] Switch between overlay tabs without clicking Apply вЂ” pending edits are **not lost** (VM per overlay is preserved in memory).

### Stream Override tab

- [ ] **"Enable stream override"** checkbox is unchecked by default.
- [ ] All fields are **dimmed** (0.4 opacity) and Custom checkboxes are unchecked.
- [ ] Check **"Enable stream override"** вЂ” nothing else changes visually until Custom is checked.
- [ ] Check **Custom** on "Width" вЂ” field becomes enabled, current base value shown.
- [ ] Set Width to a different value, Apply вЂ” with stream mode OFF the overlay uses base width.
- [ ] Enable **Stream mode** (tray or Global Settings) вЂ” overlay switches to stream width immediately.
- [ ] Uncheck **Custom** on an override field, Apply вЂ” that field reverts to base value in stream mode.

---

## TASK-503 вЂ” Global Settings Panel

- [ ] Click "Global Settings" in sidebar вЂ” Global Settings panel appears.
- [ ] **Edit mode** checkbox is **unchecked** by default.
- [ ] Check **Edit mode** вЂ” blue borders appear on all overlays immediately, no Apply needed.
- [ ] Uncheck **Edit mode** вЂ” borders disappear immediately.
- [ ] Check **Stream mode** вЂ” overlays with stream overrides switch immediately.
- [ ] Uncheck **Stream mode** вЂ” reverts immediately.
- [ ] Check **Start with Windows**, Apply вЂ” verify `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` в†’ `NrgOverlay` entry exists in Registry Editor.
- [ ] Uncheck **Start with Windows**, Apply вЂ” entry removed.
- [ ] Restart app with StreamModeActive saved вЂ” stream mode state is **restored** from config.

---

## TASK-504 вЂ” Tray Icon

- [ ] Tray icon is visible in system tray after launch.
- [ ] Right-click shows menu: **SettingsвЂ¦**, separator, **Edit mode**, **Stream mode**, separator, **Exit**.
- [ ] **SettingsвЂ¦** opens the Settings window.
- [ ] **Edit mode** checkbox reflects current state when menu opens.
- [ ] Click **Edit mode** вЂ” toggles mode, overlay borders appear/disappear.
- [ ] Click **Stream mode** вЂ” toggles mode, stream overrides apply/revert.
- [ ] Open Settings в†’ enable Edit mode there в†’ reopen tray menu вЂ” **Edit mode is checked** in tray (state synced).
- [ ] **Exit** вЂ” app closes cleanly, tray icon disappears.

---

## Known Gaps (not blocking, tracked as future work)

- **No input validation** on numeric fields вЂ” typing "abc" or "9999" in Width silently ignores or clamps without visual error. TextBox turns red (WPF validation default) but no message shown.
- **Float locale sensitivity** вЂ” on non-English systems, `0.9` must be entered as `0.9` (invariant); `0,9` (locale decimal) will fail silently. Future work: add `InvariantCultureConverter`.
- **Tray icon is a programmatic placeholder** (blue circle, "S") вЂ” replace with a proper `.ico` asset in a later polish pass.
- **Sim priority ordering** (sortable ListBox) omitted from TASK-503 scope вЂ” deferred to Phase 6.

