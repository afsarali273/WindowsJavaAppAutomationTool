# Legacy Windows/POS Automation Tracker

This tracker converts `Windows.prompt.txt` into an implementation plan for a Windows desktop automation bridge focused on VB6, legacy Win32, ActiveX/OCX, custom-drawn POS applications, and pixel-only UI surfaces.

The goal is not to turn the Java Access Bridge inspector into a Windows inspector. The goal is to build a separate Windows automation backend that can share the same outer inspector shell, recorder concepts, object repository ideas, API runner patterns, and generated-code approach later.

## Current Position

### Existing Codebase

- `JabInspector.Core`: Java Access Bridge domain models, locator generation, repository, replay, action execution, Java node resolving.
- `JabInspector.App`: shared WPF shell with Java-first inspector, recorder tab, object repository manager, Java code generation, highlight overlay, and a Windows mode toggle.
- `JabInspector.Api`: Java REST automation server.
- `java-client`: Java wrapper client for API automation.
- `WinInspector.Core`: isolated Windows foundation with window discovery, simple technology classification, UIA backend, Win32 backend, FlaUI placeholder, and basic Windows action service.

### Existing Windows Foundation

- `WindowsWindowDiscoveryService`: enumerates top-level desktop windows.
- `WindowsTechnologyClassifier`: classifies Java-hosted, POS-like, mixed desktop, and native Win32 windows.
- `WindowsAutomationRouter`: tries Windows backends in order.
- `UiaAutomationBackend`: builds a UIA tree.
- `Win32AutomationBackend`: builds raw child HWND tree.
- `FlaUiAutomationBackend`: placeholder.
- `WindowsAutomationActionService`: basic UIA-backed focus/invoke/set-text/get-text.
- `WindowsAutomationNode`: current simple node model for Windows mode.

### Strategic Direction

Keep Java and Windows code isolated at backend level:

- Java remains in `JabInspector.Core`, `JabInspector.Api`, and `java-client`.
- Windows legacy/POS work should expand `WinInspector.Core` or new `WinInspector.*` projects.
- The WPF app can remain a shared shell with provider-aware tabs and view models.
- Future API can route by provider: `java-access-bridge`, `windows-desktop`, `legacy-pos`.
- Shared concepts can be reused only at orchestration boundaries: object repository, recorder timeline, highlight overlay, generated code preview, diagnostic exports, and validation reports.

## Product Intent

Legacy POS applications often expose incomplete automation trees:

- A normal VB6 textbox may expose a useful HWND, text, class name, and control ID.
- A VB6 PictureBox or UserControl may expose only one HWND while visually containing many buttons.
- An ActiveX grid may hide row/column data from UIA but expose COM properties.
- A custom-drawn payment panel may expose no children at all but can be reconstructed from GDI text, OCR, image matching, backend data, and region coordinates.

The Windows bridge must therefore return an evidence-rich element model, not one locator. Each inspected target should contain:

- evidence from Win32, MSAA, UIA, LegacyIAccessible, control messages, ActiveX/COM, GDI, backend, OCR, image matching, and coordinate mapping;
- real or virtual element classification;
- locator candidates with confidence, priority, score, and fallback order;
- supported actions;
- validation suggestions;
- diagnostic explanation for why a locator was selected.

## Architecture Boundaries

### Keep Isolated

- Windows scanners must not depend on Java Access Bridge classes.
- Windows element models must not reuse `AccessibleNode` directly.
- Windows locator candidates must not reuse Java-only locator semantics.
- POS adapters must not live inside Java recorder/playback services.

### Share Later

- WPF shell chrome, main app mode toggle, highlight overlay.
- Recorder UX patterns and timeline design.
- Object repository file concepts, but with provider-specific schemas.
- API host process and Swagger setup, via provider-specific routes.
- Java client style can inspire a future .NET/Java/REST Windows client, but not share implementation code.

## Proposed Project Layout

Use the existing `WinInspector.Core` initially. Split into projects only when module size or dependencies justify it.

### Phase-Friendly Layout

```text
WinInspector.Core/
  Models/
    DesktopElement.cs
    LocatorCandidate.cs
    ElementEvidence.cs
    ResolvedElement.cs
    WindowsScreenContext.cs
    WindowsScreenRegion.cs
    WindowsRect.cs
  Native/
    User32DesktopNative.cs
    OleAccNative.cs
    ComNative.cs
    GdiNative.cs
  Scanners/
    Win32/
    Msaa/
    Uia/
    LegacyAccessible/
    ControlMessages/
    ActiveX/
    Gdi/
    Backend/
    Vision/
  Engine/
    InspectionPipeline.cs
    RegionInspectionPipeline.cs
    ElementResolver.cs
    CustomPanelDetector.cs
    LocatorGenerator.cs
    LocatorRanker.cs
    LocatorEngine.cs
    ActionEngine.cs
    RetryEngine.cs
    ValidationEngine.cs
    DiagnosticsService.cs
  ScreenModels/
    ScreenRecognizer.cs
    ScreenDefinition.cs
    PosScreenDefinitions.json
  Adapters/
    GenericVb6Adapter.cs
    GenericPosAdapter.cs
    InfoGenesisLikeAdapter.cs
  Recorder/
    WindowsRecorderService.cs
    WindowsRecordedAction.cs
    WindowsReplayService.cs
```

### Future Split If Needed

```text
WinInspector.Core
WinInspector.Win32
WinInspector.Msaa
WinInspector.Uia
WinInspector.ControlMessages
WinInspector.Vision
WinInspector.Pos
WinInspector.Api
```

Do not split too early. Start in `WinInspector.Core` while interfaces stabilize.

## Core Data Models

### Module M001: Unified Windows Element Model

Status: Not started

Priority: P0

Purpose: Replace the current simple `WindowsAutomationNode` output with a richer provider-neutral Windows model that can represent both real and virtual elements.

Key classes:

```csharp
public sealed class DesktopElement
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "";
    public WindowsRect Bounds { get; set; }
    public DesktopElementSource SourceType { get; set; }
    public ElementKind ElementKind { get; set; }
    public IntPtr? Hwnd { get; set; }
    public string ClassName { get; set; } = "";
    public int? ControlId { get; set; }
    public string ParentId { get; set; } = "";
    public List<string> ChildIds { get; set; } = [];
    public double Confidence { get; set; }
    public List<LocatorCandidate> Locators { get; set; } = [];
    public List<SupportedAction> SupportedActions { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = [];
}
```

