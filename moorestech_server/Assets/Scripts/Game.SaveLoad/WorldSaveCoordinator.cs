using System;
using System.Threading;
using Game.Paths;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Writer;
using UniRx;

namespace Game.SaveLoad
{
    public sealed class WorldSaveCoordinator : IWorldSaveRequest, IWorldSaveCompletionNotifier
    {
        private readonly AssembleSaveJsonText _assembleSaveJsonText;
        private readonly WorldDataDirectory _worldDataDirectory;
        private readonly SaveWriteWorker _saveWriteWorker;
        private readonly Subject<long> _onWorldSaveCompleted = new();

        // 排出と取り込みはtickスレッドと待ち合わせスレッドの両方から入るので、この錠で直列化する
        // Draining and capturing are entered from both the tick thread and a waiting thread, so this lock serializes them
        private readonly object _tickStateLock = new();
        private long _requestedGeneration;
        private long _completedGeneration;
        private long _enqueuedGeneration;

        public WorldSaveCoordinator(WorldDataDirectory worldDataDirectory, AssembleSaveJsonText assembleSaveJsonText, SaveWriteWorker saveWriteWorker)
        {
            _worldDataDirectory = worldDataDirectory;
            _assembleSaveJsonText = assembleSaveJsonText;
            _saveWriteWorker = saveWriteWorker;
        }

        // 要求済みだがまだ書き出しが完了していない保存が残っているか。終了時の待ち合わせに使う
        // Whether a requested save has not finished writing; used to wait for the flush at shutdown
        public bool HasPendingSave => Volatile.Read(ref _requestedGeneration) != Volatile.Read(ref _completedGeneration);

        // 書き出しが完了した要求番号を流す。tickスレッド上で発火する
        // Emits the generation whose write completed; fired on the tick thread
        public IObservable<long> OnWorldSaveCompleted => _onWorldSaveCompleted;

        public long RequestSave()
        {
            return Interlocked.Increment(ref _requestedGeneration);
        }

        // tick末尾で呼ぶ。完了通知を排出し、未投入の要求があれば取り込んで書き出しを投入する
        // Called at tick end: drain completions, then capture and enqueue a write for any un-enqueued request
        public void SaveIfRequested()
        {
            lock (_tickStateLock)
            {
                DrainCompletions();

                // このtickで処理する要求番号を固定し、取り込み中に届く要求を次回へ残す
                // Freeze the generation handled now so requests arriving during the capture remain pending
                var targetGeneration = Volatile.Read(ref _requestedGeneration);
                if (targetGeneration == Volatile.Read(ref _completedGeneration)) return;
                if (targetGeneration == _enqueuedGeneration) return;

                _enqueuedGeneration = targetGeneration;
                var data = _assembleSaveJsonText.Capture();
                _saveWriteWorker.Enqueue(new SaveWriteJob(targetGeneration, SaveWriteKind.PlayerSave, data, _worldDataDirectory.SaveJsonFilePath, true));
            }
        }

        // テストと終了時用。tickループが止まっている間だけ呼べる（回っている最中は待ち終えた直後に次の書き出しが積まれ、待ちの意味が無い）
        // For tests and shutdown; callable only while the tick loop is stopped, otherwise a new write is enqueued right after the wait returns
        public void WaitForPendingWrites()
        {
            _saveWriteWorker.WaitForIdle();
            DrainCompletions();
        }

        // 排出は錠の中だけで行う。tickスレッドとの二重ドレイン・_enqueuedGeneration の破損・OnNext の同時発火を防ぐため
        // Draining happens only under the lock: it prevents a double drain against the tick thread, corrupted _enqueuedGeneration, and concurrent OnNext
        private void DrainCompletions()
        {
            lock (_tickStateLock)
            {
                while (_saveWriteWorker.TryDequeueCompletion(SaveWriteKind.PlayerSave, out var completion))
                {
                    if (!completion.Success)
                    {
                        // 失敗した要求は未投入へ戻し、次のtick末尾で再取り込みする（失敗理由は書き出しスレッドが出力済み）
                        // Return a failed request to the un-enqueued state so the next tick end recaptures it; the writer already logged the cause
                        _enqueuedGeneration = Volatile.Read(ref _completedGeneration);
                        continue;
                    }

                    Volatile.Write(ref _completedGeneration, completion.Generation);
                    UnityEngine.Debug.Log("ワールドを保存しました");
                    _onWorldSaveCompleted.OnNext(completion.Generation);
                }
            }
        }
    }
}
