# Dialogue Graph System (v3.0.0)

Node-based dialogue authoring and runtime playback for Unity. Build branching conversations in a visual graph editor and ship them with a polished UGUI + TextMeshPro runtime: portraits, audio, choices, skip, autoplay, history, themes, action-driven scene events, runtime variables, conditional flow, and variable text injection.

Current package release 3.0.0 adds an end-to-end localization pipeline, Graph Jump cross-graph traversal, a unified multi-tab editor workspace, graph categorization metadata, a runtime player settings panel, broader regression coverage across traversal, validation, import/export, and UI settings, plus the packaging/docs pass needed for publish.

## Features

- Node-based graph editor with Start, Dialog, Choice, Action, Condition, Set Variable, Outcome, and End nodes
- Runtime condition branching with true/false outputs
- Runtime variable system with bool, int, float, and string values
- `DialogVariableSO` assets for reusable variable definitions and defaults
- `DialogueVariableStore` component with public get/set API for gameplay scripts
- Variable injection in dialogue text and choice text
- Graph Jump nodes for multi-graph conversations and reusable sub-dialogue flows
- Outcome nodes for named endings with stable IDs, display names, and descriptions
- Localization tables, runtime locale switching, and language selector support
- Unified editor workspace with Graphs, Characters, Actions, Variables, Context, Localization, Import / Export, and Settings tabs
- Four-tab editor sidebar for Characters, Actions, Context, and AI
- UGUI + TextMeshPro runtime with typing, fade-in, and word-by-word reveal effects
- Portraits, audio clips, skip line, skip all, and autoplay
- History/backlog overlay for past lines and choices
- Action nodes for UnityEvents or blocking `IActionHandler` coroutines
- Theme assets via `DialogThemeSO`
- Runtime settings panel for text speed, audio, auto-advance, language, and theme controls
- Validation panel with Error, Warning, and Info severities plus click-to-focus navigation
- JSON import/export for backup and version control
- AI Extension 2.0 coming-soon entry points that remain optional and never block Core workflows
- Sample graph assets, runtime prefabs, and definitions included

## Requirements

- Unity 2021.3 LTS or newer
- TextMesh Pro (`com.unity.textmeshpro`)
- UGUI (`com.unity.ugui`)
- `UnityEditor.Experimental.GraphView` for the editor window

## Installation

1. Import the package into your Unity project.
2. In Package Manager, install TextMesh Pro and UGUI if your project does not already include them. Fresh Unity 2022.3+ core template projects do not include these packages by default.
3. If this is a fresh project, import the TextMesh Pro essential resources when Unity prompts you.
4. Open `Tools > BekaForge > Dialogue Graph System > Welcome`.
5. Open `Tools > BekaForge > Dialogue Graph System > Launcher` or `Tools > BekaForge > Dialogue Graph System > Graphs`.
6. For localization authoring, open `Tools > BekaForge > Dialogue Graph System > Localization`.

If the Console shows a large cascade of missing runtime types immediately after import, the `.unitypackage` itself is incomplete. The core asset should be exported from `Assets/DialogGraphSystem` only and should not include `Packages/com.unity.*` sources or optional add-on roots.

## Upgrading From 2.4.1

Back up your project before importing 3.0.0 over an existing 2.4.1 install. Use source control, a full project copy, or both. If you rely on JSON exports for review or rollback, export important graphs before upgrading.

The 3.0 release upgrades older 2.4.1 graph assets from schema `1` to schema `2`, adding graph metadata, localization data, Graph Jump data, and runtime UI settings. A Unity package import cannot delete obsolete files that existed only in 2.4.1, so follow the exact vendor-file cleanup list in `Documentation/UPGRADE_GUIDE.md` before importing. Do not delete user-owned graphs, definitions, exports, or localization tables. After importing, let Unity finish recompiling, open the editor from `Tools > BekaForge > Dialogue Graph System > Launcher`, then validate your production graphs before saving or shipping. Test custom runtime UI bindings, action handlers, localization tables, and Graph Jump routes in a copy of the project first.

See `Assets/DialogGraphSystem/Documentation/UPGRADE_GUIDE.md` for the full upgrade checklist.

## Quickstart

1. Open the workspace from `Tools > BekaForge > Dialogue Graph System > Launcher` or `Tools > BekaForge > Dialogue Graph System > Graphs`.
2. Create or load a graph.
3. Add Dialog, Choice, Action, Condition, or Set Variable nodes.
4. Add Outcome nodes to branch endings when you want a named result with a description.
5. Connect Start to the first playable node and connect every required branch.
6. Place `DialogManager` and the dialogue UI prefab in your scene.
7. Add a `DialogueVariableStore` to the scene if the graph uses variables.
8. Assign your graph in the `DialogManager` inspector.
9. Run validation before playtesting or shipping.

