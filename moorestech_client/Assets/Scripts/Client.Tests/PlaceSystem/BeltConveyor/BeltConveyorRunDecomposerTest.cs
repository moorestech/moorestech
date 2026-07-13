using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltConveyor
{
    public class BeltConveyorRunDecomposerTest
    {
        private static readonly BlockId Straight1 = new(101);
        private static readonly BlockId Straight2 = new(102);
        private static readonly BlockId Straight3 = new(103);
        private static readonly BlockId UpBlock = new(104);
        private static readonly BlockId DownBlock = new(105);

        private static readonly List<(int length, BlockId blockId)> Variants = new() { (3, Straight3), (2, Straight2), (1, Straight1) };

        private static readonly BeltConveyorFamily Family = new(Straight1, Variants, UpBlock, DownBlock);

        // 斜面バリアントを持たないファミリー（分岐器相当）
        // A family without slope variants (equivalent to a splitter)
        private static readonly BeltConveyorFamily SlopelessFamily = new(Straight1, new List<(int length, BlockId blockId)> { (1, Straight1) }, null, null);

        private static PlaceInfo Cell(int x, int y, int z, BlockDirection dir, BlockVerticalDirection vertical, bool placeable)
        {
            return new PlaceInfo { Position = new Vector3Int(x, y, z), Direction = dir, VerticalDirection = vertical, Placeable = placeable };
        }

        [Test]
        public void 直線9マスは3連x3に分解される()
        {
            var cells = Enumerable.Range(0, 9).Select(i => Cell(0, 0, i, BlockDirection.North, BlockVerticalDirection.Horizontal, true)).ToList();
            var result = BeltConveyorRunDecomposer.Decompose(cells, Family);

            Assert.AreEqual(3, result.Count);
            Assert.IsTrue(result.All(r => r.BlockId == Straight3));
            Assert.AreEqual(new Vector3Int(0, 0, 0), result[0].Position);
            Assert.AreEqual(new Vector3Int(0, 0, 3), result[1].Position);
            Assert.AreEqual(new Vector3Int(0, 0, 6), result[2].Position);
        }

        [Test]
        public void 直線7マスは3連x2と1連x1()
        {
            var cells = Enumerable.Range(0, 7).Select(i => Cell(0, 0, i, BlockDirection.North, BlockVerticalDirection.Horizontal, true)).ToList();
            var result = BeltConveyorRunDecomposer.Decompose(cells, Family);

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(Straight3, result[0].BlockId);
            Assert.AreEqual(Straight3, result[1].BlockId);
            Assert.AreEqual(Straight1, result[2].BlockId);
        }

        [Test]
        public void 西向きランの原点は占有範囲の最小座標になる()
        {
            // 西向き（-X方向）に3マス: (5,0,0)→(3,0,0)。3連の原点は最小座標(3,0,0)
            // Westward 3-cell run: the 3-length variant's origin must be the min corner (3,0,0)
            var cells = new List<PlaceInfo>
            {
                Cell(5, 0, 0, BlockDirection.West, BlockVerticalDirection.Horizontal, true),
                Cell(4, 0, 0, BlockDirection.West, BlockVerticalDirection.Horizontal, true),
                Cell(3, 0, 0, BlockDirection.West, BlockVerticalDirection.Horizontal, true),
            };
            var result = BeltConveyorRunDecomposer.Decompose(cells, Family);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(Straight3, result[0].BlockId);
            Assert.AreEqual(new Vector3Int(3, 0, 0), result[0].Position);
            Assert.AreEqual(BlockDirection.West, result[0].Direction);
        }

        [Test]
        public void カーブでランが分割される()
        {
            // 北向き2マス→東向き2マスのL字。2連+2連に分解される
            // L-shape: 2 north cells then 2 east cells decompose into two 2-length variants
            var cells = new List<PlaceInfo>
            {
                Cell(0, 0, 0, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
                Cell(0, 0, 1, BlockDirection.East, BlockVerticalDirection.Horizontal, true),
                Cell(1, 0, 1, BlockDirection.East, BlockVerticalDirection.Horizontal, true),
                Cell(2, 0, 1, BlockDirection.East, BlockVerticalDirection.Horizontal, true),
            };
            var result = BeltConveyorRunDecomposer.Decompose(cells, Family);

            // 先頭セルは方向が異なるため1連、続く東向き3マスは3連
            // The corner cell stays 1-length; the following 3 east cells merge into one 3-length
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(Straight1, result[0].BlockId);
            Assert.AreEqual(Straight3, result[1].BlockId);
            Assert.AreEqual(new Vector3Int(0, 0, 1), result[1].Position);
        }

        [Test]
        public void 斜面セルは専用ブロックの1マスになる()
        {
            var cells = new List<PlaceInfo>
            {
                Cell(0, 0, 0, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
                Cell(0, 0, 1, BlockDirection.North, BlockVerticalDirection.Up, true),
                Cell(0, 1, 2, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
            };
            var result = BeltConveyorRunDecomposer.Decompose(cells, Family);

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(Straight1, result[0].BlockId);
            Assert.AreEqual(UpBlock, result[1].BlockId);
            Assert.AreEqual(BlockVerticalDirection.Up, result[1].VerticalDirection);
            Assert.AreEqual(Straight1, result[2].BlockId);
        }

        [Test]
        public void 設置不可セルはランに含まれず1連のまま残る()
        {
            var cells = new List<PlaceInfo>
            {
                Cell(0, 0, 0, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
                Cell(0, 0, 1, BlockDirection.North, BlockVerticalDirection.Horizontal, false),
                Cell(0, 0, 2, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
                Cell(0, 0, 3, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
            };
            var result = BeltConveyorRunDecomposer.Decompose(cells, Family);

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(Straight1, result[0].BlockId);
            Assert.IsFalse(result[1].Placeable);
            Assert.AreEqual(Straight1, result[1].BlockId);
            Assert.AreEqual(Straight2, result[2].BlockId);
            Assert.AreEqual(new Vector3Int(0, 0, 2), result[2].Position);
        }

        [Test]
        public void 斜面バリアントを持たないファミリーは傾斜セルが設置不可になる()
        {
            var cells = new List<PlaceInfo>
            {
                Cell(0, 0, 0, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
                Cell(0, 0, 1, BlockDirection.North, BlockVerticalDirection.Up, true),
            };
            var result = BeltConveyorRunDecomposer.Decompose(cells, SlopelessFamily);

            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result[0].Placeable);
            Assert.IsFalse(result[1].Placeable);
            Assert.AreEqual(Straight1, result[1].BlockId);
        }

        [Test]
        public void 位置が連続しないセルはランが分割される()
        {
            // 同方向でも座標が飛んでいる場合は結合しない（立体交差の高さ変化等）
            // Cells with a positional gap (e.g. overpass height change) must not merge
            var cells = new List<PlaceInfo>
            {
                Cell(0, 0, 0, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
                Cell(0, 1, 1, BlockDirection.North, BlockVerticalDirection.Horizontal, true),
            };
            var result = BeltConveyorRunDecomposer.Decompose(cells, Family);

            Assert.AreEqual(2, result.Count);
            Assert.IsTrue(result.All(r => r.BlockId == Straight1));
        }
    }
}
