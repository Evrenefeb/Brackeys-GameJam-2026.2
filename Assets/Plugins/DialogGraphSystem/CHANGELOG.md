# Dialogue Graph System - Changelog

All notable changes to the core package are documented here.
Versions follow semantic versioning. Dates are YYYY-MM-DD.

---

## [3.0.0] - 2026-05-25

### Summary
Major publish update from the last public `2.4.0` build. This release rolls up the `2.4.1` and `2.4.2` fixes, then adds new product surfaces that materially expand the package: localization, cross-graph traversal, a unified editor workspace, runtime player settings UI, graph categorization metadata, and broader regression coverage.

### Upgrade Note
Back up projects before importing `3.0.0` over `2.4.1`. Older graph assets may be upgraded from schema `1` to schema `2`, adding serialized metadata, localization data, Graph Jump data, and runtime UI settings. Validate production graphs after import and test custom runtime UI bindings, action handlers, localization tables, and optional AI workflows before shipping.

### Added
- A cohesive three-demo `DialogueDemo.unity` showcase with Product Tour, Shop Gate, and Reactor Control Room cards; persistent Back, Reset, and Runtime Settings controls; deterministic replay state; and visible feedback for all five packaged action IDs.
- A non-selectable `Demo_ReactorAftermath` support graph demonstrating direct-reference Graph Jump traversal and return flow.
- `Documentation/DEMO_GUIDE.md` with route setup, graph and definition mappings, localization instructions, replay controls, and scene action receivers.
- End-to-end localization workflow with localization tables, runtime localization settings, locale key generation, language catalog helpers, localization manager UI, and runtime language switching support.
- `GraphJumpNode` and `GraphReference` for cross-graph traversal, including target resolution by direct asset reference, graph GUID, runtime dialog ID, or cached asset metadata.
- Unified `DialogSystemMainWindow` workspace with tabs for Launcher, Graphs, Characters, Actions, Variables, Context, Localization, Import / Export, and Settings.
- Runtime settings panel UI for voice volume, SFX volume, typewriter audio, text speed, auto-advance, localization, and theme selection.
- Graph metadata and browsing support: stable graph GUIDs, author/tags/category metadata, category catalog assets, and richer sample graph organization.
- New regression coverage for dialog manager traversal, graph jumps, localization workflows, JSON serialization, locale key generation, optional AI support, and runtime UI binding safety.
- `package.json` for UPM and Asset Store distribution, package-local README/changelog/troubleshooting/upgrade docs, and a dedicated core package exporter for the publish/export workflow.
- Online-first public-information viewer with an eight-second timeout and bundled TextAsset fallbacks for documentation, changelog, license, AI Extension, support, and upgrade guidance.
- Explicit **View Bundled Copy** actions wherever an external browser cannot report a failed navigation back to the editor.

### Changed
- Rebuilt all package-owned sample graphs as a consistent three-entry catalog with professional metadata, stable identities, intentional node names, English/German localization, exact JSON exports, and explicit route outcomes. `Shop-Gate-Test` is now `Demo_ShopGate`, and variable filenames now match the case-sensitive `gold` and `hasKey` keys while preserving GUIDs.
- `DialogManager` now refreshes visible localized content on locale changes, owns graph-jump return flow, and applies dedicated per-character typewriter audio control.
- Validation, import/export, schema upgrade, and traversal utilities now understand graph-jump nodes and newer graph metadata.
- Settings and onboarding flows now present the package as `3.0.0`, include graph-view preferences, surface runtime UI settings, and point AI-provider setup to `Tools > BekaForge > AI Providers`.
- Demo scenes, packaged prefabs, and sample graphs were refreshed to cover the expanded runtime and authoring workflows.
- Core export guidance now targets `Assets/DialogGraphSystem` only so required runtime scripts are not omitted and Package Manager source files are not pulled into the `.unitypackage`.

### Fixed
- Release metadata drift between README, welcome content, runtime version strings, and the packaged settings asset.
- Cross-graph traversal guard rails now block recursive graph-jump cycles and excessive nested jump depth.
- Localization and runtime settings regressions are covered by dedicated edit-mode tests before publish.

---

