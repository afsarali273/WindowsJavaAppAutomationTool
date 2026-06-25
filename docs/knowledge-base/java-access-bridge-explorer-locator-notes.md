# Java Access Bridge Explorer locator and property notes

Source reviewed: `C:\Users\Afsar\Downloads\access-bridge-explorer-master (1)\access-bridge-explorer-master`

This note captures useful design ideas from the older Access Bridge Explorer project for our Java inspector, object repository, recorder, and playback resolver.

## High-level architecture

The old project is split into two useful layers:

- `AccessBridgeExplorer`: WinForms UI, tree synchronization, overlay/tooltip behavior, and event display.
- `WindowsAccessBridgeInterop`: Java Access Bridge wrapper, accessible tree model, property extraction, native function loading, path handling, and node equality.

The most relevant files are:

- `src\WindowsAccessBridgeInterop\AccessibleNode.cs`
- `src\WindowsAccessBridgeInterop\AccessibleContextNode.cs`
- `src\WindowsAccessBridgeInterop\AccessibleWindow.cs`
- `src\WindowsAccessBridgeInterop\AccessibleJvm.cs`
- `src\WindowsAccessBridgeInterop\AccessibleNodeExtensions.cs`
- `src\WindowsAccessBridgeInterop\Path.cs`
- `src\WindowsAccessBridgeInterop\PropertyOptions.cs`
- `src\AccessBridgeExplorer\ExplorerFormController.cs`

## Locator model: runtime path, not a saved XPath

Access Bridge Explorer does not persist a formal Selenium-style locator. Its main locator concept is a runtime `Path<AccessibleNode>`, which is a root-to-leaf chain of actual Java accessible nodes.

Important behaviors:

- `AccessibleNode.GetNodePathAt(screenPoint)` recursively searches children and returns a root-to-leaf path.
- Child candidate paths are sorted by screen area, then the smallest leaf rectangle is selected. Smaller usually means more specific.
- If no child matches, the current node is returned only if its own rectangle contains the point.
- `AccessibleContextNode.GetNodePathAtWorker` filters out nodes that are not `showing`.
- For `viewport` roles, the point must be inside the viewport rectangle before child nodes are accepted.
- `AccessibleWindow.GetNodePathAt` first verifies `WindowFromPoint(screenPoint)` equals the Java window `HWND`, then delegates to the accessible tree.
- `AccessibleJvm.GetNodePathAt` asks all known Java windows and expects only one matching window.

Why this matters for us:

- For recording, hit-testing should behave as close as possible to hover/tree hit-testing.
- A raw JAB hit-test can return surprising nodes when components overlap. The old project preferred tree/rectangle traversal for specificity.
- For multi-monitor/DPI cases, JAB coordinates can be valid even outside the primary monitor; we should avoid assuming positive-only screen coordinates.

## Tree synchronization strategy

The old explorer has robust logic for selecting a node in the visible hierarchy from a live node path.

In `ExplorerFormController.SelectTreeNode(Path<AccessibleNode>)`, it handles several JAB quirks:

- Some list/tree/table child nodes are transient, especially when a parent has `manages descendants`.
- Parent-to-child enumeration and child-to-parent traversal can produce different paths. Some hidden nodes appear only when walking upward.
- The UI tree can be stale when the Java app changes.
- Top-level Java windows/modals can be stale until the JVM/window list is refreshed.

Matching order inside `FindTreeNodeInList`:

1. Try child index and strong node equality.
2. Try sequential strong equality for JVM/window nodes.
3. Try child index and weak equality for transient nodes.

Weak equality is currently title-based:

```text
treeNode.AccessibleNode.GetTitle() == node.GetTitle()
```

If matching fails:

- Refresh the JVM/window list.
- Rebuild the node path from the live leaf.
- Try again.
- Refresh the top-level window subtree if needed.

Implication for our playback resolver:

- Prefer a scored resolver, not a single locator field.
- Strong candidates: window context, path, role, name, description, index-in-parent, bounds.
- Weak candidates: title/display name, role + sibling index, role + nearby bounds, parent role/name.
- For transient/managed descendants, object handle equality should be treated as short-lived only.

## Accessible property extraction

`AccessibleContextNode.AddProperties` groups properties behind `PropertyOptions` flags. This is a good model because some JAB calls are expensive or mutate UI state.

Core properties from `AccessibleContextInfo`:

- `name`
- `description`
- virtual accessible name using `GetVirtualAccessibleName`; Access Bridge Explorer labels this as `Name (JAWS algorithm)`
- `role`
- `role_en_US`
- `states`
- `states_en_US`
- bounds: `x`, `y`, `width`, `height`
- object depth via `GetObjectDepth`
- `childrenCount`
- `indexInParent`
- interface support flags:
  - `accessibleComponent`
  - `accessibleAction`
  - `accessibleSelection`
  - `accessibleText`
  - `accessibleInterfaces`

Additional expandable groups:

- Parent context
- Children
- Top-level window
- Active descendant
- Selections
- Key bindings
- Icons
- Actions
- Visible children
- Relation set
- Value
- Table
- Text
- Hypertext

