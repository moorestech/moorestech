using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.Construction;
using Client.Game.InGame.Train.RailGraph;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Tests.Common;
using Core.Master;
using Game.Construction;
using Game.Train.SaveLoad;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.TrainCostIntegration
{
    public class TrainPierPlacementEntryCostTest : TrainPlacementEntryFixture
    {
        [Test]
        public void 単体橋脚の素材不足は最終不可色と不足行を出して要求を送らない()
        {
            var preview = new RecordingPierPreview();
            var wallet = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
            var system = new TrainRailPlaceSystem(PlacementCamera, preview, Inventory, wallet);
            var block = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestTrainRail);
            var feedback = new PlacementFeedback();
            system.Enable();
            ClickRelease();

            system.ManualUpdate(new PlaceSystemUpdateContext(new BlockPlacementTarget(block.BlockGuid, null), true, feedback));

            Assert.IsTrue(preview.IsActive, "地面へのraycastが設置候補を作っていない");
            Assert.GreaterOrEqual(preview.ColorUpdates.Count, 2);
            Assert.IsTrue(preview.ColorUpdates[0], "地面判定前は設置可という前提が崩れている");
            Assert.IsFalse(preview.ColorUpdates[preview.ColorUpdates.Count - 1], "不足判定後の色が更新されていない");
            Assert.AreEqual(1, feedback.Lines.Count);
            AssertNoRequest();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void 接続判定不可は公開更新入口で橋脚の最終色へ反映する(bool sourceIsSynchronized)
        {
            var preview = new RecordingPierPreview();
            var curve = CreateObject("CurvePreview").AddComponent<RailConnectPreviewObject>();
            TestReflection.SetField(curve, "_railChain", CreateObject("RailChain").AddComponent<BezierRailChain>());
            var cache = RailGraphClientCache.CreateForEditorTest();
            var source = new SelectedConnectArea();
            if (sourceIsSynchronized)
            {
                cache.UpsertNode(0, Guid.NewGuid(), new Vector3(0, 0, -10), source.CreateConnectionDestination(), Vector3.forward, Vector3.back);
            }
            var blockStore = CreateObject("Blocks").AddComponent<BlockGameObjectDataStore>();
            var inventoryController = new LocalPlayerInventoryController(Inventory, new LocalPlayerEquipment());
            var wallet = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
            var system = new TrainRailConnectSystem(PlacementCamera, preview, curve, cache, inventoryController, blockStore, wallet);
            system.Enable();

            // 同期済みなら実素材不足、未同期ならInvalidの双方で最終色を観測する
            // Observe final colors for real material shortage and an unsynchronized Invalid source
            TestReflection.SetField(system, "_connectFromArea", source);
            ClickPress();
            var target = new ConnectToolPlacementTarget(Guid.Parse("c0000000-0000-0000-0000-000000000002"));
            var feedback = new PlacementFeedback();
            system.ManualUpdate(new PlaceSystemUpdateContext(target, false, feedback));

            Assert.IsTrue(preview.IsActive, "新設橋脚の描画経路へ到達していない");
            Assert.IsTrue(preview.ColorUpdates[0]);
            Assert.IsFalse(preview.ColorUpdates[preview.ColorUpdates.Count - 1], "接続不可が橋脚の最終色へ反映されていない");
            if (sourceIsSynchronized)
            {
                Assert.AreEqual(RailConnectionEditProtocol.RailConnectionEditFailureReason.NotEnoughRailItem,
                    TestReflection.GetField<TrainRailConnectPreviewData>(curve, "_previewDataCache").FailureReason);
                Assert.Greater(feedback.Lines.Count, 0);
            }
            AssertNoRequest();
        }

        private sealed class SelectedConnectArea : IRailComponentConnectAreaCollider
        {
            public bool IsFront => true;
            public void Initialize(BlockGameObject blockGameObject) { }
            public ConnectionDestination CreateConnectionDestination()
            {
                return new ConnectionDestination(new Vector3Int(10, 0, 10), 0, true);
            }
        }
    }
}