## [2.4.2] - 2026-05-22

### Summary
Customer configurability and release-QA patch. This release packages the new settings-driven workflow more clearly, updates release metadata to 2.4.2, and locks down the editor/runtime behavior added across the earlier customer-feedback phases.

### Added
- `DialogueRuntimeUISettings` is included in the packaged master settings asset for runtime UI visibility control.
- Double-click opening for `DialogGraph` assets in the Project window.
- Graph View settings page for minimap visibility and custom zoom limits.
- Per-character typewriter audio configuration in the packaged audio settings workflow.
- New runtime integration tests for `DialogManager` traversal, choices, and action execution.
- New migration safety tests for stable GUIDs and forward-compatibility.
- New validation regression tests for structural graph integrity.

### Changed
- README and release documentation now describe the packaged settings workflow for text pacing, choice behavior, typewriter audio, graph open behavior, minimap visibility, and zoom limits.
- Core AI entry points now use the exact `AI Extension 2.0 — Coming Soon` message and one stable Learn More target. Core 3.0.0 has no AI package, provider, key, or remote-configuration dependency.
- Welcome/version metadata, runtime version strings, and packaged settings asset metadata are synced to `2.4.2`.

### Fixed
- Release metadata drift between README, changelog, onboarding defaults, runtime version constants, and packaged settings asset version string.
- Fixed documentation typo for `EditorSidebarWindow` class name.
- Disabled production-facing debug flags by default across runtime and editor components.

---

## [2.4.1] - 2026-05-10

### Summary
Patch release — fixes Phase A (v2.1.1) items that were deferred during the v2.2–v2.4 feature push. No schema changes.

### Fixed
- `EditorSidebarWindow` class name typo corrected (was `EditorSidbarWindow`).
- `doDebug` defaults set to `false` in `DialogManager`, `DialogGraphView`, `DialogGraphEditorWindow`, and `DialogUndoUtility`.
- `DialogUndoUtility.doDebug` no longer carries `[SerializeField]` on a static field.
- Validation classes confirmed in `Editor/Services/Validation/` namespace (not `AI/Validation`).
- Version metadata synced to `2.4.1` in code, README, CHANGELOG, and settings asset.

---

## [2.4.0] - 2026-05-10

### Summary
UX polish and release-readiness update. This release focuses on making the editor easier to discover and operate while keeping graph data formats and public runtime APIs stable.

### Added
- Floating AI Assistant panel overlay in the graph editor when the optional AI extension is installed.
- AI command preset buttons for common prompts such as rewrite, grammar fix, continue, add nodes, choices, insert action, professional, shorter, and funny.
- Additional editor tooltips across graph toolbar controls, validation controls, AI settings, node fields, registered character/action fields, and runtime settings.
- Refreshed fallback welcome content for the current 2.4.0 release.

### Changed
- README and serialized settings metadata now identify 2.4.0 as the current release.
- Welcome fallback copy now points users toward validation, variables, condition/set-variable nodes, and optional preview-first AI authoring.

---

## [2.3.0] - 2026-05-09

### Summary
Architecture refactor update. This release reduces large-class pressure by extracting focused editor and runtime services while preserving existing public APIs and serialized graph data.

### Added
- `DialogGraphEdgeSyncService` for graph edge synchronization.
- `DialogGraphClipboardService` for copy, paste, and duplicate behavior.
- `DialogGraphCanvasService` for graph canvas responsibilities.
- `DialogAudioController` and `DialogHistoryController` runtime helpers behind the existing `DialogManager` surface.
- `IDialogGraphValidator` with `DefaultDialogGraphValidator`, while retaining the existing static validator facade for compatibility.

### Changed
- `DialogGraphView` now delegates edge, clipboard, and canvas responsibilities to focused services.
- Runtime audio and history handling are separated from the main dialogue manager coordination path.

---

## [2.2.0] - 2026-05-09

### Summary
Migration and validation hardening update. This release improves graph identity, schema safety, import/export resilience, and validation detail without changing the current graph schema version.

