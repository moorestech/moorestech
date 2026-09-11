using System;
using System.Collections.Generic;
using System.IO;
using Game.Paths;
using UnityEngine;

namespace Game.SaveLoad.Snapshot
{
    // 受信パケットを処理tick付きで区間ファイルへ追記する。区間はスナップショットごとに切り替わる
    // Appends received packets with their processing tick to segment files; a new segment starts at each snapshot
    public sealed class ReceivedPacketLog
    {
        private readonly object _lock = new();
        private string _directory;
        private BinaryWriter _writer;
        private ulong _currentSegmentFromTick;
        private bool _inactiveLogged;

        public bool IsActive { get; private set; }

        public void Start(string directory, ulong fromTick)
        {
            _directory = directory;
            Directory.CreateDirectory(directory);
            Rotate(fromTick);
            IsActive = true;
        }

        public void Append(ulong tick, byte[] payload)
        {
            if (!IsActive)
            {
                // 無効は正常運用（テスト・プレイテスト）なので理由は1回だけ出す
                // Being disabled is normal (tests, playtests), so log the reason only once
                if (!_inactiveLogged) Debug.Log("パケットログは未開始のため記録しません（常時記録が無効）");
                _inactiveLogged = true;
                return;
            }
            lock (_lock)
            {
                _writer.Write(tick);
                _writer.Write(payload.Length);
                _writer.Write(payload);
            }
        }

        public void Flush()
        {
            lock (_lock)
            {
                _writer?.Flush();
            }
        }

        public void Rotate(ulong fromTick)
        {
            lock (_lock)
            {
                _writer?.Flush();
                _writer?.Dispose();
                var path = Path.Combine(_directory, WorldDataDirectory.ReceivedPacketLogFileName(fromTick));
                _writer = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read));
                _currentSegmentFromTick = fromTick;
            }
        }

        // 最古スナップショット以前で始まる区間を消す。書き込み中の区間は残す
        // Delete segments starting at or before the oldest snapshot; keep the segment being written
        public void DeleteSegmentsBefore(ulong oldestSnapshotTick)
        {
            foreach (var path in SegmentFilePaths())
            {
                if (!TryParseSegmentFromTick(Path.GetFileName(path), out var fromTick)) continue;
                if (fromTick > oldestSnapshotTick || fromTick == _currentSegmentFromTick) continue;

                // 常時記録の削除は後から追跡できる必要があるので、消した区間と理由を必ず残す
                // Deleting always-on capture must stay auditable, so record which segment went and why
                Debug.Log($"パケットログ区間を削除しました path:{path} 理由:最古スナップショット{oldestSnapshotTick}より前の区間");
                DeleteSegmentFile(path);
            }
        }

        public IReadOnlyList<string> SegmentFilePaths()
        {
            var result = new List<string>();
            if (_directory == null || !Directory.Exists(_directory)) return result;
            foreach (var path in Directory.GetFiles(_directory, "packets_*.bin"))
            {
                if (TryParseSegmentFromTick(Path.GetFileName(path), out _)) result.Add(path);
            }
            result.Sort((a, b) => ParseFromTick(a).CompareTo(ParseFromTick(b)));
            return result;
        }

        public static bool TryParseSegmentFromTick(string fileName, out ulong fromTick)
        {
            fromTick = 0;
            if (!fileName.StartsWith("packets_") || !fileName.EndsWith(".bin")) return false;
            var core = fileName.Substring("packets_".Length, fileName.Length - "packets_".Length - ".bin".Length);
            return ulong.TryParse(core, out fromTick);
        }

        private static void DeleteSegmentFile(string path)
        {
            // ディスク削除は外部境界。消せなくても記録は続けたいので、失敗は出力して次の区間へ進む
            // Disk deletion is an external boundary; capture must continue, so a failure is logged and the loop moves on
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

        private static ulong ParseFromTick(string path)
        {
            TryParseSegmentFromTick(Path.GetFileName(path), out var fromTick);
            return fromTick;
        }
    }
}
