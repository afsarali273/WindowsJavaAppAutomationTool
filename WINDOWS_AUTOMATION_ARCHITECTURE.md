# Windows Automation Architecture

This repository now contains a separate Windows automation core in `WinInspector.Core`.

## Why It Is Separate

The Java inspector depends on Java Access Bridge semantics and should stay isolated from Windows-native automation concerns.

The Windows automation layer is being built for future:

- desktop inspection
- popup and dialog handling
- POS application support
- automation server execution
- mixed fallback strategies

## Current Backend Strategy

The router in `WinInspector.Core` uses this order:

1. `UIA`
2. `FlaUI`
3. `Win32`

## What Exists Today

- top-level desktop window discovery
- simple technology classification
- UIA backend for semantic Windows control trees
- Win32 backend for raw child-window fallback
- FlaUI adapter placeholder for later package integration
- routing logic that tries the next backend when one is unavailable or fails

## Recommended Long-Term Direction

- Keep Java and Windows providers isolated at the code level.
- Unify them later only at the orchestration or API layer.
- Let the future server choose the provider per window or subtree.
- Preserve provider metadata in locators so automations know whether they came from `jab`, `uia`, `flaui`, or `win32`.

## Next Good Steps

- add a separate Windows inspector UI shell
- integrate FlaUI when you want richer Windows desktop patterns
- add popup tracking and wait-for-window primitives
- add locator generation for UIA and Win32 elements