### Added
- Forward-compatibility guard for graphs created with newer schema versions.
- Explicit graph, node, choice, link, and port identity upgrade flow through `DialogGraphUpgradeService`.
- Upgrade safety snapshot checks before migration changes are accepted.
- Choice/link consistency validation for missing or orphaned choice output links.
- `DialogGraphMutationBatch` to consolidate save operations during editor mutations.
- Schema reporting and detection helpers for safer load/import decisions.

### Changed
- JSON import/export paths now run graph upgrade checks before applying imported data.
- Validation reports more precise structural issues for choice links and graph schema state.

---

## [2.1.0] - 2026-05-07

### Summary
Logic & Runtime Foundation update. This release adds runtime dialogue variables, condition branching, Set Variable nodes, variable text injection, and a stronger validation workflow so dialogue graphs can react to game state without relying on custom action payloads for every branch.

---

### Added - Runtime Variables

- **`DialogVariableSO`** - reusable variable definition asset with bool, int, float, and string defaults.
- **`DialogueVariableStore`** - scene-level runtime store for dialogue variables with public get/set API.
- **Typed variable API** - `SetBool`, `SetInt`, `SetFloat`, `SetString`, `GetBool`, `GetInt`, `GetFloat`, `GetString`, and try-get variants.
- **Runtime reset support** - `ResetToInitialVariables()` reapplies definition assets and inline startup values.
- **Change notifications** - `VariableChanged` event fires when a stored variable changes.

---

### Added - Condition / If Nodes

- **`ConditionNode`** - hidden runtime flow node that routes dialogue through True or False outputs.
- **Boolean operators** - Is True, Is False, Equals, Not Equals.
- **Integer and float operators** - Equals, Not Equals, Greater, Less.
- **String operators** - Equals, Not Equals, Contains.
- **Missing-variable fallback** - condition nodes can choose the result used when a variable is missing or cannot be evaluated.
- **Editor node view** - graph editor support for variable key, value type, operator, comparison value, fallback result, True output, and False output.

---

### Added - Set Variable Nodes

- **`VariableMutationNode`** - hidden runtime flow node shown in the editor as Set Variable.
- **Set operation** - stores bool, int, float, or string values.
- **Add/Subtract operations** - updates integer and float variables.
- **Toggle operation** - flips Boolean variables.
- **Clear String operation** - clears string variables.
- **Editor node view** - graph editor support for variable key, registered variable definition, type, operation, and value fields.

---

### Added - Variable Injection

- **Dialogue text tokens** - runtime text can include variable values with syntax such as `{playerName}` and `{coins}`.
- **Choice text tokens** - choice answer text is formatted through the same variable injection path.
- **Additional token forms** - supports `{var:variableKey}` and `{{variableKey}}`.
- **Authoring-friendly missing fallback** - missing variables keep the original token visible by default.
- **Invariant numeric formatting** - int and float values are formatted for stable runtime display.

---

### Added - Validation

- **Validation panel** - dockable validation window with Error, Warning, and Info filtering.
- **Click-to-focus** - validation rows can select and frame the affected node in the graph editor.
- **Condition checks** - missing True/False outputs, invalid condition ports, incompatible operator/type combinations, and parse errors.
- **Variable checks** - unknown variable keys, registered type mismatches, empty variable names, invalid mutation operations, and parse errors.
- **Flow checks** - duplicate GUIDs, unknown references, unreachable nodes, dead ends, missing connections, invalid Start/End flow, and multiple outgoing links where unsupported.
- **Layout checks** - node overlap warnings for cleanup before release.

---

### Changed

- `DialogGraph` now stores condition nodes, variable mutation nodes, and graph-level available variables alongside existing dialog, choice, and action nodes.
- `DialogManager` runtime traversal now resolves hidden Action, Set Variable, and Condition nodes before showing the next UI-facing dialog or choice node.
- Choice rendering now resolves variable tokens in answer text.
- Dialog line rendering now resolves variable tokens immediately before display so values updated by earlier Set Variable nodes are reflected.

---

### Fixed

- Clear String no longer silently converts Boolean, Integer, or Float variables into String variables at runtime. It now only succeeds for String variables and fails safely for other types.

---

## [2.0.0] - 2026-05-04

