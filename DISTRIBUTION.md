# Distribution Guide

This project can be published as a self-contained Windows x64 desktop package so end users do not need the .NET SDK installed.

## Build A Redistributable Package

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Publish-Inspector.ps1
```

That creates a ready-to-share folder under:

```text
artifacts\JavaAccessBridgeInspector-win-x64
```

## What To Share

Give the entire output folder to the user, not only the `.exe`.

The folder contains:

- `JavaAccessBridgeInspector.exe`
- the required .NET runtime files
- `README.md`
- `DISTRIBUTION.md`
- `Launch Inspector.cmd`

## End User Requirements

The user machine still needs:

- Windows x64
- a Java application that uses Swing or AWT
- a Java runtime that includes Java Access Bridge

The inspector now includes a `Settings` screen that can help the user:

- verify whether `WindowsAccessBridge-64.dll` is discoverable
- verify whether `jabswitch.exe` is available
- enable or disable Java Access Bridge from the app
- open Windows Accessibility settings

## Recommended End User Flow

1. Launch `JavaAccessBridgeInspector.exe` or `Launch Inspector.cmd`.
2. Open `Settings`.
3. Run `Refresh checks`.
4. If available, run `Enable JAB`.
5. Restart the target Java application.
6. Return to the main screen and choose `Refresh windows`.

## Notes

- If the target Java app is running as administrator, the inspector should also be started as administrator.
- If the target app ships its own private JRE, that JRE must expose Java Access Bridge correctly.
- The app is published as a folder-based self-contained package instead of a single-file executable to avoid native loading issues with Java Access Bridge and other desktop dependencies.
