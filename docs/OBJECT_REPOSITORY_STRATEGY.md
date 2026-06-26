# Java Object Repository and Locator Strategy

This document defines the direction for the Java object repository used by:

- Inspector locator JSON;
- Recorder object repository;
- Recorder playback;
- REST/API runner.

The main goal is reliability: never click a sibling or similar element just because it has some matching properties. If a locator is ambiguous, the framework should retry intelligently and then fail with diagnostics instead of guessing.

## What commercial tools generally do

### UFT-style repository

UFT stores named test objects with object descriptions in local or shared repositories. The key useful idea for us is separation between the test step and the object identity: a step says “click LoginButton”, while the repository owns how `LoginButton` is identified.

For our project, this maps to:

- `objectKey` as the stable test-facing name;
- rich locator snapshots as the technical identification;
- repository reuse across recorder playback and API calls;
- versioned repository files so locators can evolve without breaking old recordings.

### Tosca-style modules

Tosca’s model emphasizes module attributes and identification by selected technical properties. It can load many technical/reflected properties but only some should be used for identification. The key idea is not “store everything and match everything equally”; it is “store everything, classify it, and choose reliable properties deliberately.”

For our project, this maps to property tiers:

- stable identity properties;
- structural properties;
- contextual/window properties;
- volatile runtime properties;
- diagnostic-only metadata.

### Playwright/Selenium-style lessons

Playwright treats locators as live queries with auto-waiting/retry. Selenium’s Page Object Model separates test intent from locator mechanics. The useful ideas for us:

- locator resolution should be retried over time, not performed once immediately;
- actions should require a unique actionable element;
- object names should remain stable while locator internals evolve.

## Current JSON format

The current locator JSON is a good foundation because it already carries:

- engine;
- role and roleEnUs;
- name, virtualAccessibleName, description;
- states;
- indexInParent and objectDepth;
- childrenCount;
- role path, indexPath, xPath, indexXPath, semanticXPath;
- parent role/name;
- action names;
- text/value previews;
- bounds.

This is enough for a first robust repository. We should not replace JSON yet. Instead, evolve it into a versioned object repository format that wraps one or more locator strategies per object.

## Proposed repository schema

Recommended direction:

```json
{
  "schemaVersion": 2,
  "engine": "java-access-bridge",
  "application": {
    "name": "JOSM",
    "processName": "javaw",
    "mainWindowTitle": "Java OpenStreetMap Editor"
  },
  "objects": [
    {
      "objectKey": "downloadDialog.tabs.boundingBox",
      "friendlyName": "Bounding Box tab",
      "objectType": "page tab",
      "window": {
        "title": "Download",
        "className": "SunAwtDialog",
        "processId": 27516,
        "vmId": 657816
      },
      "locatorStrategies": [
        {
          "name": "strict-identity",
          "priority": 1,
          "mustMatch": [
            "roleEnUs",
            "name",
            "virtualAccessibleName",
            "parentRole",
            "parentName",
            "objectDepth",
            "indexInParent"
          ],
          "locator": {}
        },
        {
          "name": "structural-path",
          "priority": 2,
          "mustMatch": [
            "indexPath",
            "xPath",
            "roleEnUs",
            "name"
          ],
          "locator": {}
        },
        {
          "name": "semantic-fallback",
          "priority": 3,
          "mustMatch": [
            "roleEnUs",
            "name",
            "parentRole"
          ],
          "shouldMatch": [
            "states",
            "bounds",
            "childrenCount"
          ],
          "locator": {}
        }
      ],
      "diagnostics": {
        "capturedAtUtc": "2026-06-26T00:00:00Z",
        "captureSource": "recorder",
        "rawLocatorJson": {}
      }
    }
  ]
}
```

We can keep backward compatibility by loading today’s `JavaRecordingProject.Repository` entries and projecting them into this richer schema internally.

## Property tiers

### Tier 1: stable identity

These should be strongly weighted and often required:

