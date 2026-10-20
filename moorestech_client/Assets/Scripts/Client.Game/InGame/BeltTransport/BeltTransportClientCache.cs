using System.Collections.Generic;
using Client.Game.InGame.BeltTransport.Statistics;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using Game.Block.Blocks.BeltConveyor.Sync.Replica;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using UnityEngine;

namespace Client.Game.InGame.BeltTransport
{
    // クライアントが持つベルト搬送の複製。全量で組み、毎tickの差分で進め、照合用のハッシュを出す
    // The client's replica of the belt transport: assembled from a full state, advanced by per-tick diffs, hashed for verification
    public sealed class BeltTransportClientCache
    {
        // 途中参加の全量が届くまでは無い。順序待ちの仕組みがその前のイベントを流さないので、差分が先に来ることは無い
        // Absent until the join-time full state arrives; the ordered buffer flushes nothing before it, so no diff can come first
        public BeltTransportReplica Replica { get; private set; }

        // 組み直しとtickのたびに進む版。表示側は購読でなく、描画更新の順番の中でこの版を見て作り直す
        // Advances on every re-assembly and tick; the view compares this inside its explicit update order instead of subscribing
        public int ReplicaVersion { get; private set; }

        // 通常・分岐segmentの搬出数の統計。複製の組み立てごとに観測先として差し直し、tickごとに進める
        // Exit statistics of normal and branch segments; re-attached as the observer on every assembly and advanced each tick
        public BeltSegmentExitStatisticsTable ExitStatistics { get; }

        // 統計を取っているsegmentのマス座標→segment番号。合流と内部segmentのマスは載せない
        // Cell position → segment number for segments with statistics; merge and internal segment cells are left out
        private readonly Dictionary<Vector3Int, int> _observedSegmentIndexByCell = new();

        public BeltTransportClientCache(BeltSegmentExitStatisticsTable exitStatistics)
        {
            ExitStatistics = exitStatistics;
        }

        public void ApplyFullState(BeltTransportFullState fullState)
        {
            Replica = BeltTransportReplicaAssembler.Assemble(fullState);
            ReplicaVersion++;
            AttachExitStatistics();
        }

        // サーバーと同じtickを再生する。falseは複製がサーバーと食い違った印
        // Replays the same tick as the server; false marks the replica as diverged from the server
        public bool ApplyTickDiff(BeltTickDiff diff)
        {
            var consistent = Replica.Tick(diff);
            ReplicaVersion++;
            ExitStatistics.EndTick();
            return consistent;
        }

        public uint ComputeCurrentHash()
        {
            return BeltTransportStateHash.Compute(Replica.CaptureFullState());
        }

        // そのマスを持つ統計対象segmentの番号。複製が無い間、合流マス、ベルトでないマスはfalse
        // The number of the statistics-bearing segment owning the cell; false without a replica, on a merge cell or off the belt
        public bool TryGetObservedSegmentIndex(Vector3Int cell, out int segmentIndex)
        {
            return _observedSegmentIndexByCell.TryGetValue(cell, out segmentIndex);
        }

        // 統計を全リセットし、通常(内部を除く)と分岐のsegmentだけに観測先を差す(ユーザー裁定 2026-10-20: 合流は数えない)
        // Fully resets the statistics and attaches the observer only to normal (non-internal) and branch segments (user ruling 2026-10-20: merges are not counted)
        private void AttachExitStatistics()
        {
            ExitStatistics.Reset(Replica.Segments.Length);
            _observedSegmentIndexByCell.Clear();
            for (var i = 0; i < Replica.Segments.Length; i++)
            {
                var shape = Replica.Shapes[i];
                if (shape.Kind == BeltSegmentKind.Merge || shape.IsInternal) continue;
                Replica.Segments[i].SetExitObserver(ExitStatistics, i);
                foreach (var cell in shape.Cells) _observedSegmentIndexByCell[cell.Position] = i;
            }
        }
    }
}
