using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.Ground;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.PlaceSystem.Ground.Entry
{
    public class BeltTerrainWiringTest : FootprintGroundedEntryFixture
    {
        public override void Setup()
        {
            base.Setup();
            Ground.transform.SetPositionAndRotation(new Vector3(504, 20.7f, 600.5f), Quaternion.identity);
            Ground.GetComponent<BoxCollider>().size = new Vector3(20, 1, 20);
            Physics.SyncTransforms();
        }

        [Test]
        public void SharedOffsetIsReadAgainAndFlowsIntoBuild()
        {
            AddHill(504.5f);
            var height = new PlacementHeightOffset();
            var terrain = new GroundConveyorTerrainFloor(height);
            var builder = new BeltConveyorPlaceRunBuilder(Blocks, new CommonBlockPlaceDragState(height), terrain);
            var holding = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.GearBeltConveyor);

            // 同じアダプターを使い続け補正の固定化を検出する
            // Keep the same adapter to detect a frozen offset
            foreach (var offset in new[] { 0, -1, 1 })
            {
                height.Adjust(offset - height.Value);
                Assert.IsTrue(terrain.TryGetFloorY(new Vector3Int(504, 21, 600), out var floor));
                Assert.AreEqual(23 + offset, floor);
                var infos = builder.Build(new Vector3Int(501, 21 + offset, 600), new Vector3Int(507, 21 + offset, 600), BlockDirection.East, holding, out _, out _, out var abandoned);
                Assert.AreEqual(23 + offset, infos[3].Position.y);
                Assert.IsFalse(abandoned);
                Assert.IsTrue(infos.All(info => info.Placeable));

                // 平地は下降で埋設、上昇で余白
                // A lowered flat run stays buried; a raised run keeps its clearance
                var flat = builder.Build(new Vector3Int(501, 21 + offset, 604), new Vector3Int(507, 21 + offset, 604), BlockDirection.East, holding, out _, out _, out _);
                Assert.IsTrue(flat.All(info => info.Position.y == 21 + offset));
            }
        }

        [Test]
        public void PublicDragUpdateShowsFallbackPreviewAndOneNotice()
        {
            AddHill(502.5f);
            var height = new PlacementHeightOffset();
            var system = new BeltConveyorPlaceSystem(PlacementCamera, Preview, Blocks, Inventory, Wallet, height);
            var master = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor);
            var target = new BlockPlacementTarget(master.BlockGuid, null);
            var feedback = new PlacementFeedback();
            system.Enable();

            // 実入力で始点固定後、丘の先へ照準
            // Pin the start through real input, then aim beyond the hill
            ClickPress();
            system.ManualUpdate(new PlaceSystemUpdateContext(target, true, feedback));
            InputSystem.Update();
            PlacementCamera.transform.position = new Vector3(503.5f, 40, 600.5f);
            feedback.Clear();
            system.ManualUpdate(new PlaceSystemUpdateContext(target, false, feedback));

            Assert.IsTrue(Preview.IsActive);
            Assert.AreEqual(3, Preview.Cells.Count);
            Assert.IsTrue(Preview.Cells.All(info => info.Position.y == 21));
            Assert.AreEqual(1, feedback.Lines.Count(line => line.Key.Key == LocalizationKeys.Ui.Tooltip.PlaceBeltTerrainClimbAbandoned.Key));
            AssertNoRequest();
        }

        private void AddHill(float x)
        {
            var hill = CreateObject("Hill");
            hill.layer = Ground.layer;
            hill.transform.position = new Vector3(x, 22.7f, 600.5f);
            hill.AddComponent<BoxCollider>().size = Vector3.one;
            Physics.SyncTransforms();
        }
    }
}
