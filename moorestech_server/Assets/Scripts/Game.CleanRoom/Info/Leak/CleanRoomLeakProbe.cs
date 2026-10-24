using System.Collections.Generic;
using Game.CleanRoom.Util;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom.Info.Leak
{
    /// <summary>
    ///     密閉できていない構造の漏れ箇所を探す。
    ///     最も囲まれた種から軸レイで壁の箱を推定して中を塗り、箱の面に出た空きのうち外へ抜けるものを漏れとする。
    ///     面の外側1層がまだ境界で囲われていれば、箱をその面へ1層広げて探し直す（一面が抜けた部屋で残った境界の縁まで伸ばすため）。
    ///     Finds the openings of an unsealed structure.
    ///     It estimates the wall box with axis rays from the most enclosed seeds, fills it, and reports exposed face cells that escape outward.
    ///     If the layer beyond a face is still enclosed by boundaries, the box grows one layer on that face and the search repeats (so a room missing a whole face reaches the rim of the remaining boundary).
    /// </summary>
    public static class CleanRoomLeakProbe
    {
        // 6方向レイのうち境界に当たる本数がこれ未満の種は部屋の内側とみなさない
        // Seeds whose six rays hit fewer boundaries than this are not treated as a room interior
        private const int MinEnclosingRayHits = 4;

        // 面を広げる回数の上限（壁が延々続く構造で探索を打ち切る）
        // Cap on face growths, stopping endless wall structures
        private const int MaxFaceGrowths = 64;

        public static CleanRoomLeakReport ProbeFromBoundary(IWorldBlockDatastore world, IReadOnlyList<Vector3Int> boundaryCells)
        {
            // 境界ブロックの両側（6近傍の空き）を種の候補にし、より囲まれた側を後で選ぶ
            // Both sides of the boundary block (free six-neighbors) become candidates; the more enclosed side wins later
            var candidates = new List<Vector3Int>();
            foreach (var cell in boundaryCells)
            foreach (var direction in CleanRoomLeakRays.Directions)
            {
                var neighbor = cell + direction;
                if (candidates.Contains(neighbor) || CleanRoomLeakRays.IsBoundary(world, neighbor)) continue;
                candidates.Add(neighbor);
            }

            return ProbeFromSeeds(world, candidates);
        }

        public static CleanRoomLeakReport ProbeFromSeeds(IWorldBlockDatastore world, IReadOnlyList<Vector3Int> seedCandidates)
        {
            var seeds = new List<Vector3Int>();
            var rayHits = new List<Vector3Int>();
            SelectMostEnclosedSeeds(world, seedCandidates, seeds, rayHits);
            if (seeds.Count == 0) return CleanRoomLeakReport.NoEnclosure();

            var box = new CleanRoomLeakBox(seeds[0]);
            foreach (var cell in seeds) box.Include(cell);
            foreach (var cell in rayHits) box.Include(cell);

            for (var growth = 0; growth <= MaxFaceGrowths; growth++)
            {
                var visited = CleanRoomLeakFlood.FillInsideBox(world, seeds, box);
                if (CleanRoomCellSets.MaxRoomVolume < visited.Count) return CleanRoomLeakReport.TooLarge();

                // 面ごとに、壁が続いていれば箱を広げ、続いていなければ露出セルを漏れとして集める
                // Per face, grow the box if walls continue; otherwise collect the exposed cells as openings
                var openings = new List<Vector3Int>();
                var grown = false;
                foreach (var direction in CleanRoomLeakRays.Directions)
                {
                    var exposed = CollectExposedFaceCells(visited, direction);
                    if (exposed.Count == 0) continue;
                    if (CleanRoomLeakLayerTest.IsLayerEnclosed(world, box, direction, exposed))
                    {
                        box.GrowFace(direction);
                        grown = true;
                        continue;
                    }

                    openings.AddRange(exposed);
                }

                if (grown) continue;
                return openings.Count == 0 ? CleanRoomLeakReport.DetectionPending() : CleanRoomLeakReport.Openings(openings);
            }

            // 壁が延々続く構造は部屋の上限を超える大きさとして扱う
            // An endlessly walled structure is treated as larger than the room cap
            return CleanRoomLeakReport.TooLarge();

            #region Internal

            List<Vector3Int> CollectExposedFaceCells(HashSet<Vector3Int> visited, Vector3Int direction)
            {
                // 面上の塗りセルのうち、外側の隣が空いているもの
                // Filled cells on the face whose outer neighbor is free
                var exposed = new List<Vector3Int>();
                foreach (var cell in visited)
                {
                    if (!box.IsOnFace(cell, direction) || CleanRoomLeakRays.IsBoundary(world, cell + direction)) continue;
                    exposed.Add(cell);
                }

                return exposed;
            }

            #endregion
        }

        private static void SelectMostEnclosedSeeds(IWorldBlockDatastore world, IReadOnlyList<Vector3Int> candidates, List<Vector3Int> seeds, List<Vector3Int> rayHits)
        {
            // 6方向レイが境界に当たる本数で採点し、最多（下限以上）の候補とその命中セルだけを残す
            // Score candidates by boundary hits of six rays and keep only the best ones (at or above the minimum) with their hits
            var bestHitCount = MinEnclosingRayHits;
            foreach (var candidate in candidates)
            {
                var hits = new List<Vector3Int>();
                foreach (var direction in CleanRoomLeakRays.Directions)
                {
                    if (CleanRoomLeakRays.TryCastToBoundary(world, candidate, direction, out var hit)) hits.Add(hit);
                }

                if (hits.Count < bestHitCount) continue;
                if (bestHitCount < hits.Count)
                {
                    bestHitCount = hits.Count;
                    seeds.Clear();
                    rayHits.Clear();
                }

                seeds.Add(candidate);
                rayHits.AddRange(hits);
            }
        }
    }
}