Recommended object repository fields for our tool:

- `engine`
- `window.title`
- `window.className`
- `window.hwnd`
- `window.vmId`
- `role`
- `role.enUs`
- `name`
- `description`
- `virtualAccessibleName`
- `states`
- `states.enUs`
- `path`
- `indexInParent`
- `childrenCount`
- `bounds`
- `parent.role`
- `parent.name`
- `objectDepth`
- `accessible.component`
- `accessible.action`
- `accessible.selection`
- `accessible.text`
- `accessible.value`
- `accessible.table`
- `accessible.interfaces`
- available action names
- selected text/text excerpt when accessible text exists
- current/min/max value when accessible value exists
- table row/column/cell metadata when accessible table exists

## Tooltip/hover properties

The old explorer uses a smaller tooltip property set:

- role
- text excerpt if `AccessibleText` is supported
- name
- description
- virtual accessible name
- object depth
- bounds
- states
- accessible interfaces
- window handle for top-level windows

Implication:

- Our hover tooltip should stay lightweight.
- Expensive text/table/relation calls should be opt-in or delayed.

## JAWS algorithm / virtual accessible name

Access Bridge Explorer calls `GetVirtualAccessibleName(vmid, accessibleContext, buffer, length)` and displays the result as `Name (JAWS algorithm)`.

In the old project this appears in:

- `AccessibleContextNode.AddContextProperties`
- `AccessibleContextNode.AddToolTipProperties`
- `AccessibleContextNode.GetVirtualAccessibleName`
- generated native wrappers as `GetVirtualAccessibleName`

Our current project already imports and calls the equivalent API:

- `JabInspector.Native\AccessBridgeNative.cs`: `getVirtualAccessibleName`
- `JabInspector.Core\Services\AccessBridgeService.cs`: used as a fallback when resolving combo/selection text and context labels

Current gap:

- The virtual/JAWS name is used as a fallback value, but it is not yet stored as its own first-class `AccessibleNode`, locator JSON, or object repository field.

Recommendation:

- Add `VirtualAccessibleName` to `AccessibleNode`.
- Capture it during tree crawling.
- Store it in locator JSON and the repository as `virtualAccessibleName`.
- Use it as a high-confidence fallback for controls where `name` and `description` are empty or generic.

## Events useful for recording

The old explorer wires Java Access Bridge events and logs event source nodes:

- mouse clicked/pressed/released/entered/exited
- focus gained/lost
- caret update
- property name/description/state/text/value/selection/visible-data changes
- active descendant changes
- child changes
- menu selected/deselected/canceled
- popup menu visible/invisible/canceled

Implication:

- A smooth recorder should combine event-based hints with pointer-based fallback.
- Mouse events can identify the source node directly.
- Focus/caret/text events can help infer typing targets.
- Active descendant and selection events are important for combo boxes, lists, trees, and tables.

## Tables and managed descendants

The old project documents a JAB table quirk: for some JTable cells, default APIs return renderer-related objects rather than stable cell contexts. Their workaround is:

1. Check whether the desired cell is already in the current selection.
2. If not, clear selection.
3. Add selection for the target child index.
4. Read selected context back.

This has a side effect: it changes selection in the target app.

Implication:

- Table/cell inspection can need special logic.
- The recorder should avoid selection-mutating discovery during passive hover/record.
- Playback can use table-specific actions when explicitly requested.

## Performance safeguards

The old project uses limits:

- `CollectionSizeLimit` for child/property enumeration.
- `TextBufferLengthLimit`, `TextLineLengthLimit`, and `TextLineCountLimit`.
- Lazy property groups so expensive calls happen only when expanded.

Implication:

- During recording, capture core locator metadata synchronously.
- Capture expensive metadata asynchronously or only when saving the repository.
- Avoid full deep scans on every click; use cached tree + targeted refresh.

## Practical recommendations for our current project

### Recording

- Use one shared resolver path for hover and recording.
- First resolve by current cursor/window using the same logic as hierarchy hover.
- Record both:
  - the selected `LocatorSuggestion` JSON from our tree model;
  - a richer repository property set inspired by `AccessibleContextNode.AddProperties`.
- Mark nodes under `manages descendants` as volatile/transient.
- If a modal appears, refresh the Java window list and current tree before recording the next step.

### Playback

Use a scored locator resolver:

1. Match window/modal by current HWND, title, class, JVM id, and fallback contains/normalized title.
2. Try exact path.
3. Try role + name + parent path.
4. Try role + index-in-parent + parent role/name.
5. Try role + nearby bounds/window-relative click offset.
6. Try weak display title for transient nodes.
7. Refresh current window/modal tree and retry once.

### UI/UX

- Keep core locator fields visible.
- Put expensive groups behind expanders:
  - text details
  - table details
  - relations
  - children/visible children
  - key bindings/actions
- Add a "volatile/transient node" indicator when an ancestor has `manages descendants`.

## Comparison with our current locator JSON

Our current `LocatorGenerator` already captures the most important stable fields:

