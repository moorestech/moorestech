using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.Run;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Game.Block.Interface;
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
                null,
                null
            );
        }
    }
}
