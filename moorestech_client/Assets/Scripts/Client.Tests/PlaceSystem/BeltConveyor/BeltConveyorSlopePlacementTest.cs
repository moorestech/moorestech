using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltConveyor
{
    // 坂/直線の手持ちブロック決定とセル列へのBlockId割当を、プロダクションの決定経路（TryResolve→RunBuilder.Build→
    // CalculateSlopePoint）を実際に呼び出して検証する。CalculateSlopePointはBlockGameObjectDataStoreを要求するため
    // 空のデータストアをGameObjectへ載せて渡す（既存ブロック無し＝重なり無し扱いになる）
    // Exercises the production decision path (TryResolve -> RunBuilder.Build -> CalculateSlopePoint) rather than
    // asserting on values the test assigned itself. CalculateSlopePoint needs a BlockGameObjectDataStore, so an
    // empty one is attached to a GameObject and passed in (no registered blocks means no overlap).
    public class BeltConveyorSlopePlacementTest
    {
        private GameObject _dataStoreObject;
        private BlockGameObjectDataStore _dataStore;

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            _dataStoreObject = new GameObject("BlockGameObjectDataStore");
            _dataStore = _dataStoreObject.AddComponent<BlockGameObjectDataStore>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_dataStoreObject);
        }

        // 上りを選ぶと全セルが上りブロックになり中途に直線が混ざらない
        // Selecting the up slope fills every cell with the up block and never mixes in a straight block
        [Test]
        public void 坂を選ぶと経路の全セルがその坂ブロックになる()
        {
            var holdingBlockId = ForUnitTestModBlockId.TestGearBeltConveyorUp;
            Assert.IsTrue(BeltConveyorHoldingBlock.TryResolve(holdingBlockId, out var holdingBlock));
            Assert.IsTrue(holdingBlock.IsSlopeSelected);

            var runBuilder = new BeltConveyorPlaceRunBuilder(_dataStore);
            var placeInfos = runBuilder.Build(Vector3Int.zero, new Vector3Int(2, 0, 0), BlockDirection.East, holdingBlock, out _, out var beltReasons);

            Assert.AreEqual(3, placeInfos.Count);
            Assert.IsTrue(placeInfos.All(info => info.BlockId == holdingBlockId));
            Assert.IsTrue(placeInfos.All(info => info.Placeable));
            Assert.IsTrue(beltReasons.All(reason => reason == BeltConveyorPlacementBlockReason.None));
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, placeInfos.Select(info => info.Position.y).ToList());
        }

        // 下りを選ぶと同じ経路が毎セル1段下がり、全セルが下りブロックになる
        // Selecting the down slope makes the same path descend one per cell and fills every cell with the down block
        [Test]
        public void 下りを選ぶと経路が毎セル下がる()
        {
            var holdingBlockId = ForUnitTestModBlockId.TestGearBeltConveyorDown;
            Assert.IsTrue(BeltConveyorHoldingBlock.TryResolve(holdingBlockId, out var holdingBlock));
            Assert.IsTrue(holdingBlock.IsSlopeSelected);

            var runBuilder = new BeltConveyorPlaceRunBuilder(_dataStore);
            var placeInfos = runBuilder.Build(Vector3Int.zero, new Vector3Int(2, 0, 0), BlockDirection.East, holdingBlock, out _, out _);

            Assert.IsTrue(placeInfos.All(info => info.BlockId == holdingBlockId));
            CollectionAssert.AreEqual(new[] { 0, -1, -2 }, placeInfos.Select(info => info.Position.y).ToList());
        }

        // 直線を選ぶと坂方向は引けず、手持ちはファミリーの直線ブロックになる
        // Selecting the straight block yields no slope direction, so the holding block becomes the family's straight block
        [Test]
        public void 直線を選ぶと手持ちが直線ブロックになる()
        {
            var straightBlockId = ForUnitTestModBlockId.GearBeltConveyor;
            Assert.IsTrue(BeltConveyorHoldingBlock.TryResolve(straightBlockId, out var holdingBlock));

            Assert.IsFalse(holdingBlock.IsSlopeSelected);
            Assert.AreEqual(MasterHolder.BlockMaster.GetBlockMaster(straightBlockId).BlockGuid, holdingBlock.BlockMaster.BlockGuid);
        }

        // ベルトコンベアファミリーに属さないブロックは手持ちを解決できない
        // A block outside the belt conveyor family cannot resolve a holding block
        [Test]
        public void ファミリー外のブロックは解決に失敗する()
        {
            Assert.IsFalse(BeltConveyorHoldingBlock.TryResolve(ForUnitTestModBlockId.MachineId, out var holdingBlock));
            Assert.IsNull(holdingBlock);
        }
    }
}
