using System;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class BlueprintCreateServiceTest
    {
        [Test]
        public void AreaExtractionTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 箱内2/XZ範囲外1/Y範囲外1を設置
            // Two inside the box, one outside XZ, one above the box top
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(3, 0, 4), BlockDirection.East, Array.Empty<BlockCreateParam>(), out _);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(100, 0, 100), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(2, 5, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var created = BlueprintCreateService.TryCreateFromArea("test", new Vector3Int(0, 0, 0), new Vector3Int(5, 2, 5), out var blueprint);

            Assert.IsTrue(created);
            Assert.AreEqual(2, blueprint.Blocks.Count);

            // アンカーは占有外形で決まる
            // Derive the anchor from the occupied extent of chest and machine
            var machineInfo = ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(3, 0, 4)).BlockPositionInfo;
            var expectedAnchor = new Vector3Int(Mathf.FloorToInt(machineInfo.MaxPos.x / 2f), 0, Mathf.FloorToInt(machineInfo.MaxPos.z / 2f));
            var chestBlock = blueprint.Blocks.First(b => b.Direction == (int)BlockDirection.North);
            Assert.AreEqual(-expectedAnchor, chestBlock.Offset);
            Assert.AreEqual((int)BlockDirection.North, chestBlock.Direction);

            var machineBlock = blueprint.Blocks.First(b => b.Direction == (int)BlockDirection.East);
            Assert.AreEqual(new Vector3Int(3, 0, 4) - expectedAnchor, machineBlock.Offset);
            Assert.AreEqual((int)BlockDirection.East, machineBlock.Direction);
        }

        [Test]
        public void AnchorFollowsBlockExtentNotBoxTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 余白にアンカーが引かれない
            // Place one block in a wide box and verify margins do not move the anchor
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var created = BlueprintCreateService.TryCreateFromArea("extent", Vector3Int.zero, new Vector3Int(9, 2, 9), out var blueprint);

            Assert.IsTrue(created);
            Assert.AreEqual(Vector3Int.zero, blueprint.Blocks[0].Offset);
        }

        [Test]
        public void BoxHeightIncludesElevatedBlockTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 上面を高くするとy=5も対象
            // A taller box includes the elevated block
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(2, 5, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var created = BlueprintCreateService.TryCreateFromArea("tall", new Vector3Int(0, 0, 0), new Vector3Int(5, 10, 5), out var blueprint);

            Assert.IsTrue(created);
            Assert.AreEqual(2, blueprint.Blocks.Count);
            var elevated = blueprint.Blocks.First(b => b.Offset == new Vector3Int(1, 5, 1));
            Assert.NotNull(elevated);
            Assert.IsTrue(blueprint.Blocks.Any(b => b.Offset == new Vector3Int(-1, 0, -1)));
        }

        [Test]
        public void NegativeCoordinateAnchorIsFlooredTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 負座標の中心も下へ丸める
            // Floor the center of the (-4..-1) extent toward negative infinity
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(-4, 0, -4), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(-1, 0, -1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var created = BlueprintCreateService.TryCreateFromArea("negative", new Vector3Int(-4, 0, -4), new Vector3Int(-1, 2, -1), out var blueprint);

            Assert.IsTrue(created);
            Assert.IsTrue(blueprint.Blocks.Any(b => b.Offset == new Vector3Int(-1, 0, -1)));
            Assert.IsTrue(blueprint.Blocks.Any(b => b.Offset == new Vector3Int(2, 0, 2)));
        }

        [Test]
        public void EmptyAreaReturnsFalseTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var created = BlueprintCreateService.TryCreateFromArea("empty", new Vector3Int(0, 0, 0), new Vector3Int(5, 2, 5), out var blueprint);

            Assert.IsFalse(created);
            Assert.IsNull(blueprint);
        }

        [Test]
        public void RailFamilyBlocksAreExcludedTest()
        {
            var environment = TrainTestHelper.CreateEnvironment();

            // チェストとレールを同一ボックス内に設置
            // Place a chest and a rail inside the same box
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            TrainTestHelper.PlaceBlock(environment, ForUnitTestModBlockId.TestTrainRail, new Vector3Int(2, 0, 2), BlockDirection.North);

            var created = BlueprintCreateService.TryCreateFromArea("railExcluded", new Vector3Int(0, 0, 0), new Vector3Int(5, 2, 5), out var blueprint);

            Assert.IsTrue(created);
            // レール系は対象外でチェストのみ
            // Only the chest remains because rail-family blocks are excluded
            Assert.AreEqual(1, blueprint.Blocks.Count);
            var chestGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockGuid;
            Assert.AreEqual(chestGuid, blueprint.Blocks[0].BlockGuid);
        }
    }
}