```csharp
public sealed class LocatorCandidate
{
    public string Id { get; set; } = "";
    public LocatorType Type { get; set; }
    public string Value { get; set; } = "";
    public WindowsRect? Region { get; set; }
    public double Confidence { get; set; }
    public int Priority { get; set; }
    public int Score { get; set; }
    public Dictionary<string, string> Properties { get; set; } = [];
}
```

```csharp
public sealed class ElementEvidence
{
    public Win32Evidence? Win32 { get; set; }
    public MsaaEvidence? Msaa { get; set; }
    public UiaEvidence? Uia { get; set; }
    public LegacyAccessibleEvidence? LegacyAccessible { get; set; }
    public ControlMessageEvidence? ControlMessages { get; set; }
    public ActiveXEvidence? ActiveX { get; set; }
    public GdiEvidence? Gdi { get; set; }
    public BackendEvidence? Backend { get; set; }
    public OcrEvidence? Ocr { get; set; }
    public ImageEvidence? Image { get; set; }
    public CoordinateEvidence? Coordinate { get; set; }
}
```

Deliverables:

- [ ] Add `DesktopElement`, `LocatorCandidate`, `ElementEvidence`, `ResolvedElement`, `WindowsScreenContext`, `WindowsScreenRegion`.
- [ ] Keep existing `WindowsAutomationNode` until UI migration is ready.
- [ ] Add converters from existing UIA/Win32 backend nodes to `DesktopElement`.
- [ ] Add provider metadata fields: `provider`, `backend`, `sourceType`, `elementKind`.
- [ ] Add JSON serialization compatibility tests.

Acceptance criteria:

- [ ] A simple Win32 button can be represented as a `DesktopElement`.
- [ ] A custom drawn button can be represented as a virtual `DesktopElement`.
- [ ] A single element can carry multiple locator candidates.
- [ ] Model does not reference Java types.

## Scanner Modules

### Module M010: Win32 Scanner

Status: Existing basic backend, needs expansion

Priority: P0

Purpose: Build a complete HWND tree and point-inspection path using raw Win32 APIs. This is the first layer for VB6 and POS windows.

Current code:

- `Win32AutomationBackend`
- `User32DesktopNative`

Required APIs:

- `EnumWindows`
- `EnumChildWindows`
- `FindWindow`
- `FindWindowEx`
- `GetClassName`
- `GetWindowText`
- `GetWindowRect`
- `GetClientRect`
- `ClientToScreen`
- `ScreenToClient`
- `GetDlgCtrlID`
- `GetWindowLongPtr`
- `GetParent`
- `GetAncestor`
- `WindowFromPoint`
- `ChildWindowFromPoint`
- `ChildWindowFromPointEx`
- `RealChildWindowFromPoint`
- `IsWindowVisible`
- `IsWindowEnabled`
- `GetWindowThreadProcessId`
- `SendMessage`
- `PostMessage`

Deliverables:

- [ ] Expand native interop with missing Win32 methods.
- [ ] Add `Win32Scanner.InspectWindow`.
- [ ] Add `Win32Scanner.InspectPoint`.
- [ ] Add `Win32Scanner.FindChildrenInRegion`.
- [ ] Collect HWND, parent HWND, class name, window text, control ID, bounds, client bounds, process ID, thread ID, visibility, enabled state, style, extended style.
- [ ] Add path generation: HWND path, class path, control ID path, index path.
- [ ] Add same-HWND multi-point probe helper for custom panel detection.

Pseudo-code:

```csharp
public Win32Evidence InspectFromPoint(int x, int y)
{
    var hwnd = WindowFromPoint(new POINT(x, y));
    var realChild = RealChildWindowFromPoint(GetAncestor(hwnd, GA_ROOT), ToClientPoint(x, y));
    var target = realChild != IntPtr.Zero ? realChild : hwnd;
    return BuildEvidence(target);
}
```

Acceptance criteria:

- [ ] VB6 forms show class names like `ThunderRT6FormDC`.
- [ ] VB6 controls show `ThunderRT6TextBox`, `ThunderRT6CommandButton`, etc.
- [ ] PictureBox/UserControl shallow containers are detected.
- [ ] Click-to-inspect can return deepest real HWND where possible.

### Module M020: MSAA Scanner

Status: Not started

Priority: P0

Purpose: Inspect legacy accessibility using `IAccessible`. VB6 and older controls often expose better data through MSAA than UIA.

Required APIs:

- `AccessibleObjectFromWindow`
- `AccessibleObjectFromPoint`
- `IAccessible.get_accName`
- `IAccessible.get_accRole`
- `IAccessible.get_accValue`
- `IAccessible.get_accDescription`
- `IAccessible.get_accState`
- `IAccessible.get_accChildCount`
- `IAccessible.get_accChild`
- `IAccessible.accLocation`
- `IAccessible.accHitTest`
- `IAccessible.accNavigate`
- `IAccessible.get_accDefaultAction`
- `IAccessible.accDoDefaultAction`

Important implementation note:

MSAA children may be either COM objects or integer child IDs. Store both the parent `IAccessible` identity and child ID in evidence and locator candidates.

Deliverables:

- [ ] Add `OleAccNative` interop.
- [ ] Add `MsaaScanner.InspectWindow`.
- [ ] Add `MsaaScanner.InspectPoint`.
- [ ] Add `MsaaElementRef` with `IAccessible`, child ID, hwnd, role, state, bounds.
- [ ] Add safe recursion with max depth and max child count.
- [ ] Add MSAA default-action executor.

Pseudo-code:

```csharp
public MsaaEvidence InspectFromPoint(int x, int y)
{
    AccessibleObjectFromPoint(new POINT(x, y), out var acc, out var child);
    return ReadAccessible(acc, child);
}
```

Acceptance criteria:

- [ ] MSAA tree can be shown separately from Win32 tree.
- [ ] Child-ID-only MSAA nodes can be selected and highlighted.
- [ ] Default action can be invoked where available.

### Module M030: UIA Raw/Control/Content Scanner

