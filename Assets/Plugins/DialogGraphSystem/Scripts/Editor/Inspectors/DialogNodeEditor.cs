using UnityEditor;
using UnityEngine;
using DialogSystem.Runtime.Models.Nodes;

namespace DialogSystem.EditorTools.Dialog
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(DialogNode))]
    public class DialogNodeEditor : UnityEditor.Editor
    {
        // Serialized props
        SerializedProperty pGUID;
        SerializedProperty pSpeakerName;
        SerializedProperty pSpeakerPortrait;
        SerializedProperty pQuestionText;
        SerializedProperty pDialogAudio;
        SerializedProperty pDisplayTime;

        void OnEnable()
        {
            // BaseNode private [SerializeField] fields are discoverable by name
            pGUID = serializedObject.FindProperty("GUID");

            // DialogNode fields
            pSpeakerName = serializedObject.FindProperty("speakerName");
            pSpeakerPortrait = serializedObject.FindProperty("speakerPortrait");
            pQuestionText = serializedObject.FindProperty("questionText");
            pDialogAudio = serializedObject.FindProperty("dialogAudio");
            pDisplayTime = serializedObject.FindProperty("displayTime");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Dialog Node", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            DrawObjectNameField();

            // Identity (GUID)
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Identity", EditorStyles.boldLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.SelectableLabel(pGUID != null ? pGUID.stringValue : "(no GUID)", GUILayout.Height(18));

                    using (new EditorGUI.DisabledScope(pGUID == null))
                    {
                        if (GUILayout.Button("Copy", GUILayout.Width(54)) && pGUID != null)
                            EditorGUIUtility.systemCopyBuffer = pGUID.stringValue;

                        if (GUILayout.Button("Regenerate", GUILayout.Width(96)) && pGUID != null)
                        {
                            Undo.RecordObjects(targets, "Regenerate GUID");
                            foreach (var t in targets)
                            {
                                var so = new SerializedObject(t);
                                var guidProp = so.FindProperty("GUID");
                                if (guidProp != null)
                                {
                                    guidProp.stringValue = System.Guid.NewGuid().ToString("N");
                                    so.ApplyModifiedPropertiesWithoutUndo();
                                    EditorUtility.SetDirty(t);
                                }
                            }
                        }
                    }
                }
            }

            // Speaker
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Speaker", EditorStyles.boldLabel);
                DrawProp(pSpeakerName, "Name");
                DrawProp(pSpeakerPortrait, "Portrait");
            }

            // Content + Audio
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Content", EditorStyles.boldLabel);
                DrawProp(pQuestionText, "Line Text");
                EditorGUILayout.HelpBox(
                    "Runtime variables can be injected with {variableKey}, for example {playerName}. Missing variables keep the token visible.",
                    MessageType.None);
                EditorGUILayout.Space(2);
                DrawProp(pDialogAudio, "Dialog Audio");
            }

            // Timing
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Timing", EditorStyles.boldLabel);
                if (pDisplayTime != null)
                {
                    EditorGUI.BeginChangeCheck();
                    EditorGUILayout.PropertyField(pDisplayTime, new GUIContent("Display Time (s)"));
                    if (EditorGUI.EndChangeCheck() && pDisplayTime.floatValue < 0f)
                        pDisplayTime.floatValue = 0f;

                    EditorGUILayout.HelpBox(
                        "Use > 0 for timed auto-advance (if your runtime supports it). Use 0 to wait for player input.",
                        MessageType.Info);
                }
                else
                {
                    EditorGUILayout.HelpBox("This asset has no 'displayTime' field.", MessageType.None);
                }
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                foreach (var t in targets)
                    EditorUtility.SetDirty(t);
            }
        }

        static void DrawProp(SerializedProperty prop, string label, bool includeChildren = false)
        {
            if (prop == null)
            {
                EditorGUILayout.HelpBox($"Property '{label}' not found.", MessageType.None);
                return;
            }
            EditorGUILayout.PropertyField(prop, new GUIContent(label), includeChildren);
        }

        void DrawObjectNameField()
        {
            bool mixed = targets.Length > 1;
            EditorGUI.showMixedValue = mixed;
            string current = mixed ? "" : ((DialogNode)target).name;

            EditorGUI.BeginChangeCheck();
            string newName = EditorGUILayout.TextField(new GUIContent("Node Name"), current);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(targets, "Rename Dialog Node");
                foreach (var t in targets)
                {
                    var obj = (DialogNode)t;
                    var trimmed = string.IsNullOrWhiteSpace(newName) ? obj.name : newName.Trim();
                    obj.name = trimmed;
                    EditorUtility.SetDirty(obj);
                }
            }
            EditorGUI.showMixedValue = false;
        }
    }
}