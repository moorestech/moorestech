using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.World.Interface.DataStore;

namespace Game.CleanRoom.Util
{
    /// <summary>
    ///     部屋の幾何・接続点・稼働機械・搬送から毎秒の汚染流入 A_total を求める
    ///     Computes per-second pollution inflow A_total from geometry, connectors, machines and hatches
    /// </summary>
    public static class CleanRoomPollution
    {
        public static double ComputeATotal(CleanRoom room, IWorldBlockDatastore world)
        {
            // 汚染係数が未定義なら流入なしとして扱う
            // Treat undefined pollution coefficients as zero inflow
            var pollution = MasterHolder.CleanRoomMaster.Pollution;
            if (pollution == null) return 0;

            // 境界走査1回で接続点数とハッチ搬送レートを同時に取る
            // A single boundary scan collects both connector count and hatch throughput
            CountBoundary(out var connectorCount, out var hatchThroughputPerSecond);
            var pollutingMachineCount = CountPollutingMachines();

            return pollution.AVolume * room.Volume +
                   pollution.ASurface * room.SurfaceArea +
                   pollution.AConnector * connectorCount +
                   pollution.AMachine * pollutingMachineCount +
                   pollution.KHatch * hatchThroughputPerSecond;

            #region Internal

            void CountBoundary(out int connectors, out double hatchThroughput)
            {
                connectors = 0;
                hatchThroughput = 0;

                // 全セル×6近傍のうち部屋外セルのブロックを BlockInstanceId で重複排除して数える
                // Dedup boundary blocks by BlockInstanceId over all cells' out-of-room six-neighbors
                var visited = new HashSet<BlockInstanceId>();
                foreach (var cell in room.Cells)
                foreach (var neighbor in CleanRoomCellSets.SixNeighbors(cell))
                {
                    if (room.Contains(neighbor)) continue;
                    if (!world.TryGetBlock(neighbor, out var block)) continue;
                    if (!visited.Add(block.BlockInstanceId)) continue;
                    if (!block.TryGetComponent<ICleanRoomBoundaryComponent>(out var boundary)) continue;

                    // 壁以外の境界（ドア・ハッチ等）は接続点として計上する
                    // Non-wall boundaries (doors, hatches, etc.) count as connectors
                    if (boundary.BoundaryKind != CleanRoomBoundaryKind.Wall) connectors++;
                    if (block.TryGetComponent<ICleanRoomItemHatch>(out var hatch)) hatchThroughput += hatch.RecentThroughputPerSecond;
                }
            }

            int CountPollutingMachines()
            {
                // 部屋内部セルのブロックを重複排除し、加工中の機械のみ数える
                // Dedup blocks on interior cells and count only machines currently processing
                var count = 0;
                var visited = new HashSet<BlockInstanceId>();
                foreach (var cell in room.Cells)
                {
                    if (!world.TryGetBlock(cell, out var block)) continue;
                    if (!visited.Add(block.BlockInstanceId)) continue;
                    if (block.TryGetComponent<ICleanRoomMachine>(out var machine) && machine.IsPolluting) count++;
                }

                return count;
            }

            #endregion
        }
    }
}