### Summary
This is the largest release since the initial version. Every major surface of the editor and runtime has been updated: the graph window was fully rearchitected with a split-view sidebar, the toolbar was redesigned with an icon system, node views were polished across all five types, a four-tab sidebar replaced the old sidebar, view-state persistence was added per graph, layout auto-formatting landed as a first-class feature, and the runtime gained three text reveal effects, a theming subsystem, and a startup onboarding window. Unity 6 (6000.0.x) compatibility is verified.

---

### Added - Editor UI

- **Restyled graph window with TwoPaneSplitView** - graph canvas occupies the left pane; the sidebar lives in the right pane. The divider is draggable. Collapsing the sidebar detaches it cleanly and expands the canvas to full width.
- **Sidebar toggle button** - "Hide Sidebar" / "Show Sidebar" with matching collapse and expand icons. Width is remembered across collapses.
- **Icon system** (`DialogGraphIconManager`, `DialogGraphIconId`) - toolbar buttons and sidebar buttons now render vector icons. Every built-in action (add, delete, save, open, auto-layout, collapse, expand) has a dedicated icon constant.
- **Format Layout toolbar button** - runs `DialogGraphLayoutFormatterEditor.FormatLinkedSubgraphOnly` on the current graph and immediately saves the result. Layout is also auto-triggered after AI node insertions.
- **New Graph prompt window** (`NewGraphPromptWindow`) - collision-free name suggestion, custom folder picker, and confirmation flow. Auto-saves the previously loaded graph before switching.
- **Save Graph prompt window** (`SaveGraphPromptWindow`) - shows the current name, warns if the graph is empty, and performs a final save before switching.
- **Graph popup** - dynamically refreshes choices after create/save/load without requiring a window restart.
- **View-state persistence per graph** - pan position and zoom scale are saved in `EditorPrefs` per graph name on every save/close. Restored exactly on next open. Undo/Redo preserves the camera position so the view does not snap back to the Start node.
- **Keyboard shortcuts** - `Ctrl/Cmd+Z` (undo) and `Ctrl/Cmd+Y` / `Ctrl/Cmd+Shift+Z` (redo) captured at the root level with text-field-focus detection so they never fire while typing in node fields.
- **Auto-save on close** - the editor window saves the current graph and view state before `OnDisable`.
- **Auto-save on switch** - loading or creating a new graph automatically saves the previous one first.

---

### Added - Sidebar (four tabs)

- **Characters tab**
  - "Registered" sub-tab: lists all `DialogCharacterSO` assets found in the project.
  - "In Graph" sub-tab: lists all unique speakers detected in the currently loaded graph nodes.
  - Rename a speaker in bulk - all matching `DialogNodeView` nodes are updated atomically under a single undo group.
  - Assign or replace portrait sprites per speaker, applied to all matching nodes.
  - "Assign to Selected Node" - apply a registered character definition to the selected dialog node.
  - "Create Character Asset" - scaffold a new `DialogCharacterSO` from the sidebar.
- **Actions tab**
  - "Registered" and "In Graph" sub-tabs mirror the Characters tab pattern.
  - Edit Action ID, payload JSON, wait-for-completion flag, and delay seconds per action node row.
  - Apply changes atomically with a single undo group.
  - Insert a registered action after the currently selected node with one click.
  - "Autofill Available Actions" - matches action IDs in the graph to registered `DialogActionSO` assets and writes them into `graph.availableActions`.
  - "Register Missing Action Assets" - creates new `DialogActionSO` stubs for any action IDs used in the graph that have no matching asset.
  - Create a new `DialogActionSO` directly from the sidebar.
- **Context tab**
  - `DialogSceneContextSO` object field, `DialogEnvironmentSO` object field.
  - Scene goal, tone, and extra rules free-text fields.
  - Values are passed into the AI prompt builder when the AI tab is active.
- **AI tab** - bridged from the core sidebar into the optional AI extension via `IDialogGraphAiBridge` / `DialogGraphAiBridgeLocator`. Shows a graceful "install the extension" message if the extension is absent.
- **Search field** - live-filters visible rows across Characters and Actions tabs.
- **Summary label** - shows count of matched rows after filtering.
- **Rescan and Apply** buttons in the sidebar header.

