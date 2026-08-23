using System.Collections.Generic;
using DialogSystem.Runtime.Variables;
using NUnit.Framework;
using UnityEngine;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DialogueVariableTextFormatterTests
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
        public void FormatReplacesValidTokenForms()
        {
            var store = CreateStore();
            store.SetString("speaker", "Kira");
            store.SetInt("score", 3);
            store.SetBool("ready", true);

            var formatted = DialogueVariableTextFormatter.Format(
                "{speaker} has {{score}} points. Ready: {var:ready}.",
                store);

            Assert.That(formatted, Is.EqualTo("Kira has 3 points. Ready: true."));
        }

        [Test]
        public void FormatKeepsMissingTokensUnlessFallbackIsProvided()
        {
            var store = CreateStore();

            Assert.That(DialogueVariableTextFormatter.Format("Hello {missing}.", store), Is.EqualTo("Hello {missing}."));
            Assert.That(DialogueVariableTextFormatter.Format("Hello {missing}.", store, "?"), Is.EqualTo("Hello ?."));
        }

        [Test]
        public void FormatAcceptsHumanReadableSpacedTokenAliases()
        {
            var store = CreateStore();
            store.SetString("playerName", "Kira");
            store.SetBool("hasKey", true);

            Assert.That(
                DialogueVariableTextFormatter.Format("Hello {player name}. Key: {has key}.", store),
                Is.EqualTo("Hello Kira. Key: true."));
        }

        [Test]
        public void FormatAcceptsCaseInsensitiveTokenAliases()
        {
            var store = CreateStore();
            store.SetString("playerName", "Kira");

            Assert.That(
                DialogueVariableTextFormatter.Format("Hello {playername}.", store),
                Is.EqualTo("Hello Kira."));
        }

        [Test]
        public void FormatLeavesMalformedTokensVisible()
        {
            var store = CreateStore();
            store.SetString("name", "Arjan");

            Assert.That(DialogueVariableTextFormatter.Format("Hello {name", store), Is.EqualTo("Hello {name"));
            Assert.That(DialogueVariableTextFormatter.Format("Hello {{name}", store), Is.EqualTo("Hello {{name}"));
            Assert.That(DialogueVariableTextFormatter.Format("Hello {var:}", store), Is.EqualTo("Hello {var:}"));
        }

        private DialogueVariableStore CreateStore()
        {
            var go = new GameObject("TextFormatterStoreTest");
            _objects.Add(go);
            return go.AddComponent<DialogueVariableStore>();
        }
    }
}
