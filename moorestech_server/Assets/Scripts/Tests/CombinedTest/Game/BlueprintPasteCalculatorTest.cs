using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Blueprint;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class BlueprintPasteCalculatorTest
    {
        [Test]
        public void RotationTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var chestGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockGuid.ToString();
            var block = new BlueprintBlockJsonObject(new Vector3Int(2, 0, 3), chestGuid, (int)BlockDirection.North, new Dictionary<string, string>());
            var blueprint = new BlueprintJsonObject("rot", new List<BlueprintBlockJsonObject> { block }, new List<BlueprintLineJsonObject>(), new List<BlueprintLineJsonObject>(), System.Guid.NewGuid());

            // 単一ブロックは回転しても指定原点に揃う
            // A single block stays at the requested origin after rotation
            var rotated = BlueprintPasteCalculator.CalculatePlacements(blueprint, new Vector3Int(10, 0, 10), 1);
            Assert.AreEqual(new Vector3Int(10, 0, 10), rotated[0].Position);
            Assert.AreEqual(BlockDirection.North.HorizonRotation(), rotated[0].Direction);

            // 4回転で元に戻る（冪等性）
            // Four steps return to identity
            var full = BlueprintPasteCalculator.CalculatePlacements(blueprint, new Vector3Int(10, 0, 10), 4);
            Assert.AreEqual(new Vector3Int(10, 0, 10), full[0].Position);
            Assert.AreEqual(BlockDirection.North, full[0].Direction);
        }

        [Test]
        public void MultiCellRotationKeepsFootprintTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // マルチセル(3,1,2)で回転前後のセル集合比較
            // Compare occupied-cell sets before/after rotation for a 3x1x2 block
            var blockId = ForUnitTestModBlockId.MultiBlockGeneratorId;
            var master = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var guid = master.BlockGuid.ToString();

            var block = new BlueprintBlockJsonObject(Vector3Int.zero, guid, (int)BlockDirection.North, new Dictionary<string, string>());
            var blueprint = new BlueprintJsonObject("multi", new List<BlueprintBlockJsonObject> { block }, new List<BlueprintLineJsonObject>(), new List<BlueprintLineJsonObject>(), System.Guid.NewGuid());

            var placed = BlueprintPasteCalculator.CalculatePlacements(blueprint, Vector3Int.zero, 1)[0];

            // 回転後原点からセル数=サイズ積を確認
            // Rebuild BlockPositionInfo at the rotated origin and verify cell count
            var info = new BlockPositionInfo(placed.Position, placed.Direction, master.BlockSize);
            var actual = EnumerateCells(info);
            Assert.AreEqual(master.BlockSize.x * master.BlockSize.y * master.BlockSize.z, actual.Count);

            // 直接回転後も最小角を原点に揃える
            // Align the minimum corner after direct rotation.
            var originalInfo = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, master.BlockSize);
            var expected = new HashSet<Vector3Int>();
            foreach (var pos in EnumerateCells(originalInfo)) expected.Add(new Vector3Int(pos.z, pos.y, master.BlockSize.x - 1 - pos.x));
            Assert.IsTrue(expected.SetEquals(actual));
        }

        [Test]
        public void UnknownGuidSkippedTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var block = new BlueprintBlockJsonObject(Vector3Int.zero, System.Guid.NewGuid().ToString(), 0, new Dictionary<string, string>());
            var blueprint = new BlueprintJsonObject("unknown", new List<BlueprintBlockJsonObject> { block }, new List<BlueprintLineJsonObject>(), new List<BlueprintLineJsonObject>(), System.Guid.NewGuid());

            var result = BlueprintPasteCalculator.CalculatePlacements(blueprint, Vector3Int.zero, 0);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void 多セルを含むBPを回転しても外接箱最小角が原点に一致するTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var chestGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockGuid.ToString();
            var machineGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MultiBlockGeneratorId).BlockGuid.ToString();
            var settings = new Dictionary<string, string> { { "test", "retained" } };
            var blueprint = new BlueprintJsonObject("extent", new List<BlueprintBlockJsonObject>
            {
                new(new Vector3Int(0, 0, 0), chestGuid, (int)BlockDirection.North, new Dictionary<string, string>()),
                new(new Vector3Int(3, 1, 0), machineGuid, (int)BlockDirection.East, settings),
            }, new List<BlueprintLineJsonObject>(), new List<BlueprintLineJsonObject>(), System.Guid.NewGuid());
            var origin = new Vector3Int(10, 5, -10);

            // 全回転で最小角・形状・index維持
            // Preserve extent minimum, shape and indices at every rotation.
            var expectedMachinePositions = new[]
            {
                new Vector3Int(3, 1, 0), new Vector3Int(0, 1, 0),
                new Vector3Int(0, 1, 0), new Vector3Int(0, 1, 3),
            };
            var expectedChestPositions = new[]
            {
                Vector3Int.zero, new Vector3Int(0, 0, 4),
                new Vector3Int(4, 0, 2), new Vector3Int(2, 0, 0),
            };
            for (var rotation = 0; rotation < 4; rotation++)
            {
                var placements = BlueprintPasteCalculator.CalculatePlacements(blueprint, origin, rotation);
                var chest = BlueprintPlacementElementUtil.ToPositionInfo(placements[0]);
                var machine = BlueprintPlacementElementUtil.ToPositionInfo(placements[1]);
                Assert.AreEqual(origin, Vector3Int.Min(chest.MinPos, machine.MinPos), $"rotation={rotation}");
                Assert.AreEqual(origin + expectedChestPositions[rotation], chest.MinPos);
                Assert.AreEqual(origin + expectedMachinePositions[rotation], machine.MinPos);
                Assert.AreEqual(0, placements[0].BlockIndex);
                Assert.AreEqual(1, placements[1].BlockIndex);
                Assert.AreSame(settings, placements[1].Settings);
                Assert.IsFalse(EnumerateCells(chest).Overlaps(EnumerateCells(machine)), $"rotation={rotation}");
            }
        }

        [Test]
        public void 欠損マスタを除外しても保存indexを詰めないTest()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var chestGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockGuid.ToString();
            var blueprint = new BlueprintJsonObject("indices", new List<BlueprintBlockJsonObject>
            {
                new(Vector3Int.zero, System.Guid.NewGuid().ToString(), 0, new Dictionary<string, string>()),
                new(new Vector3Int(9, 2, 7), chestGuid, (int)BlockDirection.North, new Dictionary<string, string>()),
            }, new List<BlueprintLineJsonObject>(), new List<BlueprintLineJsonObject>(), System.Guid.NewGuid());

            // 配線の端点indexは欠損ブロックがあっても変えない
            // Keep connection endpoint indices stable when a missing block is skipped
            var origin = new Vector3Int(-3, 6, 2);
            var result = BlueprintPasteCalculator.CalculatePlacements(blueprint, origin, 1);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, result[0].BlockIndex);
            Assert.AreEqual(origin, result[0].Position);
        }

        // MinPosからMaxPosまでの占有セルを列挙する
        // Enumerate occupied cells from MinPos to MaxPos
        private static HashSet<Vector3Int> EnumerateCells(BlockPositionInfo info)
        {
            var cells = new HashSet<Vector3Int>();
            for (var x = info.MinPos.x; x <= info.MaxPos.x; x++)
            for (var y = info.MinPos.y; y <= info.MaxPos.y; y++)
            for (var z = info.MinPos.z; z <= info.MaxPos.z; z++)
                cells.Add(new Vector3Int(x, y, z));
            return cells;
        }
    }
}
