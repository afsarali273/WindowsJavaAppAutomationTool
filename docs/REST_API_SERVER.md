# Java Access Bridge REST API Server

`JabInspector.Api` is a headless REST module for driving Java Swing/AWT applications through Java Access Bridge. It is intentionally separate from the WPF inspector, but reuses the same core locator, repository, tree-crawling, resolver, and action services.

## Run

```powershell
dotnet run --project JabInspector.Api --urls http://127.0.0.1:5055
```

Swagger UI is available at:

```text
http://127.0.0.1:5055/swagger
```

The OpenAPI JSON document is available at:

```text
http://127.0.0.1:5055/swagger/v1/swagger.json
```

## Basic flow

1. Discover Java windows.

```http
GET http://127.0.0.1:5055/api/java/windows
```

2. Attach a session.

```http
POST http://127.0.0.1:5055/api/java/sessions
Content-Type: application/json

{
  "title": "Download",
  "refreshTree": true
}
```

3. Load an object repository captured by the recorder.

```http
POST http://127.0.0.1:5055/api/java/sessions/{sessionId}/repository/load
Content-Type: application/json

{
  "path": "C:\\path\\to\\recording-project.json"
}
```

4. Execute an action by repository object key.

```http
POST http://127.0.0.1:5055/api/java/sessions/{sessionId}/actions
Content-Type: application/json

{
  "objectKey": "page_tab_bounding_box_2",
  "action": "click",
  "refreshTree": true
}
```

5. Execute an action with an inline locator instead of a repository object.

```http
POST http://127.0.0.1:5055/api/java/sessions/{sessionId}/actions
Content-Type: application/json

{
  "action": "setText",
  "text": "hello",
  "locator": {
    "engine": "java-access-bridge",
    "role": "text",
    "roleEnUs": "text",
    "name": "",
    "virtualAccessibleName": "",
    "description": "",
    "states": "enabled,visible,showing",
    "statesEnUs": "enabled,visible,showing",
    "indexInParent": 0,
    "objectDepth": 5,
    "childrenCount": 0,
    "path": "frame[0]/text[0]",
    "indexPath": "0/0",
    "xPath": "/frame[1]/text[1]",
    "indexXPath": "/*[1]/*[1]",
    "semanticXPath": "//text[@role='text']",
    "parentRole": "frame",
    "parentName": "Example",
    "hasManagedDescendantAncestor": false,
    "actionNames": [],
    "textPreview": "",
    "textPreviewSource": "",
    "textCharCount": -1,
    "textCaretIndex": -1,
    "textIndexAtPoint": -1,
    "textSelected": "",
    "textWord": "",
    "textSentence": "",
    "currentValue": "",
    "minimumValue": "",
    "maximumValue": "",
    "bounds": { "x": 100, "y": 100, "width": 200, "height": 24 }
  }
}
```

## Endpoints

- `GET /api/health`
- `GET /api/java/windows`
- `POST /api/java/sessions`
- `GET /api/java/sessions`
- `DELETE /api/java/sessions/{sessionId}`
- `POST /api/java/sessions/{sessionId}/refresh`
- `GET /api/java/sessions/{sessionId}/windows`
- `POST /api/java/sessions/{sessionId}/window`
- `GET /api/java/sessions/{sessionId}/tree`
- `POST /api/java/sessions/{sessionId}/repository/load`
- `GET /api/java/sessions/{sessionId}/repository`
- `POST /api/java/sessions/{sessionId}/elements/resolve`
- `POST /api/java/sessions/{sessionId}/actions`

## Supported actions

- `focus`
- `click`
- `doubleClick`
- `setText`
- `typeText`
- `getText`

The resolver uses the same rich locator snapshot as the desktop inspector/recorder: path, indexPath, XPath, semantic fields, parent identity, depth, state, bounds, text/value metadata, and action names.

`typeText` shares the same Java virtual-keypad planner used by the desktop recorder. If the resolved target is a pane/layered-pane/panel that looks like an on-screen keypad, the API resolves child keys by accessible name/description/virtual name/text/value and physically clicks them in sequence. Otherwise it falls back to the JAB text setter path.

## Stability and production-readiness notes

The API now includes Swagger UI, request logging, global exception handling, request-size/time limits, and serialized Java Access Bridge operations so simultaneous client calls do not poke the same native automation bridge at the same time.

For real production/distribution use, still treat it as a local automation driver and run it behind your own security boundary:

- bind to `127.0.0.1` by default unless you intentionally expose it;
- add authentication/API keys before allowing remote access;
- run it under the same Windows desktop/user session as the target Java application;
- supervise it with a service runner or process monitor if you need automatic restart;
- avoid parallel actions against the same Java app; the server serializes operations, but UI automation itself is stateful.

## Modal and window handling

List Java windows/modals related to a session:

```http
GET http://127.0.0.1:5055/api/java/sessions/{sessionId}/windows
```

Switch the active session window/modal:

```http
POST http://127.0.0.1:5055/api/java/sessions/{sessionId}/window
Content-Type: application/json

{
  "title": "Open",
  "className": "SunAwtDialog",
  "refreshTree": true
}
```

Actions and resolve calls also support automatic modal routing. If an `objectKey` comes from a recorder repository, the API will try to switch to the recorded window/modal before resolving the object:

Recorder projects now include a top-level `windows` collection and each repository object/step stores a `windowKey`. The API loads those scopes with the repository and prefers `windowKey` routing before falling back to legacy title/class/process/HWND matching. `GET /api/java/sessions/{sessionId}/windows` returns both currently discovered Java windows and the repository window scopes loaded for the session.

```json
{
  "objectKey": "push_button_open_0",
  "action": "click",
  "autoSwitchWindow": true,
  "refreshTree": true,
  "resolutionPolicy": {
    "timeoutMs": 5000,
    "pollIntervalMs": 200,
    "refreshTreeOnFailure": true,
    "requireUnique": true,
    "allowCoordinateFallback": false,
    "maxCandidates": 5
  }
}
```

You can also force routing for one call:

```json
{
  "objectKey": "push_button_ok_0",
  "action": "click",
  "window": {
    "title": "Confirm",
    "className": "SunAwtDialog"
  },
  "resolutionPolicy": {
    "timeoutMs": 5000,
    "pollIntervalMs": 200,
    "refreshTreeOnFailure": true,
    "requireUnique": true
  }
}
```
