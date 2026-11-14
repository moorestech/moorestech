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
        public void GrowBeyondLevelCap_Throws()
        {
            // 根の大きさが int に収まる段まで（2^30）は広げられ、それを超えて離れたセルは黙って固まらずに例外になる
            // The root grows up to the level whose size fits in an int (2^30); a cell farther away throws instead of hanging
            var octree = new OccupiedCellOctree();
            octree.Add(Vector3Int.zero);
            octree.Add(new Vector3Int((1 << 30) - 1, 0, 0));
            Assert.IsTrue(octree.Contains(new Vector3Int((1 << 30) - 1, 0, 0)));

            Assert.Throws<System.InvalidOperationException>(() => octree.Add(new Vector3Int(1 << 30, 0, 0)));
        }

        [Test]
        public void ProbeEmptyLevel_MatchesBruteForceAlongWalks()
        {
            // 足し引きした後の木で、近くへ歩きながら問い合わせ（直前の道を使い回す）、答えを総当たりで確かめる
            // 返したノードは空で、その親は空でないこと（最も大きい空ノード）。0 なら 4×4×4 の葉に何かがあること
            // On an octree after adds and removes, probe while walking to nearby cells (reusing the previous path) and verify by brute force
            // The returned node is empty and its parent is not (the largest empty node); 0 means the 4x4x4 leaf holds something
            var random = new System.Random(99);
            var octree = new OccupiedCellOctree();
            var occupied = new HashSet<Vector3Int>();
            var cells = new List<Vector3Int>();
            for (var i = 0; i < 1500; i++)
            {
                var cell = new Vector3Int(random.Next(-300, 300), random.Next(-40, 80), random.Next(-300, 300));
                if (!occupied.Add(cell)) continue;
                cells.Add(cell);
                octree.Add(cell);
            }
            for (var i = 0; i < cells.Count; i += 4)
            {
                occupied.Remove(cells[i]);
                octree.Remove(cells[i]);
            }

            // 問い合わせは足したセルの範囲（根の中）に限る。根の外は飛ばさない約束なので別扱い
            // Probes stay within the span of added cells (inside the root); outside the root nothing is skipped by contract
            var spanMin = cells[0];
            var spanMax = cells[0];
            foreach (var cell in cells)
            {
                spanMin = Vector3Int.Min(spanMin, cell);
                spanMax = Vector3Int.Max(spanMax, cell);
            }
            var probe = new Vector3Int(0, 20, 0);
            octree.ResetProbePath();
            for (var i = 0; i < 4000; i++)
            {
                if (i % 200 == 0) octree.ResetProbePath();
                probe = random.Next(10) == 0
                    ? new Vector3Int(random.Next(-290, 290), random.Next(-35, 75), random.Next(-290, 290))
                    : probe + new Vector3Int(random.Next(-1, 2), random.Next(-1, 2), random.Next(-1, 2));
                probe = Vector3Int.Max(spanMin, Vector3Int.Min(spanMax, probe));

                var level = octree.ProbeEmptyLevel(probe, out var isOccupied);
                Assert.AreEqual(occupied.Contains(probe), isOccupied, $"occupied {probe}");
                if (isOccupied) continue;
                if (level > 0)
                {
                    Assert.IsFalse(AnyInNode(octree, occupied, probe, level), $"node level {level} at {probe} is not empty");
                    Assert.IsTrue(AnyInNode(octree, occupied, probe, level + 1), $"parent of level {level} at {probe} is empty");
                    continue;
                }
                Assert.IsTrue(AnyInNode(octree, occupied, probe, OccupiedCellOctree.LeafLevel), $"leaf at {probe} is empty but nothing was skipped");
            }
        }

        private static bool AnyInNode(OccupiedCellOctree octree, HashSet<Vector3Int> occupied, Vector3Int cell, int level)
        {
            var size = 1 << level;
            var min = new Vector3Int(octree.NodeStart(0, cell.x, level), octree.NodeStart(1, cell.y, level), octree.NodeStart(2, cell.z, level));
            foreach (var c in occupied)
            {
                if (c.x >= min.x && c.y >= min.y && c.z >= min.z && c.x < min.x + size && c.y < min.y + size && c.z < min.z + size) return true;
            }
            return false;
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
