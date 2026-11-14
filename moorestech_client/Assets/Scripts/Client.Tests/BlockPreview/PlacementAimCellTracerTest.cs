using System.Collections.Generic;
using Client.Game.InGame.Block.Index;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewObject;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests
{
    public class PlacementAimCellTracerTest
    {
        private static readonly Vector3Int Origin = new(10, 5, 20);
        private static readonly Vector3Int Size = new(2, 1, 3);

        [TestCase(BlockDirection.North)]
        [TestCase(BlockDirection.East)]
        public void OutsideRays_ResolveWorldFaces(BlockDirection direction)
        {
            var (cells, posInfo) = CreateSingle(direction);
            var c = Center(posInfo);

            AssertFace(cells, c + Vector3.right * 20, Vector3.left, PreviewSurfaceType.YZ_X);
            AssertFace(cells, c + Vector3.left * 20, Vector3.right, PreviewSurfaceType.YZ_Origin);
            AssertFace(cells, c + Vector3.up * 20, Vector3.down, PreviewSurfaceType.XZ_Y);
            AssertFace(cells, c + Vector3.down * 20, Vector3.up, PreviewSurfaceType.XZ_Origin);
            AssertFace(cells, c + Vector3.forward * 20, Vector3.back, PreviewSurfaceType.YX_Z);
            AssertFace(cells, c + Vector3.back * 20, Vector3.forward, PreviewSurfaceType.YX_Origin);

            // 上から撃ったときの距離は箱の上面まで
            // Shooting from above measures the distance to the top face
            var fromTop = new Ray(c + Vector3.up * 20, Vector3.down);
            Assert.IsTrue(PlacementAimCellTracer.TryTrace(cells, fromTop, 100f, out var distance, out _));
            Assert.AreEqual(c.y + 20 - (posInfo.MaxPos.y + 1), distance, 1e-4f);
        }

        [Test]
        public void InsideStart_HitsExitFace()
        {
            var (cells, posInfo) = CreateSingle(BlockDirection.North);
            var c = Center(posInfo);

            Assert.IsTrue(PlacementAimCellTracer.TryTrace(cells, new Ray(c, Vector3.right), 100f, out var distance, out var surface));
            Assert.AreEqual(PreviewSurfaceType.YZ_X, surface);
            Assert.AreEqual(posInfo.MaxPos.x + 1 - c.x, distance, 1e-4f);
        }

        [Test]
        public void MaxDistanceBeforeBlock_Misses()
        {
            var (cells, posInfo) = CreateSingle(BlockDirection.North);
            var c = Center(posInfo);
            var toTop = c.y + 20 - (posInfo.MaxPos.y + 1);

            Assert.IsFalse(PlacementAimCellTracer.TryTrace(cells, new Ray(c + Vector3.up * 20, Vector3.down), toTop - 0.01f, out _, out _));
        }

        [Test]
        public void NearerBlockWins()
        {
            var cells = new BlockOccupiedCellIndex();
            cells.Register(new BlockPositionInfo(new Vector3Int(10, 5, 20), BlockDirection.North, Vector3Int.one));
            cells.Register(new BlockPositionInfo(new Vector3Int(20, 5, 20), BlockDirection.North, Vector3Int.one));

            var ray = new Ray(new Vector3(30.5f, 5.5f, 20.5f), Vector3.left);
            Assert.IsTrue(PlacementAimCellTracer.TryTrace(cells, ray, 100f, out var distance, out var surface));
            Assert.AreEqual(30.5f - 21f, distance, 1e-4f);
            Assert.AreEqual(PreviewSurfaceType.YZ_X, surface);
        }

        [Test]
        public void EdgeGraze_PrefersY()
        {
            var cells = new BlockOccupiedCellIndex();
            cells.Register(new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));

            var ray = new Ray(new Vector3(3f, 3f, 0.5f), new Vector3(-1f, -1f, 0f).normalized);
            Assert.IsTrue(PlacementAimCellTracer.TryTrace(cells, ray, 100f, out _, out var surface));
            Assert.AreEqual(PreviewSurfaceType.XZ_Y, surface);
        }

        [Test]
        public void MissOrEmpty_ReturnsFalse()
        {
            var empty = new BlockOccupiedCellIndex();
            Assert.IsFalse(PlacementAimCellTracer.TryTrace(empty, new Ray(Vector3.up * 10, Vector3.down), 100f, out _, out _));

            var (cells, posInfo) = CreateSingle(BlockDirection.North);
            var above = Center(posInfo) + Vector3.up * 10 + Vector3.left * 20;
            Assert.IsFalse(PlacementAimCellTracer.TryTrace(cells, new Ray(above, Vector3.right), 100f, out _, out _));
        }

        [Test]
        public void NoMaxDistance_StopsAtBounds()
        {
            var (cells, posInfo) = CreateSingle(BlockDirection.North);
            var above = Center(posInfo) + Vector3.up * 10 + Vector3.left * 20;
            Assert.IsFalse(PlacementAimCellTracer.TryTrace(cells, new Ray(above, Vector3.right), float.PositiveInfinity, out _, out _));
        }

        [Test]
        public void WalkThroughEmptyCells_HitsFarBlock()
        {
            var cells = CreateUnitBlocks(new Vector3Int(10, 5, 20), new Vector3Int(20, 5, 20));

            var ray = new Ray(new Vector3(17.5f, 5.5f, 20.5f), Vector3.left);
            Assert.IsTrue(PlacementAimCellTracer.TryTrace(cells, ray, 100f, out var distance, out var surface));
            Assert.AreEqual(PreviewSurfaceType.YZ_X, surface);
            Assert.AreEqual(6.5f, distance, 1e-4f);
        }

        [Test]
        public void WalkInsideBounds_MissesWithInfiniteMax()
        {
            var cells = CreateUnitBlocks(Vector3Int.zero, new Vector3Int(5, 0, 0));

            var ray = new Ray(new Vector3(2.5f, 0.5f, -3f), Vector3.forward);
            Assert.IsFalse(PlacementAimCellTracer.TryTrace(cells, ray, float.PositiveInfinity, out _, out _));
        }

        [Test]
        public void EdgeGraze_WithFarBlock_StillPrefersY()
        {
            // 遠いブロックで外接直方体が X に伸び、入口セル (1,0,0) は空で、同じ t の X 越境で (0,0,0) に入る
            // The far block widens the bounds along X, so the entry cell (1,0,0) is empty and (0,0,0) is entered by an X crossing at the same t
            var cells = CreateUnitBlocks(Vector3Int.zero, new Vector3Int(5, 0, 0));

            var ray = new Ray(new Vector3(3f, 3f, 0.5f), new Vector3(-1f, -1f, 0f).normalized);
            Assert.IsTrue(PlacementAimCellTracer.TryTrace(cells, ray, 100f, out _, out var surface));
            Assert.AreEqual(PreviewSurfaceType.XZ_Y, surface);
        }

        [Test]
        public void EdgeGraze_DiagonalAfterEmptyNeighbour_PrefersY()
        {
            // 外接直方体 (0..6) の内側から対角に進み、点 (1,1) の辺で Y 越境で空の (1,0,0)、同じ t の X 越境で (0,0,0) に入る
            // Starting inside the bounds (0..6) diagonally, at the edge (1,1) a Y crossing enters empty (1,0,0), then an X crossing at the same t enters (0,0,0)
            var cells = CreateUnitBlocks(Vector3Int.zero, new Vector3Int(5, 5, 0));

            var ray = new Ray(new Vector3(2.5f, 2.5f, 0.5f), new Vector3(-1f, -1f, 0f).normalized);
            Assert.IsTrue(PlacementAimCellTracer.TryTrace(cells, ray, 100f, out _, out var surface));
            Assert.AreEqual(PreviewSurfaceType.XZ_Y, surface);
        }

        [Test]
        public void EdgeGraze_EnteringBoundsThroughX_StillPrefersY()
        {
            // 外接直方体 (0..6) へ x=6 面の点 (6,1) で入り、同じ t の Y 越境と X 越境で (5,0,0) に入る（X と Y の境界時刻はともに -2/d で一致する）
            // The ray enters the bounds (0..6) via the x=6 plane at (6,1), reaching (5,0,0) by Y and X crossings at the same t (both boundary times are -2/d)
            var cells = CreateUnitBlocks(new Vector3Int(5, 0, 0), new Vector3Int(0, 5, 0));

            var ray = new Ray(new Vector3(8f, 3f, 0.5f), new Vector3(-1f, -1f, 0f).normalized);
            Assert.IsTrue(PlacementAimCellTracer.TryTrace(cells, ray, 100f, out _, out var surface));
            Assert.AreEqual(PreviewSurfaceType.XZ_Y, surface);
        }

        [Test]
        public void EdgeGraze_AcrossEmptyNodesIntoNodeCorner_StillPrefersY()
        {
            // 空ノードを飛ばしながら進み、(16,1) の辺で Y 越境（空のセル (15,0,5) へ）と X 越境（木の基準点 x=16 のノード境界を越えて (16,0,5) へ）が同じ t になる
            // The ray skips empty nodes and reaches the (16,1) edge where the Y crossing (into empty cell (15,0,5)) and the X crossing (over the node boundary at the octree origin x=16 into (16,0,5)) share t
            var cells = CreateUnitBlocks(new Vector3Int(16, 0, 5), new Vector3Int(-30, 50, 5));

            var ray = new Ray(new Vector3(-24f, 41f, 5.5f), new Vector3(1f, -1f, 0f).normalized);
            Assert.IsTrue(PlacementAimCellTracer.TryTrace(cells, ray, float.PositiveInfinity, out var distance, out var surface));
            Assert.AreEqual(PreviewSurfaceType.XZ_Y, surface);
            Assert.AreEqual(40f * Mathf.Sqrt(2f), distance, 1e-3f);
        }

        [Test]
        public void SparseBlocks_MatchEveryCellBoxIntersection()
        {
            // まばらに置いたブロックへ乱数のレイを撃ち、全占有セルの箱と1つずつ交差させた最も近い面と比べる
            // Shoot random rays at scattered blocks and compare with the nearest face from intersecting every occupied cell box
            var random = new System.Random(12345);
            var cells = new BlockOccupiedCellIndex();
            var occupied = new List<Vector3Int>();
            for (var i = 0; i < 40; i++)
            {
                var size = new Vector3Int(random.Next(1, 3), random.Next(1, 3), random.Next(1, 3));
                var posInfo = new BlockPositionInfo(new Vector3Int(random.Next(-80, 80), random.Next(0, 40), random.Next(-80, 80)), BlockDirection.North, size);
                cells.Register(posInfo);
                for (var x = posInfo.MinPos.x; x <= posInfo.MaxPos.x; x++)
                for (var y = posInfo.MinPos.y; y <= posInfo.MaxPos.y; y++)
                for (var z = posInfo.MinPos.z; z <= posInfo.MaxPos.z; z++)
                    occupied.Add(new Vector3Int(x, y, z));
            }

            var hits = 0;
            for (var i = 0; i < 3000; i++)
            {
                var origin = new Vector3(Range(random, -100f, 100f), Range(random, -10f, 60f), Range(random, -100f, 100f));
                if (cells.TryGetBlockAt(Vector3Int.FloorToInt(origin), out _)) continue;
                // 狙ったブロックの近くへ向け、当たるレイも十分に含める
                // Aim near a chosen occupied cell so plenty of rays hit
                var target = (Vector3)occupied[random.Next(occupied.Count)] + new Vector3(Range(random, -1f, 2f), Range(random, -1f, 2f), Range(random, -1f, 2f));
                var ray = new Ray(origin, target - origin);

                var expectedHit = TryNearestCellBox(occupied, ray, out var expectedDistance, out var expectedSurface);
                var actualHit = PlacementAimCellTracer.TryTrace(cells, ray, float.PositiveInfinity, out var actualDistance, out var actualSurface);
                Assert.AreEqual(expectedHit, actualHit, $"ray {i} {ray}");
                if (!expectedHit) continue;
                hits++;
                Assert.AreEqual(expectedDistance, actualDistance, 1e-3f, $"ray {i} {ray}");
                Assert.AreEqual(expectedSurface, actualSurface, $"ray {i} {ray}");
            }
            Assert.Greater(hits, 500);
        }

        [Test]
        public void EdgeGraze_AtLargeNodeCorner_StillPrefersY()
        {
            // 木の基準点は最初のセル (64,0,5) なので x=64・y=0 はどの段でもノードの境目。外接直方体の中から空ノードを飛ばしてその辺を下から通り、Y の面（下面）を選ぶ
            // The octree origin is the first cell (64,0,5), so x=64 and y=0 are node boundaries at every level; a ray from inside the bounds skips empty nodes, grazes that edge from below and picks the Y face (bottom)
            var cells = CreateUnitBlocks(new Vector3Int(64, 0, 5), new Vector3Int(-1000, -300, 5));

            var ray = new Ray(new Vector3(-136f, -200f, 5.5f), new Vector3(1f, 1f, 0f).normalized);
            Assert.IsTrue(PlacementAimCellTracer.TryTrace(cells, ray, float.PositiveInfinity, out var distance, out var surface));
            Assert.AreEqual(PreviewSurfaceType.XZ_Origin, surface);
            Assert.AreEqual(200f * Mathf.Sqrt(2f), distance, 1e-2f);
        }

        [Test]
        public void ClustersAcrossWideRange_MatchEveryCellBoxIntersection()
        {
            // 0 をまたぐ ±3000 の範囲に固まりを散らし、大きな空ノードを飛ばす長い線でも、全占有セルの箱との交差と一致すること
            // Scatter clusters over ±3000 across zero; long rays that skip large empty nodes still match intersecting every occupied cell box
            var random = new System.Random(777);
            var cells = new BlockOccupiedCellIndex();
            var occupied = new List<Vector3Int>();
            for (var cluster = 0; cluster < 12; cluster++)
            {
                var center = new Vector3Int(random.Next(-3000, 3000), random.Next(-100, 200), random.Next(-3000, 3000));
                for (var i = 0; i < 6; i++)
                {
                    var size = new Vector3Int(random.Next(1, 4), random.Next(1, 4), random.Next(1, 4));
                    var posInfo = new BlockPositionInfo(center + new Vector3Int(random.Next(-20, 20), random.Next(-5, 5), random.Next(-20, 20)), BlockDirection.North, size);
                    cells.Register(posInfo);
                    for (var x = posInfo.MinPos.x; x <= posInfo.MaxPos.x; x++)
                    for (var y = posInfo.MinPos.y; y <= posInfo.MaxPos.y; y++)
                    for (var z = posInfo.MinPos.z; z <= posInfo.MaxPos.z; z++)
                        occupied.Add(new Vector3Int(x, y, z));
                }
            }

            var hits = 0;
            for (var i = 0; i < 1500; i++)
            {
                var origin = new Vector3(Range(random, -3500f, 3500f), Range(random, -300f, 400f), Range(random, -3500f, 3500f));
                if (cells.TryGetBlockAt(Vector3Int.FloorToInt(origin), out _)) continue;
                var target = (Vector3)occupied[random.Next(occupied.Count)] + new Vector3(Range(random, -1f, 2f), Range(random, -1f, 2f), Range(random, -1f, 2f));
                var ray = new Ray(origin, target - origin);

                var expectedHit = TryNearestCellBox(occupied, ray, out var expectedDistance, out var expectedSurface);
                var actualHit = PlacementAimCellTracer.TryTrace(cells, ray, float.PositiveInfinity, out var actualDistance, out var actualSurface);
                Assert.AreEqual(expectedHit, actualHit, $"ray {i} {ray}");
                if (!expectedHit) continue;
                hits++;
                Assert.AreEqual(expectedDistance, actualDistance, 1e-2f, $"ray {i} {ray}");
                Assert.AreEqual(expectedSurface, actualSurface, $"ray {i} {ray}");
            }
            Assert.Greater(hits, 300);
        }

        [Test]
        public void MatchesPlainCellWalk_ExactlyIncludingTiesRemovalAndRange()
        {
            // 飛ばしを使わない1マスずつの走査を基準に、当たるかどうか・距離（ビット単位）・面が一致すること
            // 辺・角の同値が起きるよう整数・半整数の始点と軸平行・45度の向きを多く混ぜ、撤去の後と有限の射程も試す
            // Against a plain cell-by-cell walk without skipping, hit, distance (bit-exact) and face all match
            // Integer and half-integer origins with axis-aligned and 45-degree directions are mixed in so edge and corner ties occur; removal and finite ranges are covered too
            var random = new System.Random(4242);
            var cells = new BlockOccupiedCellIndex();
            var occupied = new HashSet<Vector3Int>();
            var placed = new List<BlockPositionInfo>();
            for (var cluster = 0; cluster < 8; cluster++)
            {
                var center = new Vector3Int(random.Next(-300, 300), random.Next(-60, 60), random.Next(-300, 300));
                for (var i = 0; i < 25; i++)
                {
                    var size = new Vector3Int(random.Next(1, 4), random.Next(1, 4), random.Next(1, 4));
                    var posInfo = new BlockPositionInfo(center + new Vector3Int(random.Next(-12, 12), random.Next(-4, 4), random.Next(-12, 12)), BlockDirection.North, size);
                    if (OverlapsAny(occupied, posInfo)) continue;
                    cells.Register(posInfo);
                    placed.Add(posInfo);
                    ForEachCell(posInfo, c => occupied.Add(c));
                }
            }

            // 一部を撤去して、刈り込みと番号の使い直しの後の木でも走査させる
            // Remove some blocks so the walk also runs on an octree after pruning and index reuse
            for (var i = placed.Count - 1; i >= 0; i -= 3)
            {
                cells.Remove(placed[i]);
                ForEachCell(placed[i], c => occupied.Remove(c));
            }

            var directions = new List<Vector3>
            {
                Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
                new Vector3(1, 1, 0).normalized, new Vector3(-1, 1, 0).normalized, new Vector3(1, -1, 0).normalized, new Vector3(0, 1, 1).normalized,
                new Vector3(0, -1, -1).normalized, new Vector3(1, 0, 1).normalized, new Vector3(-1, 0, 1).normalized, new Vector3(1, 1, 1).normalized, new Vector3(-1, -1, 1).normalized,
            };
            var occupiedList = new List<Vector3Int>(occupied);
            var hits = 0;
            for (var i = 0; i < 4000; i++)
            {
                var target = occupiedList[random.Next(occupiedList.Count)];
                var offset = new Vector3Int(random.Next(-200, 200), random.Next(-80, 80), random.Next(-200, 200));
                var origin = (Vector3)(target + offset) + (random.Next(2) == 0 ? Vector3.zero : new Vector3(0.5f, 0.5f, 0.5f));
                if (occupied.Contains(Vector3Int.FloorToInt(origin))) continue;
                var direction = random.Next(3) == 0 ? ((Vector3)target + new Vector3(0.5f, 0.5f, 0.5f) - origin).normalized : directions[random.Next(directions.Count)];
                var maxDistance = random.Next(4) == 0 ? Range(random, 1f, 300f) : float.PositiveInfinity;
                var ray = new Ray(origin, direction);

                var expectedHit = TryPlainCellWalk(cells, occupied, ray, maxDistance, out var expectedDistance, out var expectedSurface);
                var actualHit = PlacementAimCellTracer.TryTrace(cells, ray, maxDistance, out var actualDistance, out var actualSurface);
                Assert.AreEqual(expectedHit, actualHit, $"ray {i} {ray} max {maxDistance}");
                if (!expectedHit) continue;
                hits++;
                Assert.AreEqual(expectedDistance, actualDistance, $"ray {i} {ray} max {maxDistance}");
                Assert.AreEqual(expectedSurface, actualSurface, $"ray {i} {ray} max {maxDistance}");
            }
            Assert.Greater(hits, 500);
        }

        private static bool TryPlainCellWalk(BlockOccupiedCellIndex cells, HashSet<Vector3Int> occupied, Ray ray, float maxDistance, out float distance, out PreviewSurfaceType surfaceType)
        {
            // 始点のセルから1マスずつ進む素直な走査。射程は物理レイの距離と外接直方体の出口の近いほう、同値の組は最初の軸を面の軸にする
            // Plain walk from the origin cell; range is the nearer of the physics distance and the bounding-box exit, and a tie group's first axis is the face axis
            distance = 0f;
            surfaceType = default;
            cells.TryGetBounds(out var minCell, out var maxCell);
            if (!PlacementAimBoxSlab.TryIntersect(ray, minCell, maxCell + Vector3Int.one, out _, out _, out var tExit, out _)) return false;
            var tEnd = Mathf.Min(tExit, maxDistance);
            var origin = ray.origin;
            var direction = ray.direction;
            var cell = Vector3Int.FloorToInt(origin);
            var lastT = float.NegativeInfinity;
            var faceAxis = -1;
            var axisOrder = new[] { 1, 0, 2 };
            while (true)
            {
                var best = -1;
                var t = float.PositiveInfinity;
                foreach (var axis in axisOrder)
                {
                    if (direction[axis] == 0f) continue;
                    var boundary = direction[axis] > 0f ? cell[axis] + 1 : cell[axis];
                    var candidate = (boundary - origin[axis]) / direction[axis];
                    if (candidate < t)
                    {
                        t = candidate;
                        best = axis;
                    }
                }
                if (best < 0 || t > tEnd) return false;
                cell[best] += direction[best] > 0f ? 1 : -1;
                if (t != lastT) faceAxis = best;
                lastT = t;
                if (!occupied.Contains(cell)) continue;
                distance = t;
                surfaceType = PlacementAimBoxSlab.EnterFace(faceAxis, direction[faceAxis] > 0f);
                return true;
            }
        }

        private static bool OverlapsAny(HashSet<Vector3Int> occupied, BlockPositionInfo posInfo)
        {
            var overlaps = false;
            ForEachCell(posInfo, c => overlaps |= occupied.Contains(c));
            return overlaps;
        }

        private static void ForEachCell(BlockPositionInfo posInfo, System.Action<Vector3Int> action)
        {
            for (var x = posInfo.MinPos.x; x <= posInfo.MaxPos.x; x++)
            for (var y = posInfo.MinPos.y; y <= posInfo.MaxPos.y; y++)
            for (var z = posInfo.MinPos.z; z <= posInfo.MaxPos.z; z++)
                action(new Vector3Int(x, y, z));
        }

        private static bool TryNearestCellBox(List<Vector3Int> occupied, Ray ray, out float distance, out PreviewSurfaceType surfaceType)
        {
            distance = float.PositiveInfinity;
            surfaceType = default;
            foreach (var cell in occupied)
            {
                if (!PlacementAimBoxSlab.TryIntersect(ray, cell, cell + Vector3Int.one, out var tEnter, out var enterAxis, out _, out _)) continue;
                if (tEnter <= 0f || tEnter >= distance) continue;
                distance = tEnter;
                surfaceType = PlacementAimBoxSlab.EnterFace(enterAxis, ray.direction[enterAxis] > 0f);
            }
            return !float.IsPositiveInfinity(distance);
        }

        private static float Range(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }

        private static BlockOccupiedCellIndex CreateUnitBlocks(Vector3Int first, Vector3Int second)
        {
            var cells = new BlockOccupiedCellIndex();
            cells.Register(new BlockPositionInfo(first, BlockDirection.North, Vector3Int.one));
            cells.Register(new BlockPositionInfo(second, BlockDirection.North, Vector3Int.one));
            return cells;
        }

        private static (BlockOccupiedCellIndex, BlockPositionInfo) CreateSingle(BlockDirection direction)
        {
            var cells = new BlockOccupiedCellIndex();
            var posInfo = new BlockPositionInfo(Origin, direction, Size);
            cells.Register(posInfo);
            return (cells, posInfo);
        }

        private static Vector3 Center(BlockPositionInfo posInfo)
        {
            return ((Vector3)posInfo.MinPos + posInfo.MaxPos + Vector3.one) / 2f;
        }

        private static void AssertFace(BlockOccupiedCellIndex cells, Vector3 from, Vector3 direction, PreviewSurfaceType expected)
        {
            Assert.IsTrue(PlacementAimCellTracer.TryTrace(cells, new Ray(from, direction), 100f, out _, out var surface), $"from {from}");
            Assert.AreEqual(expected, surface, $"from {from}");
        }
    }
}
