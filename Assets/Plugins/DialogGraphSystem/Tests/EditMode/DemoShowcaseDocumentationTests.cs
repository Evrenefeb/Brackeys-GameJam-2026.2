using System.IO;
using NUnit.Framework;

namespace DialogSystem.Tests.EditMode
{
    public sealed class DemoShowcaseDocumentationTests
    {
        private const string GuidePath = "Assets/DialogGraphSystem/Documentation/DEMO_GUIDE.md";

        [Test]
        public void DemoGuide_MapsEveryEntryGraphRouteAndActionReceiver()
        {
            var guide = File.ReadAllText(GuidePath);

            StringAssert.Contains("Demo_ProductTour.asset", guide);
            StringAssert.Contains("Demo_ShopGate.asset", guide);
            StringAssert.Contains("Demo_ControlRoomActions.asset", guide);
            StringAssert.Contains("Demo_ReactorAftermath.asset", guide);
            StringAssert.Contains("Product Tour", guide);
            StringAssert.Contains("Shop Gate", guide);
            StringAssert.Contains("Reactor Control Room", guide);
            StringAssert.Contains("TurnOnTV", guide);
            StringAssert.Contains("FadeLights", guide);
            StringAssert.Contains("PlayAlarm", guide);
            StringAssert.Contains("StartCountdown", guide);
            StringAssert.Contains("OpenGate", guide);
            StringAssert.Contains("English and Deutsch", guide);
            StringAssert.Contains("Reset / Re-run", guide);
            StringAssert.Contains("Action Feedback Objects", guide);
        }

        [Test]
        public void PackageDocs_ReferenceTheProfessionalThreeDemoCatalog()
        {
            var readme = File.ReadAllText("Assets/DialogGraphSystem/README.md");
            var quickStart = File.ReadAllText("Assets/DialogGraphSystem/Documentation/QUICK_START.md");
            var changelog = File.ReadAllText("Assets/DialogGraphSystem/CHANGELOG.md");
            var bundled = File.ReadAllText("Assets/DialogGraphSystem/Documentation/Bundled/DOCUMENTATION.txt");
            var combined = string.Join("\n", readme, quickStart, changelog, bundled);

            StringAssert.Contains("Documentation/DEMO_GUIDE.md", combined);
            StringAssert.Contains("exactly three selectable demos", combined);
            StringAssert.Contains("Demo_ShopGate", combined);
            StringAssert.DoesNotContain("Shop-Gate-Test", readme);
            StringAssert.DoesNotContain("Shop-Gate-Test", quickStart);
        }
    }
}
