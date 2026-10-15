using System.Collections.Generic;
using System.Linq;
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
    // 坂を持たないファミリーだけが地形由来の坂を諦めることをBuild経路で確かめる
    // Verify through Build that only a family without slopes abandons terrain-caused slopes
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

            // 同じ端点から本物の経路生成を通し、全セルの水平位置を比較する
            // Generate both paths through production code and compare every horizontal cell position
            var endpoint = new Vector3Int(4, 0, 2);
            var terrainFree = terrainFreeBuilder.Build(Vector3Int.zero, endpoint, BlockDirection.East, holdingBlock, out _, out _, out _);
            var terrain = BuilderWithHill().Build(Vector3Int.zero, endpoint, BlockDirection.East, holdingBlock, out _, out _, out _);

            CollectionAssert.AreEqual(terrainFree.Select(info => new Vector2Int(info.Position.x, info.Position.z)), terrain.Select(info => new Vector2Int(info.Position.x, info.Position.z)));
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
