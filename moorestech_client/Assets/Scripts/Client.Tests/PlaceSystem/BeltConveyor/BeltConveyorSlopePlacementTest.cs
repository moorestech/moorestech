using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Tests.PlaceSystem.BeltConveyor
{
    // 坂/直線の決定経路を通し実検証するテスト
    // - 経路: Resolve→RunBuilder.Build→CalculateSlopePoint
    // - Unity依存: 空のBlockGameObjectDataStoreを渡す（既存ブロック無し扱い）
    // Exercises the production decision path (slope/straight) end to end
    // - Path: Resolve->RunBuilder.Build->CalculateSlopePoint
    // - Unity dependency: pass an empty BlockGameObjectDataStore (treated as no existing blocks)
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

        // L字でも全セル同一坂・向きも揃う（要件8）
        // Even an L-shaped path keeps every cell the same slope block and direction (requirement 8)
        [Test]
        public void 坂を選ぶと経路の全セルがその坂ブロックになる()
        {
            var holdingBlockId = ForUnitTestModBlockId.TestGearBeltConveyorUp;
            var holdingBlock = BeltConveyorHoldingBlock.Resolve(holdingBlockId);
            Assert.AreEqual(BlockVerticalDirection.Up, holdingBlock.SlopeDirection);

            var runBuilder = new BeltConveyorPlaceRunBuilder(_dataStore, new CommonBlockPlaceDragState());
            var placeInfos = runBuilder.Build(Vector3Int.zero, new Vector3Int(2, 0, 2), BlockDirection.East, holdingBlock, out _, out var beltReasons);

            Assert.AreEqual(5, placeInfos.Count);
            Assert.IsTrue(placeInfos.All(info => info.BlockId == holdingBlockId));
            Assert.IsTrue(placeInfos.All(info => info.Placeable));
            Assert.IsTrue(placeInfos.All(info => info.VerticalDirection == BlockVerticalDirection.Up));
            Assert.IsTrue(beltReasons.All(reason => reason == BeltConveyorPlacementBlockReason.None));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4 }, placeInfos.Select(info => info.Position.y).ToList());
        }

        // 坂選択中は立体交差なし、障害物セルのみExistingBlock（要件9）
        // A slope selection has no overpass; only the obstructed cell becomes ExistingBlock (requirement 9)
        [Test]
        public void 坂選択中に障害物があってもそのセルだけExistingBlockになり高さは変わらない()
        {
            var holdingBlockId = ForUnitTestModBlockId.TestGearBeltConveyorUp;
            var holdingBlock = BeltConveyorHoldingBlock.Resolve(holdingBlockId);

            RegisterExistingBlock(new Vector3Int(1, 1, 0));

            var runBuilder = new BeltConveyorPlaceRunBuilder(_dataStore, new CommonBlockPlaceDragState());
            var placeInfos = runBuilder.Build(Vector3Int.zero, new Vector3Int(2, 0, 0), BlockDirection.East, holdingBlock, out var blockCauses, out _);

            Assert.IsTrue(placeInfos[0].Placeable);
            Assert.AreEqual(PlacementBlockCause.None, blockCauses[0]);
            Assert.IsFalse(placeInfos[1].Placeable);
            Assert.AreEqual(PlacementBlockCause.ExistingBlock, blockCauses[1]);
            Assert.IsTrue(placeInfos[2].Placeable);
            Assert.AreEqual(PlacementBlockCause.None, blockCauses[2]);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, placeInfos.Select(info => info.Position.y).ToList());
        }

        // 直線選択時の合成経路を検証、全セルへ直線BlockIdが入る
        // Verify the straight-selection pipeline fills every cell with the straight BlockId
        [Test]
        public void 直線を選ぶとBuildが全セルへ直線ブロックIDを割り当てる()
        {
            var straightBlockId = ForUnitTestModBlockId.GearBeltConveyor;
            var holdingBlock = BeltConveyorHoldingBlock.Resolve(straightBlockId);
            Assert.IsNull(holdingBlock.SlopeDirection);

            var runBuilder = new BeltConveyorPlaceRunBuilder(_dataStore, new CommonBlockPlaceDragState());
            var placeInfos = runBuilder.Build(Vector3Int.zero, new Vector3Int(2, 0, 0), BlockDirection.East, holdingBlock, out _, out _);

            Assert.AreEqual(3, placeInfos.Count);
            Assert.IsTrue(placeInfos.All(info => info.BlockId == straightBlockId));
        }

        // 下り選択で毎セル1段下がり全セル下りになる
        // Selecting Down drops one level per cell; every cell becomes the down block
        [Test]
        public void 下りを選ぶと経路が毎セル下がる()
        {
            var holdingBlockId = ForUnitTestModBlockId.TestGearBeltConveyorDown;
            var holdingBlock = BeltConveyorHoldingBlock.Resolve(holdingBlockId);
            Assert.AreEqual(BlockVerticalDirection.Down, holdingBlock.SlopeDirection);

            var runBuilder = new BeltConveyorPlaceRunBuilder(_dataStore, new CommonBlockPlaceDragState());
            var placeInfos = runBuilder.Build(Vector3Int.zero, new Vector3Int(2, 0, 0), BlockDirection.East, holdingBlock, out _, out _);

            Assert.IsTrue(placeInfos.All(info => info.BlockId == holdingBlockId));
            CollectionAssert.AreEqual(new[] { 0, -1, -2 }, placeInfos.Select(info => info.Position.y).ToList());
        }

        // 直線選択時は坂方向を引けず手持ちは直線になる
        // Selecting straight yields no slope direction; the holding block is the family's straight block
        [Test]
        public void 直線を選ぶと手持ちが直線ブロックになる()
        {
            var straightBlockId = ForUnitTestModBlockId.GearBeltConveyor;
            var holdingBlock = BeltConveyorHoldingBlock.Resolve(straightBlockId);

            Assert.IsNull(holdingBlock.SlopeDirection);
            Assert.AreEqual(straightBlockId, holdingBlock.BlockId);
            Assert.AreEqual(MasterHolder.BlockMaster.GetBlockMaster(straightBlockId).BlockGuid, holdingBlock.BlockMaster.BlockGuid);
        }

        // 非ファミリーブロックはここへ到達しない契約なので例外で表明する
        // A non-family block never reaches here by contract, so the violation is thrown
        [Test]
        public void ファミリー外のブロックは解決に失敗する()
        {
            Assert.Throws<InvalidOperationException>(() => BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.MachineId));
        }

        // 前例にならい辞書へ直接1件登録し重なりを作る
        // Following precedent, register one entry directly to create an overlap
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
