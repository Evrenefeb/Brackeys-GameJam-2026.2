using System;
using System.Linq;
using System.Reflection;
using DialogSystem.EditorTools.Resources;
using DialogSystem.Runtime.Utils;
using UnityEditor;
using UnityEngine.UIElements;

namespace DialogSystem.EditorTools.Settings.Panels
{
    public sealed class AiProvidersPanel : BasePanel
    {
        public override void BuildUI(SerializedObject masterSo)
        {
            _scrollView.Clear();
            SetPageHeader(
                "AI Providers",
                "Provider setup and general AI editor defaults.",
                DialogGraphIconId.AiRewrite);

            var embeddedPanel = CreateEmbeddedAiProvidersPanel();
            if (embeddedPanel != null)
            {
                embeddedPanel.style.flexGrow = 1;
                Add(embeddedPanel);
                return;
            }

            var card = DialogSettingsEditorUtils.Card("AI Providers Not Installed");
            var message = new Label(
                "Import the AI Providers asset to configure providers, local API keys, prompt defaults, and the general Rewrite with AI tool.");
            message.style.whiteSpace = WhiteSpace.Normal;
            card.Add(message);

            var path = new Label(TextResources.MENU_AI_PROVIDERS_DISPLAY);
            path.style.marginTop = 8;
            card.Add(path);
            Add(card);
        }

        private static VisualElement CreateEmbeddedAiProvidersPanel()
        {
            const string typeName = "AiProviders.Editor.AiProvidersSettingsPanel";
            var panelType = Type.GetType(typeName + ", AiProviders.Editor", throwOnError: false)
                            ?? AppDomain.CurrentDomain.GetAssemblies()
                                .Select(assembly => assembly.GetType(typeName, throwOnError: false))
                                .FirstOrDefault(type => type != null);

            var method = panelType?.GetMethod("CreateEmbeddedPanel", BindingFlags.Public | BindingFlags.Static);
            return method?.Invoke(null, null) as VisualElement;
        }
    }
}