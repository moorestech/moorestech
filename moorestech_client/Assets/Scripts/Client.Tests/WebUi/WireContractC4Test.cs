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
        public void TutorialOverlayElementsMatchFixture()
        {
            var dto = new TutorialPresentationData
            {
                Revision = 3,
                Sessions = new[]
                {
                    new TutorialSessionData
                    {
                        TutorialSessionId = "tutorial-1", ChallengeId = "challenge-1",
                        Elements = new TutorialOverlayElementData[]
                        {
                            new TutorialOutlineElementData
                            {
                                ElementId = "craft", AnchorId = "recipe.craft-button",
                                PaddingPx = 4, BlocksPointerInput = false,
                            },
                            new TutorialDragGuideElementData
                            {
                                ElementId = "guide-1", FromAnchorId = "hotbar.hud", ToAnchorId = "recipe.craft-button",
                            },
                        },
                    },
                },
            };

            AssertMatches(dto, "tutorial_presentation.json");
        }

        [Test]
        public void WorldPinMatchesFixture()
        {
            var dto = new WorldPinPresentationData
            {
                Revision = 4,
                Pins = new[]
                {
                    new WorldPinData
                    {
                        PinId = "map-object-pin", TutorialGuid = "abcdefab-cdef-4bcd-8fab-cdefabcdefae",
                        ScreenX = 0.25f, ScreenY = 0.75f, OnScreen = true,
                        DirectionX = 0.5f, DirectionY = -0.5f,
                    },
                },
            };

            AssertMatches(dto, "world_pins.json");
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
