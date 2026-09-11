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

            // ディレクトリ作成は外部境界。記録を始められなくても起動そのものは落とさない
            // Creating the directory is an external boundary; failing to start capture must not fail the boot
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception e)
            {
                Degrade($"パケットログの置き場を作れませんでした dir:{directory}", e);
                return;
            }

            if (!TryRotate(fromTick)) return;
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
                // ファイル書き込みは外部境界。記録の失敗でパケット処理そのものを落とさないよう隔離し、以後は記録を止める
                // File writing is an external boundary; isolate it so a capture failure never drops packet processing, then stop capturing
                try
                {
                    _writer.Write(tick);
                    _writer.Write(payload.Length);
                    _writer.Write(payload);
                }
                catch (Exception e)
                {
                    Degrade($"パケットログへの追記に失敗しました tick:{tick}", e);
                }
            }
        }

        public void Flush()
        {
            lock (_lock)
            {
                // ファイルフラッシュは外部境界。取りこぼしを理由にtick末尾の処理を落とさない
                // Flushing is an external boundary; a lost flush must not break the tick-end processing
                try
                {
                    _writer?.Flush();
                }
                catch (Exception e)
                {
                    Degrade("パケットログのフラッシュに失敗しました", e);
                }
            }
        }

        // 書き込み中の区間を閉じてFileStreamを解放する。以後のAppendは無効ログを出して無視する
        // Close the segment being written and release its FileStream; later Append logs and no-ops
        public void Stop()
        {
            lock (_lock)
            {
                // 終了時のflush/disposeも外部境界。ここで投げると呼び出し元の終了処理が途中で止まる
                // Flushing and disposing at shutdown is an external boundary too; throwing here would cut the caller's teardown short
                try
                {
                    _writer?.Flush();
                    _writer?.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogError($"パケットログの終了処理に失敗しました message:{e.Message}");
                }
                _writer = null;
                IsActive = false;
            }
        }

        public void Rotate(ulong fromTick)
        {
            TryRotate(fromTick);
        }

        // 区間の切り替えは外部境界（FileStream生成）。失敗したら記録を止めるだけにして、呼び出し元のtick処理は続けさせる
        // Switching segments opens a FileStream at an external boundary; on failure only capture stops, and the caller's tick work continues
        private bool TryRotate(ulong fromTick)
        {
            lock (_lock)
            {
                try
                {
                    _writer?.Flush();
                    _writer?.Dispose();
                    _writer = null;
                    var path = Path.Combine(_directory, WorldDataDirectory.ReceivedPacketLogFileName(fromTick));
                    _writer = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read));
                    _currentSegmentFromTick = fromTick;
                    return true;
                }
                catch (Exception e)
                {
                    Degrade($"パケットログの区間切り替えに失敗しました fromTick:{fromTick}", e);
                    return false;
                }
            }
        }

        // 記録だけを止める縮退。無音で止まると「バグ直前のパケットが無い」ことに誰も気づけないので理由を必ず出す
        // Degrade capture alone; a silent stop would leave nobody aware that the packets before the bug are missing
        private void Degrade(string reason, Exception exception)
        {
            IsActive = false;
            _writer = null;
            Debug.LogError($"{reason} 以後パケットログの記録を停止します message:{exception.Message}");
        }

        // 最古スナップショット以前で始まる区間を消す。書き込み中の区間は残す
        // Delete segments starting at or before the oldest snapshot; keep the segment being written
        public void DeleteSegmentsBefore(ulong oldestSnapshotTick)
        {
            foreach (var path in SegmentFilePaths())
            {
                if (!WorldDataDirectory.TryParsePacketLogFromTick(Path.GetFileName(path), out var fromTick)) continue;
                if (fromTick > oldestSnapshotTick || fromTick == _currentSegmentFromTick) continue;

                // 常時記録の削除は後から追跡できる必要があるので、消した区間と理由を必ず残す
                // Deleting always-on capture must stay auditable, so record which segment went and why
                Debug.Log($"パケットログ区間を削除しました path:{path} 理由:最古スナップショット{oldestSnapshotTick}より前の区間");
                DeleteSegmentFile(path);
            }
        }

        // 並びは開始tickの昇順。ファイル名規則と順序の定義は WorldDataDirectory だけが持つ
        // Ordered by starting tick; the naming rule and the ordering live only in WorldDataDirectory
        public IReadOnlyList<string> SegmentFilePaths()
        {
            return WorldDataDirectory.EnumeratePacketLogFiles(_directory);
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
    }
}
