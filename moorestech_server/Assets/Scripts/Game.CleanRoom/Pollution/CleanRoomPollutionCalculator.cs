using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom.Pollution
{
    // A_total を部屋ジオメトリ・接続点・稼働機械から算出する静的ヘルパ。CleanRoomDatastore が利用。
    // Static helper computing A_total from geometry, connectors, machines; used by CleanRoomDatastore.
    public static class CleanRoomPollutionCalculator
    {
        // 数値ソース §2（balance-parameters）。
        // Coefficients from balance-parameters §2.
        private const double AVolume = 0.10;
        private const double ASurface = 0.05;
        private const double AConnector = 0.50;
        private const double AMachine = 2.0;
        private const double KHatch = 0.30;

        // 純関数。worked example の固定アサーションはここを叩く。
        // ドアバーストは A_total に含めない（瞬間量。フェーズ5で CleanRoom.AddImpurity へ直接加算）。
        // Pure function; door bursts are NOT part of A_total (instant amount, added straight to N in phase 5).
        public static double ComputeATotal(int volume, int surfaceArea, int connectorCount, int runningMachineCount, double hatchThroughputPerSecond)
        {
            return AMachine * runningMachineCount
                   + KHatch * hatchThroughputPerSecond
                   + AVolume * volume
                   + ASurface * surfaceArea
                   + AConnector * connectorCount;
        }

        // 部屋に面する境界ブロックのうち Wall 以外（各種ハッチ）を接続点として数える。
        // BlockInstanceId 単位で重複排除（マルチセル境界ブロックの多重カウント防止）。
        // Count non-Wall boundary blocks (hatches) facing the room; dedupe by BlockInstanceId.
        public static int CountConnectors(CleanRoom room)
        {
            var world = ServerContext.WorldBlockDatastore;
            var seen = new HashSet<BlockInstanceId>();
            var count = 0;
            foreach (var cell in room.Cells)
            foreach (var n in SixNeighbors(cell))
            {
                if (room.Contains(n)) continue;
                if (!world.TryGetBlock(n, out var block)) continue;
                if (!seen.Add(block.BlockInstanceId)) continue;
                if (!block.TryGetComponent<ICleanRoomBoundaryComponent>(out var boundary)) continue;
                if (boundary.BoundaryKind != CleanRoomBoundaryKind.Wall) count++;
            }
            return count;
        }

        // 6方向の隣接セル座標を返す。
        // Yield the six face-adjacent neighbors.
        private static IEnumerable<Vector3Int> SixNeighbors(Vector3Int p)
        {
            yield return new Vector3Int(p.x + 1, p.y, p.z);
            yield return new Vector3Int(p.x - 1, p.y, p.z);
            yield return new Vector3Int(p.x, p.y + 1, p.z);
            yield return new Vector3Int(p.x, p.y - 1, p.z);
            yield return new Vector3Int(p.x, p.y, p.z + 1);
            yield return new Vector3Int(p.x, p.y, p.z - 1);
        }
    }
}