For the packaged settings workflow:

1. Open `Tools > BekaForge > Dialogue Graph System > Launcher` and switch to `Settings`, or use `Tools > BekaForge > Dialogue Graph System > Settings`.
2. Configure text pacing and the line advance key in `Text`.
3. Configure choice navigation and presentation timing feel in `Choices`.
4. Configure per-character typewriter audio in `Audio`.
5. Configure graph editor minimap visibility and zoom limits in `Graph View`.
6. Use the master settings asset at `Assets/DialogGraphSystem/Resources/DialogSettingsSO/DialogSystemSettings.asset` when you need to inspect the packaged settings sub-assets directly.

For localization authoring, open `Tools > BekaForge > Dialogue Graph System > Localization`. This opens the unified workspace directly on the Localization tab.

Start from code:

```csharp
DialogSystem.Runtime.Core.DialogManager.Instance.StartDialog(myGraph);
```

Or by mapped ID:

```csharp
DialogSystem.Runtime.Core.DialogManager.Instance
    .PlayDialogByID("YourDialogID", () => Debug.Log("Finished!"));
```

## Outcome Nodes

Outcome nodes are hidden runtime flow nodes used to mark a branch ending with structured result data. Each outcome stores:

- `outcomeId` for a stable script-facing identifier
- `displayName` for a player-facing label
- `description` for a summary of what happened on that ending

At runtime, subscribe to `OnDialogEndedWithResult` when you only need the structured result payload:

```csharp
DialogSystem.Runtime.Core.DialogManager.Instance.OnDialogEndedWithResult += result =>
{
    Debug.Log(result.OutcomeId);
    Debug.Log(result.OutcomeDisplayName);
    Debug.Log(result.OutcomeDescription);
};
```

When you need the full `OutcomeNode` asset after completion, use the runtime helpers on `DialogManager`:

```csharp
var dialogManager = DialogSystem.Runtime.Core.DialogManager.Instance;

if (dialogManager.TryGetLastCompletedOutcomeNode(out var outcomeNode))
{
    Debug.Log(outcomeNode.outcomeId);
    Debug.Log(outcomeNode.description);
}

if (dialogManager.TryGetOutcomeNode("good_ending", out var resolvedOutcome))
{
    Debug.Log(resolvedOutcome.displayName);
}
```

If you already have a graph reference and want to resolve outcomes directly from the asset, use:

```csharp
if (myGraph.TryGetOutcomeNodeById("good_ending", out var outcomeNode))
{
    Debug.Log(outcomeNode.description);
}
```

## Runtime Variables

Variables are stored in a scene-level `DialogueVariableStore`. The store supports:

- Boolean
- Integer
- Float
- String

You can seed the store with `DialogVariableSO` assets or inline startup values in the `DialogueVariableStore` inspector. Definition assets are applied first; inline startup values are applied afterward and can override definitions with the same key.

Runtime variable keys are case-sensitive and trimmed. Use stable keys such as `playerName`, `coins`, `hasKey`, or `questStage`.

Example gameplay integration:

```csharp
using DialogSystem.Runtime.Variables;
using UnityEngine;

public sealed class DialogueVariableExample : MonoBehaviour
{
    [SerializeField] private DialogueVariableStore variables;

    public void GiveCoins(int amount)
    {
        var current = variables.GetInt("coins");
        variables.SetInt("coins", current + amount);
    }

    public void SetPlayerName(string playerName)
    {
        variables.SetString("playerName", playerName);
    }
}
```

### Variable Lifecycle

`DialogueVariableStore` resets itself in `Awake()` by calling `ResetToInitialVariables()`. It does not automatically reset every time a graph starts. If your game needs per-conversation reset behavior, call `ResetToInitialVariables()` before starting the dialogue.

Variable values are runtime state. They are not written back into `DialogVariableSO` assets during play.

## Condition / If Node

Condition nodes branch the dialogue based on a runtime variable. Each condition has:

- Variable key
- Expected value type
- Operator
- Comparison value when needed
- Missing-variable fallback result
- True output
- False output

Supported operators:

- Boolean: Is True, Is False, Equals, Not Equals
- Integer: Equals, Not Equals, Greater, Less
- Float: Equals, Not Equals, Greater, Less
- String: Equals, Not Equals, Contains

Numbers use invariant formatting, so use `3.5`, not a locale-specific comma decimal. String comparisons and variable keys are case-sensitive.

If the variable is missing, or a numeric comparison value cannot be parsed, the condition uses its missing-variable fallback result.

## Set Variable Node

