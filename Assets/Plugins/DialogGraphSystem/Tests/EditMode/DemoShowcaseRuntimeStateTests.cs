using System;
using System.Collections.Generic;
using System.Reflection;
using DialogSystem.Runtime.Actions;
using DialogSystem.Runtime.Variables;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DemoShowcaseRuntimeStateTests
    {
        private readonly List<UnityEngine.Object> _objects = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _objects)
            {
                if (obj != null)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
            }

            _objects.Clear();
        }

        [Test]
        public void ActionShowcase_ResetRestoresEveryVisibleEffectToIdle()
        {
            var root = NewGameObject("Action Showcase");
            var actionShowcase = root.AddComponent<DialogDemoActionShowcase>();
            var tvImage = NewUiComponent<Image>("TV");
            var door = NewGameObject("Door").transform;
            var alarmOverlay = NewUiComponent<Image>("Alarm Overlay");
            var alarmAudio = NewGameObject("Alarm Audio").AddComponent<AudioSource>();
            var alarmStatus = NewUiComponent<TextMeshProUGUI>("Alarm Status");
            var countdownRoot = NewGameObject("Countdown Root");
            var countdown = NewUiComponent<TextMeshProUGUI>("Countdown");
            var countdownStatus = NewUiComponent<TextMeshProUGUI>("Countdown Status");
            var dimmer = NewUiComponent<Image>("Dimmer");
            var lightsStatus = NewUiComponent<TextMeshProUGUI>("Lights Status");

            door.localPosition = new Vector3(12f, 3f, 0f);
            SetField(actionShowcase, "tvImage", tvImage);
            SetField(actionShowcase, "doorTransform", door);
            SetField(actionShowcase, "alarmOverlay", alarmOverlay);
            SetField(actionShowcase, "alarmAudioSource", alarmAudio);
            SetField(actionShowcase, "alarmStatusLabel", alarmStatus);
            SetField(actionShowcase, "countdownRoot", countdownRoot);
            SetField(actionShowcase, "countdownLabel", countdown);
            SetField(actionShowcase, "countdownStatusLabel", countdownStatus);
            SetField(actionShowcase, "sceneDimOverlay", dimmer);
            SetField(actionShowcase, "lightsStatusLabel", lightsStatus);
            InvokeRequired(actionShowcase, "Awake");

            actionShowcase.TurnTvOn();
            door.localPosition = new Vector3(-50f, 3f, 0f);
            alarmOverlay.color = Color.red;
            alarmAudio.loop = true;
            alarmStatus.text = "Alarm active";
            countdownRoot.SetActive(true);
            countdown.text = "3";
            countdownStatus.text = "Counting";
            dimmer.color = new Color(0f, 0f, 0f, 0.8f);
            lightsStatus.text = "Dimmed";

            InvokeRequired(actionShowcase, "ResetShowcase");

            Assert.That(GetField<bool>(actionShowcase, "_tvIsOn"), Is.False);
            Assert.That(door.localPosition, Is.EqualTo(new Vector3(12f, 3f, 0f)));
            Assert.That(GetField<bool>(actionShowcase, "_doorIsOpen"), Is.False);
            Assert.That(alarmOverlay.color.a, Is.Zero.Within(0.0001f));
            Assert.That(alarmAudio.loop, Is.False);
            Assert.That(alarmStatus.text, Is.EqualTo("Ready"));
            Assert.That(countdownRoot.activeSelf, Is.False);
            Assert.That(countdown.text, Is.Empty);
            Assert.That(countdownStatus.text, Is.EqualTo("Ready"));
            Assert.That(dimmer.color.a, Is.Zero.Within(0.0001f));
            Assert.That(lightsStatus.text, Is.EqualTo("Ready"));
        }

        [Test]
        public void VariableShowcase_ResetWritesAllDemoDefaultsDeterministically()
        {
            var root = NewGameObject("Variable Showcase");
            var store = root.AddComponent<DialogueVariableStore>();
            var variables = root.AddComponent<DialogDemoVariableShowcase>();
            var status = NewUiComponent<TextMeshProUGUI>("Variable Status");
            SetField(variables, "variableStore", store);
            SetField(variables, "statusLabel", status);

            store.SetString("playerName", "Changed");
            store.SetInt("gold", 2);
            store.SetBool("hasKey", true);
            store.SetInt("reputation", 99);
            store.SetBool("trustLevel", true);
            store.SetFloat("temperature", 5f);
            store.SetString("questState", "changed");

            variables.ResetDemoVariables();

            Assert.That(store.GetString("playerName"), Is.EqualTo("Alex"));
            Assert.That(store.GetInt("gold"), Is.EqualTo(20));
            Assert.That(store.GetBool("hasKey"), Is.False);
            Assert.That(store.GetInt("reputation"), Is.EqualTo(1));
            Assert.That(store.GetBool("trustLevel"), Is.False);
            Assert.That(store.GetFloat("temperature"), Is.EqualTo(96f).Within(0.0001f));
            Assert.That(store.GetString("questState"), Is.EqualTo("standby"));
            Assert.That(status.text, Does.Contain("Trust: False"));
            Assert.That(status.text, Does.Contain("Temperature: 96"));
            Assert.That(status.text, Does.Contain("State: standby"));
        }

        private GameObject NewGameObject(string name)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go;
        }

        private T NewUiComponent<T>(string name) where T : Component
        {
            return NewGameObject(name, typeof(RectTransform)).AddComponent<T>();
        }

        private GameObject NewGameObject(string name, params Type[] components)
        {
            var go = new GameObject(name, components);
            _objects.Add(go);
            return go;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
            field.SetValue(target, value);
        }

        private static T GetField<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, target.GetType().Name + "." + name);
            return (T)field.GetValue(target);
        }

        private static void InvokeRequired(object target, string methodName)
        {
            var method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, target.GetType().Name + "." + methodName);
            method.Invoke(target, null);
        }
    }
}