Status: Existing basic Raw-like tree through UIA, needs multi-view support

Priority: P0

Purpose: Inspect modern and legacy Windows apps through UI Automation views and patterns.

Required views:

- Raw View
- Control View
- Content View

Required properties:

- Name
- AutomationId
- ClassName
- ControlType
- LocalizedControlType
- BoundingRectangle
- FrameworkId
- ProcessId
- NativeWindowHandle
- IsEnabled
- IsOffscreen
- IsControlElement
- IsContentElement

Required patterns:

- Invoke
- Value
- Text
- Selection
- Grid
- Table
- Scroll
- Toggle
- ExpandCollapse
- LegacyIAccessible

Deliverables:

- [ ] Split current `UiaAutomationBackend` into scanner plus tree adapter.
- [ ] Add view option: Raw, Control, Content.
- [ ] Capture pattern support into metadata.
- [ ] Add `UiaEvidence`.
- [ ] Add UIA point inspection.
- [ ] Add UIA pattern action executor.

Acceptance criteria:

- [ ] UIA Raw/Control/Content trees can be inspected independently.
- [ ] Pattern support is visible in selected element details.
- [ ] UIA actions use patterns before mouse fallback.

### Module M040: LegacyIAccessible Pattern Merger

Status: Not started

Priority: P1

Purpose: Merge `LegacyIAccessiblePattern` from UIA with MSAA evidence when standard UIA properties are weak.

Deliverables:

- [ ] Add `LegacyAccessibleScanner.TryFromUia`.
- [ ] Extract name, role, state, value, description, default action, child ID.
- [ ] Merge into `ElementEvidence.LegacyAccessible`.
- [ ] Generate locator candidates from legacy fields.

Acceptance criteria:

- [ ] A UIA element with poor `Name` can inherit useful MSAA name/value.
- [ ] Locator explanation shows when LegacyIAccessible improved confidence.

### Module M050: Control Message Extractors

Status: Not started

Priority: P1

Purpose: Extract real data from classic controls without OCR.

Control extractors:

- `ListBoxExtractor`: `LB_GETCOUNT`, `LB_GETTEXT`, `LB_GETTEXTLEN`, `LB_GETITEMRECT`, `LB_GETCURSEL`, `LB_SETCURSEL`, `LB_SELECTSTRING`.
- `ComboBoxExtractor`: `CB_GETCOUNT`, `CB_GETLBTEXT`, `CB_GETLBTEXTLEN`, `CB_GETCURSEL`, `CB_SETCURSEL`, `CB_SELECTSTRING`.
- `ListViewExtractor`: `LVM_GETITEMCOUNT`, `LVM_GETITEMTEXT`, `LVM_GETITEMRECT`, `LVM_GETCOLUMN`, `LVM_GETHEADER`.
- `TreeViewExtractor`: `TVM_GETNEXTITEM`, `TVM_GETITEM`, `TVM_GETITEMRECT`.
- `EditExtractor`: `WM_GETTEXT`, `WM_SETTEXT`, `WM_GETTEXTLENGTH`, `EM_GETLINE`, `EM_GETLINECOUNT`.
- `ButtonExtractor`: `BM_CLICK`, `BM_GETCHECK`, `BM_SETCHECK`.
- `TabExtractor`: `TCM_GETITEMCOUNT`, `TCM_GETITEM`, `TCM_SETCURSEL`.

Deliverables:

- [ ] Add `IControlMessageExtractor`.
- [ ] Add extractor registry by class name and style.
- [ ] Extract child virtual elements for list items, combo items, tabs, tree nodes, grid-like controls where possible.
- [ ] Add message-based action executor.
- [ ] Add timeout and cross-process safety around `SendMessage`.

Acceptance criteria:

- [ ] Classic ListBox item text appears as child elements.
- [ ] Classic ComboBox selected item can be read and changed.
- [ ] Edit/TextBox values can be read and set without OCR.
- [ ] Button click can use `BM_CLICK`.

### Module M060: ActiveX/COM Inspector

Status: Not started

Priority: P2

Purpose: Detect and optionally inspect VB6/OCX controls such as MSFlexGrid, Sheridan, Infragistics, ComponentOne, and custom POS controls.

Deliverables:

- [ ] Add OCX/module detector using process module list.
- [ ] Add known class/control catalog.
- [ ] Add optional COM/IDispatch reader.
- [ ] Try common properties: `Rows`, `Cols`, `TextMatrix`, `ListCount`, `Item`, `Caption`, `Value`, `SelectedIndex`, `Text`, `Enabled`, `Visible`.
- [ ] Add guardrails: feature toggle, timeout, exception isolation.

Risks:

- Cross-process COM inspection may not be available.
- Some controls expose no automation object.
- Incorrect COM calls can destabilize the target app.

Acceptance criteria:

- [ ] ActiveX detection can identify likely OCX-backed controls.
- [ ] COM extraction is optional and disabled by default for safety.

### Module M070: GDI Text Capture

Status: Research/optional

Priority: P3

Purpose: Capture drawn text before it becomes pixels, for custom drawn controls where OCR is less reliable.

Possible technologies:

- EasyHook
- Microsoft Detours
- MinHook
- Frida

Target APIs:

- `TextOutA/W`
- `ExtTextOutA/W`
- `DrawTextA/W`
- `TabbedTextOut`
- `BitBlt`
- `StretchBlt`

Deliverables:

- [ ] Create design spike document before implementation.
- [ ] Decide whether hook runs in-process, sidecar, or diagnostic-only mode.
- [ ] Capture text, x/y, bounds if available, font, color, HWND/DC, timestamp.
- [ ] Convert captured text blocks into virtual elements.

Risks:

- Injection may trigger antivirus/security tools.
- In-process hooks can crash legacy POS systems.
- Production use may need customer approval.

Acceptance criteria:

- [ ] Prototype can capture drawn text from a controlled sample app.
- [ ] Feature remains optional and isolated from normal scanner path.

## Vision and Virtualization Modules

### Module M100: Screenshot and Region Service

Status: Not started

Priority: P1

Purpose: Capture reliable screenshots and crops for region inspection, OCR, template matching, recorder evidence, and diagnostics.

Deliverables:

