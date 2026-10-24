using System;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Server.Boot.Loop;
using UnityEngine;

namespace Client.Tests.EditModeInPlayingTest.Util
{
    // テストの準備をサーバーのtick末尾で走らせる。メインスレッドから触ると、ゲーム更新スレッドの網再構築とdirtyの消費を取り合う
    // Runs test setup at the server tick end; touching the world from the main thread races the update thread's network rebuild for the dirty flag
    public abstract class ServerTickEndTestStep : IServerThreadAction
    {
        private const int MaxWaitFrames = 600;
        private readonly UniTaskCompletionSource _completion = new();

        protected abstract void Execute();

        public async UniTask RunOnServerAsync()
        {
            var queue = ServerThreadActionQueueAccess.Current;
            Assert.IsNotNull(queue, "The embedded server must be running.");
            Assert.IsTrue(queue.TryEnqueue(this), "The server tick end must accept test work.");

            // 例外はゲーム更新ループがログへ出して握るので、完了しないまま待ち続けないよう上限で打ち切る
            // The update loop logs and swallows exceptions, so cap the wait instead of hanging on a step that never completes
            for (var frame = 0; frame < MaxWaitFrames && _completion.Task.Status == UniTaskStatus.Pending; frame++) await UniTask.Yield();
            queue.Release(this);
            Assert.AreNotEqual(UniTaskStatus.Pending, _completion.Task.Status, $"{GetType().Name} did not finish at the server tick end; see the server log for its exception.");
            await _completion.Task;
        }

        public void Run()
        {
            Execute();
            _completion.TrySetResult();
        }

        public void OnServerStopped()
        {
            Debug.LogWarning($"[ServerTickEndTestStep] サーバー停止により {GetType().Name} を打ち切りました");
            _completion.TrySetException(new InvalidOperationException($"The server stopped before {GetType().Name} finished."));
        }
    }
}
