using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Core.Update;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Writer;
using UniRx;
using UnityEngine;

namespace Game.SaveLoad.Snapshot
{
    // 周期および即時要求でワールドの保存像を取り込み、別スレッドで書き出し、保持時間を過ぎた古い世代を消す
    // Captures world images periodically or on request, writes them off-thread, and prunes generations past the retention window
    public sealed class WorldSnapshotRing : ISnapshotCaptureRequest, ISnapshotWrittenNotifier
    {
        private readonly AssembleSaveJsonText _assembler;
        private readonly SaveWriteWorker _worker;
        private readonly WorldDataDirectory _directory;
        private readonly ReceivedPacketLog _packetLog;
        private readonly SnapshotGenerationRetention _retention;
        private readonly Subject<SnapshotWritten> _onSnapshotWritten = new();
        private readonly Dictionary<ulong, List<long>> _requestIdsByTick = new();
        private readonly List<long> _pendingImmediateRequestIds = new();
        private readonly object _requestLock = new();

        // 排出と取り込みはtickスレッドと待ち合わせスレッドの両方から入るので、この錠で直列化する
        // Draining and capturing are entered from both the tick thread and a waiting thread, so this lock serializes them
        private readonly object _tickStateLock = new();
        private uint _periodTicks;
        private ulong _nextPeriodicTick;
        private long _immediateRequestCounter;

        public WorldSnapshotRing(AssembleSaveJsonText assembler, SaveWriteWorker worker, WorldDataDirectory directory, ReceivedPacketLog packetLog)
        {
            _assembler = assembler;
            _worker = worker;
            _directory = directory;
            _packetLog = packetLog;
            _retention = new SnapshotGenerationRetention(directory, packetLog);
        }

        public bool IsActive { get; private set; }
        public IObservable<SnapshotWritten> OnSnapshotWritten => _onSnapshotWritten;

        public void Start(uint periodTicks, uint retentionTicks, int maxGenerations)
        {
            if (_directory.SnapshotDirectory == null)
            {
                Debug.LogWarning("セーブファイルの場所が無いワールド構成のため常時記録を開始しません");
                return;
            }

            _periodTicks = periodTicks;
            _retention.Configure(retentionTicks, maxGenerations);
            _nextPeriodicTick = GameUpdater.CurrentTick + periodTicks;
            Directory.CreateDirectory(_directory.SnapshotDirectory);

            // 前セッションのtickは今回の剪定対象にならず残り続け、区間ファイルは再生へ異セッションのパケットを混ぜる
            // Previous-session ticks never enter this session's pruning, and their segments would mix foreign packets into replay
            SnapshotDirectoryCleaner.DeletePreviousSessionFiles(_directory.SnapshotDirectory);
            _packetLog.Start(_directory.SnapshotDirectory, GameUpdater.CurrentTick + 1);
            IsActive = true;
            // 基準スナップショットを開始tickで取る。無いと最初の周期までの区間は再生の出発点を持たない
            // Take the baseline snapshot at the start tick; without it the span up to the first period has no point to replay from
            lock (_tickStateLock)
            {
                CaptureInto(GameUpdater.CurrentTick, new List<long>());
            }
            Debug.Log($"常時記録を開始しました period:{periodTicks}tick 保持:{retentionTicks}tick 上限:{maxGenerations}世代 dir:{_directory.SnapshotDirectory}");
        }

        // 次のtick末尾で取る。受理された要求IDは完了通知の RequestId と突き合わせる
        // Taken at the next tick end; an accepted request id is matched against SnapshotWritten.RequestId
        public SnapshotCaptureRequestResult RequestImmediateSnapshot()
        {
            if (!IsActive)
            {
                const string reason = "常時記録が無効のため即時スナップショット要求を受け付けられません";
                Debug.LogWarning(reason);
                return SnapshotCaptureRequestResult.FromRejected(reason);
            }
            var id = Interlocked.Increment(ref _immediateRequestCounter);
            lock (_requestLock)
            {
                _pendingImmediateRequestIds.Add(id);
            }
            return SnapshotCaptureRequestResult.FromAccepted(id);
        }

