using System.Collections.Generic;
using Client.Game.InGame.Block.Index;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests
{
    public class OccupiedCellOctreeTest
    {
        [Test]
        public void RandomAddRemove_AcrossZero_MatchesHashSet()
        {
            // 0 をまたいで正負の両側へ根を広げながら足し引きし、常に HashSet と一致すること
            // Add and remove while the root grows to both sides of zero; the octree always matches a HashSet
            var random = new System.Random(2024);
            var octree = new OccupiedCellOctree();
            var expected = new HashSet<Vector3Int>();
            var cells = new List<Vector3Int>();
            for (var i = 0; i < 6000; i++)
            {
                if (cells.Count > 0 && random.NextDouble() < 0.4)
                {
                    var index = random.Next(cells.Count);
                    var removed = cells[index];
                    cells[index] = cells[cells.Count - 1];
                    cells.RemoveAt(cells.Count - 1);
                    expected.Remove(removed);
                    octree.Remove(removed);
                    continue;
                }
                var cell = new Vector3Int(random.Next(-3000, 3000), random.Next(-200, 400), random.Next(-3000, 3000));
                if (!expected.Add(cell)) continue;
                cells.Add(cell);
                octree.Add(cell);
            }

            foreach (var cell in expected) Assert.IsTrue(octree.Contains(cell), $"missing {cell}");
            for (var i = 0; i < 20000; i++)
            {
                var probe = new Vector3Int(random.Next(-3100, 3100), random.Next(-250, 450), random.Next(-3100, 3100));
                Assert.AreEqual(expected.Contains(probe), octree.Contains(probe), $"probe {probe}");
            }
        }

        [Test]
        public void RemoveAll_ThenAddAgain_ReusesNodes()
        {
            // 全部外すと根以外のノードは空きに戻り、同じセルを足し直してもノードは増えない
            // Removing everything frees every node but the root, and re-adding the same cells allocates no more nodes
            var octree = new OccupiedCellOctree();
            var cells = new[] { new Vector3Int(-500, 3, 7), new Vector3Int(900, 60, -1200), new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0) };
            foreach (var cell in cells) octree.Add(cell);
            var allocated = octree.AllocatedNodeCount;

            foreach (var cell in cells) octree.Remove(cell);
            foreach (var cell in cells) Assert.IsFalse(octree.Contains(cell));
            Assert.AreEqual(1, octree.AllocatedNodeCount);

            foreach (var cell in cells) octree.Add(cell);
            foreach (var cell in cells) Assert.IsTrue(octree.Contains(cell));
            Assert.AreEqual(allocated, octree.AllocatedNodeCount);
        }

        [Test]
        public void ProbeEmptyLevel_ReportsLeafCellAndEmptyNodes()
        {
            // 埋まったセル・同じ葉の空セル・葉の無い 4×4×4・もっと大きい空ノードを見分けること
            // Distinguish an occupied cell, an empty cell in the same leaf, a leafless 4x4x4 and a larger empty node
            var octree = new OccupiedCellOctree();
            octree.Add(Vector3Int.zero);
            octree.Add(new Vector3Int(100, 0, 0));

            octree.ResetProbePath();
            Assert.AreEqual(0, octree.ProbeEmptyLevel(Vector3Int.zero, out var occupied));
            Assert.IsTrue(occupied);
            Assert.AreEqual(0, octree.ProbeEmptyLevel(new Vector3Int(1, 0, 0), out occupied));
            Assert.IsFalse(occupied);
            Assert.AreEqual(OccupiedCellOctree.LeafLevel, octree.ProbeEmptyLevel(new Vector3Int(4, 0, 0), out occupied));
            Assert.IsFalse(occupied);
            Assert.Greater(octree.ProbeEmptyLevel(new Vector3Int(50, 0, 0), out occupied), OccupiedCellOctree.LeafLevel);
            Assert.IsFalse(occupied);
        }
    }
}
