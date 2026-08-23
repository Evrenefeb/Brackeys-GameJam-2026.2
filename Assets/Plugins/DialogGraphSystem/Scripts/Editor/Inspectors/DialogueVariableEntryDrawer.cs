using DialogSystem.Runtime.Variables;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.EditorTools.Dialog
{
    /// <summary>
    /// Draws inline variable defaults with only the active value field visible.
    /// </summary>
    [CustomPropertyDrawer(typeof(DialogueVariableEntry))]
    public sealed class DialogueVariableEntryDrawer : PropertyDrawer
    {
        private const float LineSpacing = 2f;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return (EditorGUIUtility.singleLineHeight * 3f) + (LineSpacing * 2f);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var key = property.FindPropertyRelative("key");
            var type = property.FindPropertyRelative("type");

            var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(line, key, new GUIContent("Key"));

            line.y += EditorGUIUtility.singleLineHeight + LineSpacing;
            EditorGUI.PropertyField(line, type, new GUIContent("Type"));

            line.y += EditorGUIUtility.singleLineHeight + LineSpacing;
            DrawValueField(line, property, (DialogueVariableValueType)type.enumValueIndex);

            EditorGUI.EndProperty();
        }

        private static void DrawValueField(Rect position, SerializedProperty property, DialogueVariableValueType type)
        {
            switch (type)
            {
                case DialogueVariableValueType.Boolean:
                    EditorGUI.PropertyField(position, property.FindPropertyRelative("boolValue"), new GUIContent("Value"));
                    break;
                case DialogueVariableValueType.Integer:
                    EditorGUI.PropertyField(position, property.FindPropertyRelative("intValue"), new GUIContent("Value"));
                    break;
                case DialogueVariableValueType.Float:
                    EditorGUI.PropertyField(position, property.FindPropertyRelative("floatValue"), new GUIContent("Value"));
                    break;
                case DialogueVariableValueType.String:
                    EditorGUI.PropertyField(position, property.FindPropertyRelative("stringValue"), new GUIContent("Value"));
                    break;
            }
        }
    }
}
