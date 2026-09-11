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
        private readonly BlockingCollection<SaveWriteJob> _jobs = new();
        private readonly ConcurrentQueue<SaveWriteCompletion> _playerSaveCompletions = new();
        private readonly ConcurrentQueue<SaveWriteCompletion> _snapshotCompletions = new();
        private int _inFlight;

        public SaveWriteWorker()
        {
            var thread = new Thread(Run) { Name = "[moorestech] セーブ書き出しスレッド", IsBackground = true };
            thread.Start();
        }

        // 投入済みで完了通知をまだ積んでいない書き出しが残っているか
        // Whether an enqueued write has yet to post its completion
        public bool HasInFlight => Volatile.Read(ref _inFlight) != 0;

        public void Enqueue(SaveWriteJob job)
        {
            Interlocked.Increment(ref _inFlight);
            _jobs.Add(job);
        }

        public bool TryDequeueCompletion(SaveWriteKind kind, out SaveWriteCompletion completion)
        {
            var queue = kind == SaveWriteKind.PlayerSave ? _playerSaveCompletions : _snapshotCompletions;
            return queue.TryDequeue(out completion);
        }

        // テストと終了時の待ち合わせ専用。tickスレッドからは呼ばない
        // For tests and shutdown only; never call from the tick thread
        public void WaitForIdle()
        {
            while (HasInFlight) Thread.Sleep(1);
        }

        private void Run()
        {
            foreach (var job in _jobs.GetConsumingEnumerable())
            {
                var success = Write(job);
                var queue = job.Kind == SaveWriteKind.PlayerSave ? _playerSaveCompletions : _snapshotCompletions;
                queue.Enqueue(new SaveWriteCompletion(job.Generation, job.Kind, job.Data.CurrentTick, job.TargetPath, success));

                // 完了を積んでから在庫を減らす。待ち合わせ側が空振りで先に抜けないようにする
                // Post the completion before clearing the in-flight count so a waiter never returns ahead of it
                Interlocked.Decrement(ref _inFlight);
            }
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
