using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltConveyor
{
    public class BeltConveyorStraightCellBlockResolverTest
    {
        private static readonly BlockId StraightBlock = new(101);
        private static readonly BlockId UpBlock = new(102);
        private static readonly BlockId DownBlock = new(103);

        [Test]
        public void 水平セルは個数と配置情報を保って直線ブロックになる()
        {
            var cells = new List<PlaceInfo>
            {
                Cell(0, 0, 0, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
                Cell(0, 0, 1, BlockDirection.North, BlockVerticalDirection.Horizontal, false),
                Cell(0, 0, 2, BlockDirection.East, BlockVerticalDirection.Horizontal, true),
            };

            var result = BeltConveyorStraightCellBlockResolver.ResolveStraightRun(cells, StraightBlock, UpBlock, DownBlock, NoneReasons(cells.Count));

            // セルを縮約せず配置属性を維持する
            // Preserve placement attributes without collapsing cells
            Assert.AreEqual(cells.Count, result.Count);
            Assert.IsTrue(result.All(info => info.BlockId == StraightBlock));
            for (var i = 0; i < cells.Count; i++)
            {
                Assert.AreEqual(cells[i].Position, result[i].Position);
                Assert.AreEqual(cells[i].Direction, result[i].Direction);
                Assert.AreEqual(cells[i].Placeable, result[i].Placeable);
            }
        }

        // 水平セルは渡された水平ブロックをそのまま使う（分岐器手持ちが直線へ化けないことの下流側の担保）
        // Horizontal cells use the given horizontal block as is, guaranteeing a held splitter is not swapped for a straight belt downstream
        [Test]
        public void 直線以外を手持ちにしても水平セルはその水平ブロックのまま()
        {
            var splitterBlock = new BlockId(201);
            var cells = new List<PlaceInfo>
            {
                Cell(0, 0, 0, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
                Cell(0, 0, 1, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
            };

            var result = BeltConveyorStraightCellBlockResolver.ResolveStraightRun(cells, splitterBlock, null, null, NoneReasons(cells.Count));

            Assert.IsTrue(result.All(info => info.BlockId == splitterBlock));
        }

        [Test]
        public void 上り下りセルは対応する坂ブロックになる()
        {
            var cells = new List<PlaceInfo>
            {
                Cell(0, 0, 0, BlockDirection.North, BlockVerticalDirection.Up, true),
                Cell(0, 1, 1, BlockDirection.North, BlockVerticalDirection.Down, true),
            };

            var result = BeltConveyorStraightCellBlockResolver.ResolveStraightRun(cells, StraightBlock, UpBlock, DownBlock, NoneReasons(cells.Count));

            Assert.AreEqual(UpBlock, result[0].BlockId);
            Assert.AreEqual(DownBlock, result[1].BlockId);
            Assert.AreEqual(BlockVerticalDirection.Up, result[0].VerticalDirection);
            Assert.AreEqual(BlockVerticalDirection.Down, result[1].VerticalDirection);
        }

        [Test]
        public void 坂ブロック無指定の傾斜セルは水平ブロックのまま設置不可になる()
        {
            var cells = new List<PlaceInfo>
            {
                Cell(0, 0, 0, BlockDirection.North, BlockVerticalDirection.Up, true),
            };

            var beltReasons = NoneReasons(cells.Count);
            var result = BeltConveyorStraightCellBlockResolver.ResolveStraightRun(cells, StraightBlock, null, null, beltReasons);

            Assert.AreEqual(StraightBlock, result[0].BlockId);
            Assert.IsFalse(result[0].Placeable);

            // 坂欠落はベルト固有理由の列へ書き戻される
            // The missing slope is written back into the belt-specific reason column
            Assert.AreEqual(BeltConveyorPlacementBlockReason.SlopeBlockMissing, beltReasons[0]);
        }

        [Test]
        public void 先に不可になったセルは坂欠落を後追いの理由にしない()
        {
            var cells = new List<PlaceInfo>
            {
                Cell(0, 0, 0, BlockDirection.North, BlockVerticalDirection.Up, false),
            };
            var beltReasons = new List<BeltConveyorPlacementBlockReason> { BeltConveyorPlacementBlockReason.ImpossibleOverpass };

            BeltConveyorStraightCellBlockResolver.ResolveStraightRun(cells, StraightBlock, null, null, beltReasons);

            Assert.AreEqual(BeltConveyorPlacementBlockReason.ImpossibleOverpass, beltReasons[0]);
        }

        private static List<BeltConveyorPlacementBlockReason> NoneReasons(int cellCount)
        {
            var reasons = new List<BeltConveyorPlacementBlockReason>(cellCount);
            for (var i = 0; i < cellCount; i++) reasons.Add(BeltConveyorPlacementBlockReason.None);
            return reasons;
        }

        private static PlaceInfo Cell(int x, int y, int z, BlockDirection direction, BlockVerticalDirection verticalDirection, bool placeable)
        {
            return new PlaceInfo
            {
                Position = new Vector3Int(x, y, z),
                Direction = direction,
                VerticalDirection = verticalDirection,
                Placeable = placeable,
            };
        }
    }
}
