using System;
using System.Collections.Generic;
using System.IO;
using Core.Update;
using Game.SaveLoad.Snapshot.Segments;
using UnityEngine;

namespace Game.SaveLoad.Snapshot
{
    // 受信パケットを処理tick付きで区間ファイルへ追記する。区間はスナップショットごとに切り替わる
    // Appends received packets with their processing tick to segment files; a new segment starts at each snapshot
    public sealed class ReceivedPacketLog
    {
        private const int UnboundSenderPlayerId = 0;
        internal const int SegmentMagic = 0x504B544C;
        // 版2はパケットと切断のtick末尾FIFO順を表す。版1の切断tickを同じ意味で再生しない
        // Version 2 records tick-end FIFO order; version 1 disconnect ticks must not be replayed as that order
        internal const int SegmentVersion = 2;
        private readonly object _lock = new();
        private readonly ReceivedPacketLogCaptureState _captureState = new();
        private ReceivedPacketLogSegments _segments = new(null);
        private BinaryWriter _writer;
        private bool _inactiveLogged;
        public bool IsActive => _captureState.IsActive;
        public string DegradeReason => _captureState.DegradeReason;
        public ulong DegradedAtTick => _captureState.DegradedAtTick;

        public void Start(string directory, ulong fromTick)
        {
            _segments = new ReceivedPacketLogSegments(directory);

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
            _captureState.Start();
        }

        public void Append(ulong tick, int? senderPlayerId, byte[] payload)
        {
            AppendRecord(tick, ReceivedPacketRecordKind.Packet, senderPlayerId, payload);
        }

        public void AppendDisconnect(ulong tick, int? playerId)
        {
            AppendRecord(tick, ReceivedPacketRecordKind.Disconnect, playerId, Array.Empty<byte>());
        }

        internal static int? DecodeSenderPlayerId(int storedPlayerId)
        {
            return storedPlayerId == UnboundSenderPlayerId ? null : storedPlayerId;
        }

        private void AppendRecord(ulong tick, ReceivedPacketRecordKind kind, int? senderPlayerId, byte[] payload)
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
                    _writer.Write((byte)kind);
                    _writer.Write(senderPlayerId ?? UnboundSenderPlayerId);
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
                    _segments.MarkClosed();
                }
                catch (Exception e)
                {
                    Debug.LogError($"パケットログの終了処理に失敗しました message:{e.Message} 区間{_segments.CurrentFromTick}は書き込み中のまま扱います");
                }
                _writer = null;
                _captureState.Stop();
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
                    var path = _segments.PathFor(fromTick);
                    _writer = new BinaryWriter(new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read));
                    _writer.Write(SegmentMagic);
                    _writer.Write(SegmentVersion);
                    _segments.MarkOpened(fromTick);
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
            _writer = null;
            _captureState.Degrade(reason, tick, exception);
        }

        public void DeleteSegmentsBefore(ulong oldestSnapshotTick)
        {
            _segments.DeleteBefore(oldestSnapshotTick);
        }

        public IReadOnlyList<string> SegmentFilePaths()
        {
            return _segments.FilePaths();
        }

        public IReadOnlyList<string> CompletedSegmentFilePaths()
        {
            lock (_lock)
            {
                return _segments.CompletedFilePaths();
            }
        }
    }
}
