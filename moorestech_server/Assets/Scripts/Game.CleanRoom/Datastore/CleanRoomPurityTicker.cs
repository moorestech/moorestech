using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.CleanRoom.Pollution;
using Game.World.Interface.DataStore;

namespace Game.CleanRoom
{
    // 毎tick: 全部屋の不純度Nを積分し、フィルター摩耗とドアバーストを反映、閾値行を更新、孤立猶予を減算する。
    // Each tick: integrate N for every room, apply filter wear and door bursts, update threshold rows, and decay orphan grace.
    public class CleanRoomPurityTicker
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly CleanRoomBlockRegistries _registries;

        // 汚染レート供給シーム。既定はジオメトリ＋接続点から A_total を算出。テストは override 可能。
        // Pollution provider seam; defaults to A_total from geometry + connectors. Tests may override.
        private Func<CleanRoom, double> _pollutionPerSecondProvider = DefaultPollutionPerSecond;

        // 閾値行（マスタから1回変換してキャッシュ）。
        // Threshold rows converted once from the master.
        private IReadOnlyList<CleanRoomThresholdRow> _thresholdRows;

        public CleanRoomPurityTicker(IWorldBlockDatastore worldBlockDatastore, CleanRoomBlockRegistries registries)
        {
            _worldBlockDatastore = worldBlockDatastore;
            _registries = registries;
        }

        public void SetPollutionPerSecondProvider(Func<CleanRoom, double> provider)
        {
            _pollutionPerSecondProvider = provider;
        }

        // 全部屋の N を積分し閾値行を二条件＋ヒステリシスで更新、孤立猶予を減算する。
        // Integrate N for every room, update the threshold row, and tick down orphan grace.
        public void Tick(CleanRoomWorld world)
        {
            EnsureThresholdRows();

            foreach (var room in world.Rooms)
            {
                var aTotal = _pollutionPerSecondProvider(room);

                // 部屋内フィルターを一度だけ集める（n·q と摩耗配分で共有）。
                // Collect in-room filters once (shared between n·q and wear distribution).
                var filters = _registries.CollectForRoom(room);
                var nq = 0.0;
                foreach (var f in filters) nq += f.RemovalVolumePerSecond;

                // 今tickの除去総量を旧濃度から算出（N をマイナスにしない）。IntegrateTick の除去項と一致。
                // Removed amount this tick from the OLD concentration (never below N); matches IntegrateTick's removal term.
                var removedTotal = nq * room.Concentration * GameUpdater.SecondsPerTick;
                if (removedTotal > room.ImpurityCount) removedTotal = room.ImpurityCount;

                // dN 積分（0クランプは純関数内）。
                // Integrate dN (zero clamp inside the pure function).
                var newN = CleanRoomPurityRules.IntegrateTick(room.ImpurityCount, room.Volume, aTotal, nq, GameUpdater.SecondsPerTick);
                var delta = newN - room.ImpurityCount;
                if (delta >= 0.0) room.AddImpurity(delta);
                else room.RemoveImpurity(-delta);

                // 除去寄与をフィルターへ配分（汚染レート比例の摩耗）。
                // Distribute removed impurity to filters (wear proportional to removal rate).
                if (nq > 0.0 && removedTotal > 0.0)
                    foreach (var f in filters)
                        f.ApplyRemovedImpurity(removedTotal * (f.RemovalVolumePerSecond / nq));

                // 閾値行の更新（ACH = n·q/V）。
                // Update threshold row with ACH = n·q/V.
                var ach = room.Volume > 0 ? nq / room.Volume : 0.0;
                room.SetThresholdIndex(CleanRoomPurityRules.DecideThresholdIndex(room.ThresholdIndex, room.Concentration, ach, _thresholdRows));

                // ドアハッチの通過バーストは A_total を経由せず N へ直接加算する（balance §2 の単位注意）。
                // Door-passage bursts go straight into N, never through A_total (unit note in balance §2).
                AddDoorBursts(room);
            }

            // 孤立状態の猶予を毎tick減らし、切れたら Invalid（破棄は次の再検出時）。
            // Tick down orphan grace; on expiry mark Invalid (discarded at the next re-detection).
            foreach (var orphan in world.Orphans)
            {
                if (orphan.Status != CleanRoomRoomStatus.Degraded) continue;
                var remaining = orphan.GraceRemainingSeconds - GameUpdater.SecondsPerTick;
                if (remaining > 0.0) orphan.SetStatus(CleanRoomRoomStatus.Degraded, remaining);
                else orphan.SetStatus(CleanRoomRoomStatus.Invalid, 0.0);
            }
        }

        // 部屋に面するドアハッチの保留バーストを合算して N へ直接加算する（peek は非破壊＝共有境界は両部屋に全額計上）。
        // 部屋セルの6近傍の境界ブロック → ICleanRoomDoorHatch を BlockInstanceId で重複排除して集める。
        // Sum the pending bursts of door hatches facing the room and add straight to N (peek is non-destructive -> both shared-boundary rooms book the full burst).
        private void AddDoorBursts(CleanRoom room)
        {
            var seen = new HashSet<BlockInstanceId>();
            var burst = 0.0;
            foreach (var cell in room.Cells)
            foreach (var n in CleanRoomWorld.SixNeighbors(cell))
            {
                if (room.Contains(n)) continue;
                if (!_worldBlockDatastore.TryGetBlock(n, out var block)) continue;
                if (!seen.Add(block.BlockInstanceId)) continue;
                if (block.TryGetComponent<ICleanRoomDoorHatch>(out var door))
                    burst += door.PeekPendingBurst();
            }
            if (burst > 0.0) room.AddImpurity(burst);
        }

        private void EnsureThresholdRows()
        {
            if (_thresholdRows != null) return;

            // マスタ要素 → 判定行へ1回だけ変換（生成型のプロパティ名は実生成結果に合わせる）。
            // Convert master elements to decision rows once.
            var rows = new List<CleanRoomThresholdRow>();
            foreach (var element in MasterHolder.CleanRoomThresholdMaster.Rows)
                rows.Add(new CleanRoomThresholdRow(element.MaxConcentration, element.RequiredAirChangeRate));
            _thresholdRows = rows;
        }

        // 既定の汚染レート: 接続点・アイテムハッチ搬送・稼働機械を実走査して A_total を算出する。
        // 接続点とハッチスループットは同一境界走査で集計（二重走査回避）。機械は内部セル走査。
        // Default pollution rate: scan connectors + item-hatch throughput (single boundary pass) + running machines for A_total.
        private static double DefaultPollutionPerSecond(CleanRoom room)
        {
            CleanRoomPollutionCalculator.ScanBoundary(room, out var connectorCount, out var hatchThroughput);
            var runningMachineCount = CleanRoomPollutionCalculator.CountRunningMachines(room);
            return CleanRoomPollutionCalculator.ComputeATotal(room.Volume, room.SurfaceArea, connectorCount, runningMachineCount, hatchThroughput);
        }
    }
}
