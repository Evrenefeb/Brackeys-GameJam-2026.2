using DialogSystem.EditorTools.Localization;
using NUnit.Framework;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DialogLocaleKeyGeneratorTests
    {
        [Test]
        public void GenerateDialogNodeKey_UsesFullReadableNodeId()
        {
            var key = DialogLocaleKeyGenerator.GenerateDialogNodeKey("Demo Product Tour", "demo_tour_d1");

            Assert.That(key, Is.EqualTo("demo_product_tour.demo_tour_d1.text"));
        }

        [Test]
        public void GenerateDialogNodeKey_DoesNotCollapseReadablePrefixes()
        {
            var first = DialogLocaleKeyGenerator.GenerateDialogNodeKey("graph", "demo_tour_d1");
            var second = DialogLocaleKeyGenerator.GenerateDialogNodeKey("graph", "demo_tour_d2");

            Assert.That(first, Is.Not.EqualTo(second));
        }

        [Test]
        public void GenerateChoiceKey_UsesFullChoiceId()
        {
            var key = DialogLocaleKeyGenerator.GenerateChoiceKey(
                "graph",
                "demo_tour_c1",
                "b88acddfc2e341eeb7534be9afd3107c");

            Assert.That(key, Is.EqualTo("graph.demo_tour_c1.choice_b88acddfc2e341eeb7534be9afd3107c"));
        }
    }
}
