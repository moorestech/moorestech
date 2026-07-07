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

            // ボックス内2つ・XZ範囲外1つ・Y範囲外1つを設置
            // Two inside the box, one outside XZ, one above the box top
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(3, 0, 4), BlockDirection.East, Array.Empty<BlockCreateParam>(), out _);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(100, 0, 100), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(2, 5, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var created = BlueprintCreateService.TryCreateFromArea("test", new Vector3Int(0, 0, 0), new Vector3Int(5, 2, 5), out var blueprint);

            Assert.IsTrue(created);
            // Y上限2のボックスなのでy=5のブロックは含まれない
            // The y=5 block is excluded because the box top is y=2
            Assert.AreEqual(2, blueprint.Blocks.Count);

            // アンカーはボックスXZ中心・最下段(2, 0, 2)。原点(0,0,0)のオフセットは(-2,0,-2)
            // Anchor is box XZ center and bottom Y (2, 0, 2)
            var chestBlock = blueprint.Blocks.First(b => b.Offset == new Vector3Int(-2, 0, -2));
            Assert.AreEqual((int)BlockDirection.North, chestBlock.Direction);

            var machineBlock = blueprint.Blocks.First(b => b.Offset == new Vector3Int(1, 0, 2));
            Assert.AreEqual((int)BlockDirection.East, machineBlock.Direction);
        }

        [Test]
        public void BoxHeightIncludesElevatedBlockTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 上面を高くしたボックスならy=5のブロックも含まれる
            // A taller box includes the elevated block
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(2, 5, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var created = BlueprintCreateService.TryCreateFromArea("tall", new Vector3Int(0, 0, 0), new Vector3Int(5, 10, 5), out var blueprint);

            Assert.IsTrue(created);
            Assert.AreEqual(2, blueprint.Blocks.Count);
            var elevated = blueprint.Blocks.First(b => b.Offset == new Vector3Int(0, 5, 0));
            Assert.NotNull(elevated);
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
            // レール系はコピー対象外なのでチェストのみ含まれる
            // Only the chest remains because rail-family blocks are excluded
            Assert.AreEqual(1, blueprint.Blocks.Count);
            var chestGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockGuid;
            Assert.AreEqual(chestGuid, blueprint.Blocks[0].BlockGuid);
        }
    }
}
