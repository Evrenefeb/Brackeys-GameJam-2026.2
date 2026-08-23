using System.Collections.Generic;
using DialogSystem.Runtime.Variables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DialogueVariableStoreTests
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
        public void SetAndGetSupportsAllValueTypes()
        {
            var store = CreateStore();

            store.SetBool("flag", true);
            store.SetInt("count", 7);
            store.SetFloat("ratio", 2.5f);
            store.SetString("name", "Kira");

            Assert.That(store.GetBool("flag"), Is.True);
            Assert.That(store.GetInt("count"), Is.EqualTo(7));
            Assert.That(store.GetFloat("ratio"), Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(store.GetString("name"), Is.EqualTo("Kira"));
            Assert.That(store.GetFloat("count"), Is.EqualTo(7f).Within(0.0001f));
        }

        [Test]
        public void ResetToInitialVariablesAppliesDefinitionDefaultsAndInlineOverrides()
        {
            var definition = CreateDefinition("mood", "calm");
            var store = CreateStore();
            ConfigureInitialValues(
                store,
                new[] { definition },
                new[]
                {
                    new DialogueVariableEntry
                    {
                        key = "mood",
                        type = DialogueVariableValueType.String,
                        stringValue = "focused"
                    },
                    new DialogueVariableEntry
                    {
                        key = "score",
                        type = DialogueVariableValueType.Integer,
                        intValue = 12
                    }
                });

            store.ResetToInitialVariables();
            store.SetString("mood", "changed");
            store.SetInt("score", 99);

            store.ResetToInitialVariables();

            Assert.That(store.GetString("mood"), Is.EqualTo("focused"));
            Assert.That(store.GetInt("score"), Is.EqualTo(12));
        }

        [Test]
        public void MissingAndWrongTypeReadsReturnFallbacks()
        {
            var store = CreateStore();
            store.SetString("title", "Captain");
            store.SetBool("flag", true);

            Assert.That(store.GetInt("missing", 42), Is.EqualTo(42));
            Assert.That(store.GetBool("title", true), Is.True);
            Assert.That(store.GetString("flag", "fallback"), Is.EqualTo("fallback"));
            Assert.That(store.TryGetInt("title", out _), Is.False);
        }

        private DialogueVariableStore CreateStore()
        {
            var go = new GameObject("DialogueVariableStoreTest");
            _objects.Add(go);
            return go.AddComponent<DialogueVariableStore>();
        }

        private DialogVariableSO CreateDefinition(string key, string defaultValue)
        {
            var definition = ScriptableObject.CreateInstance<DialogVariableSO>();
            definition.name = key;
            definition.SetKey(key);
            definition.SetDefaultValue(defaultValue);
            _objects.Add(definition);
            return definition;
        }

        private static void ConfigureInitialValues(
            DialogueVariableStore store,
            IReadOnlyList<DialogVariableSO> definitions,
            IReadOnlyList<DialogueVariableEntry> entries)
        {
            var serializedObject = new SerializedObject(store);

            var definitionProperty = serializedObject.FindProperty("variableDefinitions");
            definitionProperty.arraySize = definitions.Count;
            for (var i = 0; i < definitions.Count; i++)
            {
                definitionProperty.GetArrayElementAtIndex(i).objectReferenceValue = definitions[i];
            }

            var entriesProperty = serializedObject.FindProperty("initialVariables");
            entriesProperty.arraySize = entries.Count;
            for (var i = 0; i < entries.Count; i++)
            {
                var source = entries[i];
                var element = entriesProperty.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("key").stringValue = source.key;
                element.FindPropertyRelative("type").enumValueIndex = (int)source.type;
                element.FindPropertyRelative("boolValue").boolValue = source.boolValue;
                element.FindPropertyRelative("intValue").intValue = source.intValue;
                element.FindPropertyRelative("floatValue").floatValue = source.floatValue;
                element.FindPropertyRelative("stringValue").stringValue = source.stringValue;
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
