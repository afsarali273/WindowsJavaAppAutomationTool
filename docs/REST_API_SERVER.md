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
- `typeText` currently uses the JAB text setter path
- `getText`

The resolver uses the same rich locator snapshot as the desktop inspector/recorder: path, indexPath, XPath, semantic fields, parent identity, depth, state, bounds, text/value metadata, and action names.

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

```json
{
  "objectKey": "push_button_open_0",
  "action": "click",
  "autoSwitchWindow": true,
  "refreshTree": true
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
  }
}
```
