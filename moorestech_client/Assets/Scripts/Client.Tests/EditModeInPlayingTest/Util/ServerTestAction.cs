using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Server.Boot.Loop;
using UnityEngine;

namespace Client.Tests.EditModeInPlayingTest.Util
{
    internal abstract class ServerTestAction : IServerThreadAction
    {
        private readonly UniTaskCompletionSource _completion = new();
        private ServerThreadActionQueue _queue;
        internal int ServerThreadId { get; private set; }

        internal async UniTask ExecuteAsync()
        {
            // 実サーバーのtick末尾へ渡す
            // Dispatch to the running server's tick end
            _queue = ServerThreadActionQueueAccess.Current;
            if (_queue == null || !_queue.TryEnqueue(this))
            {
                Debug.LogError($"[ServerTestAction] サーバーが処理を受理できません: {GetType().Name}");
                throw new InvalidOperationException("The running server did not accept the test action");
            }
            await _completion.Task.Timeout(TimeSpan.FromSeconds(10));
            await UniTask.SwitchToMainThread();
        }

        public void Run()
        {
            // 更新と同じスレッドで操作を完了する
            // Complete the operation on the update thread
            ServerThreadId = Thread.CurrentThread.ManagedThreadId;
            ExecuteOnServer();
            Debug.Log($"[ServerTestAction] {GetType().Name} thread:{ServerThreadId} name:{Thread.CurrentThread.Name}");
            _queue.Release(this);
            _completion.TrySetResult();
        }

        public void OnServerStopped()
        {
            Debug.LogError($"[ServerTestAction] 処理完了前にサーバーが停止しました: {GetType().Name}");
            _completion.TrySetException(new InvalidOperationException("Server stopped before test action completed"));
        }

        protected abstract void ExecuteOnServer();
    }
}
