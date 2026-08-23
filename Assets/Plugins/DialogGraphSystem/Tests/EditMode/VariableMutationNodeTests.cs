using System.Collections.Generic;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Variables;
using NUnit.Framework;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class VariableMutationNodeTests
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
        public void ApplySupportsSetOperation()
        {
            var store = CreateStore();

            Assert.That(Apply("title", DialogueVariableValueType.String, VariableMutationOperation.Set, "Captain", store, out var error), Is.True, error);
            Assert.That(store.GetString("title"), Is.EqualTo("Captain"));
        }

        [Test]
        public void ApplySupportsAddAndSubtractForNumericTypes()
        {
            var store = CreateStore();
            store.SetInt("score", 10);
            store.SetFloat("energy", 2.5f);

            Assert.That(Apply("score", DialogueVariableValueType.Integer, VariableMutationOperation.Add, "3", store, out var addError), Is.True, addError);
            Assert.That(Apply("score", DialogueVariableValueType.Integer, VariableMutationOperation.Subtract, "4", store, out var subtractError), Is.True, subtractError);
            Assert.That(Apply("energy", DialogueVariableValueType.Float, VariableMutationOperation.Add, "1.25", store, out var floatError), Is.True, floatError);

            Assert.That(store.GetInt("score"), Is.EqualTo(9));
            Assert.That(store.GetFloat("energy"), Is.EqualTo(3.75f).Within(0.0001f));
        }

        [Test]
        public void ApplySupportsToggleAndClearString()
        {
            var store = CreateStore();
            store.SetBool("unlocked", false);
            store.SetString("note", "remember this");

            Assert.That(Apply("unlocked", DialogueVariableValueType.Boolean, VariableMutationOperation.Toggle, null, store, out var toggleError), Is.True, toggleError);
            Assert.That(Apply("note", DialogueVariableValueType.String, VariableMutationOperation.ClearString, null, store, out var clearError), Is.True, clearError);

            Assert.That(store.GetBool("unlocked"), Is.True);
            Assert.That(store.GetString("note"), Is.Empty);
        }

        [Test]
        public void InvalidOperationsFailSafelyWithoutMutatingExistingValue()
        {
            var store = CreateStore();
            store.SetBool("flag", true);
            store.SetInt("score", 5);

            Assert.That(Apply("flag", DialogueVariableValueType.Boolean, VariableMutationOperation.Add, "1", store, out var boolAddError), Is.False);
            Assert.That(boolAddError, Is.Not.Empty);
            Assert.That(Apply("score", DialogueVariableValueType.Integer, VariableMutationOperation.Set, "not-an-int", store, out var parseError), Is.False);
            Assert.That(parseError, Is.Not.Empty);
            Assert.That(Apply("", DialogueVariableValueType.String, VariableMutationOperation.Set, "value", store, out var emptyNameError), Is.False);
            Assert.That(emptyNameError, Is.Not.Empty);

            Assert.That(store.GetBool("flag"), Is.True);
            Assert.That(store.GetInt("score"), Is.EqualTo(5));
        }

        private DialogueVariableStore CreateStore()
        {
            var go = new GameObject("VariableMutationStoreTest");
            _objects.Add(go);
            return go.AddComponent<DialogueVariableStore>();
        }

        private bool Apply(
            string variableName,
            DialogueVariableValueType valueType,
            VariableMutationOperation operation,
            string value,
            DialogueVariableStore store,
            out string error)
        {
            var mutation = ScriptableObject.CreateInstance<VariableMutationNode>();
            mutation.variableName = variableName;
            mutation.valueType = valueType;
            mutation.operation = operation;
            mutation.value = value;
            _objects.Add(mutation);
            return mutation.Apply(store, out error);
        }
    }
}
