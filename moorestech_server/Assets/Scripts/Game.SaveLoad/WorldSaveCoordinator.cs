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
        // 同一要求の再取り込み上限。恒久失敗（権限拒否等）を毎tick再取り込みするとtickスレッドが全世界Captureで焼き付く
        // Recapture cap for one request: retrying a permanent failure (denied permissions, etc.) every tick burns the tick thread on full-world captures
        private const int MaxWriteAttemptsPerRequest = 3;
        private int _failedAttempts;
        private long _requestedGeneration;
        private long _completedGeneration;
        private long _enqueuedGeneration;

        // 諦めた要求の番号。0は「直近の書き出しは諦めていない」
        // Generation of the request that was given up; 0 means the latest write was not abandoned
        private long _abandonedGeneration;

        public WorldSaveCoordinator(WorldDataDirectory worldDataDirectory, AssembleSaveJsonText assembleSaveJsonText, SaveWriteWorker saveWriteWorker)
        {
            _worldDataDirectory = worldDataDirectory;
            _assembleSaveJsonText = assembleSaveJsonText;
            _saveWriteWorker = saveWriteWorker;
        }

        // 要求済みだがまだ書き出しが完了していない保存が残っているか。終了時の待ち合わせに使う
        // Whether a requested save has not finished writing; used to wait for the flush at shutdown
        public bool HasPendingSave => Volatile.Read(ref _requestedGeneration) != Volatile.Read(ref _completedGeneration);

        // 諦めた保存があるか。諦めは完了と同じく待ちを明けるので、成功と区別するには終了経路がこれを見る必要がある
        // Whether a save was given up; giving up clears the wait just like a completion, so the shutdown path must read this to tell it from success
        public bool HasAbandonedSave => Volatile.Read(ref _abandonedGeneration) != 0;

        // 諦めた要求番号。0は諦めが無い状態
        // The generation that was given up; 0 means nothing is abandoned
        public long AbandonedGeneration => Volatile.Read(ref _abandonedGeneration);

        // 書き出しが完了した要求番号を流す。通常はtickスレッド、終了時の WaitForPendingWrites 経由では待ち合わせスレッドから発火する
        // Emits the generation whose write completed; normally on the tick thread, and on the waiting thread when it comes through WaitForPendingWrites at shutdown
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
        public bool WaitForPendingWrites()
        {
            var drained = _saveWriteWorker.WaitForIdle();
            DrainCompletions();
            return drained;
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
                        _failedAttempts++;
                        if (_failedAttempts < MaxWriteAttemptsPerRequest)
                        {
                            // 失敗した要求は未投入へ戻し、次のtick末尾で再取り込みする（失敗理由は書き出しスレッドが出力済み）
                            // Return a failed request to the un-enqueued state so the next tick end recaptures it; the writer already logged the cause
                            _enqueuedGeneration = Volatile.Read(ref _completedGeneration);
                            continue;
                        }

                        // 諦めた要求は未完了のまま残さない。残すと終了時の待ち合わせが永久に明けない
                        // A given-up request must not stay pending, or the shutdown wait never clears
                        UnityEngine.Debug.LogError($"セーブの書き出しに{_failedAttempts}回失敗したため要求{completion.Generation}を諦めます path:{completion.TargetPath}");
                        // 諦めを状態として残す。完了と同じ前進だけで済ませると終了経路が成功を名乗る
                        // Record the give-up as state; advancing like a completion alone would let the shutdown path claim success
                        Volatile.Write(ref _abandonedGeneration, completion.Generation);
                        Volatile.Write(ref _completedGeneration, completion.Generation);
                        _failedAttempts = 0;
                        continue;
                    }

                    _failedAttempts = 0;
                    // 書き出せた時点で過去の諦めは解消する。以後の終了は保存済みとして閉じてよい
                    // A successful write clears any earlier give-up, so later shutdowns may close as saved
                    Volatile.Write(ref _abandonedGeneration, 0);
                    Volatile.Write(ref _completedGeneration, completion.Generation);
                    UnityEngine.Debug.Log("ワールドを保存しました");
                    _onWorldSaveCompleted.OnNext(completion.Generation);
                }
            }
        }
    }
}