- [ ] Add `ScreenshotService` with monitor/DPI aware capture.
- [ ] Capture full window, client area, selected region, and around-point crop.
- [ ] Store screenshot metadata: DPI, monitor, hwnd, timestamp, window bounds.
- [ ] Add crop naming and retention policy for recordings.

Acceptance criteria:

- [ ] Screenshot coordinates align with highlight overlays on multi-monitor setups.
- [ ] Crops can be attached to recorded actions and diagnostics.

### Module M110: OCR Region Scanner

Status: Not started

Priority: P1

Purpose: Use OCR as fallback and virtual element generator, not as primary strategy.

Recommended libraries:

- Windows OCR via `Windows.Media.Ocr` when usable from desktop.
- Tesseract for offline fallback.
- PaddleOCR or external service only as optional future plugin.

Deliverables:

- [ ] Add `OcrScanner.ReadRegion`.
- [ ] Add confidence, bounds, line/group information.
- [ ] Support region-specific OCR.
- [ ] Add text grouping for POS button labels and order-list rows.
- [ ] Add OCR cache by screenshot hash and region.

Acceptance criteria:

- [ ] OCR is restricted to known regions when screen model is available.
- [ ] OCR result can produce virtual text/button/list item elements.

### Module M120: Template and Image Matching

Status: Not started

Priority: P2

Purpose: Recognize stable visual elements, icons, buttons, error popups, and POS tender/menu buttons.

Recommended libraries:

- OpenCvSharp for template matching and shape detection.
- ImageSharp or SkiaSharp for image preprocessing.

Deliverables:

- [ ] Add `TemplateMatcher`.
- [ ] Add template repository: template path, screen, region, scale tolerance, DPI metadata.
- [ ] Add match confidence and bounding boxes.
- [ ] Add source crop capture from recorder.
- [ ] Add button-like rectangle detector.

Acceptance criteria:

- [ ] Template match can be region-limited.
- [ ] Template match can contribute locator candidates.

### Module M130: Relative Coordinate Mapper

Status: Not started

Priority: P1

Purpose: Use relative coordinates as the final fallback when no semantic or visual locator survives.

Deliverables:

- [ ] Map point relative to window, client area, parent HWND, screen region, and custom panel.
- [ ] Store ratio-based coordinates, not absolute screen coordinates.
- [ ] Add DPI and window-size normalization.
- [ ] Penalize coordinate locators heavily in ranking.

Acceptance criteria:

- [ ] A virtual POS button can be clicked after window moves or scales moderately.
- [ ] Absolute coordinates are generated only for diagnostics, not preferred automation.

## Intelligence Engine Modules

### Module M200: Inspection Pipeline

Status: Not started

Priority: P0

Purpose: Inspect a point or region by collecting evidence from all enabled scanners, then resolving it into a `DesktopElement`.

Point inspection pseudo-code:

```csharp
public DesktopElement InspectPoint(int x, int y)
{
    var evidence = new ElementEvidence();
    evidence.Win32 = win32Scanner.InspectFromPoint(x, y);
    evidence.Msaa = msaaScanner.InspectFromPoint(x, y);
    evidence.Uia = uiaScanner.InspectFromPoint(x, y);
    evidence.LegacyAccessible = legacyScanner.TryFromUia(evidence.Uia);
    evidence.ControlMessages = controlMessageScanner.TryExtract(evidence.Win32);
    evidence.ActiveX = activeXScanner.TryExtract(evidence.Win32);
    evidence.Gdi = gdiCapture.TryFindTextNearPoint(x, y);
    evidence.Backend = backendLookup.TryMatchNearPoint(x, y);
    evidence.Ocr = ocrScanner.ReadAround(x, y);
    evidence.Image = imageScanner.DetectAround(x, y);
    evidence.Coordinate = coordinateMapper.Map(x, y);

    var element = elementResolver.Merge(evidence);
    element.Locators = locatorGenerator.Generate(element, evidence);
    element.Locators = locatorRanker.Rank(element.Locators);
    return element;
}
```

Region inspection pseudo-code:

```csharp
public IReadOnlyList<DesktopElement> InspectRegion(WindowsRect region)
{
    var elements = new List<DesktopElement>();
    elements.AddRange(win32Scanner.FindChildrenInRegion(region));
    elements.AddRange(msaaScanner.FindChildrenInRegion(region));
    elements.AddRange(uiaScanner.FindChildrenInRegion(region));
    elements.AddRange(controlMessageScanner.TryExtractChildren(region));
    elements.AddRange(activeXScanner.TryExtractChildren(region));

    var regionResult = new RegionInspectionResult
    {
        Region = region,
        RealElements = elements,
        OcrTextBlocks = ocrScanner.ExtractTextBlocks(region),
        ImageMatches = imageScanner.MatchKnownTemplates(region),
        GdiTextBlocks = gdiCapture.GetTextBlocks(region)
    };

    if (customPanelDetector.IsCustomDrawnPanel(regionResult))
    {
        elements.AddRange(virtualElementExtractor.Extract(region, regionResult));
    }

    return elementResolver.DeduplicateAndMerge(elements);
}
```

Deliverables:

- [ ] Add point inspection orchestration.
- [ ] Add region inspection orchestration.
- [ ] Add scanner timeout policy.
- [ ] Add scanner enable/disable settings.
- [ ] Add evidence diagnostics.

Acceptance criteria:

- [ ] Point inspection returns merged evidence from at least Win32 and UIA in MVP.
- [ ] Pipeline remains usable when optional scanners are disabled.

### Module M210: Custom Panel Detector

Status: Not started

Priority: P1

Purpose: Detect when a selected area is a custom-drawn panel and needs virtual element reconstruction.

Signals:

- Win32 child count is zero or very low.
- MSAA child count is zero.
- UIA Raw View returns only Pane/Client/Custom.
- Multiple click points inside panel return same HWND.
- Element names are empty.
- OCR finds multiple text blocks inside panel.
- Screenshot shows multiple button-like regions.
- Class is PictureBox/UserControl/AfxWnd/custom canvas-like.
- No Invoke/Value/Text/Grid pattern support.

Pseudo-code:

