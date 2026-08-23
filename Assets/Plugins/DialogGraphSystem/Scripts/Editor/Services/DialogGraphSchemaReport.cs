using DialogSystem.Runtime.Models;

namespace DialogSystem.EditorTools.Services
{
    /// <summary>
    /// Read-only schema inspection result for a <see cref="DialogGraph"/>.
    /// </summary>
    public sealed class DialogGraphSchemaReport
    {
        #region ---------------- Public API ----------------

        public int GraphSchemaVersion { get; }
        public int CurrentSchemaVersion { get; }
        public bool IsLegacy { get; }
        public int LinkCount { get; }
        public int LinksMissingLinkGuidCount { get; }
        public int DuplicateLinkGuidCount { get; }
        public int MissingGraphGuidCount { get; }
        public int MissingNodeGuidCount { get; }
        public int DuplicateNodeGuidCount { get; }
        public int MissingChoiceIdCount { get; }
        public int DuplicateChoiceIdCount { get; }
        public int MissingFromPortKeyCount { get; }
        public int MissingToPortKeyCount { get; }
        public int NullLinkCount { get; }
        public int EmptyLinkCount { get; }
        public bool HasEmptyOrNullLinks => NullLinkCount > 0 || EmptyLinkCount > 0;
        public bool IsNewerSchema => GraphSchemaVersion > CurrentSchemaVersion;
        public bool IsUpgradeNeeded { get; }

        public DialogGraphSchemaReport(
            int graphSchemaVersion,
            int currentSchemaVersion,
            bool isLegacy,
            int linkCount,
            int linksMissingLinkGuidCount,
            int duplicateLinkGuidCount,
            int missingGraphGuidCount,
            int missingNodeGuidCount,
            int duplicateNodeGuidCount,
            int missingChoiceIdCount,
            int duplicateChoiceIdCount,
            int missingFromPortKeyCount,
            int missingToPortKeyCount,
            int nullLinkCount,
            int emptyLinkCount,
            bool isUpgradeNeeded)
        {
            GraphSchemaVersion = graphSchemaVersion;
            CurrentSchemaVersion = currentSchemaVersion;
            IsLegacy = isLegacy;
            LinkCount = linkCount;
            LinksMissingLinkGuidCount = linksMissingLinkGuidCount;
            DuplicateLinkGuidCount = duplicateLinkGuidCount;
            MissingGraphGuidCount = missingGraphGuidCount;
            MissingNodeGuidCount = missingNodeGuidCount;
            DuplicateNodeGuidCount = duplicateNodeGuidCount;
            MissingChoiceIdCount = missingChoiceIdCount;
            DuplicateChoiceIdCount = duplicateChoiceIdCount;
            MissingFromPortKeyCount = missingFromPortKeyCount;
            MissingToPortKeyCount = missingToPortKeyCount;
            NullLinkCount = nullLinkCount;
            EmptyLinkCount = emptyLinkCount;
            IsUpgradeNeeded = isUpgradeNeeded;
        }

        #endregion
    }
}
