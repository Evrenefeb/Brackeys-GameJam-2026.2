# Dialogue Graph System Quick Start

This guide creates and runs a small dialogue using only the files shipped in the package.

## Requirements

- Unity 2021.3 LTS or newer.
- TextMesh Pro package and TMP Essential Resources.
- UGUI package.

Vector Graphics is not required.

## Create A Graph

1. Open `Tools > BekaForge > Dialogue`.
2. Open the `Graphs` tab.
3. Create a new graph in `Assets/DialogGraphSystem/Graphs`.
4. Add one Dialog node and enter speaker/text content.
5. Add one Choice node when you need player options.
6. Connect Start to the first node and connect every playable branch to the next node or End.
7. Run validation before playtesting.
8. Save the graph.

## Run In The Demo Scene

1. Open `Assets/DialogGraphSystem/DemoScenes/DialogueDemo.unity`.
2. Enter Play Mode and choose **Product Tour**, **Shop Gate**, or **Reactor Control Room**.
3. Use **Back to Demo Menu**, **Reset / Re-run**, and **Settings** to inspect routes without reopening the scene.
4. On the Shop Gate card, enable the key or trust setup toggle when you want those success routes; leave them disabled to inspect denial.
5. Use Product Tour's Runtime Settings flow to switch between English and Deutsch.

The scene exposes exactly three selectable demos. `Demo_ReactorAftermath` is a registered supporting Graph Jump target, not a fourth menu entry. See `DEMO_GUIDE.md` for the graph, character, variable, action, environment, and outcome mapping.

To run your own graph in the same scene, select the `DialogManager` object, add the graph to its registry, and either call it from your own UI or copy the showcase controller pattern.

## Import And Export Safely

1. Open the launcher and switch to `Import / Export`.
2. Export a graph to JSON before risky changes.
3. Import as a new graph when testing unknown JSON.
4. Use overwrite only when you want to replace an existing graph.
5. Overwrite creates a backup before changing the target graph.

See `IMPORT_EXPORT.md` for the detailed backup and failure behavior.