---

### Added - Graph View and Nodes

- **`BaseNodeView`** - shared base class for all five node types. Handles USS class application, icon loading, and dirty-repaint scheduling.
- **`DialogNodeView`** - speaker name, text body, portrait sprite field, portrait preview image, audio clip field, display time, Auto Next port, and entry badge. Supports `ApplyCharacterDefinition` and `ApplySidebarCharacterEdits` for bulk sidebar operations.
- **`ChoiceNodeView`** - prompt text and dynamic per-choice answer text fields with individual output ports.
- **`ActionNodeView`** - action ID, payload JSON, wait-for-completion toggle, wait seconds field. Supports `ApplySidebarEdits`.
- **`StartNodeView`** and **`EndNodeView`** - styled distinctly from content nodes.
- **`DialogGraphLayoutFormatterEditor`** - layout algorithm that positions nodes in topological order along the linked subgraph only, leaving unlinked nodes untouched. `DefaultSettings` is exposed for external callers (e.g. AI extension).

---

### Added - Services and Utilities

- **`DialogActionDiscoveryService`** - scans the project for all `DialogActionSO` assets.
- **`DialogActionRegistryService`** / **`DialogCharacterRegistryService`** / **`DialogEnvironmentRegistryService`** - create new definition assets at the correct folder paths.
- **`DialogDefinitionRegistryUtility`** - shared lookup helpers across the definition types.
- **`DialogGraphAssetPaths`** - centralizes all asset path constants, folder-creation helpers, and a `GetVisibleGraphNames()` cache with explicit invalidation.
- **`DialogGraphDefinitionResolver`** - resolves definitions by ID across the project (`FindActionById`, `FindCharacterById`, `CreateSuggestedActionId`, `CreateSuggestedCharacterId`, `PingAndSelect`).
- **`IDialogGraphAiBridge` / `DialogGraphAiBridgeLocator`** - optional compile-time seam between the core editor and the AI extension. The core package compiles and runs with no changes whether the AI extension is installed or not.
- **`DialogUndoUtility`** - helpers for undo group collapsing and undo-aware asset recording.
- **`DialogGraphIconManager`** - loads the icon atlas, creates `Image` elements, swaps icons at runtime, and exposes `HasIcon` for guarded access.
- **`DialogGraphAiContextBuilder`** - builds a structured context string from the loaded graph, active sidebar fields, and definitions, used as the system prompt for AI commands.

---

### Added - Validation

- **`DialogGraphValidator`** - walks the graph asset and collects `DialogGraphValidationIssue` objects with Error / Warning / Info severity for disconnected nodes, missing speakers, missing action IDs, choice ports without targets, and structural gaps.
- **`ValidateGraphResultWindow`** - modal result window listing issues by severity with click-to-select node navigation.

---

### Added - Export / Import

- **`DialogJsonImportBridge`** - clean import layer between the raw JSON utility and the editor window. Handles GUID policy application and graph refresh after import. Validates graph structure before applying to prevent malformed imports from corrupting the live asset.

---

### Added - Onboarding

- **`DialogWelcomeWindow`** - branded first-run window with product name, summary, feature highlights, and a "Do not show on startup" checkbox. Current menu path: `Tools > BekaForge > Dialogue Welcome`.
- **`DialogWelcomeStartup`** - automatically opens the welcome window on the first project load after import, gated by an `EditorPrefs` key.
- **`DialogWelcomeBrandingSO`** / **`DialogWelcomeBrandingEditorService`** - ScriptableObject-driven branding content loaded from `Resources/Brand` at editor startup.

---

### Added - Settings

- **AI panel** - detects the AI extension via `OptionalAiSupport.IsInstalled`. If installed: provider selection, connection status card, structured JSON toggle, AI enabled toggle, temperature, and max-token settings. If absent: a clear install prompt instead of a broken panel.
- **`OptionalAiSupport`** - reflection-based bridge to AI extension types. Supports `FindProviders()`, `TryCreateProviderAsset()` provider templates, `TryAssignDefaultProvider()`, `GetDefaultProviderAsset()`, `TrySendPrompt()`, and `CloneProviderWithLocalSecrets()`. API keys are copied from `EditorPrefs` into a transient clone and are not written into project assets.
- **`DialogAiEditorLocalSettings`** - stores `aiEnabled`, `useStructuredJsonResponse`, `temperature`, `maxTokens`, `lastConnectionTestMessage`, and per-provider API keys in `EditorPrefs`.
- **About panel** - product version, documentation link, license, and asset store link.

