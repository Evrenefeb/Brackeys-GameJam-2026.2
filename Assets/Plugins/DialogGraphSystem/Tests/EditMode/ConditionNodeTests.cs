using System.Collections.Generic;
using DialogSystem.Runtime.Models.Nodes;
using DialogSystem.Runtime.Variables;
using NUnit.Framework;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class ConditionNodeTests
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
        public void EvaluateSupportsBoolOperators()
        {
            var store = CreateStore();
            store.SetBool("flag", true);

            Assert.That(Evaluate("flag", DialogueVariableValueType.Boolean, ConditionOperator.IsTrue, null, store), Is.True);
            Assert.That(Evaluate("flag", DialogueVariableValueType.Boolean, ConditionOperator.IsFalse, null, store), Is.False);
            Assert.That(Evaluate("flag", DialogueVariableValueType.Boolean, ConditionOperator.Equals, "1", store), Is.True);
            Assert.That(Evaluate("flag", DialogueVariableValueType.Boolean, ConditionOperator.NotEquals, "false", store), Is.True);
        }

        [Test]
        public void EvaluateSupportsIntOperators()
        {
            var store = CreateStore();
            store.SetInt("score", 10);

            Assert.That(Evaluate("score", DialogueVariableValueType.Integer, ConditionOperator.Equals, "10", store), Is.True);
            Assert.That(Evaluate("score", DialogueVariableValueType.Integer, ConditionOperator.NotEquals, "9", store), Is.True);
            Assert.That(Evaluate("score", DialogueVariableValueType.Integer, ConditionOperator.Greater, "5", store), Is.True);
            Assert.That(Evaluate("score", DialogueVariableValueType.Integer, ConditionOperator.Less, "20", store), Is.True);
        }

        [Test]
        public void EvaluateSupportsFloatOperators()
        {
            var store = CreateStore();
            store.SetFloat("ratio", 1.5f);

            Assert.That(Evaluate("ratio", DialogueVariableValueType.Float, ConditionOperator.Equals, "1.5", store), Is.True);
            Assert.That(Evaluate("ratio", DialogueVariableValueType.Float, ConditionOperator.NotEquals, "2", store), Is.True);
            Assert.That(Evaluate("ratio", DialogueVariableValueType.Float, ConditionOperator.Greater, "1.4", store), Is.True);
            Assert.That(Evaluate("ratio", DialogueVariableValueType.Float, ConditionOperator.Less, "2", store), Is.True);
        }

        [Test]
        public void EvaluateSupportsStringOperators()
        {
            var store = CreateStore();
            store.SetString("line", "Kira jokes about milk");

            Assert.That(Evaluate("line", DialogueVariableValueType.String, ConditionOperator.Equals, "Kira jokes about milk", store), Is.True);
            Assert.That(Evaluate("line", DialogueVariableValueType.String, ConditionOperator.NotEquals, "Arjan answers", store), Is.True);
            Assert.That(Evaluate("line", DialogueVariableValueType.String, ConditionOperator.Contains, "milk", store), Is.True);
        }

        [Test]
        public void MissingConditionVariableUsesConfiguredFallback()
        {
            var store = CreateStore();
            var condition = CreateCondition("missing", DialogueVariableValueType.Boolean, ConditionOperator.IsTrue, null);
            condition.missingVariableResult = true;

            Assert.That(condition.Evaluate(store), Is.True);
        }

        private DialogueVariableStore CreateStore()
        {
            var go = new GameObject("ConditionNodeStoreTest");
            _objects.Add(go);
            return go.AddComponent<DialogueVariableStore>();
        }

        private bool Evaluate(
            string variableName,
            DialogueVariableValueType valueType,
            ConditionOperator conditionOperator,
            string comparisonValue,
            DialogueVariableStore store)
        {
            return CreateCondition(variableName, valueType, conditionOperator, comparisonValue).Evaluate(store);
        }

        private ConditionNode CreateCondition(
            string variableName,
            DialogueVariableValueType valueType,
            ConditionOperator conditionOperator,
            string comparisonValue)
        {
            var condition = ScriptableObject.CreateInstance<ConditionNode>();
            condition.variableName = variableName;
            condition.valueType = valueType;
            condition.conditionOperator = conditionOperator;
            condition.comparisonValue = comparisonValue;
            _objects.Add(condition);
            return condition;
        }
    }
}
