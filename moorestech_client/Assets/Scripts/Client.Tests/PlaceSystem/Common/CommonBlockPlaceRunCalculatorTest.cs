using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.Common
{
    // 列生成の純ロジックを検証する（実DI起動もGameObjectも要らない）
    // Verify the pure run-building logic, with no real DI boot and no GameObject
    public class CommonBlockPlaceRunCalculatorTest
    {
        // 常にDirection=指定値・VerticalDirection=Horizontalで設置情報を返すこと
        // Placement info always carries the given Direction and a Horizontal VerticalDirection
        [Test]
        public void 列の全セルが指定の向きと水平姿勢を持つ()
        {
            var blockMasterElement = MakeBlock(Vector3Int.one);

            var run = CommonBlockPlacePointCalculator.CalculateRun(
                new Vector3Int(0, 0, 0), new Vector3Int(2, 0, 0), BlockDirection.East, blockMasterElement);

            Assert.AreEqual(3, run.Cells.Count);
            foreach (var cell in run.Cells)
            {
                Assert.AreEqual(BlockDirection.East, cell.Direction);
                Assert.AreEqual(BlockVerticalDirection.Horizontal, cell.VerticalDirection);
                Assert.IsTrue(cell.Placeable);
            }
        }

        // 生成直後の不可原因はすべてNone（重なり判定はY確定後に別途行う）
        // Every block cause is None right after generation; overlaps are judged later, once Y is final
        [Test]
        public void 生成直後の不可原因は全てNone()
        {
            var run = CommonBlockPlacePointCalculator.CalculateRun(
                Vector3Int.zero, new Vector3Int(2, 0, 0), BlockDirection.North, MakeBlock(Vector3Int.one));

            Assert.AreEqual(run.Cells.Count, run.BlockCauses.Count);
            foreach (var cause in run.BlockCauses) Assert.AreEqual(PlacementBlockCause.None, cause);
        }

        // blockSize分だけ間隔を空けて配置点を刻むこと
        // Placement points are spaced by blockSize
        [Test]
        public void 配置点はブロックサイズ分の間隔で刻まれる()
        {
            var blockMasterElement = MakeBlock(new Vector3Int(2, 1, 1));

            var run = CommonBlockPlacePointCalculator.CalculateRun(
                new Vector3Int(0, 0, 0), new Vector3Int(4, 0, 0), BlockDirection.North, blockMasterElement);

            Assert.AreEqual(3, run.Cells.Count);
            Assert.AreEqual(new Vector3Int(0, 0, 0), run.Cells[0].Position);
            Assert.AreEqual(new Vector3Int(2, 0, 0), run.Cells[1].Position);
            Assert.AreEqual(new Vector3Int(4, 0, 0), run.Cells[2].Position);
        }

        // 東西向きは占有のX・Zが入れ替わるので、刻みも入れ替えた寸法で詰めて並ぶこと
        // Facing east swaps the footprint's X and Z, so the step must use the swapped size and pack tightly
        [Test]
        public void 東西向きの長方形ブロックは回転後の寸法で隙間なく刻まれる()
        {
            var blockMasterElement = MakeBlock(new Vector3Int(1, 1, 3));

            var xRun = CommonBlockPlacePointCalculator.CalculateRun(
                Vector3Int.zero, new Vector3Int(6, 0, 0), BlockDirection.East, blockMasterElement);
            var zRun = CommonBlockPlacePointCalculator.CalculateRun(
                Vector3Int.zero, new Vector3Int(0, 0, 2), BlockDirection.West, blockMasterElement);

            Assert.AreEqual(new[] { 0, 3, 6 }, PositionsOnAxis(xRun, 0));
            Assert.AreEqual(new[] { 0, 1, 2 }, PositionsOnAxis(zRun, 2));
            AssertCellsTouchWithoutOverlap(xRun, BlockDirection.East, blockMasterElement.BlockSize);
            AssertCellsTouchWithoutOverlap(zRun, BlockDirection.West, blockMasterElement.BlockSize);
        }

        // 上下向きは占有の高さが入れ替わるので、縦積みも回転後の高さで刻むこと
        // Up/down facing swaps the footprint height, so vertical stacking steps by the rotated height
        [Test]
        public void 上下向きの縦積みは回転後の高さで隙間なく刻まれる()
        {
            var blockMasterElement = MakeBlock(new Vector3Int(1, 1, 2));

            var run = CommonBlockPlacePointCalculator.CalculateRun(
                Vector3Int.zero, new Vector3Int(0, 4, 0), BlockDirection.UpNorth, blockMasterElement);

            Assert.AreEqual(new[] { 0, 2, 4 }, PositionsOnAxis(run, 1));
            AssertCellsTouchWithoutOverlap(run, BlockDirection.UpNorth, blockMasterElement.BlockSize);
        }

        // 伸長軸を呼び出し側へ返す（地面ヒットの縦積み列を追従から外すのに使う）
        // Reports the extended axis to the caller, used to exclude vertical stacking runs from terrain following
        [Test]
        public void 伸長軸を返す()
        {
            var blockMasterElement = MakeBlock(Vector3Int.one);

            var xRun = CommonBlockPlacePointCalculator.CalculateRun(
                Vector3Int.zero, new Vector3Int(2, 0, 0), BlockDirection.North, blockMasterElement);
            var zRun = CommonBlockPlacePointCalculator.CalculateRun(
                Vector3Int.zero, new Vector3Int(0, 0, 2), BlockDirection.North, blockMasterElement);
            var yRun = CommonBlockPlacePointCalculator.CalculateRun(
                Vector3Int.zero, new Vector3Int(0, 2, 0), BlockDirection.North, blockMasterElement);

            Assert.AreEqual(PlacementRunAxis.X, xRun.Axis);
            Assert.AreEqual(PlacementRunAxis.Z, zRun.Axis);
            Assert.AreEqual(PlacementRunAxis.Y, yRun.Axis);
        }

        // カーソルセルは添字で持つ（地形追従でYが動いても引き当てられる）
        // The cursor cell is held as an index so it survives terrain following moving Y
        [Test]
        public void カーソルセルの添字は終点のセルを指す()
        {
            var run = CommonBlockPlacePointCalculator.CalculateRun(
                Vector3Int.zero, new Vector3Int(2, 0, 0), BlockDirection.North, MakeBlock(Vector3Int.one));

            Assert.AreEqual(2, run.CursorIndex);
            Assert.AreEqual(new Vector3Int(2, 0, 0), run.Cells[run.CursorIndex].Position);
        }

        // 刻み幅で割り切れない終点は列に載らないため末尾セルを充てる
        // An end point the step cannot reach is not on the run, so the last cell stands in
        [Test]
        public void 刻み幅で届かない終点は末尾セルを指す()
        {
            var run = CommonBlockPlacePointCalculator.CalculateRun(
                Vector3Int.zero, new Vector3Int(3, 0, 0), BlockDirection.North, MakeBlock(new Vector3Int(2, 1, 1)));

            Assert.AreEqual(run.Cells.Count - 1, run.CursorIndex);
            Assert.AreEqual(new Vector3Int(2, 0, 0), run.Cells[run.CursorIndex].Position);
        }

        private static int[] PositionsOnAxis(PlacementRun run, int axisIndex)
        {
            var values = new int[run.Cells.Count];
            for (var i = 0; i < run.Cells.Count; i++) values[i] = run.Cells[i].Position[axisIndex];
            return values;
        }

        // 隣り合うセルの占有範囲が、重ならずに1マス差で接していること
        // Adjacent cells' occupied ranges touch with a one-cell offset and never overlap
        private static void AssertCellsTouchWithoutOverlap(PlacementRun run, BlockDirection direction, Vector3Int blockSize)
        {
            for (var i = 1; i < run.Cells.Count; i++)
            {
                var previous = new BlockPositionInfo(run.Cells[i - 1].Position, direction, blockSize);
                var current = new BlockPositionInfo(run.Cells[i].Position, direction, blockSize);

                Assert.IsFalse(previous.IsOverlap(current), $"cell {i - 1} and {i} overlap");
                var gap = current.OriginalPos - previous.MaxPos;
                Assert.AreEqual(1, Mathf.Max(gap.x, gap.y, gap.z), $"cell {i - 1} and {i} leave a gap");
            }
        }

        private static BlockMasterElement MakeBlock(Vector3Int blockSize)
        {
            return new BlockMasterElement(
                0,
                Guid.Empty,
                "TestBlock",
                "TestBlockType",
                null,
                1, // placementsPerCost
                null,
                "テスト",
                "テスト",
                0,
                false,
                blockSize,
                null,
                false, // openable
                null
            );
        }
    }
}