Set Variable nodes modify `DialogueVariableStore` values during dialogue flow. They are hidden runtime flow nodes: the player does not see them, and the conversation continues to the next connected node.

Supported operations:

- Set: stores a bool, int, float, or string value
- Add: adds to an int or float variable
- Subtract: subtracts from an int or float variable
- Toggle: flips a bool variable
- Clear String: clears a string variable

Invalid operations fail safely and log a warning when debug logging is enabled. For example, Toggle only supports Boolean variables, and Clear String only supports String variables.

Current missing-variable behavior:

- Set creates or replaces the variable with the configured type.
- Add/Subtract read missing numeric variables as `0`, then write the result.
- Toggle reads a missing Boolean as `false`, then writes `true`.
- Clear String writes an empty string for string variables.

Document this behavior in your game code if variables are part of save/load or quest state.

## Variable Injection

Dialogue text and choice text can include runtime variable values.

Common syntax:

```text
Hello {playerName}, you have {coins} coins.
```

If `playerName` is `Kira` and `coins` is `12`, the displayed line becomes:

```text
Hello Kira, you have 12 coins.
```

Supported token forms:

- `{variableKey}`
- `{ varWithSpacesTrimmed }`
- `{var:variableKey}`
- `{{variableKey}}`

Missing variables keep their original token visible by default, such as `{missingVariable}`. This makes authoring mistakes easier to spot.

Malformed tokens do not throw exceptions. They are left as text where possible.

## Validation Panel

Open validation from the graph editor toolbar or `Tools > BekaForge > Dialogue Graph System > Validation Panel`.

Validation reports structured issues with severity:

- Error: graph structure can break runtime flow or package safety.
- Warning: likely authoring mistake or incomplete setup.
- Info: polish or layout note.

Validation checks include:

- Missing connections
- Duplicate GUIDs
- Unknown GUID references
- Missing Start or End boundary
- Start incoming links
- End outgoing links
- Unreachable nodes
- Dead ends
- Dialog/action/set-variable multiple outgoing links
- Choice count and output port mismatch
- Empty dialog or choice text
- Missing or unknown action IDs
- Missing or unknown variable keys
- Condition branch issues
- Set Variable type/operation mismatches
- Node overlap warnings

Click an issue or its Focus button to select and frame the affected node in the graph editor.

## Runtime Settings Workflow

The package creates and uses a master settings asset at `Assets/DialogGraphSystem/Resources/DialogSettingsSO/DialogSystemSettings.asset`.

That asset owns the packaged sub-settings used by the runtime:

- `DialogTextSettings`
- `DialogChoiceSettings`
- `DialogInputSettings`
- `DialogAudioSettings`
- `DialogueRuntimeUISettings`

Use `Tools > BekaForge > Dialogue Graph System > Launcher` and switch to `Settings` for the primary editor workflow. The direct menu path `Tools > BekaForge > Dialogue Graph System > Settings` forwards to the same place. The settings window exposes `Text`, `Choices`, `Audio`, `Runtime UI`, `Graph View`, and `About` panels.

`DialogManager` also supports local text and audio overrides in its inspector. If those override fields are left empty, the runtime falls back to the shared packaged settings asset.

## Choice Timing And Typewriter Audio

Choice behavior and pacing are split across the packaged settings sub-assets:

- In `Text`, configure reveal speed, skip behavior, fast-forward, auto-advance delay, and punctuation pauses.
- In `Choices`, configure navigation wrap, hold repeat delay, first-selection behavior, confirm inputs, and selected-state visuals.
- In `Audio`, configure optional per-character typewriter audio, clip pool, play-every-N-characters frequency, pitch variance, minimum interval, and whitespace/rich-text filtering.

The default runtime settings asset in this package already includes tuned sample values for these controls, so a new importer can run the sample content immediately and then refine behavior from the settings window.

## Graph Asset Opening

Dialogue graph assets open directly in the graph editor when you double-click a `DialogGraph` asset in the Project window. Non-dialogue assets still use Unity's normal open behavior.

## Graph View Preferences

Open `Tools > BekaForge > Dialogue Graph System > Launcher` and switch to `Settings -> Graph View` to configure:

- minimap visibility
- minimum zoom
- maximum zoom

These values are stored in `EditorPrefs` per machine and apply immediately to open graph windows. They are editor preferences, not serialized graph data.

## Actions

Action nodes let dialogue drive gameplay or UI logic mid-conversation.

- Fire-and-forget: bind a UnityEvent to a `DialogActionSO`.
- Blocking: implement `IActionHandler` and let dialogue wait for the coroutine.

Action nodes are hidden runtime flow nodes like Set Variable and Condition nodes.

## Sample Assets

