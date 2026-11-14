using Client.Game.InGame.Block.Index;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests
{
    public class BlockOccupiedCellIndexTest
    {
        [Test]
        public void Register_MapsEveryFootprintCell()
        {
            var index = new BlockOccupiedCellIndex();
            var origin = new Vector3Int(10, 5, 20);
            var north = new BlockPositionInfo(origin, BlockDirection.North, new Vector3Int(2, 1, 3));
            index.Register(north);

            for (var x = 10; x <= 11; x++)
            for (var z = 20; z <= 22; z++)
            {
                Assert.IsTrue(index.TryGetBlockAt(new Vector3Int(x, 5, z), out var found));
                Assert.AreSame(north, found);
            }
            Assert.IsFalse(index.TryGetBlockAt(new Vector3Int(12, 5, 20), out _));
            Assert.IsFalse(index.TryGetBlockAt(new Vector3Int(10, 6, 20), out _));

            var east = new BlockPositionInfo(origin, BlockDirection.East, new Vector3Int(2, 1, 3));
            var eastIndex = new BlockOccupiedCellIndex();
            eastIndex.Register(east);
            for (var x = east.MinPos.x; x <= east.MaxPos.x; x++)
            for (var y = east.MinPos.y; y <= east.MaxPos.y; y++)
            for (var z = east.MinPos.z; z <= east.MaxPos.z; z++)
            {
                Assert.IsTrue(eastIndex.TryGetBlockAt(new Vector3Int(x, y, z), out var found));
                Assert.AreSame(east, found);
            }
        }

        [Test]
        public void Remove_OnlyClearsCellsOwnedByThatBlock()
        {
            var index = new BlockOccupiedCellIndex();
            var a = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, new Vector3Int(2, 1, 1));
            var b = new BlockPositionInfo(new Vector3Int(1, 0, 0), BlockDirection.North, Vector3Int.one);
            index.Register(a);

            index.Remove(b);
            Assert.IsTrue(index.TryGetBlockAt(new Vector3Int(1, 0, 0), out var found));
            Assert.AreSame(a, found);

            index.Remove(a);
            Assert.IsFalse(index.TryGetBlockAt(Vector3Int.zero, out _));
            Assert.IsFalse(index.TryGetBlockAt(new Vector3Int(1, 0, 0), out _));
        }

        [Test]
        public void ReRegisterSmaller_LeavesNoStaleCells()
        {
            var index = new BlockOccupiedCellIndex();
            var large = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, new Vector3Int(3, 3, 3));
            var small = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);
            index.Register(large);
            index.Remove(large);
            index.Register(small);

            Assert.IsFalse(index.TryGetBlockAt(new Vector3Int(2, 2, 2), out _));
            Assert.IsTrue(index.TryGetBlockAt(Vector3Int.zero, out var found));
            Assert.AreSame(small, found);
        }

        [Test]
        public void Bounds_GrowOnlyAndFalseWhenEmpty()
        {
            var index = new BlockOccupiedCellIndex();
            Assert.IsFalse(index.TryGetBounds(out _, out _));

            var first = new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one);
            var second = new BlockPositionInfo(new Vector3Int(5, 2, -3), BlockDirection.North, Vector3Int.one);
            index.Register(first);
            index.Register(second);
            Assert.IsTrue(index.TryGetBounds(out var min, out var max));
            Assert.AreEqual(new Vector3Int(0, 0, -3), min);
            Assert.AreEqual(new Vector3Int(5, 2, 0), max);

            index.Remove(second);
            Assert.IsTrue(index.TryGetBounds(out min, out max));
            Assert.AreEqual(new Vector3Int(0, 0, -3), min);
            Assert.AreEqual(new Vector3Int(5, 2, 0), max);
        }

        [Test]
        public void IsChunkEmpty_FollowsRegisterAndRemove()
        {
            // 負の座標のチャンク（-16..-1）と、重なって置き換わるセルの数え方を確かめる
            // Check a negative-coordinate chunk (-16..-1) and counting when a cell is taken over by another block
            var index = new BlockOccupiedCellIndex();
            var a = new BlockPositionInfo(new Vector3Int(-1, 0, -16), BlockDirection.North, new Vector3Int(2, 1, 1));
            var b = new BlockPositionInfo(new Vector3Int(-1, 0, -16), BlockDirection.North, Vector3Int.one);
            Assert.IsTrue(index.IsChunkEmpty(new Vector3Int(-16, 0, -1)));

            index.Register(a);
            Assert.IsFalse(index.IsChunkEmpty(new Vector3Int(-16, 15, -1)));
            Assert.IsFalse(index.IsChunkEmpty(new Vector3Int(0, 0, -16)));
            Assert.IsTrue(index.IsChunkEmpty(new Vector3Int(-17, 0, -16)));
            Assert.IsTrue(index.IsChunkEmpty(new Vector3Int(-1, 0, -17)));

            // b が (-1,0,-16) を奪ったあと a を外しても、b のセルが残るのでそのチャンクは空にならない
            // After b takes (-1,0,-16), removing a keeps that chunk non-empty because b's cell remains
            index.Register(b);
            index.Remove(a);
            Assert.IsFalse(index.IsChunkEmpty(new Vector3Int(-1, 0, -16)));
            Assert.IsTrue(index.IsChunkEmpty(new Vector3Int(0, 0, -16)));

            index.Remove(b);
            Assert.IsTrue(index.IsChunkEmpty(new Vector3Int(-1, 0, -16)));
        }
    }
}
