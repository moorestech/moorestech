using System;
using Client.Game.InGame.BeltTransport.View;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BeltTransport
{
    // 出口までの距離と経路からのワールド座標: 直線・曲がり・坂(segment内と境界)・機械からの搬入
    // World positions from the distance to exit and the path: straight, turn, slopes (inside a segment and at its edge), machine entry
    public class BeltItemWorldPositionCalculatorTest
    {
        private const int Cell = BeltConstants.ItemWidth;
        private const float Surface = 0.35f;
        private const float Tolerance = 1e-4f;

        [Test]
        public void StraightLineInterpolatesBetweenCellCenters()
        {
            var shapes = new[] { Segment(Front(0, 0, 0), Front(0, 0, 1), Front(0, 0, 2)) };

            // 先頭が1マスめの出口に達した: アイテム中心は1マスめの中心
            // Head at the first cell's exit: the item center is the first cell's center
            AssertPosition(new Vector3(0.5f, Surface, 0.5f), shapes, 0, 2 * Cell, BeltEntryDirection.FromBack);
            // 先頭が2マスめの半ば: 1マスめと2マスめの中心の中間
            // Head halfway through the second cell: midway between the first and second centers
            AssertPosition(new Vector3(0.5f, Surface, 1.0f), shapes, 0, Cell + Cell / 2, BeltEntryDirection.FromBack);
            // 先頭が出口: 末尾マスの中心
            // Head at the exit: the last cell's center
            AssertPosition(new Vector3(0.5f, Surface, 2.5f), shapes, 0, 0, BeltEntryDirection.FromBack);
        }

        [Test]
        public void MachineEntryStartsAtTheNeighbourCenterInTheEntryDirection()
        {
            var shapes = new[] { Segment(Front(0, 0, 0), Front(0, 0, 1), Front(0, 0, 2)) };

            // 機械からの進入距離1: 後ろの隣マスの中心から1/256だけ進んだ位置
            // Machine entry at distance 1: 1/256 past the center of the neighbour behind
            AssertPosition(new Vector3(0.5f, Surface, -0.5f + 1f / Cell), shapes, 0, 3 * Cell - 1, BeltEntryDirection.FromBack);
            // 左上から入った: 左隣で1段上のマスの平らな中心と1マスめの中心の中間
            // Entered from the upper left: midway between the flat center of the neighbour one step up on the left and the first cell's center
            AssertPosition(new Vector3(0f, 1f + Surface - 0.5f, 0.5f), shapes, 0, 3 * Cell - Cell / 2, BeltEntryDirection.FromLeftAbove);
        }

        [Test]
        public void TurnInterpolatesTowardTheNextCellCenter()
        {
            var shapes = new[] { Segment(Front(0, 0, 0), CellAt(0, 0, 1, BeltDirection.Right), CellAt(1, 0, 1, BeltDirection.Right)) };

            // 曲がり角の次のマスの半ば: 角マスの中心から右へ半マス
            // Halfway into the cell after the corner: half a cell to the right of the corner center
            AssertPosition(new Vector3(1.0f, Surface, 1.5f), shapes, 0, Cell / 2, BeltEntryDirection.FromBack);
        }

        [Test]
        public void SlopeInsideASegmentRaisesItsCenterByHalfAStep()
        {
            // 平ら → 上り坂 → 1段上の平ら
            // flat -> up slope -> flat one step up
            var shapes = new[] { Segment(Front(0, 0, 0), Front(0, 0, 1), Front(0, 1, 2)) };

            // 1マスめと3マスめは平らなので中心は上がらない
            // The first and third cells are flat, so their centers do not rise
            AssertPosition(new Vector3(0.5f, Surface, 0.5f), shapes, 0, 2 * Cell, BeltEntryDirection.FromBack);
            AssertPosition(new Vector3(0.5f, 1f + Surface, 2.5f), shapes, 0, 0, BeltEntryDirection.FromBack);
            // 2マスめは坂なので中心が半段上がる
            // The second cell is a slope, so its center rises half a step
            AssertPosition(new Vector3(0.5f, Surface + 0.5f, 1.5f), shapes, 0, Cell, BeltEntryDirection.FromBack);
            AssertPosition(new Vector3(0.5f, Surface + 0.75f, 2.0f), shapes, 0, Cell / 2, BeltEntryDirection.FromBack);
        }

        [Test]
        public void SlopeAtASegmentEdgeIsReadFromTheLinkEntryDirection()
        {
            // segment0は上り坂1マス、segment1はその先の1段上の平ら。坂は出力接続の「下から入る」で分かる
            // Segment 0 is one up-slope cell, segment 1 the flat cell one step up beyond it; the slope shows in the output link's "from below" entry
            var up = Segment(new[] { Front(0, 0, 0) }, Array.Empty<BeltLinkShape>(), new[] { new BeltLinkShape(BeltDirection.Front, BeltEntryDirection.FromBackBelow, 1) });
            var flat = Segment(new[] { Front(0, 1, 1) }, new[] { new BeltLinkShape(BeltDirection.Back, BeltEntryDirection.FromBackBelow, 0) }, Array.Empty<BeltLinkShape>());
            var shapes = new[] { up, flat };
            // 坂マスの中心は半段上がり、その先の平らマスは上がらない
            // The slope cell's center rises half a step, the flat cell beyond it does not
            AssertPosition(new Vector3(0.5f, Surface + 0.5f, 0.5f), shapes, 0, 0, BeltEntryDirection.FromBack);
            AssertPosition(new Vector3(0.5f, 1f + Surface, 1.5f), shapes, 1, 0, BeltEntryDirection.FromBackBelow);

            // segment1の1マスめの半ば: 搬入元は接続先segmentの末尾(坂)の中心
            // Halfway into segment 1's first cell: the source is the center of the linked segment's last cell, the slope
            AssertPosition(new Vector3(0.5f, 1f + Surface - 0.25f, 1.0f), shapes, 1, Cell / 2, BeltEntryDirection.FromBackBelow);
        }

        [Test]
        public void SourceFromAPartnerSegmentUsesItsLastCellEvenWhenLevel()
        {
            // segment0は1段上の平ら → 下り坂、segment1は同じ高さの平ら。境界は水平の進入だが搬入元のマスは坂
            // Segment 0 is a flat cell one step up then a down slope; segment 1 a flat cell at the same height. The edge entry is level, yet the source cell is a slope
            var down = Segment(new[] { Front(0, 1, 0), Front(0, 0, 1) }, Array.Empty<BeltLinkShape>(), new[] { new BeltLinkShape(BeltDirection.Front, BeltEntryDirection.FromBack, 1) });
            var flat = Segment(new[] { Front(0, 0, 2) }, new[] { new BeltLinkShape(BeltDirection.Back, BeltEntryDirection.FromBack, 0) }, Array.Empty<BeltLinkShape>());
            var shapes = new[] { down, flat };
            // 下り坂マスの中心は半段上がる
            // The down-slope cell's center rises half a step
            AssertPosition(new Vector3(0.5f, Surface + 0.5f, 1.5f), shapes, 0, 0, BeltEntryDirection.FromBack);
            AssertPosition(new Vector3(0.5f, Surface + 0.25f, 2.0f), shapes, 1, Cell / 2, BeltEntryDirection.FromBack);
        }

        private static void AssertPosition(Vector3 expected, BeltSegmentShape[] shapes, int segmentIndex, int distanceToExit, BeltEntryDirection entryDirection)
        {
            var actual = BeltItemWorldPositionCalculator.Calculate(shapes, segmentIndex, distanceToExit, entryDirection);
            Assert.AreEqual(expected.x, actual.x, Tolerance, $"x at distance {distanceToExit}");
            Assert.AreEqual(expected.y, actual.y, Tolerance, $"y at distance {distanceToExit}");
            Assert.AreEqual(expected.z, actual.z, Tolerance, $"z at distance {distanceToExit}");
        }

        private static BeltSegmentShape Segment(params BeltCellShape[] cells)
        {
            return Segment(cells, Array.Empty<BeltLinkShape>(), Array.Empty<BeltLinkShape>());
        }

        private static BeltSegmentShape Segment(BeltCellShape[] cells, BeltLinkShape[] inputs, BeltLinkShape[] outputs)
        {
            return new BeltSegmentShape(BeltSegmentKind.Normal, false, cells, 32, cells[cells.Length - 1].Forward, inputs, outputs);
        }

        private static BeltCellShape Front(int x, int y, int z)
        {
            return CellAt(x, y, z, BeltDirection.Front);
        }

        private static BeltCellShape CellAt(int x, int y, int z, BeltDirection forward)
        {
            return new BeltCellShape(new Vector3Int(x, y, z), forward);
        }
    }
}