```csharp
public bool IsCustomDrawnPanel(RegionInspectionResult result)
{
    var score = 0;
    if (result.Win32ChildCount == 0) score++;
    if (result.MsaaChildCount == 0) score++;
    if (result.UiaChildCount <= 1) score++;
    if (result.OcrTextBlocks.Count >= 3) score++;
    if (result.DetectedButtonRegions.Count >= 3) score++;
    if (result.SameHwndForMultiplePoints) score++;
    if (KnownCanvasClasses.Contains(result.PrimaryClassName)) score++;
    if (!result.HasActionablePatterns) score++;
    return score >= 3;
}
```

Deliverables:

- [ ] Add scoring model and explanation.
- [ ] Add known legacy class list: `ThunderRT6PictureBoxDC`, `ThunderRT6UserControlDC`, `AfxWnd`, `Static`, `CustomControl`, `Panel`.
- [ ] Add UI panel indicator: `Real Control`, `Likely Container`, `Custom Drawn Panel`.

Acceptance criteria:

- [ ] Shallow VB6 UserControl with multiple OCR labels is marked as custom drawn.
- [ ] Normal textbox/button is not marked as custom drawn.

### Module M220: Virtual Element Extractor

Status: Not started

Priority: P1

Purpose: Reconstruct virtual buttons, list items, menu items, tender actions, and popup buttons from non-semantic panels.

Evidence sources:

- GDI text blocks.
- OCR text blocks.
- Button-like visual shapes.
- Template matches.
- Backend menu/order data.
- Screen region definitions.
- Relative position.

Deliverables:

- [ ] Add `VirtualElementExtractor`.
- [ ] Merge OCR/GDI text with nearby button-like rectangles.
- [ ] Build virtual roles: `VirtualButton`, `VirtualListItem`, `VirtualMenuItem`, `VirtualTab`, `VirtualPopupButton`.
- [ ] Generate virtual child tree under real parent panel.
- [ ] Assign confidence based on source agreement.

Acceptance criteria:

- [ ] A custom payment panel can expose virtual `Cash` and `Card` buttons.
- [ ] A custom order list can expose virtual row items from OCR/GDI/backend evidence.

### Module M230: Element Resolver and Deduplicator

Status: Not started

Priority: P0

Purpose: Merge overlapping evidence from multiple scanners into stable `DesktopElement` objects.

Deliverables:

- [ ] Add overlap-based deduplication.
- [ ] Add name/role/class/path similarity merge.
- [ ] Preserve all evidence sources.
- [ ] Select primary source by confidence and actionability.
- [ ] Add explanation text for merge decision.

Acceptance criteria:

- [ ] Same button detected by Win32, UIA, and MSAA becomes one element with three evidence sources.
- [ ] OCR-only virtual element does not hide a real accessible control unless confidence warrants it.

### Module M240: Locator Generator and Ranker

Status: Not started

Priority: P0

Purpose: Generate multiple locators and rank them by stability, confidence, uniqueness, validation support, and fallback order.

Base ranking:

- Win32 HWND/control ID/class path: 95
- UIA AutomationId: 90
- MSAA name/role/child ID: 88
- Control-message item identity: 86
- LegacyIAccessible: 84
- ActiveX property identity: 83
- GDI text in known region: 80
- Backend data identity: 78
- OCR text in known region: 72
- Image template: 68
- Relative coordinate: 45
- Absolute coordinate: 15

Pseudo-code:

```csharp
public int Score(LocatorCandidate locator)
{
    var score = BaseScore(locator.Type);
    score += (int)(locator.Confidence * 10);
    if (locator.Properties.ContainsKey("isUnique")) score += 10;
    if (locator.Properties.ContainsKey("knownRegion")) score += 8;
    if (locator.Properties.ContainsKey("hasValidation")) score += 8;
    if (locator.Properties.ContainsKey("duplicateText")) score -= 15;
    if (locator.Type.ToString().Contains("Coordinate")) score -= 20;
    return score;
}
```

Deliverables:

- [ ] Add locator generation from every evidence source.
- [ ] Add uniqueness checks within current screen/region.
- [ ] Add ranking explanation.
- [ ] Add locator JSON preview in Windows inspector.

Acceptance criteria:

- [ ] Real controls prefer Win32/UIA/MSAA locators.
- [ ] Custom drawn controls prefer GDI/OCR/template locators before relative coordinates.
- [ ] Absolute coordinates are last.

### Module M250: Locator Resolvers

Status: Not started

Priority: P1

Purpose: Resolve locator candidates at runtime using the right backend.

Interface:

```csharp
public interface ILocatorResolver
{
    bool CanResolve(LocatorCandidate locator);
    ResolvedElement? Resolve(LocatorCandidate locator, WindowsScreenContext context);
}
```

Resolvers:

- `Win32LocatorResolver`
- `MsaaLocatorResolver`
- `UiaLocatorResolver`
- `LegacyAccessibleLocatorResolver`
- `ControlMessageLocatorResolver`
- `ActiveXLocatorResolver`
- `GdiTextLocatorResolver`
- `BackendLocatorResolver`
- `OcrLocatorResolver`
- `ImageTemplateLocatorResolver`
- `RelativeCoordinateLocatorResolver`
- `AbsoluteCoordinateLocatorResolver`

Acceptance criteria:

- [ ] Locator engine can try candidates in ranked order.
- [ ] Failed resolver reasons are captured.

## Action, Retry, Validation

### Module M300: Action Engine

Status: Existing basic Windows actions, needs replacement

Priority: P1

Purpose: Execute actions through the strongest available channel, validate outcome, and fallback.

Actions:

- Click
- Double click
- Type
- Set text
- Select
- Press key
- Get text
- Get value
- Wait for element
- Assert visible
- Assert text
- Scroll
- Drag/drop
- Screenshot

Execution channels:

- Win32 messages.
- MSAA default action.
- UIA patterns.
- Control-specific messages.
- ActiveX/COM calls.
- Mouse/keyboard fallback.
- OCR/image coordinate click fallback.

Click pseudo-code:

```csharp
public ActionResult Click(DesktopElement element, ActionValidation? validation)
{
    foreach (var locator in element.Locators.OrderByDescending(x => x.Score))
    {
        var resolved = locatorEngine.Resolve(locator, screenContext.Current);
        if (resolved is null) continue;

        var result = executorRegistry.Click(resolved);
        if (!result.Success) continue;

        if (validation is null || validationEngine.Validate(validation))
        {
            locatorStats.MarkSuccess(locator);
            return ActionResult.Success(locator);
        }

        locatorStats.MarkValidationFailure(locator);
    }

    diagnostics.CaptureFailurePackage(element);
    return ActionResult.Failed(element);
}
```

