using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using Game.SaveLoad.Json;
using UnityEngine;

namespace Game.SaveLoad.Writer
{
    // 保存像のJSON化と書き込みをtickスレッドの外で直列に行う。完了は種別ごとのキューで返し、消費側がtickスレッドで排出する
    // Serializes and writes save images off the tick thread in order; completions return via per-kind queues drained on the tick thread
    public sealed class SaveWriteWorker
    {
        // 待ちの上限。1ミリ秒ポーリングなのでおよそ60秒。超えたら書き出しが進んでいないとみなす
        // Wait cap: roughly 60 seconds at 1 ms polling; beyond it the writer is treated as stalled
        private const int WaitForIdleMaxPolls = 60000;

        private readonly BlockingCollection<SaveWriteJob> _jobs = new();
        private readonly ConcurrentQueue<SaveWriteCompletion> _playerSaveCompletions = new();
        private readonly ConcurrentQueue<SaveWriteCompletion> _snapshotCompletions = new();
        private int _inFlight;
        private int _writerThreadStarted;

        // 投入済みで完了通知をまだ積んでいない書き出しが残っているか
        // Whether an enqueued write has yet to post its completion
        public bool HasInFlight => Volatile.Read(ref _inFlight) != 0;

        public void Enqueue(SaveWriteJob job)
        {
            EnsureWriterThreadStarted();
            Interlocked.Increment(ref _inFlight);
            _jobs.Add(job);

            #region Internal

            // 書き出しスレッドは初回投入まで起こさない。セーブしないコンテナ（テストが大量に作る）でスレッドが増えないようにする
            // The writer thread starts on the first job so containers that never save (tests create many) add no threads
            void EnsureWriterThreadStarted()
            {
                if (Interlocked.CompareExchange(ref _writerThreadStarted, 1, 0) != 0) return;

                var thread = new Thread(Run) { Name = "[moorestech] セーブ書き出しスレッド", IsBackground = true };
                thread.Start();
            }

            #endregion
        }

        public bool TryDequeueCompletion(SaveWriteKind kind, out SaveWriteCompletion completion)
        {
            var queue = FindCompletionQueue(kind);
            if (queue != null) return queue.TryDequeue(out completion);

            completion = null;
            return false;
        }

        // テストと終了時の待ち合わせ専用。tickスレッドからは呼ばない
        // For tests and shutdown only; never call from the tick thread
        public void WaitForIdle()
        {
            for (var polls = 0; polls < WaitForIdleMaxPolls; polls++)
            {
                if (!HasInFlight) return;
                Thread.Sleep(1);
            }

            // 待ち切れないのは書き出しが進んでいないとき。無限に待たず理由を出して抜ける
            // Failing to drain means the writer is not progressing, so log the cause instead of blocking forever
            Debug.LogError($"セーブ書き出しの完了を待ち切れませんでした 未完了:{Volatile.Read(ref _inFlight)}件");
        }

        // 投入口を閉じて書き出しスレッドを終わらせる。閉じないとサーバーインスタンスごとにスレッドが積み上がる
        // Close the intake so the writer thread ends; without this a thread accumulates per server instance
        public void Stop()
        {
            if (_jobs.IsAddingCompleted) return;
            _jobs.CompleteAdding();
        }

        private void Run()
        {
            foreach (var job in _jobs.GetConsumingEnumerable())
            {
                var success = false;

                // このループが唯一の消費者。スレッド境界を隔離し、どんな失敗でもスレッドを殺さず完了を積む
                // This loop is the only consumer; isolate the thread boundary so no failure kills it and stops every later write silently
                try
                {
                    success = Write(job);
                }
                catch (Exception e)
                {
                    Debug.LogError($"セーブの書き出しが想定外の例外で失敗しました path:{job.TargetPath} kind:{job.Kind} generation:{job.Generation} {e}");
                }
                finally
                {
                    PostCompletion(job, success);
                }
            }

            #region Internal

            void PostCompletion(SaveWriteJob completedJob, bool completedSuccess)
            {
                // 取り込み時に必ず入る。デシリアライズ由来の欠損は WorldLoaderFromJson が入口で弾く
                // Always present on a captured image; a missing value from deserialization is rejected at the load entrance
                FindCompletionQueue(completedJob.Kind)?.Enqueue(
                    new SaveWriteCompletion(completedJob.Generation, completedJob.Data.CurrentTick.Value, completedJob.TargetPath, completedSuccess));

                // 完了を積んでから在庫を減らす。待ち合わせ側が空振りで先に抜けないようにする
                // Post the completion before clearing the in-flight count so a waiter never returns ahead of it
                Interlocked.Decrement(ref _inFlight);
            }

            #endregion
        }

        // 種別ごとに専用キューを返す。既定へ寄せると新しい種別の完了が他種別の要求元へ流れ、偽の完了が発火する
        // Returns the queue dedicated to a kind; falling back to a default would hand a new kind's completion to another requester as a false completion
        private ConcurrentQueue<SaveWriteCompletion> FindCompletionQueue(SaveWriteKind kind)
        {
            switch (kind)
            {
                case SaveWriteKind.PlayerSave: return _playerSaveCompletions;
                case SaveWriteKind.Snapshot: return _snapshotCompletions;
            }

            Debug.LogError($"完了キューが定義されていない書き出し種別です kind:{kind}。この種別の完了は誰にも届きません");
            return null;
        }

        private static bool Write(SaveWriteJob job)
        {
            var json = AssembleSaveJsonText.Serialize(job.Data);
            var tmpPath = job.TargetPath + ".tmp";

            // ディスク書き込みは外部境界。失敗は完了通知に載せ、要求側が未消化のまま次回へ持ち越す
            // Disk I/O is an external boundary; a failure rides the completion so the requester keeps it pending for retry
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(job.TargetPath));

                // 書き込み途中のクラッシュでセーブが破損しないよう一時ファイル経由で置換する
                // Swap through a temporary file so a mid-write crash cannot corrupt the save
                File.WriteAllText(tmpPath, json);
                if (job.KeepBackup && File.Exists(job.TargetPath))
                {
                    File.Replace(tmpPath, job.TargetPath, job.TargetPath + ".bak");
                }
                else
                {
                    // .NET Standard 2.1 には上書き付き Move が無いので削除してから移動する
                    // .NET Standard 2.1 lacks an overwriting Move, so delete then move
                    if (File.Exists(job.TargetPath)) File.Delete(job.TargetPath);
                    File.Move(tmpPath, job.TargetPath);
                }
                return true;
            }
            catch (IOException e)
            {
                Debug.LogError($"セーブの書き出しに失敗しました path:{job.TargetPath} kind:{job.Kind} generation:{job.Generation} message:{e.Message}");
                return false;
            }
            catch (UnauthorizedAccessException e)
            {
                Debug.LogError($"セーブの書き出しが権限で拒否されました path:{job.TargetPath} kind:{job.Kind} generation:{job.Generation} message:{e.Message}");
                return false;
            }
        }
    }
}
