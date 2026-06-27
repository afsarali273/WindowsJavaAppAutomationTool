# Windows Java App Automation Tool

A Windows desktop inspector and automation workbench built primarily for Java Swing/AWT applications through Java Access Bridge, with an isolated Windows-native inspection foundation for future UI Automation, Win32, and POS-focused scenarios.

This project is designed to help with:

- inspecting Java application hierarchies;
- viewing accessible properties and bounds;
- highlighting live UI elements on screen;
- generating locators for automation;
- running common automation actions such as focus, click, type, set text, and get text;
- evolving toward a broader Windows application automation platform.

## Roadmap and TODOs

The active implementation backlog is tracked in [docs/ROADMAP.md](docs/ROADMAP.md).

Near-term focus is on making the Java inspector and recorder feel as stable as tools like Inspect.exe/Spy++:

- shared element resolution between tree view, hover, picker, recorder, and playback;
- more reliable highlighting across DPI and multi-monitor setups;
- drag-to-inspect element picking;
- smoother Recorder Studio UX with pause, timeline, playback, and rebind flows;
- richer locator metadata for modal-safe Java automation.

## Current Scope

Today, the application supports two inspection modes through a shared UI:

- Java mode for Java Access Bridge-based inspection and automation;
- Windows mode for early Windows-native inspection and action routing.

The Java and Windows backends are intentionally kept separate in the codebase so each automation stack can evolve independently without becoming tangled.

## Solution Structure

- `JabInspector.App`  
  WPF desktop application and shared inspector UI.

- `JabInspector.Core`  
  Java Access Bridge inspection, tree crawling, locators, diagnostics, and Java automation logic.

- `JabInspector.Native`  
  Native interop for Java Access Bridge and Windows APIs used by the Java inspector.

- `WinInspector.Core`  
  Isolated Windows-native inspection and automation foundation using UIA, Win32 fallback, and future FlaUI integration.

- `JabInspector.Tests`  
  Lightweight test harness for core logic and routing behavior.

- `java-client`  
  Maven-based Java client wrapper for the REST API, supporting both persistent session automation and session-independent window/object repository automation.

## Features

### Java inspection

- Detect running Java Swing/AWT windows
- Attach to a Java window and build the accessibility hierarchy
- View name, description, role, states, interfaces, bounds, context IDs, and child counts
- Generate locator JSON for the selected node
- Export the Java tree snapshot

### Highlighting and hover inspection

- Highlight selected elements on screen
- Use mixed-DPI-aware bounds handling for multi-monitor setups
- Hover over the target Java app to inspect the deepest accessible node under the cursor
- Auto-expand and auto-select the matching hierarchy node while hovering

### Automation

- Focus
- Click
- Double-click
- Set text
- Type text
- Get text

Java mode uses Java Access Bridge where possible and falls back to physical input when needed.  
Windows mode uses backend-specific action routing where available and physical input fallback for some controls.

### REST API Java client

The `java-client` Maven project provides a Java wrapper for external automation scripts:

- Selenium/WebDriver-style flow with a persistent `JavaDriver` session.
- UFT/TOSCA-style flow with `JavaAutomation.window(...).object(...).click()` one-shot actions.
- Window/modal routing by title, class name, process id, VM id, HWND, or repository window scope.

See [java-client/README.md](java-client/README.md) for usage examples.

### Settings and diagnostics

- Review Java Access Bridge requirements from inside the app
- Check common paths such as `jabswitch.exe`, `WindowsAccessBridge-64.dll`, and `JAVA_HOME`
- Enable or disable Java Access Bridge from the UI
- Open Windows accessibility settings
- View live diagnostic and action logs

## Requirements

### For development

- Windows x64
- .NET 8 SDK
- PowerShell
- Java 17 and Maven, only if building the optional Java REST client

### For Java inspection

- A Java runtime containing Java Access Bridge
- A target application built with Swing or AWT
- The target app running at the same privilege level as the inspector, or lower

### For Windows inspection

- Windows x64
- A target desktop application that exposes UIA or Win32-discoverable elements

## Installation and Setup

### 1. Clone the repository

```powershell
git clone git@github.com:afsarali273/WindowsJavaAppAutomationTool.git
cd WindowsJavaAppAutomationTool
```

### 2. Restore and build

