using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Core.Master;
using Game.PlacementTarget;
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
                new BlockPlacementTarget(blockMaster.BlockGuid, null),
                2,
                "",
                wheelOwnedByTool: false);

            // 原文を運ばずWeb用Guidを固定
            // Pin Web GUIDs without carrying source names
            Assert.AreEqual("block", dto.SelectedTargetType);
            Assert.AreEqual(blockMaster.BlockGuid.ToString("D"), dto.SelectedBlockGuid);
            Assert.IsNull(dto.SelectedName);
        }

        [Test]
        public void PlacementModeConnectToolMatchesFixture()
        {
            AssertMatches(
                new PlacementModeDto
                {
                    SelectedTargetType = "connectTool",
                    SelectedConnectToolGuid = "abcdefab-cdef-4bcd-8fab-cdefabcdefac",
                    Height = 2,
                    UnavailableReason = "",
                },
                "placement_mode_connect_tool.json");
        }

        [Test]
        public void PlacementModeFactoryPublishesConnectToolGuidWithoutMasterName()
        {
            var connectToolGuid = Guid.Parse("abcdefab-cdef-4bcd-8fab-cdefabcdefac");
            var dto = PlacementModeDtoFactory.Create(
                new ConnectToolPlacementTarget(connectToolGuid),
                2,
                "",
                wheelOwnedByTool: false);

            // masterのNameを引かずGuidだけを配信する
            // Publish only the GUID without reading the master name
            Assert.AreEqual("connectTool", dto.SelectedTargetType);
            Assert.AreEqual(connectToolGuid.ToString("D"), dto.SelectedConnectToolGuid);
            Assert.IsNull(dto.SelectedName);
        }

        [Test]
        public void PlacementModeTrainCarMatchesFixture()
        {
            AssertMatches(
                new PlacementModeDto
                {
                    SelectedTargetType = "trainCar",
                    SelectedTrainCarGuid = "abcdefab-cdef-4bcd-8fab-cdefabcdefad",
                    Height = 2,
                    UnavailableReason = "",
                },
                "placement_mode_train_car.json");
        }

        [Test]
        public void PlacementModeFactoryPublishesTrainCarGuidWithoutMasterName()
        {
            var trainCarGuid = Guid.Parse("abcdefab-cdef-4bcd-8fab-cdefabcdefad");
            var dto = PlacementModeDtoFactory.Create(
                new TrainCarPlacementTarget(trainCarGuid),
                2,
                "",
                wheelOwnedByTool: false);

            // masterのNameを引かずGuidだけを配信する
            // Publish only the GUID without reading the master name
            Assert.AreEqual("trainCar", dto.SelectedTargetType);
            Assert.AreEqual(trainCarGuid.ToString("D"), dto.SelectedTrainCarGuid);
            Assert.IsNull(dto.SelectedName);
        }

        [Test]
        public void PlacementModeFactorySeparatesTypedCopyToolFromRawBlueprintName()
        {
            var copyTool = PlacementModeDtoFactory.Create(
                new BlueprintCopyPlacementTarget(
                    MasterHolder.BuildToolMaster.All[0].BuildToolGuid),
                2,
                "",
                wheelOwnedByTool: false);
            var blueprint = PlacementModeDtoFactory.Create(
                new BlueprintPlacementTarget(
                    Guid.Parse("60000000-0000-4000-8000-000000000001"),
                    "My Blueprint"),
                2,
                "",
                wheelOwnedByTool: false);

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
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PlacementModeDtoFactory.Create(new UnknownPlacementTarget(), 2, "", wheelOwnedByTool: false));
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
                    TextKey = "ui.tooltip.requiredItems",
                    TextParams = new[] { "Iron Pickaxe" },
                },
                "tooltip.json");
        }

        // 寸法値はWeb側が持つため、wireへ出るtooltipは表示状態と辞書キーだけを運ぶ
        // The web side owns sizes, so the tooltip reaching the wire carries only visibility and the dictionary key
        [Test]
        public void TooltipWireCarriesOnlyVisibilityKeyAndParams()
        {
            var wire = JToken.Parse(WebUiJson.Serialize(new TooltipDto
            {
                Visible = true,
                TextKey = "ui.tooltip.requiredItems",
                TextParams = new[] { "Iron Pickaxe" },
            }));
            var wireKeys = wire.Children<JProperty>().Select(property => property.Name).OrderBy(name => name).ToArray();

            CollectionAssert.AreEqual(new[] { "textKey", "textParams", "visible" }, wireKeys);
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
            public Guid Id => Guid.Parse("70000000-0000-4000-8000-000000000001");
            public PlacementTargetKind Kind => (PlacementTargetKind)999;
            public string DisplayName => "unknown";
            public IReadOnlyList<(Guid itemGuid, int count)> CreateRequiredItems() => Array.Empty<(Guid, int)>();

            public bool Equals(IPlacementTarget other)
            {
                return ReferenceEquals(this, other);
            }
        }
    }
}
