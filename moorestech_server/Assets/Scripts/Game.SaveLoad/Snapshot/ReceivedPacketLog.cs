using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Core.Update;
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

        // 書き込み中の区間があるか。閉じ切っていない区間はバッファ境界で切れているため複製してはいけない
        // Whether a segment is still open; an unclosed segment ends at a buffer boundary and must never be copied
        private bool _currentSegmentOpen;
        private bool _inactiveLogged;
        private int _isActive;

        // 縮退した理由と止めたtick。tickスレッドが書き、取得完了を組む側が別スレッドから読むので可視性を明示する
        // The degradation reason and the tick it stopped at; the tick thread writes them and the completion builder reads them from another thread
        private string _degradeReason = string.Empty;
        private long _degradedAtTick;

        // tickスレッドが追記し、終了経路が別スレッドから止めるので、可視性を明示する
        // The tick thread appends while shutdown stops it from another thread, so visibility is made explicit
        public bool IsActive => Volatile.Read(ref _isActive) != 0;

        // 縮退した理由。空なら記録は欠けていない。取得結果に載せないと欠損が「取れた」と申告される
        // Why capture degraded; empty means nothing is missing. Without this on the capture result a gap is reported as a successful capture
        public string DegradeReason => Volatile.Read(ref _degradeReason);

        // 記録を止めたtick。縮退していなければ0
        // The tick capture stopped at; 0 while healthy
        public ulong DegradedAtTick => (ulong)Volatile.Read(ref _degradedAtTick);

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
                Degrade($"パケットログの置き場を作れませんでした dir:{directory}", fromTick, e);
                return;
            }

            if (!TryRotate(fromTick)) return;
            Volatile.Write(ref _isActive, 1);
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
                // 錠の外で見た IsActive は Stop() と競合しうる。書き出し先が既に閉じているならここで降りる
                // The IsActive read outside the lock can race Stop(), so bail out here when the writer is already closed
                if (_writer == null) return;

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
                    Degrade($"パケットログへの追記に失敗しました tick:{tick}", tick, e);
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
                    Degrade("パケットログのフラッシュに失敗しました", GameUpdater.CurrentTick, e);
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

                    // 閉じ切れた区間だけを完成扱いにする。失敗した区間はこのまま書き込み中として扱い複製から外す
                    // Only a segment that closed cleanly counts as complete; a failed one stays open and is kept out of copies
                    _currentSegmentOpen = false;
                }
                catch (Exception e)
                {
                    Debug.LogError($"パケットログの終了処理に失敗しました message:{e.Message} 区間{_currentSegmentFromTick}は書き込み中のまま扱います");
                }
                _writer = null;
                Volatile.Write(ref _isActive, 0);
            }
        }

        public void Rotate(ulong fromTick)
        {
            // 停止済み・縮退済みのまま区間を作り直すと、記録されないファイルだけがディスクに増える
            // Recreating a segment after a stop or a degradation would leave files on disk that nothing ever writes to
            if (!IsActive) return;
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
                    _currentSegmentOpen = true;
                    return true;
                }
                catch (Exception e)
                {
                    Degrade($"パケットログの区間切り替えに失敗しました fromTick:{fromTick}", fromTick, e);
                    return false;
                }
            }
        }

        // 記録だけを止める縮退。理由と止めたtickを状態として持ち、ログと取得結果の両方へ出す
        // Degrade capture alone, holding the reason and the stop tick as state so both the log and the capture result carry them
        private void Degrade(string reason, ulong tick, Exception exception)
        {
            Volatile.Write(ref _isActive, 0);
            _writer = null;

            // 最初の理由を残す。後続の失敗で上書きすると、記録が止まった本当のきっかけが消える
            // Keep the first reason; overwriting it with later failures would erase what actually stopped the capture
            if (Volatile.Read(ref _degradeReason).Length == 0)
            {
                Volatile.Write(ref _degradedAtTick, (long)tick);
                Volatile.Write(ref _degradeReason, reason);
            }
            Debug.LogError($"{reason} 以後パケットログの記録を停止します tick:{tick} message:{exception.Message}");
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

        // 閉じ切った区間だけ。書き込み中の区間はバッファ境界で末尾が切れており、複製すると読み側がレコード破損で全滅する
        // Closed segments only; an open segment ends mid-record at a buffer boundary and a copy of it kills the reader outright
        public IReadOnlyList<string> CompletedSegmentFilePaths()
        {
            lock (_lock)
            {
                if (!_currentSegmentOpen) return SegmentFilePaths();

                var openSegmentFileName = WorldDataDirectory.ReceivedPacketLogFileName(_currentSegmentFromTick);
                return SegmentFilePaths().Where(path => Path.GetFileName(path) != openSegmentFileName).ToList();
            }
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
