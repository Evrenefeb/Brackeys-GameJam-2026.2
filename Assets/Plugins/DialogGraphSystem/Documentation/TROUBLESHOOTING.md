# Dialogue Graph System Troubleshooting

## Missing TextMesh Pro

Install TextMesh Pro from Package Manager and import TMP Essential Resources from `Window > TextMeshPro > Import TMP Essential Resources`. Fresh Unity 2022.3+ core template projects do not include TextMesh Pro by default.

## Missing UGUI

Install the Unity UI package (`com.unity.ugui`) from Package Manager. The runtime prefabs use UGUI and TextMesh Pro, and fresh Unity 2022.3+ core template projects do not include UGUI by default.

## Many Missing Runtime Types Right After Import

If Unity reports many missing runtime types immediately after import, such as `DialogGraph`, `DialogAudioSettings`, `DialogUIManager`, `ToggleSwitch`, `DialogActionRunner`, or `DialogSystem.Runtime.Transcript`, the imported `.unitypackage` is incomplete.

This is a package export problem, not a per-type scripting issue. Use a core package export generated from `Assets/DialogGraphSystem` only. Do not distribute or import a package that also contains `Packages/com.unity.*`, `Assets/DialogSystemAIExtension`, or other non-core roots.

For internal release builds, use `Tools > BekaForge > Dialogue Graph System > Packaging > Export Core Package...`.

## Vector Graphics

Vector Graphics is not required. If a project still reports a Vector Graphics dependency, search for stale custom references outside the shipped Dialogue Graph System package.

## Graph Does Not Run

- Confirm the scene has a `DialogManager`.
- Confirm the runtime UI prefab is present.
- Confirm the graph is assigned or registered in the manager dialogue set.
- Run graph validation and fix errors.
- Check that every playable branch reaches another node or End.

## Import Fails

- Load and validate the JSON before import.
- Import unknown JSON as a new graph first.
- For overwrite failures, read the failure stage and backup path in the dialog.
- If a backup exists, open the backup graph and compare it with the target graph.

## Sample Scene Missing Reference

Open `Assets/DialogGraphSystem/DemoScenes/DialogueDemo.unity` and check the Console and Inspector for missing references. If a sample graph was intentionally removed, remove the corresponding dialogue set entry from `DialogueManager`.

## Online Documentation Is Unavailable

Use **View Bundled Copy** beside the relevant documentation, changelog, license, support, upgrade, or AI information action. In-app online requests automatically fall back after a connection error, protocol error, data-processing error, timeout, or empty response.

## AI Extension Entry Point Is Unavailable

AI Extension 2.0 is coming soon. It is not included in Core 3.0.0 and is not required for manual graph authoring, localization, validation, JSON import/export, or runtime playback. Use **Learn More** or **View Bundled Copy** for the current readiness notice.

