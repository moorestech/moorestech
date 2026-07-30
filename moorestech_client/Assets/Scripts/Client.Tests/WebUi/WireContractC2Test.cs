using System;
using System.IO;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Core.Master;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.WebUi
{
    public class WireContractC2Test
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(
                new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void PlacementModeMatchesFixture()
        {
            AssertMatches(
                new PlacementModeDto
                {
                    SelectedTargetType = "block",
                    SelectedBlockGuid = "abcdefab-cdef-4bcd-8fab-cdefabcdefab",
                    Height = 2,
                    UnavailableReason = "",
                },
                "placement_mode.json");
        }

        [Test]
        public void PlacementModeFactoryPublishesOnlyResolvableBlockIdentity()
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId);
            var dto = PlacementModeDtoFactory.Create(
                new BlockPlacementTarget(ForUnitTestModBlockId.BlockId, null),
                2,
                "");

            // 原文を運ばずWeb用Guidを固定
            // Pin Web GUIDs without carrying source names
            Assert.AreEqual("block", dto.SelectedTargetType);
            Assert.AreEqual(blockMaster.BlockGuid.ToString("D"), dto.SelectedBlockGuid);
            Assert.IsNull(dto.SelectedName);
        }

        [Test]
        public void PlacementModeFactorySeparatesTypedCopyToolFromRawBlueprintName()
        {
            var copyTool = PlacementModeDtoFactory.Create(
                new BlueprintCopyToolPlacementTarget(),
                2,
                "");
            var blueprint = PlacementModeDtoFactory.Create(
                new BlueprintPlacementTarget("My Blueprint"),
                2,
                "");

            // BPコピーはtyped、命名BPはraw
            // Type blueprint copies while preserving authored blueprint names
            Assert.AreEqual("blueprintCopy", copyTool.SelectedTargetType);
            Assert.IsNull(copyTool.SelectedName);
            Assert.AreEqual("raw", blueprint.SelectedTargetType);
            Assert.AreEqual("My Blueprint", blueprint.SelectedName);
        }

        [Test]
        public void PlacementModeFactoryRejectsUnknownTargetType()
        {
            Assert.Throws<InvalidOperationException>(() =>
                PlacementModeDtoFactory.Create(new UnknownPlacementTarget(), 2, ""));
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
        public void TooltipMatchesFixture()
        {
            AssertMatches(
                new TooltipDto
                {
                    Visible = true,
                    TextKey = "Cannot remove",
                    FontSize = 36,
                    IsLocalize = false,
                },
                "tooltip.json");
        }

        private static void AssertMatches(object dto, string fixtureName)
        {
            var actual = JToken.Parse(WebUiJson.Serialize(dto));
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", fixtureName);
            var expected = JToken.Parse(File.ReadAllText(path));
            Assert.IsTrue(JToken.DeepEquals(expected, actual), $"{fixtureName} mismatch");
        }

        private sealed class UnknownPlacementTarget : IPlacementTarget
        {
            public bool Equals(IPlacementTarget other)
            {
                return ReferenceEquals(this, other);
            }
        }
    }
}
