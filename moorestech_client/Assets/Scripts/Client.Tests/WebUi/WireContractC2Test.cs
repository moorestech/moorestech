using System.IO;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi
{
    public class WireContractC2Test
    {
        [Test]
        public void PlacementModeMatchesFixture()
        {
            AssertMatches(new PlacementModeDto { SelectedName = "Conveyor Belt", Height = 2, UnavailableReason = "", EnergizedRangeVisible = true }, "placement_mode.json");
        }

        [Test]
        public void DeleteModeMatchesFixture()
        {
            AssertMatches(new DeleteModeDto { UnavailableReason = "Cannot remove" }, "delete_mode.json");
        }

        [Test]
        public void CommonHudMatchesFixtures()
        {
            AssertMatches(new VisibilityDto { Visible = true }, "visibility.json");
        }

        [Test]
        public void MiningHudMatchesFixture()
        {
            AssertMatches(new MiningHudDto { Visible = true, TargetName = "Rock", Mining = true, Progress = 0.5f }, "mining_hud.json");
        }

        [Test]
        public void TooltipMatchesFixture()
        {
            AssertMatches(new TooltipDto { Visible = true, TextKey = "Cannot remove", FontSize = 36 }, "tooltip.json");
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
