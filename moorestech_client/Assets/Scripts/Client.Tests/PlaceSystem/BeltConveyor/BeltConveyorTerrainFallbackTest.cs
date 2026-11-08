using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Tests.PlaceSystem.ConveyorOverpass;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Client.Tests.PlaceSystem.BeltConveyor
{
    // Build経路の坂なし地形断念を検証
    // Verify through Build that only a slope-less family abandons terrain-caused slopes
    public class BeltConveyorTerrainFallbackTest
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

        [Test]
        public void 坂なし分岐器は丘で地形なしの高さに戻して設置できる()
        {
            var holdingBlock = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.GearBeltConveyorSplitter);
            Assert.IsFalse(holdingBlock.Family.UpBlockId.HasValue);
            Assert.IsFalse(holdingBlock.Family.DownBlockId.HasValue);
            var runBuilder = BuilderWithHill();

            var infos = runBuilder.Build(Vector3Int.zero, new Vector3Int(4, 0, 0), BlockDirection.East, holdingBlock, out _, out var reasons, out var terrainClimbAbandoned);

            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 0 }, infos.Select(info => info.Position.y).ToArray());
            Assert.IsTrue(infos.All(info => info.Placeable));
            Assert.IsTrue(reasons.All(reason => reason == BeltConveyorPlacementBlockReason.None));
            Assert.IsTrue(terrainClimbAbandoned);
        }

        [Test]
        public void 坂ありファミリーは同じ丘を登って降りる()
        {
            var holdingBlock = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.GearBeltConveyor);
            var runBuilder = BuilderWithHill();

            var infos = runBuilder.Build(Vector3Int.zero, new Vector3Int(4, 0, 0), BlockDirection.East, holdingBlock, out _, out _, out var terrainClimbAbandoned);

            CollectionAssert.AreEqual(new[] { 0, 0, 1, 0, 0 }, infos.Select(info => info.Position.y).ToArray());
            Assert.IsTrue(infos.All(info => info.Placeable));
            Assert.IsFalse(terrainClimbAbandoned);
        }

        [Test]
        public void 地形なしでも坂が必要なら従来の坂欠落理由だけを出す()
        {
            var holdingBlock = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.GearBeltConveyorSplitter);
            var terrain = new FakeConveyorTerrainFloor(new Dictionary<Vector2Int, int>());
            var runBuilder = new BeltConveyorPlaceRunBuilder(_dataStore, new CommonBlockPlaceDragState(new PlacementHeightOffset()), terrain);

            var infos = runBuilder.Build(Vector3Int.zero, new Vector3Int(2, 1, 0), BlockDirection.East, holdingBlock, out _, out var reasons, out var terrainClimbAbandoned);

            Assert.IsTrue(infos.Any(info => !info.Placeable));
            Assert.IsTrue(reasons.Contains(BeltConveyorPlacementBlockReason.SlopeBlockMissing));
            Assert.IsFalse(terrainClimbAbandoned);
        }

        [Test]
        public void 地形があってもL字経路の水平形状は変わらない()
        {
            var holdingBlock = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.GearBeltConveyor);
            var noTerrain = new FakeConveyorTerrainFloor(new Dictionary<Vector2Int, int>());
            var terrainFreeBuilder = new BeltConveyorPlaceRunBuilder(_dataStore, new CommonBlockPlaceDragState(new PlacementHeightOffset()), noTerrain);

            // 実経路で同一端点の全セルXZを比較
            // Compare every cell XZ of production paths with identical endpoints
            var endpoint = new Vector3Int(4, 0, 2);
            var terrainFree = terrainFreeBuilder.Build(Vector3Int.zero, endpoint, BlockDirection.East, holdingBlock, out _, out _, out _);
            var terrain = BuilderWithHill().Build(Vector3Int.zero, endpoint, BlockDirection.East, holdingBlock, out _, out _, out _);

            CollectionAssert.AreEqual(terrainFree.Select(info => new Vector2Int(info.Position.x, info.Position.z)), terrain.Select(info => new Vector2Int(info.Position.x, info.Position.z)));
        }

        [Test]
        public void 坂ありでも急な地形は元の平坦列へ戻して断念を伝える()
        {
            var terrain = new FakeConveyorTerrainFloor(new Dictionary<Vector2Int, int> { [new Vector2Int(1, 0)] = 2 });
            var builder = new BeltConveyorPlaceRunBuilder(_dataStore, new CommonBlockPlaceDragState(new PlacementHeightOffset()), terrain);
            var holding = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.GearBeltConveyor);
            var infos = builder.Build(Vector3Int.zero, new Vector3Int(2, 0, 0), BlockDirection.East, holding, out _, out var reasons, out var abandoned);

            Assert.IsTrue(abandoned);
            Assert.IsTrue(infos.All(info => info.Placeable && info.Position.y == 0));
            Assert.IsTrue(reasons.All(reason => reason == BeltConveyorPlacementBlockReason.None));
        }

        [TestCase(1, false)]
        [TestCase(2, true)]
        public void 包絡線が占有へ届いたら跨ぐか地形なしへ戻す(int obstacleHeight, bool expectedAbandoned)
        {
            // 実DataStoreの重複判定まで通す
            // Exercise overlap checks through the real DataStore
            var position = new Vector3Int(2, obstacleHeight, 0);
            var existing = new GameObject("ExistingBlock");
            existing.transform.SetParent(_dataStoreObject.transform);
            var block = existing.AddComponent<BlockGameObject>();
            typeof(BlockGameObject).GetField("<BlockPosInfo>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(block, new BlockPositionInfo(position, BlockDirection.North, Vector3Int.one));
            var dictionary = (Dictionary<Vector3Int, BlockGameObject>)typeof(BlockGameObjectDataStore)
                .GetField("_blockObjectsDictionary", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_dataStore);
            dictionary.Add(position, block);
            var terrain = new FakeConveyorTerrainFloor(new Dictionary<Vector2Int, int> { [new Vector2Int(3, 0)] = 3 });
            if (obstacleHeight == 1) terrain = new FakeConveyorTerrainFloor(new Dictionary<Vector2Int, int> { [new Vector2Int(3, 0)] = 2 });
            var builder = new BeltConveyorPlaceRunBuilder(_dataStore, new CommonBlockPlaceDragState(new PlacementHeightOffset()), terrain);
            var holding = BeltConveyorHoldingBlock.Resolve(ForUnitTestModBlockId.GearBeltConveyor);

            var infos = builder.Build(Vector3Int.zero, new Vector3Int(6, 0, 0), BlockDirection.East, holding, out _, out _, out var abandoned);

            Assert.AreEqual(expectedAbandoned, abandoned);
            Assert.IsTrue(infos.All(info => info.Placeable && info.Position != position));
            Assert.AreEqual(expectedAbandoned ? 0 : 2, infos[2].Position.y);
        }

        private BeltConveyorPlaceRunBuilder BuilderWithHill()
        {
            // 中央セルだけ地形下限を一段上げ、両端には坂の余地を残す
            // Lift only the middle cell by one level, leaving ramp room at both ends
            var terrain = new FakeConveyorTerrainFloor(new Dictionary<Vector2Int, int> { [new Vector2Int(2, 0)] = 1 });
            return new BeltConveyorPlaceRunBuilder(_dataStore, new CommonBlockPlaceDragState(new PlacementHeightOffset()), terrain);
        }
    }
}
