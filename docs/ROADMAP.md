# Java Inspector and Recorder Roadmap

This document tracks known TODOs, bugs, UX improvements, and future automation-platform enhancements.

The current product focus is:

1. Java Access Bridge inspector reliability.
2. Stable element highlighting and picking.
3. Smooth Java recorder and playback.
4. Clean, compact, Inspect.exe/Spy++-style desktop UX.

Windows/UIA/Win32/FlaUI support exists as an isolated foundation, but Java inspection and recording are the near-term priority.

## Priority legend

- `P0` - blocks core usage or causes incorrect automation.
- `P1` - important workflow issue; should be handled soon.
- `P2` - useful enhancement or cleanup.
- `P3` - longer-term platform idea.

## Current high-priority TODOs

### P0/P1 - Shared Java element resolution service

- [x] Create a shared `JavaElementInspectionService`.
- [x] Move physical/logical screen-point hit testing into `JavaElementInspectionService`.
- [x] Use the same screen-point resolver for:
  - hover inspection;
  - drag/pick element;
  - passive recorder click capture.
- [ ] Extend the same resolver contract to:
  - manual recorder capture;
  - hierarchy-driven highlight.
- [x] Return a structured result:
  - resolved node;
  - visible/drawable ancestor;
  - physical bounds;
  - whether physical or logical JAB hit testing was used;
  - whether ancestor fallback was used.
- [x] Remove duplicated physical/logical hit-test logic from hover, picker, and passive recorder paths.

**Completed:** Created `JavaElementInspectionService` and `JavaInspectionResult` in `JabInspector.Core`. Hover inspection, drag picker, and passive recorder capture now share the same physical/logical JAB screen-point resolution pipeline. The service intentionally delegates actual JAB hit testing to the existing ViewModel path so native handle lifetime stays centralized.

Why: inspector and recorder should never disagree about which element is under the cursor.

### P0/P1 - Highlight manager

- [x] Create an app-layer `HighlightManager` abstraction.
- [x] Support separate highlight modes:
  - transient hover highlight;
  - hierarchy selection flash;
  - persistent keep-highlight;
  - recorder action flash;
  - playback step highlight.
- [x] Always clear/reset previous highlight before showing another highlight.
- [x] Avoid multiple stale highlights.
- [x] Keep persistent highlight independent from mouse movement.
- [ ] Ensure all highlight modes use the same DPI/multi-monitor physical bounds logic.
- [x] Integrate `HighlightManager` into `MainWindow.xaml.cs` to replace direct `HighlightOverlay` calls.

**Completed:** Added an app-layer `HighlightManager` in `JabInspector.App` and routed MainWindow highlight ownership through it. The manager coordinates transient flashes, hover/picker persistent highlights, recorder action flashes, playback-style modes, and full cleanup while retaining the existing native physical-pixel `HighlightOverlay` renderer.

Why: current highlight behavior has improved, but highlight ownership is still spread across UI code.

**Decision:** Do not keep the attempted Core `HighlightManager`; highlight orchestration belongs in the WPF app layer because it owns overlays, Dispatcher timers, and visual behavior. `HighlightMode` can remain as a lightweight shared enum if useful.

### P1 - Drag-to-inspect picker

- [x] Add a crosshair/target icon to the compact toolbar.
- [x] Support click-and-drag picker mode:
  - [x] user drags icon over target app;
  - [x] element under cursor highlights live;
  - [x] release selects the element in the inspector;
  - [x] hierarchy expands to the selected element;
  - [x] properties and locator update.
- [ ] Add single-click "pick next element" mode.
- [x] Picker should not require target app to come to foreground.
- [x] Picker should use the shared `JavaElementInspectionService`.

**Completed**: Drag-to-inspect picker implemented, allowing users to drag the picker button over the target Java application to inspect elements in real-time. The picker highlights the element under the cursor, selects it in the accessibility hierarchy, and updates the properties and locator views upon release.

Why: this gives a stable Inspect.exe-style workflow and avoids unstable hover/cursor behavior.

### P1 - Tree selection behavior

- [x] Selecting/expanding tree nodes should not bring the target app to front.
- [x] Selecting tree nodes should not call JAB `requestFocus`.
- [ ] Tree expand should never trigger highlight unless explicitly configured.
- [ ] Add setting: "Highlight on tree selection".
- [ ] Add setting: "Auto-scroll hierarchy on hover/picker".
- [ ] Ensure tree expand/collapse is fast for large trees.

Why: inspecting should not disturb the target app unless the user explicitly performs an action.

## Inspector UX TODOs

### P1 - Compact Inspect.exe-style shell

