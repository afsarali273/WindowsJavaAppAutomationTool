# Java Access Bridge Inspector

A focused Windows desktop inspector and interactive automation workbench for Java Swing/AWT applications through Java Access Bridge.

The repository also now contains an isolated `WinInspector.Core` foundation for future Windows-native inspection and automation using UIA, Win32 fallback, and a planned FlaUI adapter. This is intentionally separate from the Java inspector codepath for now.

## Prerequisites

- Windows x64
- .NET 8 SDK (a newer SDK capable of targeting .NET 8 also works)
- A JDK/JRE containing Java Access Bridge
- A Swing/AWT target application running at the same or lower privilege level

Enable Java Access Bridge from the target Java installation:

```powershell
jabswitch /enable
```

If the Java application ships a private JRE, run that JRE's `jabswitch` or configure its Access Bridge separately.

## Run

```powershell
dotnet run --project JabInspector.App
```

## Distribution

To build a redistributable Windows package for other users:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-Inspector.ps1
```

Share the generated folder under `artifacts\JavaAccessBridgeInspector-win-x64`.
It is self-contained, so the target user does not need the .NET SDK installed.

See [DISTRIBUTION.md](/C:/Users/Afsar/POC/JavaAutomation/DISTRIBUTION.md) for end-user rollout guidance.

In the app, choose **Refresh windows**, select a detected Java window, and choose **Attach & inspect**. Selecting a hierarchy node reveals its role, name, states, geometry, native IDs, supported interfaces, and generated locator JSON.

Clicking a hierarchy row also brings the target Java window forward, requests focus on that accessible element, and briefly highlights its live on-screen bounds. Non-focusable semantic nodes use their nearest visible ancestor for the highlight.

### Hover inspection

After attaching, choose **Hover inspect** in the toolbar. While **Hover: ON** is shown, moving the pointer over the attached Java window:

- resolves the deepest accessible element under the pointer;
- keeps a native mixed-DPI-aware border around it;
- updates the Properties and locator panels with its live details;
- expands its ancestor path, selects the matching hierarchy row, and scrolls it into view;
- falls back to the nearest visible ancestor when a semantic element has no bounds.

Transient controls that were created after the original tree crawl are inserted as temporary live nodes beneath the nearest matching ancestor while hovered.

Choose **Hover: ON** again to stop hover inspection and remove the persistent border.

## Automation

The **Automation** tab acts on the currently selected hierarchy node:

- **Focus** requests focus through Java Access Bridge.
- **Click** prefers the control's semantic accessible action and falls back to a physical click.
- **Double-click** sends a native double-click using live mixed-DPI-aware bounds.
- **Set text** replaces editable text through Java Access Bridge.
- **Type keys** focuses the control and sends Unicode keyboard input at its current caret.
- **Get text** reads AccessibleText or AccessibleValue, then falls back to the accessible name/description.

Automation affects the live target immediately. Select elements carefully, especially before physical click or keyboard actions.

## Troubleshooting

- **DLL missing:** set `JAVA_HOME` and ensure `WindowsAccessBridge-64.dll` exists under its `bin` directory or is on `PATH`.
- **No Java windows:** run `jabswitch -enable`, restart the target app, and confirm it uses Swing/AWT.
- **Bitness mismatch:** both this inspector and the bridge DLL must be x64.
- **Elevated target:** run the inspector as administrator when the target application is elevated.
- **Corrupt native values:** compare `AccessibleContextInfo` with `AccessBridgePackages.h` from the exact installed JDK; vendor layouts can differ.
- **User setup uncertainty:** open the in-app **Settings** screen to review JAB requirements, paths, and enable/disable actions.

## Known limitations

- Works best with Swing/AWT; JavaFX and custom-rendered widgets may expose little data.
- Some controls do not provide useful accessible names.
- Crawls are capped at depth 25 and 500 children per node to protect UI responsiveness.
- Native Access Bridge calls require manual integration testing against the installed JDK.

## Tests

The dependency-free test harness covers locator paths, JSON shape, bounds validation, and diagnostics:

```powershell
dotnet run --project JabInspector.Tests
```

## Future Windows Automation

See [WINDOWS_AUTOMATION_ARCHITECTURE.md](/C:/Users/Afsar/POC/JavaAutomation/WINDOWS_AUTOMATION_ARCHITECTURE.md) for the separate Windows automation stack that is being prepared for native desktop and POS scenarios.
