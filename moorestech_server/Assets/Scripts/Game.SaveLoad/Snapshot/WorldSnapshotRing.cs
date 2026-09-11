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
    // 周期および即時要求でワールドの保存像を取り込み、別スレッドで書き出し、世代数を超えた古い世代を消す
    // Captures world images periodically or on request, writes them off-thread, and prunes generations beyond the limit
    public sealed class WorldSnapshotRing : ISnapshotCaptureRequest, ISnapshotWrittenNotifier
    {
        private readonly AssembleSaveJsonText _assembler;
        private readonly SaveWriteWorker _worker;
        private readonly WorldDataDirectory _directory;
        private readonly ReceivedPacketLog _packetLog;
        private readonly Subject<SnapshotWritten> _onSnapshotWritten = new();
        private readonly List<ulong> _writtenTicks = new();
        private readonly Dictionary<ulong, List<long>> _requestIdsByTick = new();
        private readonly List<long> _pendingImmediateRequestIds = new();
        private readonly object _requestLock = new();
        private uint _periodTicks;
        private int _generations;
        private ulong _nextPeriodicTick;
        private long _immediateRequestCounter;

        public WorldSnapshotRing(AssembleSaveJsonText assembler, SaveWriteWorker worker, WorldDataDirectory directory, ReceivedPacketLog packetLog)
        {
            _assembler = assembler;
            _worker = worker;
            _directory = directory;
            _packetLog = packetLog;
        }

        public bool IsActive { get; private set; }
        public IObservable<SnapshotWritten> OnSnapshotWritten => _onSnapshotWritten;
        public IReadOnlyList<ulong> WrittenTicks => _writtenTicks;

        public void Start(uint periodTicks, int generations)
        {
            if (_directory.SnapshotDirectory == null)
            {
                Debug.LogWarning("セーブファイルの場所が無いワールド構成のため常時記録を開始しません");
                return;
            }

            _periodTicks = periodTicks;
            _generations = generations;
            _nextPeriodicTick = GameUpdater.CurrentTick + periodTicks;
            Directory.CreateDirectory(_directory.SnapshotDirectory);
            _packetLog.Start(_directory.SnapshotDirectory, GameUpdater.CurrentTick + 1);
            IsActive = true;
            Debug.Log($"常時記録を開始しました period:{periodTicks}tick generations:{generations} dir:{_directory.SnapshotDirectory}");
        }

        // 次のtick末尾で取る。戻り値の要求IDは完了通知の RequestId と突き合わせる
        // Taken at the next tick end; match the returned request id against SnapshotWritten.RequestId
        public long RequestImmediateSnapshot()
        {
            if (!IsActive)
            {
                Debug.LogWarning("常時記録が無効のため即時スナップショット要求を無視しました");
                return 0;
            }
            var id = Interlocked.Increment(ref _immediateRequestCounter);
            lock (_requestLock)
            {
                _pendingImmediateRequestIds.Add(id);
            }
            return id;
        }

        // FinalTickEndUpdates から毎tick呼ばれる。完了通知の排出→取り込み判定の順
        // Called every tick from FinalTickEndUpdates: drain completions, then decide whether to capture
        public void Update()
        {
            DrainCompletions();
            if (!IsActive) return;

            var tick = GameUpdater.CurrentTick;
            var requestIds = TakePendingRequestIds();
            var periodicDue = tick >= _nextPeriodicTick;
            if (requestIds.Count == 0 && !periodicDue) return;
            if (periodicDue) _nextPeriodicTick += _periodTicks;

            _requestIdsByTick[tick] = requestIds;
            var data = _assembler.Capture();
            _packetLog.Flush();
            _packetLog.Rotate(tick + 1);
            _worker.Enqueue(new SaveWriteJob(0, SaveWriteKind.Snapshot, data, _directory.SnapshotFilePath(tick), false));
        }

        // テストと終了時用。書き出しスレッドが空くまで待ち、完了通知をこのスレッドで排出する
        // For tests and shutdown: wait until the writer is idle, then drain completions on this thread
        public void WaitForPendingWrites()
        {
            _worker.WaitForIdle();
            DrainCompletions();
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

        private void DrainCompletions()
        {
            while (_worker.TryDequeueCompletion(SaveWriteKind.Snapshot, out var completion))
            {
                var requestIds = _requestIdsByTick.TryGetValue(completion.Tick, out var ids) ? ids : new List<long>();
                _requestIdsByTick.Remove(completion.Tick);
                if (!completion.Success)
                {
                    Debug.LogError($"スナップショットの書き出しに失敗しました tick:{completion.Tick} 要求ID:{string.Join(",", requestIds)}");
                    continue;
                }

                _writtenTicks.Add(completion.Tick);
                Prune();

                // 周期スナップショットには要求元が無いので、要求ID0の通知を1件だけ流す
                // A periodic snapshot has no requester, so emit a single notification with request id 0
                if (requestIds.Count == 0) requestIds.Add(0);
                foreach (var requestId in requestIds)
                {
                    _onSnapshotWritten.OnNext(new SnapshotWritten(requestId, completion.Tick, completion.TargetPath));
                }
            }
        }

        private void Prune()
        {
            while (_writtenTicks.Count > _generations)
            {
                var oldest = _writtenTicks[0];
                _writtenTicks.RemoveAt(0);

                // 常時記録の削除は後から追跡できる必要があるので、消した世代と理由を必ず残す
                // Deleting always-on capture must stay auditable, so record which generation went and why
                Debug.Log($"スナップショットを削除しました tick:{oldest} 理由:保持世代数{_generations}を超過");
                DeleteSnapshotFile(_directory.SnapshotFilePath(oldest));
                _packetLog.DeleteSegmentsBefore(_writtenTicks[0]);
            }
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
