# Dialogue Graph System Upgrade Guide

Use this guide when importing Dialogue Graph System 3.0.0 over a 2.4.1 project.

## Before Upgrading

1. Commit your project or create a full project backup.
2. Export important production graphs to JSON.
3. Upgrade in a copy or branch before touching a production project.
4. Remove only the obsolete vendor-owned 2.4.1 release files listed below, if they exist.
5. Let Unity finish recompiling before opening graph assets.

### Remove obsolete 2.4.1 vendor files

A Unity package import overwrites matching paths but cannot delete files that were present only in an older release. Before importing 3.0.0, remove these obsolete 2.4.1 vendor files and their paired `.meta` files if they are present:

- `Assets/DialogGraphSystem/Tests`
- `Assets/DialogGraphSystem/Assets.csc.rsp`
- `Assets/DialogGraphSystem/Scripts/Editor/AI/DialogNodeRewriteService.cs`
- `Assets/DialogGraphSystem/Scripts/Editor/SettingsWindow/DialogAiEditorLocalSettings.cs`
- `Assets/DialogGraphSystem/Scripts/Editor/SettingsWindow/OptionalAiSupport.cs`
- `Assets/DialogGraphSystem/Scripts/Editor/SettingsWindow/Panels/AiPanel.cs`

These paths contained package-owned tests, a compiler response file, and the retired 2.4.1 AI preview implementation. Leaving them behind can compile stale editor code or run obsolete tests after the Core 3.0.0 overlay. If your team modified any listed file, move or diff those changes outside the package root before removal.

Do not delete user-owned graphs, definitions, exports, or localization tables. In particular, preserve `Assets/DialogGraphSystem/Graphs`, production assets under `Definitions`, and any custom files your project owns; source control and JSON exports are the recovery boundary.

## Migration Behavior

The 3.0 release migrates older graph assets to the current schema when they are opened, saved, imported, or explicitly upgraded by editor services. Migration can assign missing graph, node, choice, link, and port identity values.

The upgrade flow is designed to preserve existing playable graph content. If Unity reports migration warnings, review the graph in the editor and run validation before shipping.

## Breaking Or Risky Areas

- Custom runtime UI bindings should be tested after import.
- Custom action handlers should be tested in Play Mode.
- Localization tables should be validated after graph edits.
- Core 3.0.0 does not require the AI extension, an AI provider, or remote AI configuration.

## Recommended Upgrade Checklist

1. Complete the obsolete-file cleanup above without deleting user content.
2. Import the package.
3. Import TMP Essential Resources if Unity prompts you.
4. Open `Tools > BekaForge > Dialogue Graph System > Launcher`.
5. Open each production graph.
6. Run validation.
7. Save migrated graphs only after reviewing validation results.
8. Test runtime dialogue playback in a scene copy.
9. Close and reopen Unity, then reopen representative graphs and confirm their data remains intact.
10. Exercise both a same-graph route and a Graph Jump route in Play Mode.
11. Switch runtime locale and verify localized dialogue and choices.
12. Export migrated graphs to JSON, import them as copies, and compare the resulting structure.

## AI Extension Boundary

AI Extension 2.0 is not part of the 3.0.0 upgrade. It remains one ready/not-ready checkpoint for DGS 3.0.1. Do not add an AI package, provider, key, or remote configuration to validate the Core upgrade.

## Recovery

Use source control, project backups, and JSON exports as recovery points. JSON overwrite import also creates a graph asset backup before replacing an existing graph.