- engine
- role
- name
- description
- states
- index in parent
- path
- bounds

The next useful improvements are:

- add `roleEnUs` and `statesEnUs`
- add `parentRole`, `parentName`, and parent path
- add `childrenCount`
- add `virtualAccessibleName`
- add `objectDepth`
- add supported interface/action flags
- add `isManagedDescendant` / `hasManagedDescendantAncestor`
- store window-relative bounds and click point for DPI/multi-monitor resilience

## Notes from JavaAccessBridge.Net-Sample

Second source reviewed: `C:\Users\Afsar\Downloads\JavaAccessBridge.Net-Sample-master\JavaAccessBridge.Net-Sample-master`

This project is an older, smaller .NET/VB sample. It is not as robust as Access Bridge Explorer, but it is useful because it shows the minimal JAB flow and a simple hierarchy dump pattern.

Relevant files:

- `ReadMe.md`
- `JabApiLib\JavaAccessBridge\JabApi.cs`
- `JabApiLib\JavaAccessBridge\JabHelpers.cs`
- `JabTestAppVB\JABDump.vb`

### Minimal JAB flow

The sample follows this sequence:

1. Call `Windows_run()` during form load.
2. Accept a target Java `HWND` manually, usually copied from Spy++.
3. Call `isJavaWindow(hwnd)`.
4. Call `getAccessibleContextFromHWND(hwnd, out vmID, out rootContext)`.
5. Recursively call:
   - `getAccessibleContextInfo`
   - `getAccessibleChildFromContext`
6. Dump the hierarchy to text.

Our tool automates the HWND discovery step, but this is still a helpful troubleshooting recipe. If auto-discovery ever fails, a manual-HWND diagnostic mode could be useful.

### Child-index lineage

During recursion, the sample builds a raw child-index lineage string:

```text
0, 2, 1, 4
```

This is not as readable as our role-based path:

```text
frame[0]/root pane[0]/panel[2]/push button[4]
```

But it has one advantage: it is very close to JAB's native `getAccessibleChildFromContext(parent, index)` traversal.

Recommendation:

- Keep our readable role path.
- Also consider storing a raw `indexPath` array/string in the repository for fallback playback resolution.
- Example repository field: `indexPath=0/2/1/4`.
- During playback, `indexPath` could be tried after exact path and before broad tree scoring.

### Fast hierarchy scan filter

The sample only descends into children when:

```text
role_en_US != "unknown" && states_en_US contains "visible"
```

The comment says this was an optimization that made traversal acceptably fast.

Recommendation:

- For full inspector mode, keep crawling all relevant nodes.
- For recorder pre-scan or hover-time refresh, consider a "visible-first scan" mode.
- Avoid discarding non-visible nodes permanently; some Java apps expose useful hidden roots/containers.

### Quick text value extraction

When a node supports `accessibleText`, the sample calls `getAccessibleTextItems(..., index: 0)` and stores `AccessibleTextItemsInfo.sentence` as `textValue`.

This is less complete than using `getAccessibleTextRange`, but it is cheap and often good enough for labels, combo text, and simple fields.

Recommendation:

- Add an optional lightweight text preview field for nodes with `AccessibleText`.
- Candidate fields:
  - `textPreview.sentence`
  - `textPreview.word`
  - `textPreview.character`
- Keep this optional/lazy during recording to avoid lag.

### Focus and window helpers

The sample exposes a few APIs that are useful for diagnostics:

- `getAccessibleContextWithFocus`
- `getHWNDFromAccessibleContext`
- `getNextJavaWindow`
- `getVersionInfo`

Recommendation:

- Add these to future diagnostics/backlog.
- `getAccessibleContextWithFocus` can help recording typed text when pointer-based click detection misses the actual focused editor.
- `getHWNDFromAccessibleContext` can help modal/window correlation.
- `getVersionInfo` can improve the settings/requirements page.

### Java object lifetime warning

The sample contains TODO comments noting that Java objects should be released with `releaseJavaObject`, otherwise the JVM can leak memory.

Our current code already releases many temporary contexts, especially hover contexts, but this is worth keeping as a rule:

- Any context handle returned by JAB and not retained as part of the current tree should be released.
- Long-running recorder sessions should avoid accumulating temporary contexts.
- If we add event-based recording, event source handles should either be converted to metadata quickly or released when no longer needed.

### Interop style caution

This sample uses:

- `windowsaccessbridge.dll`
- .NET 2 / VS2010 era interop
- manual `Marshal.AllocHGlobal` for structures
- 32-bit-style `IntPtr` wrappers

Do not copy this interop style directly into our modern app. Our app should continue using:

- `WindowsAccessBridge-64.dll`
- strongly typed `long` context handles for 64-bit JAB
- direct struct marshalling where safe
- resolver-based DLL discovery

### Known sample bug/quirk

`AccessibleTreeItem.SquarePixels` returns `x * y`, not `width * height`.

So its `ItemComparer` should not be reused as an area/specificity heuristic. For specificity ranking, use:

```text
width * height
```

This matches the better approach used by Access Bridge Explorer.
