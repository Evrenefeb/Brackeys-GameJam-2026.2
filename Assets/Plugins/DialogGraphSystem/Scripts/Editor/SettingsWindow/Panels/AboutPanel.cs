using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Settings.Panels
{
    public class AboutPanel : BasePanel
    {
        public override void BuildUI(SerializedObject masterSo)
        {
            var card = new VisualElement(); card.AddToClassList("dgs-card");
            var h = new Label("About"); h.AddToClassList("dgs-card-title");
            card.Add(h);

            var verRow = new VisualElement(); verRow.style.flexDirection = FlexDirection.Row;
            verRow.Add(new Label("Version: ") { name = "caption" });
            verRow.Add(new Label(masterSo.FindProperty("version").stringValue));
            card.Add(verRow);

            var logModeProp = masterSo.FindProperty("runtimeLogMode");
            if (logModeProp != null)
            {
                var logMode = new PropertyField(logModeProp, "Runtime Log Mode");
                logMode.Bind(masterSo);
                card.Add(logMode);
            }

            var dbgProp = masterSo.FindProperty("enableDebugLogs");
            var dbg = new Toggle("Enable Debug Logs") { value = dbgProp.boolValue };
            dbg.RegisterValueChangedCallback(evt =>
            {
                dbgProp.boolValue = evt.newValue; masterSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(masterSo.targetObject); AssetDatabase.SaveAssets();
            });
            card.Add(dbg);

            Add(card);
        }
    }
}