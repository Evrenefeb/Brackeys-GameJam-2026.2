using DialogSystem.EditorTools.Services;
using DialogSystem.Runtime.Variables;
using NUnit.Framework;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DialogAssetInitializerTests
    {
        private readonly System.Collections.Generic.List<Object> _objects = new();

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
        public void InitializeVariable_SetsAssetNameAndSanitizedKey()
        {
            var asset = ScriptableObject.CreateInstance<DialogVariableSO>();
            _objects.Add(asset);

            DialogAssetInitializer.InitializeVariable(
                asset,
                "Player Mood",
                DialogueVariableValueType.String,
                "Calm");

            Assert.That(asset.name, Is.EqualTo("Player Mood"));
            Assert.That(asset.Key, Is.EqualTo("player_mood"));
            Assert.That(asset.ValueType, Is.EqualTo(DialogueVariableValueType.String));
            Assert.That(asset.StringDefaultValue, Is.EqualTo("Calm"));
        }
    }
}
