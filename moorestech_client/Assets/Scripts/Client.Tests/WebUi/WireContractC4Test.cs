using System.IO;
using Client.Game.InGame.Tutorial;
using Client.Skit.UI;
using Client.WebUiHost.Common;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi
{
    public class WireContractC4Test
    {
        [Test]
        public void BlockingSkitMatchesFixture()
        {
            var dto = SkitPresentationData.CreateBlocking(
                "skit-1", 2, "Moore", "Choose",
                new[] { new SkitChoice { ChoiceId = "route-a", Label = "Route A" } },
                true, true, false, false, false, "instant", 0,
                new[] { "select", "set-auto", "skip", "set-ui-hidden" });

            AssertMatches(dto, "skit_presentation.json");
        }

        [Test]
        public void TutorialHighlightMatchesFixture()
        {
            var dto = new TutorialPresentationData
            {
                TutorialSessionId = "tutorial-1", Revision = 3, ChallengeId = "challenge-1",
                Highlights = new[]
                {
                    new TutorialHighlightData
                    {
                        HighlightId = "craft", AnchorId = "recipe.craft-button", Kind = "outline",
                        Message = "Craft", PaddingPx = 4, BlocksPointerInput = false,
                    },
                },
            };

            AssertMatches(dto, "tutorial_presentation.json");
        }

        private static void AssertMatches(object dto, string fixtureName)
        {
            var actual = JToken.Parse(WebUiJson.Serialize(dto));
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", fixtureName);
            var expected = JToken.Parse(File.ReadAllText(path));
            Assert.IsTrue(JToken.DeepEquals(expected, actual), $"{fixtureName} mismatch");
        }
    }
}