---

### Added - Runtime

- **Three text reveal effects** - `TypingRevealEffect` (character-by-character, configurable speed), `FadeInRevealEffect` (alpha fade-in per character), `WordRevealEffect` (word-by-word). All implement `ITextRevealEffect`.
- **`DialogEventSystemBootstrap`** - ensures a valid `EventSystem` is present at runtime. Auto-creates one if missing, with automatic detection of Legacy and New Input System backends.
- **Theming subsystem** - `DialogThemeSO` ScriptableObject with per-node color tokens and font overrides, applied at runtime via `DialogUIController`.
- **`DialogSettingsRuntime`** - singleton loaded from `Resources/DialogSettingsSO`. Exposes master settings to the runtime without hard-coded references.
- **`ChoiceSelection`** subsystem - runtime keyboard, gamepad, and XR-aware choice navigation.

---

### Changed

- Toolbar buttons replaced IMGUI elements with pure UIElements (`Button`, `Label`, `Image`) styled via USS. Text overflow on narrow windows uses ellipsis.
- `DialogGraphEditorWindow` converted from a monolithic class into a coordinator that delegates to `DialogGraphView`, `EditorSidebarWindow`, and the service layer.
- Undo/Redo path captures view state before reload and restores camera position after, eliminating the "scroll to Start on undo" regression.
- `DialogJsonIOWindow` import tab re-routes through `DialogJsonImportBridge` for safer asset handling.
- USS classes migrated to consistent `.dlg-` prefix (`dlg-graph`, `dlg-toolbar`, `dlg-sidebar`, `dlg-btn`, `dlg-split`, `dlg-toolbar-group`, `dlg-spacer`). Node classes use `node-base`, `node-dialog`, `node-choice`, `node-action`.
- `FormatLayout` exposed as a public method on `DialogGraphEditorWindow` so the AI extension can call it after inserting nodes.

---

### Fixed

- Camera state no longer resets to the Start node after Undo/Redo.
- Sidebar collapse and re-expand no longer leaves a detached split element in the visual hierarchy.
- Graph popup no longer shows stale names after creating or saving a graph under a new name.
- Text-field focus detection prevents `Ctrl+Z` / `Ctrl+Y` from firing undo/redo while typing in node fields.
- `NewGraphPromptWindow` no longer allows empty names or names that conflict with existing graph assets.
- `InsertRegisteredActionAfterSelectedNode` correctly rejects choice nodes with an explanatory message.
- Action rows in the sidebar correctly match by original action ID on apply, preventing cross-node field bleed.
- Portrait preview in `DialogNodeView` updates immediately on sprite assignment without requiring a manual repaint.

---

### Small Details Caught in Audit

- `GetAllGraphAssetNamesFallback()` backed by a cache with explicit invalidation so repeated toolbar refreshes do not call `AssetDatabase.FindAssets` on every frame.
- `CharacterBinding` and `ActionBinding` are `[Serializable]` nested classes so they survive domain reloads mid-session.
- `FindFirstSpriteForSpeaker` scans nodes lazily and returns the first matched portrait without a full registry lookup.
- `CollectSpeakersFromNodes()` is an `IEnumerable<string>` so the sidebar does not allocate a full list when only the count is needed.
- `FormatLayout(string preserveNodeGuid)` accepts an optional GUID so the AI extension can keep the anchor node at a stable position after inserting new nodes around it.
- `RestoreGraphViewState` uses `schedule.ExecuteLater(16 ms)` so the view transform is applied after the layout pass.
- `IsTextInputFocused()` checks both the `TextField` type and type name substring to also catch third-party text input widgets that do not inherit from `TextField`.
- Provider clone in `OptionalAiSupport.TrySendPrompt` is `DestroyImmediate`d in a `finally` block so it never leaks into the scene even if the HTTP call throws.