- `Assets/DialogGraphSystem/DemoScenes/DialogueDemo.unity` - polished three-demo showcase scene
- `Assets/DialogGraphSystem/Graphs/Demo_ProductTour.asset` - localized onboarding, branching, runtime controls, actions, and outcomes
- `Assets/DialogGraphSystem/Graphs/Demo_ShopGate.asset` - variables, conditions, mutations, six route outcomes, and exactly-once gate actions
- `Assets/DialogGraphSystem/Graphs/Demo_ControlRoomActions.asset` - visible actions, state mutation, Graph Jump, and final threshold outcomes
- `Assets/DialogGraphSystem/Graphs/Demo_ReactorAftermath.asset` - non-selectable supporting Graph Jump continuation
- `Assets/DialogGraphSystem/Definitions/` - sample characters, actions, variables, environments, and themes
- `Assets/DialogGraphSystem/Resources/Prefabs/UI/` - runtime UI prefabs

Open the demo scene and choose exactly one of the three cards: **Product Tour**, **Shop Gate**, or **Reactor Control Room**. The persistent Back, Reset, and Settings controls make every route replayable without reopening the scene. Product Tour includes English/German switching; Shop Gate offers payment, key, trust, and denied routes; Reactor Control Room exercises all five packaged action IDs and a modular Graph Jump.

See `Assets/DialogGraphSystem/Documentation/DEMO_GUIDE.md` for route setup, graph/definition mappings, language switching, and scene action receivers. Before shipping your own project, run validation on copied sample graphs after modifying them.

## AI Extension 2.0 — Coming Soon

AI Extension 2.0 is not included in Core 3.0.0 and is not required for graph authoring, localization, validation, JSON import/export, or runtime playback.

- Core has no AI package or provider dependency.
- Core uses no remote AI configuration.
- Manual authoring and translation remain fully available.
- AI Extension 2.0 remains one ready/not-ready checkpoint for DGS 3.0.1.
- No purchase or availability promise is made for Core 3.0.0.

Use the in-editor **Learn More** action for the online page or **View Bundled Copy** when offline.

## Documentation

- Documentation site: `https://bekaforge.com/dgs-docs/`
- Changelog: `https://bekaforge.com/dgs-docs/changelog.html`
- License: `https://bekaforge.com/dgs-docs/license.html`
- Support: `https://bekaforge.com/dgs-docs/support.html`
- Core package: this README, `CHANGELOG.md`, `LICENSE.md`, and the bundled TextAssets under `Documentation/Bundled`
- Quick start: `Assets/DialogGraphSystem/Documentation/QUICK_START.md`
- Demo guide: `Assets/DialogGraphSystem/Documentation/DEMO_GUIDE.md`
- Upgrade guide: `Assets/DialogGraphSystem/Documentation/UPGRADE_GUIDE.md`
- Import/export guide: `Assets/DialogGraphSystem/Documentation/IMPORT_EXPORT.md`
- Troubleshooting: `Assets/DialogGraphSystem/Documentation/TROUBLESHOOTING.md`
- AI Extension 2.0 coming-soon notice: `Assets/DialogGraphSystem/Documentation/Bundled/AI_EXTENSION.txt`

## Known Technical Debt

- Node layout positions are still serialized on runtime node assets through `BaseNode.nodePosition`. This is intentional for the current release so existing graph assets keep their editor layout and public APIs remain stable. Runtime traversal should not depend on node positions. A future editor/runtime separation pass can migrate layout persistence into editor-owned records.

## Troubleshooting

- If Unity reports many missing runtime types right after import, reimport a corrected core package export. This failure is caused by an incomplete `.unitypackage`, not by individual missing classes in the source tree.
- If runtime text appears missing in a fresh project, import the TextMesh Pro essential resources from `Window -> TextMeshPro -> Import TMP Essential Resources`.
- If a Condition node always chooses the wrong branch, verify the variable key, type, operator, comparison value, and missing-variable fallback.
- If variable injection displays `{token}` unchanged, verify a `DialogueVariableStore` exists in the scene and contains a matching case-sensitive key.
- If a Set Variable node appears to do nothing, run validation and check the Console for operation/type mismatch warnings.
- If per-character typewriter audio does not play, verify `Audio -> Enable Typewriter Audio` is enabled, at least one clip is assigned, and the runtime `DialogManager` has a valid audio source available.
- If the minimap or zoom limits do not appear to update, confirm the values were saved from `Graph View`. Open graph windows should react immediately; reopening the window is a fallback if Unity editor state is stale.
- If an online documentation request fails, times out, or returns an empty response, the in-editor viewer automatically shows the corresponding bundled copy. Browser actions also provide **View Bundled Copy**.
- AI Extension 2.0 is coming soon and is not required for Core 3.0.0.

