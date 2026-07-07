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
            // Y上限2のためy=5は対象外
            // The y=5 block is excluded because the box top is y=2
            Assert.AreEqual(2, blueprint.Blocks.Count);

            // アンカー(2,0,2)、原点のオフセット(-2,0,-2)
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

            // 上面を高くするとy=5も対象
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
        public void NegativeCoordinateAnchorIsFlooredTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 負座標ボックス(-4,0,-4)-(-1,2,-1)の中心はfloorで(-3,0,-3)になる（ゼロ方向丸めだと(-2,0,-2)）
            // The center of the negative box floors to (-3,0,-3); truncation toward zero would give (-2,0,-2)
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(-3, 0, -3), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var created = BlueprintCreateService.TryCreateFromArea("negative", new Vector3Int(-4, 0, -4), new Vector3Int(-1, 2, -1), out var blueprint);

            Assert.IsTrue(created);
            Assert.AreEqual(new Vector3Int(0, 0, 0), blueprint.Blocks[0].Offset);
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