```powershell
dotnet build .\JavaAccessBridgeInspector.sln
```

### 3. Enable Java Access Bridge

If you want to inspect Java applications, enable Java Access Bridge in the target Java installation:

```powershell
jabswitch /enable
```

If the application uses a private JRE, enable Java Access Bridge in that JRE as well.

### 4. Run the application

```powershell
dotnet run --project .\JabInspector.App
```

## How to Use

### Java mode

1. Launch the target Java application.
2. Open the inspector.
3. Keep the mode set to `Java`.
4. Select `Refresh windows`.
5. Select the target Java window.
6. Select `Attach and inspect`.
7. Use the hierarchy, properties, locator, logs, and automation tab as needed.

### Windows mode

1. Open the inspector.
2. Change the mode to `Windows`.
3. Select `Refresh desktop`.
4. Pick a window from the list.
5. Select `Inspect selection`.
6. Use the hierarchy and automation actions on the discovered nodes.

## Hover Inspection

After attaching to a Java window, select `Hover inspect`.

While hover inspection is enabled, the app will:

- detect the deepest accessible element under the mouse;
- highlight it on screen;
- update the properties and locator view;
- expand and select the corresponding node in the hierarchy.

This is especially useful when you are trying to understand complex nested Swing UIs quickly.

## Automation Actions

The Automation tab works on the currently selected node.

- `Focus` requests focus on the target control
- `Click` prefers semantic invoke/select actions and falls back when needed
- `Double-click` uses physical input
- `Set text` writes text directly when the control supports it
- `Type text` sends keyboard input to the current caret position
- `Get text` reads accessible text/value/name depending on what the control exposes

## Build a Redistributable Package

To generate a self-contained Windows package:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-Inspector.ps1
```

The published output is created under:

```text
artifacts\JavaAccessBridgeInspector-win-x64
```

Share the full output folder with end users, not only the executable.

For rollout guidance, see [DISTRIBUTION.md](C:/Users/Afsar/POC/JavaAutomation/DISTRIBUTION.md).

## Troubleshooting

### No Java windows detected

- Make sure the target app is actually Swing or AWT based
- Run `jabswitch /enable`
- Restart the target Java application after enabling JAB
- Verify that the correct Java runtime is being used

### Access Bridge initialization failed

- Confirm that `WindowsAccessBridge-64.dll` exists
- Ensure the inspector and Java Access Bridge are both x64
- Verify `JAVA_HOME` if the DLL is expected there

### Inspector cannot interact with the target app

- Run the inspector at the same privilege level as the target app
- If the target is elevated, run the inspector as administrator too

### Highlighting seems wrong on multiple monitors

- Re-attach to the target window so live bounds are refreshed
- Prefer selecting a visible child node rather than a purely semantic node with no bounds
- Use the nearest visible ancestor fallback when the selected node is not directly drawable

### Windows mode actions do not work on every control

- Some controls expose UIA patterns cleanly, others do not
- Win32-discovered controls may require focus plus physical input fallback
- Custom-rendered controls may expose limited metadata

## Known Limitations

- Java mode works best with Swing/AWT
- JavaFX or custom-rendered controls may expose partial or poor accessibility data
- Some combo boxes and custom widgets may not surface meaningful text
- Windows mode is an early integration foundation and will continue to expand
- FlaUI integration is planned but not fully implemented yet

## Running Tests

```powershell
dotnet run --project .\JabInspector.Tests
```

The current test harness covers:

- locator generation
- JSON serialization
- bounds validation
- diagnostics
- Windows classifier behavior
- Windows backend fallback routing

## Architecture Notes

The project is moving toward a broader automation platform for:

- Java desktop applications
- Windows desktop applications
- POS and mixed-technology applications

To support that direction, the Java and Windows backends are isolated while the UI remains shared.

See [WINDOWS_AUTOMATION_ARCHITECTURE.md](C:/Users/Afsar/POC/JavaAutomation/WINDOWS_AUTOMATION_ARCHITECTURE.md) for the Windows-side architecture notes.

## Recommended Next Steps

Likely next enhancements for the project include:

- stronger Windows action coverage by backend and pattern type;
- richer diagnostics for UIA, Win32, and Java runtime setup;
- more resilient locator strategies;
- popup, dialog, and multi-window automation flows;
- a future automation server layer on top of the inspector foundations.
