using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
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
        private GameObject _existingBlockObject;

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
            if (_existingBlockObject != null) Object.DestroyImmediate(_existingBlockObject);
        }

        // 角を含む経路（L字）でも全セルが同じ坂ブロックになり、VerticalDirectionも揃う（要件8の合成網）
        // Even a path with a corner (L-shape) fills every cell with the same slope block, with a matching VerticalDirection (composition coverage for requirement 8)
        [Test]
        public void 坂を選ぶと経路の全セルがその坂ブロックになる()
        {
            var holdingBlockId = ForUnitTestModBlockId.TestGearBeltConveyorUp;
            Assert.IsTrue(BeltConveyorHoldingBlock.TryResolve(holdingBlockId, out var holdingBlock));
            Assert.IsTrue(holdingBlock.IsSlopeSelected);

            var runBuilder = new BeltConveyorPlaceRunBuilder(_dataStore);
            var placeInfos = runBuilder.Build(Vector3Int.zero, new Vector3Int(2, 0, 2), BlockDirection.East, holdingBlock, out _, out var beltReasons);

            Assert.AreEqual(5, placeInfos.Count);
            Assert.IsTrue(placeInfos.All(info => info.BlockId == holdingBlockId));
            Assert.IsTrue(placeInfos.All(info => info.Placeable));
            Assert.IsTrue(placeInfos.All(info => info.VerticalDirection == BlockVerticalDirection.Up));
            Assert.IsTrue(beltReasons.All(reason => reason == BeltConveyorPlacementBlockReason.None));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, placeInfos.Select(info => info.Position.y).ToList());
        }

        // 坂選択中は立体交差の自動持ち上げを通さないため、障害物があっても高さは変わらず、そのセルだけExistingBlockで設置不可になる（要件9）
        // A selected slope never runs the auto overpass lift, so an obstacle leaves the height profile untouched and only that cell becomes unplaceable with ExistingBlock (requirement 9)
        [Test]
        public void 坂選択中に障害物があってもそのセルだけExistingBlockになり高さは変わらない()
        {
            var holdingBlockId = ForUnitTestModBlockId.TestGearBeltConveyorUp;
            Assert.IsTrue(BeltConveyorHoldingBlock.TryResolve(holdingBlockId, out var holdingBlock));

            RegisterExistingBlock(new Vector3Int(1, 1, 0));

            var runBuilder = new BeltConveyorPlaceRunBuilder(_dataStore);
            var placeInfos = runBuilder.Build(Vector3Int.zero, new Vector3Int(2, 0, 0), BlockDirection.East, holdingBlock, out var blockCauses, out _);

            Assert.IsTrue(placeInfos[0].Placeable);
            Assert.AreEqual(PlacementBlockCause.None, blockCauses[0]);
            Assert.IsFalse(placeInfos[1].Placeable);
            Assert.AreEqual(PlacementBlockCause.ExistingBlock, blockCauses[1]);
            Assert.IsTrue(placeInfos[2].Placeable);
            Assert.AreEqual(PlacementBlockCause.None, blockCauses[2]);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, placeInfos.Select(info => info.Position.y).ToList());
        }

        // 直線選択時の合成（軸決め→CalculatePoint→CellBlockResolver.Resolve）を実行し、全セルへ直線ブロックIDが入ることを検証する
        // Exercise the straight composition (axis pick -> CalculatePoint -> CellBlockResolver.Resolve) and confirm every cell gets the straight block id
        [Test]
        public void 直線を選ぶとBuildが全セルへ直線ブロックIDを割り当てる()
        {
            var straightBlockId = ForUnitTestModBlockId.GearBeltConveyor;
            Assert.IsTrue(BeltConveyorHoldingBlock.TryResolve(straightBlockId, out var holdingBlock));
            Assert.IsFalse(holdingBlock.IsSlopeSelected);

            var runBuilder = new BeltConveyorPlaceRunBuilder(_dataStore);
            var placeInfos = runBuilder.Build(Vector3Int.zero, new Vector3Int(2, 0, 0), BlockDirection.East, holdingBlock, out _, out _);

            Assert.AreEqual(3, placeInfos.Count);
            Assert.IsTrue(placeInfos.All(info => info.BlockId == straightBlockId));
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

        // 前例（CommonBlockPlaceExistingBlockCauseTest）にならい、辞書へ直接1件登録して重なりを作る
        // Following the precedent (CommonBlockPlaceExistingBlockCauseTest), register one entry directly in the dictionary to create an overlap
        private void RegisterExistingBlock(Vector3Int position)
        {
            _existingBlockObject = new GameObject("ExistingBlock");
            var blockGameObject = _existingBlockObject.AddComponent<BlockGameObject>();
            SetBlockPosInfo(blockGameObject, new BlockPositionInfo(position, BlockDirection.North, Vector3Int.one));

            var dictionary = (Dictionary<Vector3Int, BlockGameObject>)typeof(BlockGameObjectDataStore)
                .GetField("_blockObjectsDictionary", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_dataStore);
            dictionary.Add(position, blockGameObject);
        }

        // 自動プロパティのバッキングフィールドへ直接書き、Initializeの外部依存を避ける
        // Writes the auto-property backing field directly, avoiding Initialize's external dependencies
        private static void SetBlockPosInfo(BlockGameObject blockGameObject, BlockPositionInfo posInfo)
        {
            typeof(BlockGameObject)
                .GetField($"<{nameof(BlockGameObject.BlockPosInfo)}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(blockGameObject, posInfo);
        }
    }
}