---

## [1.4.0] - 2025-11-19

### Added
- Global Settings Panel: centralized configuration for Text, Audio, Choices, and Input with live preview.
- Choice Settings: new animations, highlight modes, arrow-key navigation for keyboard, controller, and XR.
- Configurable text behaviors: typing speed, instant reveal, skip logic.

### Changed
- Import workflow simplified and made safer.
- Graph load now auto-creates a valid default asset if the file is missing or empty.
- Cleaner initialization flow and reduced clutter in inspectors.

### Fixed
- Undo/Redo reliability for node creation, port linking, and field editing.
- Save/reload no longer fails to serialize or loads incompletely.
- Choice navigation no longer skips or desyncs on gamepad or XR input.
- Minor runtime and editor issues with node initialization and UI assets.

---

## [1.3.2] - 2025-11-08

### Added
- Universal input compatibility (Old Input Manager and New Input System, no manual changes required).
- `InputHelper` utility centralizing keyboard, mouse, touch, gamepad, and VR input for dialog advancement.
- Cooldown logic preventing accidental double-skip.

### Changed
- Fixed compile issues in New-Input-System-only projects.
- Verified stable across Unity 2021-2025 LTS.

---

## [1.3.1] - 2025-09-16

### Added
- Action Nodes and Handlers: `ActionNode` with `actionId` + `payloadJson`, coroutine-based `IActionHandler`.
- Demo handlers: `DemoHandler_Countdown` (blocking countdown), `DemoUnityEventActions` (UnityEvents).
- Runtime UI Bridge: `DialogUIController` with show/hide, set text, set speaker, portrait, autoplay, skip.
- Helper utilities: `PayloadHelper`, `TextResources`, small audio samples.
- Per-type node views: Start, Dialog, Choice, Action, End, and `DialogEdge`.
- `ActionDialogDemo` scene showcasing action chains.

### Changed
- Split node classes into typed hierarchy with `BaseNode` and explicit `NodeKind`.
- All GraphView code moved under `Scripts/Editor`.

### Fixed
- Link handling between typed node views and edges.
- Autoplay icon initialization and panel click listener binding.

---

## [1.3.0]

### Added
- Characters Sidebar: rescan speakers, set portrait, apply to nodes.
- JSON Import and Export via `DialogJsonIOWindow`.
- Runtime UI Panel (UGUI) with typing effect, choices, skip, autoplay.
- `DialogManager` API: play by graph or ID with line-start and line-complete hooks.
- Sample scene with three demo conversations and portraits.
- Minimap and improved toolbar (Add Node, Save, Clear, Hide/Show Characters).
- Assembly definitions and namespaces: `DialogSystem.Runtime`, `DialogSystem.Editor`.

### Changed
- Refined node layout and port styling.
- No debug logs in release builds; editor code isolated.

### Fixed
- Edge instability on fast undo/redo.
- Minor GUID and entry-node validation issues.

---

## [1.2]

### Added
- Dialogue History: `DialogueHistory`, `DialogueHistoryView`, `DialogueHistoryPanel` prefab.
- JSON IO Window with export/import tabs, JSON preview, drag-and-drop, recent files, backup safety.
- GUID Policies on Import: Preserve, Regenerate on conflict, Regenerate all.
- Graph Editor: minimap, duplicate selection, safe delete.
- Node Fields: display time, audio clip, portrait preview, entry badge, Auto Next port.
- UI Prefabs: `Dialogue_Panel`, `Choice_Btn`, `DialogueHistoryPanel`.
- Runtime Events: line shown, choice picked, conversation reset, Play by Type.

### Changed
- Folders reorganized into `_Scripts/Runtime` and `_Scripts/Editor`.
- `DialogManager` refactored into `MonoSingleton` with cleaner UI references.

### Fixed
- Safer asset deletion (prevents dangling references).
- Multiple UX polish improvements.

---

## [1.0]

### Initial Release
- Basic dialog graph asset with entry node and choices.
- Early graph editor (add/remove nodes, connect ports).
- Simple runtime playback (speaker, text, choices).
- Basic JSON export utility.
- Minimal demo UI.




