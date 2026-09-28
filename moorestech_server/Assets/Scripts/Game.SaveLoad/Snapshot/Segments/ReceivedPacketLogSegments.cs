using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Paths;
using UnityEngine;

namespace Game.SaveLoad.Snapshot.Segments
{
    // 区間ファイルの一覧と保持範囲を管理する。呼び出し側のロックで開閉状態を守る
    // Manage segment enumeration and retention; the caller's lock protects open/closed state
    internal sealed class ReceivedPacketLogSegments
    {
        private readonly string _directory;
        private bool _currentSegmentOpen;
        internal ulong CurrentFromTick { get; private set; }

        internal ReceivedPacketLogSegments(string directory)
        {
            _directory = directory;
        }

        internal string PathFor(ulong fromTick)
        {
            return Path.Combine(_directory, WorldDataDirectory.ReceivedPacketLogFileName(fromTick));
        }

        internal void MarkOpened(ulong fromTick)
        {
            CurrentFromTick = fromTick;
            _currentSegmentOpen = true;
        }

        internal void MarkClosed()
        {
            _currentSegmentOpen = false;
        }

        internal IReadOnlyList<string> FilePaths()
        {
            return WorldDataDirectory.EnumeratePacketLogFiles(_directory);
        }

        internal IReadOnlyList<string> CompletedFilePaths()
        {
            if (!_currentSegmentOpen) return FilePaths();

            // 書き込み中は末尾がレコード途中で切れうるので、取得結果へ複製しない
            // An open segment can end midway through a record, so exclude it from capture copies
            var openFileName = WorldDataDirectory.ReceivedPacketLogFileName(CurrentFromTick);
            return FilePaths().Where(path => Path.GetFileName(path) != openFileName).ToList();
        }

        internal void DeleteBefore(ulong oldestSnapshotTick)
        {
            foreach (var path in FilePaths())
            {
                if (!WorldDataDirectory.TryParsePacketLogFromTick(Path.GetFileName(path), out var fromTick)) continue;
                if (oldestSnapshotTick < fromTick || fromTick == CurrentFromTick) continue;

                // 削除した区間と理由を残して、記録の保持範囲を追跡できるようにする
                // Record the segment and reason so the retained capture range remains auditable
                Debug.Log($"パケットログ区間を削除しました path:{path} 理由:最古スナップショット{oldestSnapshotTick}より前の区間");
                DeleteFile(path);
            }

            #region Internal

            // ディスク削除は外部境界。失敗はログへ残し、実際のファイル一覧にも残す
            // Disk deletion is an external boundary; failures remain in the log and the actual file listing
            void DeleteFile(string path)
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException e)
                {
                    Debug.LogError($"パケットログ区間の削除に失敗しました path:{path} message:{e.Message}");
                }
                catch (UnauthorizedAccessException e)
                {
                    Debug.LogError($"パケットログ区間の削除が権限で拒否されました path:{path} message:{e.Message}");
                }
            }

            #endregion
        }
    }
}