- [x] Reduce default window size.
- [x] Startup size based on monitor work-area percentage.
- [x] Remove redundant top toolbar buttons.
- [x] Move diagnostics into Settings.
- [x] Hide embedded recording tab in favor of separate Recorder Studio.
- [x] Make left window/navigation pane wider than hierarchy pane.
- [ ] Improve icon set with real vector icons instead of Unicode glyphs.
- [ ] Add toolbar overflow behavior for very small monitors.
- [ ] Add status bar indicators:
  - Java mode / Windows mode;
  - attached window;
  - node count;
  - recording state;
  - log file shortcut.
- [ ] Update README screenshots/instructions after UX stabilizes.

### P2 - Layout and accessibility

- [ ] Add keyboard shortcuts:
  - `F5` refresh windows;
  - `Ctrl+C` copy locator;
  - `Ctrl+E` export;
  - `Ctrl+R` open recorder;
  - `Esc` clear hover/picker/highlight.
- [ ] Improve high contrast support.
- [ ] Add resizable/saved pane widths.
- [ ] Persist last selected mode and window sizing.
- [ ] Add "always on top" toggle for inspector.

## Recorder UX TODOs

### P0/P1 - Recorder reliability

- [ ] Recorder should use the same element resolution path as inspector hover/picker.
- [ ] Recording overlay should always attach to the active Java window/modal.
- [ ] Recording overlay should update when modal/window changes.
- [ ] Recording overlay should be click-through and non-focusable.
- [ ] Recording highlight should flash the exact recorded element or visible ancestor.
- [ ] Ensure passive recording does not capture clicks inside the inspector itself.
- [ ] Improve modal detection during recording and playback.
- [ ] Add detailed failure logging for missed passive clicks:
  - current window;
  - point;
  - candidate HWND chain;
  - JAB physical result;
  - JAB logical result;
  - final selected node;
  - bounds.

### P1 - Recorder Studio redesign

- [ ] Make Recorder Studio the only recorder workspace.
- [ ] Use timeline-first design:
  - recorded steps on left;
  - selected step details on right;
  - collapsible repository/log/playback sections.
- [ ] Show live recording status:
  - recording/paused/stopped;
  - elapsed time;
  - step count;
  - current target window/modal.
- [x] Add Pause/Resume.
- [ ] Add "Resume" button label/state instead of a combined Pause/Resume label.
- [ ] Add delete step.
- [ ] Add reorder step.
- [ ] Add edit step input text.
- [ ] Add edit object friendly name.
- [ ] Add "rebind object" using picker.
- [ ] Add "play selected step".
- [ ] Add "play from selected step".

### P1/P2 - Recorder data model

- [x] Store rich object metadata on recorded steps:
  - role;
  - name;
  - description;
  - virtual/JAWS name;
  - path;
  - object depth.
- [x] Store object repository locator JSON.
- [ ] Add raw `indexPath` alongside role path.
- [ ] Add window-relative bounds.
- [ ] Add window-relative click point.
- [ ] Add active modal/window context history.
- [ ] Add optional text preview fields:
  - sentence;
  - word;
  - selected text;
  - value.
- [ ] Add volatile/transient node marker to the visible UI.
- [ ] Add schema version to recording project JSON.

## Playback TODOs

### P1 - Playback robustness

- [x] Use repository-backed resolver.
- [x] Include virtual/JAWS name and object depth in resolver scoring.
- [ ] Add raw `indexPath` resolution.
- [ ] Add closest-candidate diagnostics when playback cannot find an element.
- [ ] Add automatic tree refresh and retry when step resolution fails.
- [ ] Add modal/window wait strategy per step.
- [ ] Add action-specific fallback strategy:
  - semantic JAB action;
  - focus + keyboard;
  - physical click;
  - coordinate fallback only as last resort.
- [ ] Add playback speed/delay controls.

### P2 - Playback UX

- [ ] Show playback timeline statuses:
  - pending;
  - running;
  - passed;
  - failed;
  - skipped.
- [ ] Highlight each step target during playback.
- [ ] On failure, show:
  - expected locator;
  - current window;
  - closest candidates;
  - rebind button.
- [ ] Save playback report.

## Locator and accessibility metadata TODOs

### P1 - Locator completeness

- [x] Include normal accessible name.
- [x] Include description.
- [x] Include virtual/JAWS name.
- [x] Include object depth.
- [x] Include role, role_en_US, states, states_en_US.
- [x] Include role-based path.
- [ ] Include raw child index path.
- [ ] Include parent path.
- [ ] Include accessible interfaces bitset as parsed flags.
- [ ] Include available actions.
- [ ] Include text/value preview lazily.
- [ ] Include table/list/tree-specific metadata.

