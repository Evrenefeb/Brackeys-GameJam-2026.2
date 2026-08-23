using System;
using System.Collections.Generic;
using System.Linq;
using DialogSystem.Runtime.Actions;
using DialogSystem.Runtime.Core;
using DialogSystem.Runtime.Demo;
using DialogSystem.Runtime.Models;
using DialogSystem.Runtime.Variables;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DialogSystem.Tests.EditMode
{
    public static class DemoShowcaseSceneBuilder
    {
        private const string ScenePath = "Assets/DialogGraphSystem/DemoScenes/DialogueDemo.unity";
        private static TMP_FontAsset _font;

        public static void RebuildFromCommandLine()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Rebuild(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Failed to save " + ScenePath);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[DemoShowcaseSceneBuilder] DialogueDemo showcase rebuilt successfully.");
        }

        internal static void Rebuild(Scene scene)
        {
            var canvas = FindAll<Canvas>(scene).Single(candidate =>
                candidate.GetComponentsInChildren<Transform>(true)
                    .Any(transform => string.Equals(transform.name, "MainMenu", StringComparison.Ordinal) ||
                                      string.Equals(transform.name, "DGS Showcase Shell", StringComparison.Ordinal)));
            var manager = FindSingle<DialogManager>(scene);
            var actionRunner = FindSingle<DialogActionRunner>(scene);
            var actionShowcase = FindSingle<DialogDemoActionShowcase>(scene);
            var variableShowcase = FindSingle<DialogDemoVariableShowcase>(scene);
            _font = FindAll<TextMeshProUGUI>(scene).Select(label => label.font).FirstOrDefault(font => font != null);

            var runtimeSettingsButton = FindAll<Button>(scene)
                .FirstOrDefault(button => string.Equals(button.name, "SettingsBtn", StringComparison.Ordinal));

            DestroyNamed(scene, "MainMenu");
            DestroyNamed(scene, "DGS Showcase Shell");
            DestroyNamed(scene, "DGS Showcase Controller");

            RegisterGraphs(manager);
            ConfigureActionShowcase(actionShowcase);
            ConfigureActionRunner(actionRunner, actionShowcase);

            var shell = Rect("DGS Showcase Shell", canvas.transform);
            Stretch(shell);
            shell.SetAsLastSibling();

            var menu = Panel("ShowcaseMenu", shell, new Color32(10, 15, 29, 246));
            Stretch(menu);

            var glow = Panel("Accent Glow", menu, new Color32(48, 106, 255, 28));
            SetRect(glow, new Vector2(0f, 0.65f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);

            var content = Rect("Content", menu);
            SetRect(content, Vector2.zero, Vector2.one, new Vector2(72f, 118f), new Vector2(-72f, -46f));

            var eyebrow = Text("Eyebrow", content, "DIALOGUE GRAPH SYSTEM 3.0", 20f, FontStyles.Bold, new Color32(105, 171, 255, 255));
            SetTop(eyebrow.rectTransform, 0f, 32f, 900f, 30f);

            var title = Text("Title", content, "Three focused demos. One production-ready runtime.", 42f, FontStyles.Bold, Color.white);
            SetTop(title.rectTransform, 0f, 76f, 1500f, 58f);

            var subtitle = Text(
                "Subtitle",
                content,
                "Choose the capability you want to inspect. Every demo is replayable, localized, and built from the same graph, variable, action, and UI systems that ship in the package.",
                20f,
                FontStyles.Normal,
                new Color32(184, 198, 222, 255));
            subtitle.enableWordWrapping = true;
            SetTop(subtitle.rectTransform, 0f, 136f, 1420f, 58f);

            var cards = Rect("Cards", content);
            SetTop(cards, 0f, 220f, 1620f, 560f);

            var productButton = CreateCard(
                cards,
                "01 Product Tour Card",
                -550f,
                "01 / PRODUCT TOUR",
                "Product Tour",
                "A concise guided path through authored dialogue and the runtime controls players actually use.",
                "Demonstrates:\n- localization + variable text\n- meaningful branching\n- visible scene actions\n- history, autoplay, skip, settings",
                "Start Product Tour",
                new Color32(65, 126, 255, 255),
                out _);

            var shopButton = CreateCard(
                cards,
                "02 Shop Gate Card",
                0f,
                "02 / GAMEPLAY STATE",
                "Shop Gate",
                "Negotiate passage with money, a key, or trust—and see success and denial remain explicit.",
                "Demonstrates:\n- variables and substitutions\n- conditions and mutations\n- six deterministic outcomes\n- exactly-once gate actions",
                "Start Shop Gate",
                new Color32(48, 191, 142, 255),
                out var shopCard);

            var reactorButton = CreateCard(
                cards,
                "03 Reactor Control Room Card",
                550f,
                "03 / ACTION EVENTS",
                "Reactor Control Room",
                "Run calm or emergency containment while the room responds to each authored action.",
                "Demonstrates:\n- visible actions and timing\n- variable mutation\n- modular Graph Jump flow\n- distinct final outcomes",
                "Start Reactor Demo",
                new Color32(246, 113, 91, 255),
                out _);

            var keyToggle = Toggle("Start with checkpoint key", shopCard, new Vector2(-104f, 96f));
            var trustToggle = Toggle("Start trusted", shopCard, new Vector2(112f, 96f));

            var activeDemo = Panel("Active Demo HUD", shell, new Color32(14, 22, 40, 238));
            SetRect(activeDemo, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -142f), new Vector2(-28f, -24f));
            var activeTitle = Text("Active Demo", activeDemo, "Choose a demo", 28f, FontStyles.Bold, Color.white);
            SetRect(activeTitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, -22f), new Vector2(520f, 32f));
            activeTitle.alignment = TextAlignmentOptions.Left;
            var activeHint = Text("Hint", activeDemo, "Use the dialogue controls normally. Back and Reset remain available throughout the run.", 16f, FontStyles.Normal, new Color32(174, 190, 215, 255));
            SetRect(activeHint.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 16f), new Vector2(760f, 28f));
            activeHint.alignment = TextAlignmentOptions.Left;
            var stateReadout = Text("State Readout", activeDemo, "", 14f, FontStyles.Normal, new Color32(158, 214, 255, 255));
            SetRect(stateReadout.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-420f, -55f), new Vector2(390f, 105f));
            stateReadout.alignment = TextAlignmentOptions.TopLeft;
            stateReadout.enableWordWrapping = false;
            activeDemo.gameObject.SetActive(false);

            var persistent = Panel("Persistent Controls", shell, new Color32(10, 15, 29, 250));
            SetRect(persistent, Vector2.zero, Vector2.right, new Vector2(24f, 20f), new Vector2(-24f, 98f));
            var controlHint = Text("Control Hint", persistent, "SHOWCASE NAVIGATION", 14f, FontStyles.Bold, new Color32(121, 149, 191, 255));
            SetRect(controlHint.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(360f, 32f));
            controlHint.rectTransform.pivot = new Vector2(0f, 0.5f);
            controlHint.alignment = TextAlignmentOptions.Left;

            var backButton = Button("Back to Demo Menu", persistent, "Back to Demo Menu", new Color32(39, 53, 78, 255));
            SetRect(backButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-564f, 0f), new Vector2(236f, 48f));
            var resetButton = Button("Reset Demo", persistent, "Reset / Re-run", new Color32(39, 53, 78, 255));
            SetRect(resetButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-316f, 0f), new Vector2(214f, 48f));
            var settingsButton = Button("Settings", persistent, "Settings", new Color32(65, 126, 255, 255));
            SetRect(settingsButton.GetComponent<RectTransform>(), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-102f, 0f), new Vector2(186f, 48f));

            var controllerObject = new GameObject("DGS Showcase Controller");
            SceneManager.MoveGameObjectToScene(controllerObject, scene);
            controllerObject.transform.SetParent(canvas.transform, false);
            var controller = controllerObject.AddComponent<DialogDemoShowcaseController>();
            ConfigureController(
                controller,
                manager,
                actionShowcase,
                variableShowcase,
                menu.gameObject,
                activeDemo.gameObject,
                activeTitle,
                productButton,
                shopButton,
                reactorButton,
                backButton,
                resetButton,
                runtimeSettingsButton,
                settingsButton,
                keyToggle,
                trustToggle);

            ConfigureVariableShowcase(variableShowcase, stateReadout);
        }

        private static void RegisterGraphs(DialogManager manager)
        {
            manager.dialogGraphs = new List<DialogManager.DialogGraphModel>
            {
                Entry("Demo_ProductTour", DemoShowcaseGraphSpecs.ProductPath),
                Entry("Demo_ShopGate", DemoShowcaseGraphSpecs.ShopPath),
                Entry("Demo_ControlRoomActions", DemoShowcaseGraphSpecs.ReactorPath),
                Entry("Demo_ReactorAftermath", DemoShowcaseGraphSpecs.AftermathPath)
            };
            EditorUtility.SetDirty(manager);
        }

        private static DialogManager.DialogGraphModel Entry(string id, string path)
        {
            return new DialogManager.DialogGraphModel
            {
                dialogID = id,
                dialogGraph = AssetDatabase.LoadAssetAtPath<DialogGraph>(path)
            };
        }

        private static void ConfigureActionRunner(DialogActionRunner runner, DialogDemoActionShowcase showcase)
        {
            runner.global = new ConversationActionSet
            {
                dialogueKey = "<global>",
                bindings = new List<ActionBinding>(),
                handlers = new List<MonoBehaviour> { showcase }
            };
            runner.dialogueSets = new List<ConversationActionSet>();
            runner.useGlobalFallback = true;
            EditorUtility.SetDirty(runner);
        }

        private static void ConfigureActionShowcase(DialogDemoActionShowcase showcase)
        {
            var serialized = new SerializedObject(showcase);
            SetString(serialized, "turnOnTvActionId", "TurnOnTV");
            SetString(serialized, "openDoorActionId", "OpenGate");
            SetString(serialized, "playAlarmActionId", "PlayAlarm");
            SetString(serialized, "startCountdownActionId", "StartCountdown");
            SetString(serialized, "fadeLightsActionId", "FadeLights");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(showcase);
        }

        private static void ConfigureController(
            DialogDemoShowcaseController controller,
            DialogManager manager,
            DialogDemoActionShowcase actions,
            DialogDemoVariableShowcase variables,
            GameObject menu,
            GameObject activeDemo,
            TextMeshProUGUI activeTitle,
            Button product,
            Button shop,
            Button reactor,
            Button back,
            Button reset,
            Button runtimeSettings,
            Button settings,
            Toggle keyToggle,
            Toggle trustToggle)
        {
            var serialized = new SerializedObject(controller);
            Set(serialized, "dialogManager", manager);
            Set(serialized, "actionShowcase", actions);
            Set(serialized, "variableShowcase", variables);
            Set(serialized, "productTourGraph", AssetDatabase.LoadAssetAtPath<DialogGraph>(DemoShowcaseGraphSpecs.ProductPath));
            Set(serialized, "shopGateGraph", AssetDatabase.LoadAssetAtPath<DialogGraph>(DemoShowcaseGraphSpecs.ShopPath));
            Set(serialized, "reactorControlRoomGraph", AssetDatabase.LoadAssetAtPath<DialogGraph>(DemoShowcaseGraphSpecs.ReactorPath));
            Set(serialized, "menuRoot", menu);
            Set(serialized, "activeDemoRoot", activeDemo);
            Set(serialized, "activeDemoLabel", activeTitle);
            Set(serialized, "productTourButton", product);
            Set(serialized, "shopGateButton", shop);
            Set(serialized, "reactorControlRoomButton", reactor);
            Set(serialized, "backToMenuButton", back);
            Set(serialized, "resetDemoButton", reset);
            Set(serialized, "runtimeSettingsButton", runtimeSettings);
            Set(serialized, "showcaseSettingsButton", settings);
            Set(serialized, "startWithKeyToggle", keyToggle);
            Set(serialized, "startTrustedToggle", trustToggle);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureVariableShowcase(DialogDemoVariableShowcase variables, TextMeshProUGUI stateReadout)
        {
            var serialized = new SerializedObject(variables);
            serialized.FindProperty("playerName").stringValue = "Alex";
            serialized.FindProperty("gold").intValue = 20;
            serialized.FindProperty("hasKey").boolValue = false;
            serialized.FindProperty("reputation").intValue = 1;
            serialized.FindProperty("trustLevel").boolValue = false;
            serialized.FindProperty("temperature").floatValue = 96f;
            serialized.FindProperty("questState").stringValue = "standby";
            Set(serialized, "statusLabel", stateReadout);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(variables);
        }

        private static Button CreateCard(
            RectTransform parent,
            string objectName,
            float x,
            string eyebrow,
            string title,
            string description,
            string demonstrates,
            string buttonLabel,
            Color accent,
            out RectTransform card)
        {
            card = Panel(objectName, parent, new Color32(24, 34, 53, 255));
            SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(500f, 540f));

            var accentBar = Panel("Accent", card, accent);
            SetRect(accentBar, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -7f), new Vector2(0f, 7f));

            var eyebrowText = Text("Category", card, eyebrow, 15f, FontStyles.Bold, accent);
            SetTop(eyebrowText.rectTransform, 26f, 34f, 448f, 26f);
            eyebrowText.alignment = TextAlignmentOptions.Left;

            var titleText = Text("Title", card, title, 30f, FontStyles.Bold, Color.white);
            SetTop(titleText.rectTransform, 26f, 72f, 448f, 46f);
            titleText.alignment = TextAlignmentOptions.Left;

            var descriptionText = Text("Description", card, description, 17f, FontStyles.Normal, new Color32(197, 208, 226, 255));
            SetTop(descriptionText.rectTransform, 26f, 128f, 448f, 82f);
            descriptionText.alignment = TextAlignmentOptions.TopLeft;
            descriptionText.enableWordWrapping = true;

            var featureText = Text("Demonstrates", card, demonstrates, 16f, FontStyles.Normal, new Color32(157, 177, 208, 255));
            SetTop(featureText.rectTransform, 26f, 230f, 448f, 140f);
            featureText.alignment = TextAlignmentOptions.TopLeft;
            featureText.enableWordWrapping = true;

            var start = Button("Start", card, buttonLabel, accent);
            SetRect(start.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(448f, 54f));
            return start;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static RectTransform Panel(string name, Transform parent, Color color)
        {
            var rect = Rect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return rect;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, string copy, float size, FontStyles style, Color color)
        {
            var rect = Rect(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = copy;
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static Button Button(string name, Transform parent, string label, Color color)
        {
            var rect = Panel(name, parent, color);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.12f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            colors.disabledColor = new Color32(54, 63, 78, 180);
            button.colors = colors;

            var text = Text("Label", rect, label, 16f, FontStyles.Bold, Color.white);
            Stretch(text.rectTransform);
            return button;
        }

        private static Toggle Toggle(string label, Transform parent, Vector2 position)
        {
            var root = Rect(label, parent);
            SetRect(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), position, new Vector2(206f, 34f));
            var toggle = root.gameObject.AddComponent<Toggle>();
            toggle.isOn = false;
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };

            var box = Panel("Box", root, new Color32(50, 67, 93, 255));
            SetRect(box, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(24f, 24f));
            var check = Panel("Checkmark", box, new Color32(66, 204, 151, 255));
            SetRect(check, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 14f));
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();

            var text = Text("Label", root, label, 13f, FontStyles.Normal, new Color32(205, 215, 231, 255));
            SetRect(text.rectTransform, new Vector2(0f, 0f), Vector2.one, new Vector2(34f, 0f), Vector2.zero);
            text.alignment = TextAlignmentOptions.Left;
            return toggle;
        }

        private static void SetTop(RectTransform rect, float x, float top, float width, float height)
        {
            SetRect(
                rect,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(x, -(top + height * 0.5f)),
                new Vector2(width, height));
        }

        private static void Stretch(RectTransform rect)
        {
            SetRect(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 positionOrMin, Vector2 sizeOrMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            if (anchorMin == anchorMax)
            {
                rect.anchoredPosition = positionOrMin;
                rect.sizeDelta = sizeOrMax;
            }
            else
            {
                rect.offsetMin = positionOrMin;
                rect.offsetMax = sizeOrMax;
            }
        }

        private static void Set(SerializedObject serialized, string propertyName, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new MissingFieldException(serialized.targetObject.GetType().Name, propertyName);
            property.objectReferenceValue = value;
        }

        private static void SetString(SerializedObject serialized, string propertyName, string value)
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null)
                throw new MissingFieldException(serialized.targetObject.GetType().Name, propertyName);
            property.stringValue = value;
        }

        private static void DestroyNamed(Scene scene, string name)
        {
            foreach (var transform in scene.GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                         .Where(transform => string.Equals(transform.name, name, StringComparison.Ordinal))
                         .ToArray())
            {
                UnityEngine.Object.DestroyImmediate(transform.gameObject);
            }
        }

        private static T FindSingle<T>(Scene scene) where T : UnityEngine.Object
        {
            var matches = FindAll<T>(scene).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Expected exactly one {typeof(T).Name}; found {matches.Length}.");
            return matches[0];
        }

        private static IEnumerable<T> FindAll<T>(Scene scene) where T : UnityEngine.Object
        {
            return scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
        }
    }
}
