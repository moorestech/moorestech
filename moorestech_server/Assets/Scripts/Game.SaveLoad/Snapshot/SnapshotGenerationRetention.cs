using System;
using System.Collections.Generic;
using System.IO;
using Game.Paths;
using UnityEngine;

namespace Game.SaveLoad.Snapshot
{
    // 書き出し済みスナップショット世代の権威リストを持ち、保持時間を基準に古い世代を消す
    // Owns the authoritative list of written snapshot generations and drops old ones by retention time
    public sealed class SnapshotGenerationRetention
    {
        private readonly WorldDataDirectory _directory;
        private readonly ReceivedPacketLog _packetLog;
        private readonly List<ulong> _writtenTicks = new();
        private uint _retentionTicks;
        private int _maxGenerations;

        public SnapshotGenerationRetention(WorldDataDirectory directory, ReceivedPacketLog packetLog)
        {
            _directory = directory;
            _packetLog = packetLog;
        }

        public void Configure(uint retentionTicks, int maxGenerations)
        {
            _retentionTicks = retentionTicks;
            _maxGenerations = maxGenerations;
        }

        // 剪定は時間基準。保持区間を覆う最古の1本より前だけを消すので、即時取得が周期世代の枠を食わない
        // Pruning is time-based: only snapshots older than the one covering the retention window go, so an immediate capture never eats a periodic generation
        public void AddAndPrune(ulong tick)
        {
            _writtenTicks.Add(tick);
            var newestTick = _writtenTicks[_writtenTicks.Count - 1];
            var retentionStartTick = newestTick > _retentionTicks ? newestTick - _retentionTicks : 0UL;

            // 2番目に古い世代がまだ保持区間の開始を覆っているなら、最古は要らない
            // If the second-oldest still covers the start of the retention window, the oldest is no longer needed
            while (_writtenTicks.Count > 1 && _writtenTicks[1] <= retentionStartTick)
            {
                RemoveOldest($"保持区間の開始tick{retentionStartTick}より前");
            }

            // 上限はディスク保護。ここで消すと保持時間の保証を割るので理由を分けて残す
            // The cap protects the disk; deleting here breaks the retention guarantee, so log it under its own reason
            while (_writtenTicks.Count > _maxGenerations)
            {
                RemoveOldest($"上限{_maxGenerations}世代を超過（保持時間{_retentionTicks}tickの保証を割る）");
            }
        }

        // バンドルへ載せるファイル名はこの権威リストから作る。ディスクを辞書順で舐めると最古が最新として載る
        // Bundle file names come from this authoritative list; a lexicographic disk scan would make the oldest look newest
        public List<string> CopyFileNames()
        {
            var names = new List<string>(_writtenTicks.Count);
            foreach (var tick in _writtenTicks) names.Add(WorldDataDirectory.SnapshotFileName(tick));
            return names;
        }

        private void RemoveOldest(string reason)
        {
            var oldest = _writtenTicks[0];
            _writtenTicks.RemoveAt(0);

            // 常時記録の削除は後から追跡できる必要があるので、消した世代と理由を必ず残す
            // Deleting always-on capture must stay auditable, so record which generation went and why
            Debug.Log($"スナップショットを削除しました tick:{oldest} 理由:{reason}");
            DeleteSnapshotFile(_directory.SnapshotFilePath(oldest));
            _packetLog.DeleteSegmentsBefore(_writtenTicks[0]);
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
