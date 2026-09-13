using System;
using System.Collections.Generic;
using System.IO;
using Game.Paths;
using UnityEngine;

namespace Game.SaveLoad.Snapshot
{
    // 書き出し済みスナップショット世代の権威リストを持ち、周期分は保持時間で、即時確保分は本数で消す
    // Owns the authoritative list of written snapshot generations, dropping periodic ones by retention time and immediate ones by count
    public sealed class SnapshotGenerationRetention
    {
        private readonly WorldDataDirectory _directory;
        private readonly ReceivedPacketLog _packetLog;

        // 周期と即時を分けて持つ。混ぜると即時確保が本数上限を押し上げ、直前2分の周期世代を追い出す
        // Periodic and immediate are held apart; mixed, an immediate capture pushes the cap up and evicts the last two minutes of periodic generations
        private readonly List<ulong> _periodicTicks = new();
        private readonly List<ulong> _immediateTicks = new();
        private uint _retentionTicks;
        private int _maxImmediateGenerations;

        public SnapshotGenerationRetention(WorldDataDirectory directory, ReceivedPacketLog packetLog)
        {
            _directory = directory;
            _packetLog = packetLog;
        }

        public void Configure(uint retentionTicks, int maxImmediateGenerations)
        {
            _retentionTicks = retentionTicks;
            _maxImmediateGenerations = maxImmediateGenerations;
        }

        // 周期分は保持区間を覆う最古の1本より前だけ、即時確保分は本数上限を超えた分だけ消す
        // Periodic snapshots older than the one covering the retention window go; immediate ones go only past the count cap
        public void AddAndPrune(ulong tick, SnapshotCaptureKind kind)
        {
            if (kind == SnapshotCaptureKind.Immediate) _immediateTicks.Add(tick);
            else _periodicTicks.Add(tick);

            var retentionStartTick = tick > _retentionTicks ? tick - _retentionTicks : 0UL;

            // 2番目に古い周期世代がまだ保持区間の開始を覆っているなら、最古は要らない
            // If the second-oldest periodic generation still covers the start of the retention window, the oldest is no longer needed
            while (_periodicTicks.Count > 1 && _periodicTicks[1] <= retentionStartTick)
            {
                RemoveOldest(_periodicTicks, $"保持区間の開始tick{retentionStartTick}より前");
            }

            // 即時確保はバグ報告のための控えなので時間では消さない。ディスク保護は本数だけで行う
            // An immediate capture is a bug-report keepsake, so time never drops it; only the count protects the disk
            while (_immediateTicks.Count > _maxImmediateGenerations)
            {
                RemoveOldest(_immediateTicks, $"即時確保の上限{_maxImmediateGenerations}件を超過");
            }
        }

        // バンドルへ載せるファイル名はこの権威リストから作る。ディスクを辞書順で舐めると最古が最新として載る
        // Bundle file names come from this authoritative list; a lexicographic disk scan would make the oldest look newest
        public List<string> CopyFileNames()
        {
            var ticks = new List<ulong>(_periodicTicks.Count + _immediateTicks.Count);
            ticks.AddRange(_periodicTicks);
            ticks.AddRange(_immediateTicks);
            ticks.Sort();

            var names = new List<string>(ticks.Count);
            foreach (var tick in ticks) names.Add(WorldDataDirectory.SnapshotFileName(tick));
            return names;
        }

        private void RemoveOldest(List<ulong> ticks, string reason)
        {
            var oldest = ticks[0];
            ticks.RemoveAt(0);

            // 常時記録の削除は後から追跡できる必要があるので、消した世代と理由を必ず残す
            // Deleting always-on capture must stay auditable, so record which generation went and why
            Debug.Log($"スナップショットを削除しました tick:{oldest} 理由:{reason}");
            DeleteSnapshotFile(_directory.SnapshotFilePath(oldest));

            // 区間ファイルは周期・即時のどちらからも再生の出発点になるので、残る最古より前だけを消す
            // A segment feeds replay from either kind, so only segments before the oldest surviving generation may go
            _packetLog.DeleteSegmentsBefore(OldestRetainedTick());
        }

        private ulong OldestRetainedTick()
        {
            if (_periodicTicks.Count == 0) return _immediateTicks[0];
            if (_immediateTicks.Count == 0) return _periodicTicks[0];
            return Math.Min(_periodicTicks[0], _immediateTicks[0]);
        }

        private static void DeleteSnapshotFile(string path)
        {
            // ディスク削除は外部境界。消せなくても記録は続けたいので、失敗は出力して次の世代へ進む
            // Disk deletion is an external boundary; capture must continue, so a failure is logged and the loop moves on
            try
            {
                File.Delete(path);
            }
            catch (IOException e)
            {
                Debug.LogError($"スナップショットの削除に失敗しました path:{path} message:{e.Message}");
            }
            catch (UnauthorizedAccessException e)
            {
                Debug.LogError($"スナップショットの削除が権限で拒否されました path:{path} message:{e.Message}");
            }
        }
    }
}
