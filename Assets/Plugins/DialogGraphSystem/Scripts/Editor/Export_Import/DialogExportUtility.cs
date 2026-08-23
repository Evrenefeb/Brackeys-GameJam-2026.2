using System;
using System.Collections.Generic;

namespace DialogSystem.EditorTools.ExportImport
{
    [Serializable]
    public class DialogGraphExport
    {
        public string graphGuid;
        public int schemaVersion;
        public string graphTitle;
        public string description;
        public string author;
        public List<string> tags = new();
        public string primaryCategory;
        public List<string> categories = new();
        public string lastModifiedUtc;
        public string editorVersion;
        public string sceneGoal;
        public string tone;
        public string extraRules;

        public ExportStartNode startNode;
        public ExportEndNode endNode;

        public List<DialogExportDialogNode> dialogNodes = new();
        public List<DialogExportChoiceNode> choiceNodes = new();
        public List<DialogExportActionNode> actionNodes = new();
        public List<DialogExportConditionNode> conditionNodes = new();
        public List<DialogExportVariableMutationNode> variableMutationNodes = new();
        public List<DialogExportGraphJumpNode> graphJumpNodes = new();
        public List<DialogExportOutcomeNode> outcomeNodes = new();

        public List<ExportLink> links = new();
        public List<ExportGroupLayoutRecord> groupLayouts = new();
    }

    [Serializable]
    public class DialogExportDialogNode
    {
        public string title;
        public string guid;
        public string speaker;
        public string question;
        public float nodePositionX;
        public float nodePositionY;
        public float displayTime;
    }

    [Serializable]
    public class DialogExportChoiceNode
    {
        public string guid;
        public string text;
        public float nodePositionX;
        public float nodePositionY;
        public List<ExportChoice> choices = new();
    }

    [Serializable]
    public class ExportChoice
    {
        public string choiceId;
        public string portKey;
        public string answerText;
        public string nextNodeGUID;
    }

    [Serializable]
    public class ExportLink
    {
        public string linkGuid;
        public string fromGuid;
        public string toGuid;
        public string fromPortKey;
        public string toPortKey;
        public int fromPortIndex;
    }

    [Serializable]
    public class ExportStartNode
    {
        public string guid;
        public float nodePositionX;
        public float nodePositionY;
        public bool isInitialized;
    }

    [Serializable]
    public class ExportEndNode
    {
        public string guid;
        public float nodePositionX;
        public float nodePositionY;
        public bool isInitialized;
    }

    [Serializable]
    public class DialogExportActionNode
    {
        public string guid;
        public string actionId;
        public string payloadJson;
        public bool waitForCompletion;
        public float waitSeconds;
        public float nodePositionX;
        public float nodePositionY;
    }

    [Serializable]
    public class DialogExportConditionNode
    {
        public string guid;
        public string variableName;
        public string valueType;
        public string conditionOperator;
        public string comparisonValue;
        public bool missingVariableResult;
        public float nodePositionX;
        public float nodePositionY;
    }

    [Serializable]
    public class DialogExportVariableMutationNode
    {
        public string guid;
        public string variableName;
        public string valueType;
        public string operation;
        public string value;
        public float nodePositionX;
        public float nodePositionY;
    }

    [Serializable]
    public class DialogExportGraphJumpNode
    {
        public string guid;
        public float nodePositionX;
        public float nodePositionY;
        public DialogExportGraphReference targetGraph;
    }

    [Serializable]
    public class DialogExportGraphReference
    {
        public string graphGuid;
        public string runtimeDialogId;
        public string graphName;
        public string assetPath;
        public string entryGuid;
    }

    [Serializable]
    public class DialogExportOutcomeNode
    {
        public string guid;
        public string outcomeId;
        public string displayName;
        public string description;
        public float nodePositionX;
        public float nodePositionY;
    }

    [Serializable]
    public class ExportGroupLayoutRecord
    {
        public string groupId;
        public string title;
        public string category;
        public float x;
        public float y;
        public float width;
        public float height;
        public float colorR;
        public float colorG;
        public float colorB;
        public float colorA;
        public List<string> nodeGuids = new();
}
}