### P2 - Locator scoring

- [x] Score path, role, name, virtual name, description, parent, object depth, bounds, actions.
- [ ] Add index-path resolver.
- [ ] Add string normalization for punctuation/whitespace variants.
- [ ] Add bounds tolerance based on DPI/window resize.
- [ ] Add stable ranking debug output.
- [ ] Add unit tests for resolver scoring.

## Java Access Bridge diagnostics TODOs

### P1 - Settings diagnostics

- [x] Show JAB requirements.
- [x] Detect `WindowsAccessBridge-64.dll`.
- [x] Detect `jabswitch.exe`.
- [x] Enable/disable JAB using `jabswitch`.
- [ ] Add `getVersionInfo` to show:
  - JVM version;
  - AccessBridge class version;
  - JavaAccessBridge DLL version;
  - WindowsAccessBridge DLL version.
- [ ] Add `getAccessibleContextWithFocus` diagnostic.
- [ ] Add `getHWNDFromAccessibleContext` diagnostic.
- [ ] Add manual HWND attach diagnostic.
- [ ] Add "copy diagnostics bundle" button.

### P2 - Runtime compatibility

- [ ] Check architecture mismatch clearly.
- [ ] Detect private/bundled JRE paths from running Java processes.
- [ ] Detect whether target app is elevated.
- [ ] Detect whether inspector is elevated.
- [ ] Add warning when privilege levels differ.

## Native and memory management TODOs

### P1 - JAB object lifetime

- [ ] Audit every JAB handle returned by:
  - `getAccessibleContextAt`;
  - `getAccessibleChildFromContext`;
  - `getAccessibleParentFromContext`;
  - selection/active descendant APIs.
- [ ] Ensure temporary handles are released.
- [ ] Keep retained tree handles deliberate and documented.
- [ ] Add debug counters for retained/released contexts where feasible.

### P2 - Native wrapper completeness

- [x] Add `getObjectDepth`.
- [ ] Add `getVersionInfo`.
- [ ] Add `getHWNDFromAccessibleContext`.
- [ ] Add `getAccessibleContextWithFocus`.
- [ ] Add `getAccessibleTextItems`.
- [ ] Add selected text APIs.
- [ ] Add table APIs only behind lazy/explicit calls.

## Packaging and distribution TODOs

### P1 - End-user distribution

- [ ] Update README to reflect latest compact auto-attach UX.
- [ ] Add real distribution guide if missing from repo.
- [ ] Add publish script verification.
- [ ] Create self-contained win-x64 package.
- [ ] Add first-run JAB requirements screen.
- [ ] Add app icon and version metadata.
- [ ] Add log folder shortcut in Settings.

### P2 - Installer/fleet rollout

- [ ] Consider MSIX or installer.
- [ ] Add update/version check strategy.
- [ ] Add portable mode.
- [ ] Add enterprise config file for defaults.

## Windows backend TODOs

Windows mode is intentionally isolated for now.

- [ ] Keep Win/UIA/Win32 backend separate from Java backend.
- [ ] Revisit shared UI once Java workflow stabilizes.
- [ ] Add FlaUI implementation when needed.
- [ ] Add Windows-specific recorder only after Java recorder is stable.

## Technical debt TODOs

### P1

- [ ] Move Java screen-point resolving out of `MainWindow.xaml.cs`.
- [ ] Move highlight bounds calculation out of `MainWindow.xaml.cs`.
- [ ] Move auto-attach-from-point out of `MainWindow.xaml.cs`.
- [ ] Reduce code-behind responsibility to UI orchestration only.
- [ ] Add tests for `LocatorGenerator`.
- [ ] Add tests for `JavaNodeResolverService`.

### P2

- [ ] Replace Unicode toolbar icons with vector/icon resources.
- [ ] Fix any remaining mojibake separator text in XAML.
- [ ] Review and clean stale README sections.
- [ ] Add nullable/serialization compatibility tests for recording JSON.
- [ ] Add logging categories/levels.

## Suggested next implementation order

1. Build `JavaElementInspectionService` (complete for hover, picker, and passive recorder capture)
2. Build app-layer `HighlightManager`
3. Add drag-to-inspect picker (complete)
4. Extend shared inspection service to manual recorder capture and hierarchy highlight
5. Fix overlay follow/current-modal behavior.
6. Redesign Recorder Studio timeline.
7. Add index-path locator.
8. Add playback failure diagnostics and rebind.
9. Update README and distribution docs.