- `roleEnUs` / `role`;
- `name`;
- `virtualAccessibleName`;
- `description` when meaningful;
- `parentRole`;
- `parentName`;
- `objectDepth`;
- `indexInParent`;
- `actionNames` for buttons/menu items.

### Tier 2: stable structure

These help distinguish siblings and repeated labels:

- `indexPath`;
- `xPath`;
- `indexXPath`;
- role path;
- parent path once added;
- window title/class/vm/process context.

### Tier 3: useful but variable

These should be used for scoring/tolerance, not hard identity:

- bounds;
- states such as selected/focused;
- childrenCount;
- text preview;
- currentValue;
- visible/showing state.

### Tier 4: diagnostic-only or volatile

These should usually not select an element alone:

- screen coordinates;
- selected/focused/active transient state;
- runtime process id if application restarts often;
- generated HWND;
- caret/text index at point.

## Robust resolver algorithm

The resolver should follow this order:

1. Ensure the correct Java window/modal is active.
2. Refresh tree if the current tree is stale or the previous resolution failed.
3. Try strict strategies in priority order.
4. For each strategy:
   - find all candidates;
   - reject invisible/non-showing candidates unless explicitly allowed;
   - require exactly one strong match;
   - if multiple candidates match, apply sibling disambiguators;
   - if still multiple, fail as ambiguous.
5. Retry until timeout:
   - short poll interval, e.g. 150–250 ms;
   - refresh tree after first failed pass;
   - re-detect modal/window on each retry.
6. Before action:
   - validate candidate still exists;
   - validate candidate is actionable/visible;
   - optionally highlight/log candidate.
7. Execute action using fallback chain:
   - semantic JAB action;
   - focus + JAB set text or keyboard;
   - physical click only if unique candidate is confirmed;
   - coordinate fallback only when recorded point is still inside the same confirmed element.

## Important rule: fallback must not mean “try another similar element”

Fallback should change the locator strategy for the same intended element, not switch to a different sibling.

Bad fallback:

- expected `Bounding Box` tab;
- cannot find exact match;
- clicks first `page tab`.

Good fallback:

- expected `Bounding Box` tab;
- exact path changed;
- retry by name + role + parent tab list + depth + sibling index;
- if two candidates remain, fail with ambiguity diagnostics.

## Retry model

Recommended default retry policy:

```json
{
  "timeoutMs": 5000,
  "pollIntervalMs": 200,
  "refreshTreeAfterMs": 600,
  "autoSwitchModal": true,
  "requireUnique": true,
  "allowCoordinateFallback": false
}
```

For API calls, expose these as optional request fields later.

For recorder playback, store them as project-level playback settings.

## Diagnostics on failure

When resolution fails, return:

- expected object key;
- expected locator strategy;
- active window/modal;
- whether tree was refreshed;
- number of candidates per strategy;
- top 3 closest candidates with score and mismatch reasons;
- recommendation:
  - refresh tree;
  - switch modal;
  - rebind object;
  - add/adjust stable property.

This should be shared between desktop playback and API runner.

## Implementation milestones

1. Add `schemaVersion` to recording project JSON.
2. Add `LocatorStrategy` model. **Done: foundational model added in Core.**
3. Wrap existing locator JSON into `locatorStrategies`.
4. Add `ResolutionPolicy`. **Done: policy added in Core and exposed through API resolve/action requests.**
5. Add `ResolutionResult` with: **Done: resolver now returns detailed status/candidates through `ResolveDetailed`.**
   - status: found, notFound, ambiguous, staleWindow, staleTree;
   - selected node;
   - candidates;
   - mismatch diagnostics.
6. Update recorder playback to use `ResolutionResult`.
7. Update REST API runner to use the same resolver service.
8. Add unit tests for similar sibling controls:
   - page tabs;
   - repeated OK buttons in different modals;
   - repeated text fields;
   - menu items with same label under different parents.

## Decision

Keep JSON as the file format for now. It is readable, easy to diff, and easy for API clients to generate. The important change is not the file extension; it is making the repository versioned, strategy-based, strict about uniqueness, and shared by both playback and API execution.