Acceptance criteria:

- [ ] UIA Invoke is preferred for UIA InvokePattern controls.
- [ ] `BM_CLICK` is preferred for classic buttons.
- [ ] Mouse click is fallback, not default.
- [ ] Action reports the locator and executor that succeeded.

### Module M310: Retry and Fallback Engine

Status: Not started

Priority: P1

Purpose: Coordinate retries, screen refresh, fallback locators, diagnostics, and locator statistics.

Default policy:

```csharp
public sealed class RetryPolicy
{
    public int MaxRetriesPerLocator { get; set; } = 2;
    public bool RefreshBeforeRetry { get; set; } = true;
    public bool AllowFallbackLocator { get; set; } = true;
    public bool AllowVisualFallback { get; set; } = true;
    public bool AllowCoordinateFallback { get; set; } = true;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(500);
}
```

Deliverables:

- [ ] Retry same locator after short wait.
- [ ] Refresh current tree before retry.
- [ ] Re-run OCR/image scan for visual locators.
- [ ] Detect popup/screen change during retry.
- [ ] Try fallback locator.
- [ ] Capture diagnostic package after failure.

Acceptance criteria:

- [ ] Failed primary locator can fall back to OCR/template/relative coordinate.
- [ ] Failure report explains every attempted locator.

### Module M320: Validation Engine

Status: Not started

Priority: P1

Purpose: Validate every meaningful POS action through UI, screen, backend, or file/log evidence.

Validation types:

- `screenContainsText`
- `screenDoesNotContainText`
- `elementVisible`
- `elementGone`
- `elementEnabled`
- `elementDisabled`
- `windowTitleChanged`
- `popupAppeared`
- `popupClosed`
- `screenStateIs`
- `orderListContains`
- `orderListDoesNotContain`
- `databaseRecordExists`
- `databaseRecordUpdated`
- `journalContains`
- `receiptContains`
- `fileCreated`
- `logContains`

Acceptance criteria:

- [ ] Click `Coffee` can validate that order list contains `Coffee`.
- [ ] Click `Cash` can validate that tender popup or amount tendered state appears.
- [ ] Validation can use screen OCR or backend lookup.

## Screen and POS Modules

### Module M400: Screen Model Layer

Status: Not started

Priority: P1

Purpose: Identify current POS screen and define reusable regions.

Screen types:

- Login
- Home
- Revenue center selection
- Table selection
- Order entry
- Menu category
- Modifier popup
- Payment/tender
- Receipt
- Report/export
- Error popup
- Manager approval popup
- Printer offline popup
- Network offline popup

Deliverables:

- [ ] Add `ScreenDefinition`.
- [ ] Add `ScreenRegion`.
- [ ] Add `ScreenRecognizer`.
- [ ] Add JSON format for screen definitions.
- [ ] Add region editor later in UI.

Example:

```json
{
  "name": "OrderEntry",
  "regions": {
    "leftMenu": { "xRatio": 0.00, "yRatio": 0.12, "widthRatio": 0.22, "heightRatio": 0.78 },
    "menuItems": { "xRatio": 0.23, "yRatio": 0.12, "widthRatio": 0.45, "heightRatio": 0.78 },
    "orderList": { "xRatio": 0.70, "yRatio": 0.12, "widthRatio": 0.28, "heightRatio": 0.70 }
  }
}
```

Acceptance criteria:

- [ ] Current screen can be recognized from window title, OCR/GDI text, known controls, image signals, and prior action.
- [ ] Region names can be used by locators and validations.

### Module M410: POS Adapter Layer

Status: Not started

Priority: P2

Purpose: Provide workflow-level APIs above raw element automation.

Adapters:

- `GenericVb6Adapter`
- `GenericPosAdapter`
- `InfoGenesisLikeAdapter`
- `LoginAdapter`
- `OrderEntryAdapter`
- `PaymentAdapter`
- `PopupAdapter`
- `ReceiptReportAdapter`

Example APIs:

```csharp
pos.Login("cashier01", "1234");
pos.OrderEntry().AddItem("Coffee").ValidateOrderContains("Coffee");
pos.Payment().TenderCash(50.00).ValidateCheckClosed();
```

Acceptance criteria:

- [ ] Adapter methods are built on recorder/object repository/screen model primitives.
- [ ] POS workflow does not hard-code absolute screen coordinates.

### Module M420: Backend/Config/Database Lookup

Status: Not started

Priority: P3

Purpose: Use POS data sources for menu items, tender names, order state, receipt validation, journals, and reports.

Possible sources:

- SQL Server
- Oracle
- ODBC
- Local database
- INI/XML/JSON/flat files
- POS cache files
- Transaction journal
- Receipt logs
- Kitchen printer logs
- Settlement logs

Acceptance criteria:

- [ ] Backend lookup can be configured per application.
- [ ] Backend evidence can enrich virtual elements and validations.

## Recorder and Repository

### Module M500: Windows Recorder

Status: Not started

Priority: P1

Purpose: Record actions with rich evidence, screenshots, locator candidates, and validation suggestions.

Captured on click:

- Mouse position.
- Active window.
- HWND under cursor.
- Parent HWND chain.
- Win32 class/text/control ID.
- MSAA object under point.
- UIA element under point.
- LegacyIAccessible data.
- Control-message data if available.
- Screenshot before click.
- Screenshot crop around click.
- OCR text around click.
- GDI text near click.
- Image template crop.
- Screen name.
- Region name.
- Relative coordinate.
- Backend match if available.
- Screenshot after click.
- Suggested validation.

Deliverables:

- [ ] Add `WindowsRecordedAction`.
- [ ] Add `WindowsRecordingProject`.
- [ ] Add schema versioning.
- [ ] Add screenshot crop/template asset folder.
- [ ] Add replay service.
- [ ] Keep separate from Java recording schema, but align high-level concepts.

Example recorded action:

```json
{
  "provider": "windows-legacy",
  "screen": "Payment",
  "action": "click",
  "target": {
    "name": "Cash",
    "role": "VirtualButton",
    "bounds": { "x": 710, "y": 620, "width": 120, "height": 70 },
    "locators": [
      { "type": "gdi-text", "value": "Cash", "region": "paymentButtons", "priority": 1, "confidence": 0.95 },
      { "type": "ocr", "value": "Cash", "region": "paymentButtons", "priority": 2, "confidence": 0.91 },
      { "type": "image-template", "value": "templates/payment/cash.png", "priority": 3, "confidence": 0.86 },
      { "type": "relative-coordinate", "value": "paymentButtons:0.74,0.81", "priority": 4, "confidence": 0.55 }
    ]
  },
  "validation": {
    "type": "screenContainsText",
    "value": "Amount Tendered",
    "region": "payment-popup",
    "timeoutMs": 5000
  }
}
```

Acceptance criteria:

- [ ] Recorder captures at least Win32/UIA evidence in MVP.
- [ ] Custom panel actions capture screenshot crop, OCR text, and relative coordinate.
- [ ] Replay uses locator chain, not recorded x/y first.

### Module M510: Windows Object Repository

Status: Not started

Priority: P1

Purpose: Store provider-specific Windows elements and locators with statistics and validation hints.

Deliverables:

- [ ] Define `.wrecording.json` or provider-tagged `.automation.json` schema.
- [ ] Store multiple locators per object.
- [ ] Store screen and region context.
- [ ] Store success/failure statistics per locator.
- [ ] Add repository manager support in WPF later.

Acceptance criteria:

- [ ] A single object can carry Win32, UIA, MSAA, OCR, image, and coordinate locators.
- [ ] Repository can be loaded by replay and future API runner.

### Module M520: Self-Healing Locator Statistics

Status: Not started

Priority: P2

Purpose: Promote locators that repeatedly resolve and validate successfully; demote unreliable ones.

Stats:

- success count
- failure count
- validation failure count
- last success
- last failure
- average resolve time
- confidence
- promoted/demoted status

Acceptance criteria:

- [ ] If OCR locator succeeds after UIA fails, OCR confidence can rise.
- [ ] Changes are stored as statistics, not destructive replacement, unless user approves.

## UI Integration

### Module M600: Shared Inspector Shell Integration

Status: Existing foundation

Priority: P1

Purpose: Add Windows legacy inspection without disturbing the Java inspector.

Rules:

- Keep top-level mode toggle: Java / Windows.
- Java tabs continue using Java data models.
- Windows mode uses `WinInspector.Core` models.
- Shared UI can host Windows-specific panels when mode is Windows.
- Do not mix Java recorder state with Windows recorder state.

Deliverables:

- [ ] Windows selected element details panel.
- [ ] Windows locator candidate table.
- [ ] Windows source/evidence viewer.
- [ ] Real tree tabs: Win32, MSAA, UIA Raw, UIA Control, UIA Content.
- [ ] Virtual element tree panel.
- [ ] Screenshot panel with overlays.
- [ ] Region selection tool.
- [ ] Custom panel detection status.
- [ ] OCR/GDI text block viewer.
- [ ] Action test panel.
- [ ] Diagnostic export button.

Acceptance criteria:

- [ ] Switching to Java mode preserves current Java workflows.
- [ ] Switching to Windows mode shows Windows-specific evidence and locator candidates.
- [ ] Java object repository is not polluted by Windows objects.

### Module M610: Windows Highlight and Overlay

Status: Existing overlay can be reused

Priority: P1

Deliverables:

- [ ] Reuse `HighlightOverlay` for real and virtual Windows elements.
- [ ] Support screenshot overlay rectangles.
- [ ] Support region selection and region highlighting.
- [ ] Confirm multi-monitor DPI handling.

Acceptance criteria:

- [ ] Real HWND, UIA element, OCR block, and virtual button all highlight correctly.

## API and Client Integration

### Module M700: Windows REST API

Status: Not started

Priority: P2

Purpose: Add provider-specific REST endpoints similar to Java API, but backed by Windows locator engine.

Candidate endpoints:

- `GET /api/windows/windows`
- `POST /api/windows/inspect/point`
- `POST /api/windows/inspect/region`
- `POST /api/windows/actions/run`
- `POST /api/windows/repository/load`
- `POST /api/windows/recordings/replay`
- `GET /api/windows/health`

Rules:

- Do not reuse Java session contracts.
- Share API host only if routing remains provider-specific.
- Stateless one-shot actions should be supported early for POS use.

Acceptance criteria:

- [ ] External client can run a Windows action using window selector plus locator chain.
- [ ] API returns diagnostics on failure.

### Module M710: Future Client Wrappers

Status: Future

Priority: P3

Possible clients:

- C# fluent wrapper.
- Java wrapper mirroring existing `java-client`.
- Python wrapper for RPA users.

Example:

```java
desktop.window(title("InfoGenesis"))
       .screen("Payment")
       .element("cash_button")
       .click()
       .validate(screenContainsText("Amount Tendered"));
```

## MVP Roadmap

### Phase 1: Win32 Inspector MVP

Goal: reliable HWND discovery and point inspection.

Tasks:

- [ ] Expand `User32DesktopNative`.
- [ ] Add rich `Win32Evidence`.
- [ ] Add full HWND tree.
- [ ] Add point inspection with `WindowFromPoint` and `RealChildWindowFromPoint`.
- [ ] Add class/text/control ID/style/client bounds.
- [ ] Add highlight selected HWND.
- [ ] Add basic Win32 click/type/get text for classic controls.

Exit criteria:

- [ ] Can inspect a VB6 sample form and see meaningful class/text/control IDs.

### Phase 2: MSAA and UIA Evidence

Goal: multiple real-control evidence layers.

Tasks:

- [ ] Add MSAA scanner.
- [ ] Add UIA Raw/Control/Content scanner modes.
- [ ] Add LegacyIAccessible merge.
- [ ] Add evidence viewer in UI.
- [ ] Generate initial locator candidates.

Exit criteria:

- [ ] Same selected element shows Win32, MSAA, and UIA evidence when available.

### Phase 3: Control Message Extraction

Goal: read classic controls without OCR.

Tasks:

- [ ] Add message extractors for Edit, Button, ListBox, ComboBox, Tab.
- [ ] Add ListView and TreeView after core extractors stabilize.
- [ ] Generate virtual child elements for items/tabs.
- [ ] Add message-based actions.

