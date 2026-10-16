using System;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Modes;
using Client.Game.InGame.BlockSystem.PlaceSystem.GearChainPoleConnect.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail;
using Core.Master;
using Game.Context;
using Game.UnlockState;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.Ground.Entry
{
    public class FootprintGroundedPlaceSystemWiringTest : FootprintGroundedEntryFixture
    {
        [TestCase(0)]
        [TestCase(2)]
        public void BeltManualUpdateProducesFootprintHeight(int offset)
        {
            var height = new PlacementHeightOffset();
            height.Adjust(offset);
            var system = new BeltConveyorPlaceSystem(PlacementCamera, Preview, Blocks, Inventory, Wallet, height);
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId);

            // プレビュー出力まで通し旧レイ入口への退行を捕まえる
            // Drive preview output to catch a regression to the old ray entry
            system.ManualUpdate(new PlaceSystemUpdateContext(new BlockPlacementTarget(master.BlockGuid, null), false, new PlacementFeedback()));

            Assert.AreEqual(1, Preview.Cells.Count);
            Assert.AreEqual(21 + offset, Preview.Cells[0].Position.y);
        }

        [Test]
        public void PierManualUpdateProducesFootprintHeight()
        {
            var system = new TrainRailPlaceSystemService(PlacementCamera, Preview);
            system.Enable();

            // 橋脚の最終設置セルを公開入口から得る
            // Obtain the final pier placement cell through the public entry
            var placeInfo = system.ManualUpdate(ForUnitTestModBlockId.TestTrainRail, new PlacementFeedback());

            Assert.IsNotNull(placeInfo);
            Assert.AreEqual(21, placeInfo.Position.y);
        }

        [Test]
        public void ElectricPoleEvaluationProducesFootprintHeight()
        {
            var selection = new ElectricWirePoleSelection();
            selection.RefreshUnlockedPoles(ServerContext.GetService<IGameUnlockStateDataController>());
            Assert.IsTrue(selection.TryGetSelectedPole(out var blockId, out _));
            Assert.AreEqual(ForUnitTestModBlockId.ElectricPoleId, blockId);
            var ghost = new ElectricWirePoleGhostPart(PlacementCamera, Preview, Inventory, new CommonBlockPlacePointCalculator(Blocks), Wallet);

            // 手組みの評価値でなく電柱ゴーストを計算させる
            // Calculate the pole ghost rather than fabricate evaluation values
            Assert.IsTrue(ghost.TryEvaluateGhost(selection, new PlacementFeedback(), out var evaluation));
            Assert.AreEqual(21, evaluation.PlaceInfo.Position.y);
        }

        [Test]
        public void GearCollectorProducesFootprintHeight()
        {
            var collector = new GearChainPoleFrameInputCollector(PlacementCamera, Inventory, Blocks, new GearChainPoleExtendPreviewObject(Preview), Wallet);
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearChainPole);

            // 実レイと地表探査から収集したセルを確かめる
            // Check the cell collected from a real ray and ground probing
            var input = collector.CollectPlaceExtend(null, master, false, Guid.Empty);

            Assert.AreEqual(GearChainPoleGhostState.Shown, input.GhostState);
            Assert.AreEqual(21, input.GhostPlaceInfo.Position.y);
        }

        [Test]
        public void MissingGroundFlowsFromCollectorIntoDecisionFeedback()
        {
            RemoveFootprintGround();
            var collector = new GearChainPoleFrameInputCollector(PlacementCamera, Inventory, Blocks, new GearChainPoleExtendPreviewObject(Preview), Wallet);
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearChainPole);

            // 状態を手組みせず収集から判断まで通す
            // Run collection through decision without fabricating the ghost state
            var input = collector.CollectPlaceExtend(null, master, false, Guid.Empty);
            var result = GearChainPolePlaceExtendMode.Decide(input);

            Assert.AreEqual(GearChainPoleGhostState.GroundNotFound, input.GhostState);
            Assert.IsFalse(result.Preview.GhostVisible);
            Assert.AreEqual(1, result.FeedbackLines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceGroundNotFound.Key, result.FeedbackLines[0].Key.Key);
        }
    }
}
