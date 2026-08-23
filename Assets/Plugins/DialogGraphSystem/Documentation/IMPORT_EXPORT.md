# Dialogue Graph System Import And Export

Dialogue Graph System supports JSON export for review, backup, and transfer between projects.

## Export Workflow

1. Open `Tools > BekaForge > Dialogue Graph System > Launcher`.
2. Switch to `Import / Export`.
3. Select a graph asset.
4. Choose export options.
5. Save the JSON file outside your production graph folder when using it as a backup.

## Import As New Graph

Use this for unknown or externally edited JSON.

1. Load and validate the JSON.
2. Choose an import folder.
3. Enter a graph name.
4. If no graph exists at that path, the importer creates a new graph asset.

## Overwrite Existing Graph

Use overwrite only when you intentionally want to replace an existing graph asset.

The overwrite flow is transaction-style, not truly database-atomic:

1. Captures the original graph state.
2. Parses and validates JSON.
3. Builds and migrates a temporary validation graph.
4. Creates a mandatory backup of the target graph.
5. Verifies the backup can be loaded.
6. Mutates the target graph only after those steps succeed.
7. Saves and refreshes the AssetDatabase.

## Backup Behavior

Backups are written to a `Backups` folder beside the target graph through Unity `AssetDatabase` APIs. The import success dialog or Console log includes the backup path.

Backups are retained after successful overwrite. Old backups may be cleaned up by the importer while keeping the newest backups.

## Failure Behavior

If parsing, DTO validation, temporary build, temporary migration, or backup verification fails, the original graph is not modified.

If a later commit step fails after target mutation has started, the importer attempts to restore the target graph content from the verified backup and reports the backup path.

Failure messages include:

- Failure stage.
- User-facing error message.
- Backup path when a backup exists.
- Additional details when available.

## Recovery Steps

1. Read the failure dialog and note the backup path.
2. Do not continue editing the target graph until you inspect it.
3. Open the backup graph if one was created.
4. Re-export a clean JSON file if needed.
5. Retry import as a new graph before retrying overwrite.