Exit criteria:

- [ ] ListBox/ComboBox items are visible as inspectable children.

### Phase 4: Custom Panel Detection and Region Inspection

Goal: identify POS panels where real children do not exist.

Tasks:

- [ ] Add screenshot service.
- [ ] Add region selection.
- [ ] Add custom panel scoring.
- [ ] Add OCR region scanner.
- [ ] Add button-like shape detection.

Exit criteria:

- [ ] Shallow VB6 PictureBox/UserControl with visual buttons is classified as custom drawn.

### Phase 5: Virtual Element Tree

Goal: reconstruct actionable virtual children.

Tasks:

- [ ] Add virtual element extractor.
- [ ] Generate OCR/GDI/template/relative-coordinate locators.
- [ ] Show virtual tree under real parent.
- [ ] Click virtual elements through resolved bounds.

Exit criteria:

- [ ] Custom POS-like panel exposes virtual buttons such as `Cash`, `Card`, `Food`, `Drinks`.

### Phase 6: Locator Engine and Action Fallback

Goal: robust execution.

Tasks:

- [ ] Implement locator generator/ranker.
- [ ] Implement locator resolver chain.
- [ ] Implement action engine.
- [ ] Implement retry and fallback.
- [ ] Add diagnostics package.

Exit criteria:

- [ ] A recorded element can be replayed using ranked locators with fallback.

### Phase 7: Windows Recorder

Goal: record rich Windows evidence.

Tasks:

- [ ] Add Windows recorder service.
- [ ] Capture before/after screenshots and crops.
- [ ] Store locator candidates.
- [ ] Store validation suggestions.
- [ ] Add Windows playback.

Exit criteria:

- [ ] A click flow can be recorded and replayed against a legacy sample app.

### Phase 8: Screen Models and POS Adapter

Goal: workflow-level POS automation.

Tasks:

- [ ] Add screen definitions and region maps.
- [ ] Add screen recognizer.
- [ ] Add order entry/payment/login adapter primitives.
- [ ] Add validation helpers.

Exit criteria:

- [ ] POS-like workflow can do login, add item, tender, validate order/payment state.

### Phase 9: Advanced Evidence

Goal: enterprise-grade resilience.

Tasks:

- [ ] ActiveX/COM extraction.
- [ ] GDI text capture spike.
- [ ] Backend/config lookup.
- [ ] Self-healing locator stats.
- [ ] API endpoints.

Exit criteria:

- [ ] Optional advanced modules can be enabled without destabilizing basic inspection.

## Recommended Libraries

### Core Windows

- Built-in P/Invoke for Win32.
- `System.Windows.Automation` for UIA where sufficient.
- FlaUI for richer UIA wrappers after package integration.
- `Accessibility` COM interop or custom `oleacc.dll` interop for MSAA.

### Vision

- OpenCvSharp for template matching and shape detection.
- Tesseract or Windows OCR for OCR fallback.
- ImageSharp or SkiaSharp for image preprocessing and crop handling.

### Hooks

- EasyHook, MinHook, Detours, or Frida for GDI capture spike only.

### Serialization and Diagnostics

- `System.Text.Json`.
- Existing app logging patterns.
- ZIP diagnostic export package later.

## Testing Strategy

### Test Applications

- Simple Win32 test app.
- VB6-style control sample if available.
- WinForms app with standard controls.
- WPF app for UIA comparison.
- Custom-drawn sample app with a panel and drawn buttons.
- POS-like mock app with menu, order list, payment panel, and popups.

### Automated Tests

- Unit tests for locator scoring.
- Unit tests for custom panel detection.
- Unit tests for region coordinate mapping.
- Unit tests for serialization schema.
- Integration tests for Win32 scanner against test app.
- Integration tests for UIA scanner against test app.
- Replay tests against mock POS app.

### Manual Tests

- Multi-monitor DPI highlight alignment.
- Point inspection on VB6/POS controls.
- Popup/modal switching.
- OCR on region-limited POS panels.
- Action fallback diagnostics.

## Risks and Mitigations

### Risk: OCR Is Noisy

Mitigation:

- Use OCR only after semantic/control-message strategies.
- Limit OCR to known regions.
- Combine OCR with button shape, templates, backend data, and validation.

### Risk: Coordinates Are Fragile

Mitigation:

- Use relative coordinates only.
- Tie coordinates to regions or parent HWND.
- Rank coordinate locators last.

### Risk: GDI Hooking Can Destabilize POS Apps

Mitigation:

- Keep GDI capture optional.
- Implement as a spike first.
- Require explicit enablement.
- Never enable by default in production.

### Risk: ActiveX/COM Inspection Is Inconsistent

Mitigation:

- Treat COM as optional evidence.
- Isolate calls with timeouts and exception guards.
- Prefer known control catalogs and safe property reads.

### Risk: UIA Tree Is Too Shallow

Mitigation:

- Use Win32, MSAA, LegacyIAccessible, control messages, OCR, and virtual extraction.
- Do not make UIA the single source of truth.

## Definition of Done for the Legacy Windows Bridge

- [ ] Inspector can inspect real Win32/VB6 controls.
- [ ] Inspector can detect custom-drawn panels.
- [ ] Inspector can reconstruct virtual elements from OCR/GDI/template/backend/regions.
- [ ] Every selected element shows evidence, locator candidates, ranking, and explanation.
- [ ] Recorder stores multiple locator candidates and validation suggestions.
- [ ] Replay resolves locators with retry/fallback and validates outcome.
- [ ] Diagnostics explain why an action failed.
- [ ] Java inspector remains stable and isolated.
- [ ] Windows backend can later be exposed through REST API without rewriting core logic.

## Immediate Next Implementation Slice

Recommended first implementation module:

1. Expand Win32 native interop and `Win32Scanner`.
2. Add `DesktopElement`, `LocatorCandidate`, and `Win32Evidence`.
3. Add point inspection in Windows mode.
4. Add locator candidate generation for Win32 controls.
5. Show locator candidates in the existing inspector UI when Windows mode is selected.

This gives us a practical MVP foundation before adding MSAA, UIA multi-view, OCR, or POS-specific behavior.
