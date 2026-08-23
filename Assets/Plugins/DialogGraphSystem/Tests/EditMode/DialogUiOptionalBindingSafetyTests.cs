using System.Collections.Generic;
using System.Text.RegularExpressions;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Settings.Panels;
using DialogSystem.Runtime.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DialogUiOptionalBindingSafetyTests
    {
        private readonly List<Object> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _objects)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void ShowDialogLineDoesNotReenableDisabledSpeakerOrPortrait()
        {
            var controller = CreateController();
            var dialogText = CreateTmpText("DialogText");
            var speakerName = CreateTmpText("SpeakerName");
            SetField(controller, "dialogText", dialogText);
            SetField(controller, "speakerName", speakerName);
            controller.portraitImage = CreateImage("Portrait");

            var settings = CreateSettings();
            settings.showBackgroundPanel = false;
            settings.showSpeakerName = false;
            settings.showPortrait = false;
            settings.showSkipButton = false;
            settings.showAutoButton = false;
            settings.showHistoryButton = false;
            settings.showSettingsButton = false;
            settings.showContinueIndicator = false;
            settings.showAutoSkipIcon = false;
            settings.showChoicePanel = false;
            settings.showHistoryPanel = false;

            controller.ApplySettings(settings);
            controller.ShowDialogLine("Kira", CreateSprite());

            Assert.That(speakerName.gameObject.activeSelf, Is.False);
            Assert.That(controller.portraitImage.enabled, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void BuildChoicesDoesNotLogWhenChoicePanelIsDisabledAndContainerIsMissing()
        {
            var controller = CreateController();
            var settings = CreateSettings();
            settings.showBackgroundPanel = false;
            settings.showSpeakerName = false;
            settings.showPortrait = false;
            settings.showSkipButton = false;
            settings.showAutoButton = false;
            settings.showHistoryButton = false;
            settings.showSettingsButton = false;
            settings.showContinueIndicator = false;
            settings.showAutoSkipIcon = false;
            settings.showChoicePanel = false;
            settings.showHistoryPanel = false;

            controller.ApplySettings(settings);

            var node = ScriptableObject.CreateInstance<ChoiceNode>();
            _objects.Add(node);
            node.choices.Add(new Choice { answerText = "A" });

            var choiceSettings = ScriptableObject.CreateInstance<DialogChoiceSettings>();
            _objects.Add(choiceSettings);

            controller.BuildChoices(node, choiceSettings, _ => { });

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ApplySettingsWarnsWhenEnabledSkipButtonBindingIsMissing()
        {
            var controller = CreateController();
            var settings = CreateSettings();
            settings.showBackgroundPanel = false;
            settings.showSpeakerName = false;
            settings.showPortrait = false;
            settings.showSkipButton = true;
            settings.showAutoButton = false;
            settings.showHistoryButton = false;
            settings.showSettingsButton = false;
            settings.showContinueIndicator = false;
            settings.showAutoSkipIcon = false;
            settings.showChoicePanel = false;
            settings.showHistoryPanel = false;

            LogAssert.Expect(
                LogType.Warning,
                new Regex(@".*skip button feature is enabled.*"));

            controller.ApplySettings(settings);
        }

        [Test]
        public void SetPortraitSideDoesNotReorderManualLayoutWhenSwappingIsDisabled()
        {
            var controller = CreateController();
            var row = new GameObject("Row", typeof(RectTransform));
            _objects.Add(row);

            var portraitObject = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
            _objects.Add(portraitObject);
            portraitObject.transform.SetParent(row.transform, false);

            var textObject = new GameObject("DialogText", typeof(RectTransform), typeof(TextMeshProUGUI));
            _objects.Add(textObject);
            textObject.transform.SetParent(row.transform, false);

            controller.portraitImage = portraitObject.GetComponent<Image>();
            SetField(controller, "dialogText", textObject.GetComponent<TextMeshProUGUI>());

            controller.SetPortraitSide(true);

            Assert.That(row.transform.GetChild(0), Is.EqualTo(portraitObject.transform));
            Assert.That(row.transform.GetChild(1), Is.EqualTo(textObject.transform));
        }

        [Test]
        public void DialogUIControllerImplementsRuntimeViewWithoutReplacingLegacyApi()
        {
            var controller = CreateController();
            var dialogText = CreateTmpText("DialogText");
            var speakerName = CreateTmpText("SpeakerName");
            SetField(controller, "dialogText", dialogText);
            SetField(controller, "speakerName", speakerName);
            controller.portraitImage = CreateImage("Portrait");

            var view = (IDialogueRuntimeView)controller;
            view.ShowDialogueLine(new DialogueLinePresentation("Kira", "Hello", CreateSprite()));
            view.SetText("Hello");

            Assert.That(view.DialogueTextTarget, Is.EqualTo(dialogText));
            Assert.That(controller.speakerName.text, Is.EqualTo("Kira"));
            Assert.That(controller.dialogText.text, Is.EqualTo("Hello"));
            Assert.That(controller.portraitImage.sprite, Is.Not.Null);
        }

        [Test]
        public void RuntimeViewRebuildChoicesCreatesChoiceButtons()
        {
            var controller = CreateController();
            var choicesRoot = new GameObject("Choices");
            _objects.Add(choicesRoot);
            controller.choicesContainer = choicesRoot.transform;
            controller.choiceButtonPrefab = CreateChoiceButtonPrefab();

            var view = (IDialogueRuntimeView)controller;
            var choices = new[]
            {
                new DialogueChoicePresentation(0, "A"),
                new DialogueChoicePresentation(1, "B")
            };

            var choiceSettings = ScriptableObject.CreateInstance<DialogChoiceSettings>();
            _objects.Add(choiceSettings);

            view.RebuildChoices(choices, choiceSettings, _ => { });

            Assert.That(controller.choicesContainer.childCount, Is.EqualTo(2));
            Assert.That(controller.choicesContainer.gameObject.activeSelf, Is.True);
        }

        private DialogUIController CreateController()
        {
            var gameObject = new GameObject("DialogUIControllerTests");
            _objects.Add(gameObject);
            return gameObject.AddComponent<DialogUIController>();
        }

        private DialogueRuntimeUISettings CreateSettings()
        {
            var settings = ScriptableObject.CreateInstance<DialogueRuntimeUISettings>();
            _objects.Add(settings);
            return settings;
        }

        private TextMeshProUGUI CreateTmpText(string name)
        {
            var gameObject = new GameObject(name);
            _objects.Add(gameObject);
            return gameObject.AddComponent<TextMeshProUGUI>();
        }

        private Image CreateImage(string name)
        {
            var gameObject = new GameObject(name);
            _objects.Add(gameObject);
            return gameObject.AddComponent<Image>();
        }

        private Sprite CreateSprite()
        {
            var texture = new Texture2D(1, 1);
            _objects.Add(texture);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            _objects.Add(sprite);
            return sprite;
        }

        private GameObject CreateChoiceButtonPrefab()
        {
            var prefab = new GameObject("ChoiceButtonPrefab");
            _objects.Add(prefab);
            prefab.SetActive(false);
            prefab.AddComponent<RectTransform>();
            var button = prefab.AddComponent<Button>();

            var labelGo = new GameObject("Label");
            _objects.Add(labelGo);
            labelGo.transform.SetParent(prefab.transform);
            var label = labelGo.AddComponent<TextMeshProUGUI>();

            var hotkeyGo = new GameObject("Hotkey");
            _objects.Add(hotkeyGo);
            hotkeyGo.transform.SetParent(prefab.transform);
            var hotkeyLabel = hotkeyGo.AddComponent<TextMeshProUGUI>();

            var view = prefab.AddComponent<ChoiceButtonView>();
            SetPrivateField(view, "_button", button);
            SetPrivateField(view, "_choiceText", label);
            SetPrivateField(view, "_hotkeyHolder", hotkeyGo);
            SetPrivateField(view, "_hotkeyText", hotkeyLabel);
            prefab.SetActive(true);
            return prefab;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName);
            Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"Field '{fieldName}' was not found.");
            field.SetValue(target, value);
        }
    }
}