        // FinalTickEndUpdates から毎tick呼ばれる。完了通知の排出→取り込み判定の順
        // Called every tick from FinalTickEndUpdates: drain completions, then decide whether to capture
        public void Update()
        {
            lock (_tickStateLock)
            {
                DrainCompletions();
                if (!IsActive) return;

                var tick = GameUpdater.CurrentTick;
                var requestIds = TakePendingRequestIds();
                var periodicDue = tick >= _nextPeriodicTick;
                if (requestIds.Count == 0 && !periodicDue) return;
                if (periodicDue) _nextPeriodicTick += _periodTicks;

                // Rotate が内部で flush してから区間を切り替えるので、ここで重ねてflushしない
                // Rotate flushes before switching segments, so no extra flush belongs here
                _packetLog.Rotate(tick + 1);
                CaptureInto(tick, requestIds);
            }
        }

        // 終了時に常時記録の持つOSリソース（書き出しスレッド・区間ファイルのハンドル）を1本の道で手放す
        // Release the OS resources always-on capture holds (writer thread, segment file handle) through one shutdown path
        public void Stop()
        {
            lock (_tickStateLock)
            {
                IsActive = false;
            }
            WaitForPendingWrites();
            _packetLog.Stop();
            _worker.Stop();
            Debug.Log("常時記録を停止しました");
        }

        // テストと終了時用。tickループが止まっている間だけ呼べる（回っている最中は待ち終えた直後に次の書き出しが積まれ、待ちの意味が無い）
        // For tests and shutdown; callable only while the tick loop is stopped, otherwise a new write is enqueued right after the wait returns
        public void WaitForPendingWrites()
        {
            _worker.WaitForIdle();
            DrainCompletions();
        }

        // 取り込みと要求IDの記録。区間は呼び出し側が取り込みtickの直後から始めてある
        // Captures the world image and records the request ids; the caller has already begun the next segment right after this tick
        private void CaptureInto(ulong tick, List<long> requestIds)
        {
            _requestIdsByTick[tick] = requestIds;
            var data = _assembler.Capture();
            _worker.Enqueue(new SaveWriteJob(0, SaveWriteKind.Snapshot, data, _directory.SnapshotFilePath(tick), false));
        }

        private List<long> TakePendingRequestIds()
        {
            lock (_requestLock)
            {
                var taken = new List<long>(_pendingImmediateRequestIds);
                _pendingImmediateRequestIds.Clear();
                return taken;
            }
        }

        // 排出は錠の中だけで行う。tickスレッドとの二重ドレイン・世代リストの破損・OnNext の同時発火を防ぐため
        // Draining happens only under the lock: it prevents a double drain against the tick thread, a corrupted generation list, and concurrent OnNext
        private void DrainCompletions()
        {
            lock (_tickStateLock)
            {
                while (_worker.TryDequeueCompletion(SaveWriteKind.Snapshot, out var completion))
                {
                    var requestIds = _requestIdsByTick.TryGetValue(completion.Tick, out var ids) ? ids : new List<long>();
                    _requestIdsByTick.Remove(completion.Tick);
                    if (completion.Success) _retention.AddAndPrune(completion.Tick);
                    else Debug.LogError($"スナップショットの書き出しに失敗しました tick:{completion.Tick} 要求ID:{string.Join(",", requestIds)}");

                    Publish(completion, requestIds);
                }
            }
        }

        // 失敗も要求元へ流す。流さないと要求元は来ない完了を永久に待つ
        // Failures are emitted too; otherwise a requester waits forever for a completion that never comes
        private void Publish(SaveWriteCompletion completion, List<long> requestIds)
        {
            var snapshotFileNames = _retention.CopyFileNames();
            var packetLogFileNames = CopyPacketLogFileNames();
            if (requestIds.Count == 0)
            {
                _onSnapshotWritten.OnNext(SnapshotWritten.ForPeriodic(completion.Tick, completion.Success, _directory.SnapshotDirectory, snapshotFileNames, packetLogFileNames));
                return;
            }

            foreach (var requestId in requestIds)
            {
                _onSnapshotWritten.OnNext(SnapshotWritten.ForRequest(requestId, completion.Tick, completion.Success, _directory.SnapshotDirectory, snapshotFileNames, packetLogFileNames));
            }
        }

        private List<string> CopyPacketLogFileNames()
        {
            var paths = _packetLog.SegmentFilePaths();
            var names = new List<string>(paths.Count);
            foreach (var path in paths) names.Add(Path.GetFileName(path));
            return names;
        }
    }
}
