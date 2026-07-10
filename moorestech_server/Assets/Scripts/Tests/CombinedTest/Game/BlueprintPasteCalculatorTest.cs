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
            var blueprint = new BlueprintJsonObject("rot", new List<BlueprintBlockJsonObject> { block });

            // 90度回転: (2,0,3)→(3,0,-2)
            // One clockwise step: offset (2,0,3) -> (3,0,-2), North -> East
            var rotated = BlueprintPasteCalculator.CalculatePlacements(blueprint, new Vector3Int(10, 0, 10), 1);
            Assert.AreEqual(new Vector3Int(13, 0, 8), rotated[0].Position);
            Assert.AreEqual(BlockDirection.North.HorizonRotation(), rotated[0].Direction);

            // 4回転で元に戻る（冪等性）
            // Four steps return to identity
            var full = BlueprintPasteCalculator.CalculatePlacements(blueprint, new Vector3Int(10, 0, 10), 4);
            Assert.AreEqual(new Vector3Int(12, 0, 13), full[0].Position);
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
            var blueprint = new BlueprintJsonObject("multi", new List<BlueprintBlockJsonObject> { block });

            var placed = BlueprintPasteCalculator.CalculatePlacements(blueprint, Vector3Int.zero, 1)[0];

            // 回転後原点からセル数=サイズ積を確認
            // Rebuild BlockPositionInfo at the rotated origin and verify cell count
            var info = new BlockPositionInfo(placed.Position, placed.Direction, master.BlockSize);
            var actual = EnumerateCells(info);
            Assert.AreEqual(master.BlockSize.x * master.BlockSize.y * master.BlockSize.z, actual.Count);

            // 回転前セルの直接回転と回転後集合が一致
            // Directly-rotated cells must equal the rotated BlockPositionInfo cells
            var originalInfo = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, master.BlockSize);
            var expected = new HashSet<Vector3Int>();
            foreach (var pos in EnumerateCells(originalInfo)) expected.Add(new Vector3Int(pos.z, pos.y, -pos.x));
            Assert.IsTrue(expected.SetEquals(actual));
        }

        [Test]
        public void UnknownGuidSkippedTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var block = new BlueprintBlockJsonObject(Vector3Int.zero, System.Guid.NewGuid().ToString(), 0, new Dictionary<string, string>());
            var blueprint = new BlueprintJsonObject("unknown", new List<BlueprintBlockJsonObject> { block });

            var result = BlueprintPasteCalculator.CalculatePlacements(blueprint, Vector3Int.zero, 0);
            Assert.AreEqual(0, result.Count);
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
