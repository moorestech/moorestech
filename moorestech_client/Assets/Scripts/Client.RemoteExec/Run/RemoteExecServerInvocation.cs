using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Server.Boot.Loop;
using UnityEngine;

namespace Client.RemoteExec.Run
{
    internal sealed class RemoteExecServerStoppedBeforeStartException : InvalidOperationException
    {
        internal RemoteExecServerStoppedBeforeStartException()
            : base("内蔵サーバーが終了したため、サーバー側実行を取り消しました")
        {
        }
    }

    // 開始後にサーバーが止まった実行。結果は不明で、成功も失敗も名乗らない
    // A run whose server stopped after start; the outcome is unknown and claims neither success nor failure
    internal sealed class RemoteExecServerAbandonedException : InvalidOperationException
    {
        internal RemoteExecServerAbandonedException()
            : base("内蔵サーバーが終了したため、開始済みのサーバー側実行の結果は不明です")
        {
        }
    }

    internal sealed class RemoteExecServerInvocation : IServerThreadAction, IDisposable
    {
        private readonly MethodInfo _entry;
        private readonly CancellationToken _cancellationToken;
        private readonly CancellationTokenRegistration _registration;
        private readonly ServerThreadActionQueue _queue;
        private readonly UniTaskCompletionSource<object> _completion = new();
        private int _admissionState;

        internal bool HasStarted => Volatile.Read(ref _admissionState) == 1;

        internal RemoteExecServerInvocation(MethodInfo entry, CancellationToken cancellationToken, ServerThreadActionQueue queue)
        {
            _entry = entry;
            _cancellationToken = cancellationToken;
            _queue = queue;
            _registration = cancellationToken.Register(CancelBeforeStart);
        }

        internal UniTask<object> Completion => _completion.Task;

        public void Run()
        {
            // 開始と取消を排他にし、実行中の取消では直列化を解かない
            // Arbitrate start against cancellation; cancellation after start must not release serialization
            if (Interlocked.CompareExchange(ref _admissionState, 1, 0) != 0) return;

            // 外部から送られ動的に実行するコードの同期例外を待機者へ渡す
            // Forward synchronous exceptions at the dynamically compiled external-code boundary
            try
            {
                var invocation = (UniTask<object>)_entry.Invoke(null, new object[] { _cancellationToken });
                CompleteAsync(invocation).Forget();
            }
            catch (Exception error)
            {
                _completion.TrySetException(error);
            }
        }

        // 未開始なら拒否、開始済みなら結果不明として閉じる。どちらも待機者を放置しない
        // Refuse when unstarted and close as unknown when already started; neither leaves the waiter hanging
        public void OnServerStopped()
        {
            if (Interlocked.CompareExchange(ref _admissionState, 3, 0) == 0)
            {
                Debug.LogWarning("[RemoteExec] 内蔵サーバー停止により実行前の要求を拒否しました");
                _completion.TrySetException(new RemoteExecServerStoppedBeforeStartException());
                return;
            }
            if (Interlocked.CompareExchange(ref _admissionState, 4, 1) != 1) return;
            // 完了済みなら結果を上書きしない。閉じた事実だけをログへ出す
            // A finished invocation keeps its outcome; only an actual close is logged
            if (!_completion.TrySetException(new RemoteExecServerAbandonedException())) return;
            Debug.LogError("[RemoteExec] 内蔵サーバー停止により開始済みのサーバー側実行を結果不明で閉じました");
        }

        public void Dispose()
        {
            _registration.Dispose();
            _queue?.Release(this);
        }

        private void CancelBeforeStart()
        {
            if (Interlocked.CompareExchange(ref _admissionState, 2, 0) != 0) return;
            Debug.LogWarning("[RemoteExec] 切断済み要求のサーバー実行を取り消しました");
            _completion.TrySetCanceled(_cancellationToken);
        }

        private async UniTaskVoid CompleteAsync(UniTask<object> invocation)
        {
            // 外部から送られ動的に実行するコードの非同期例外も待機者へ渡す
            // Forward async exceptions at the dynamically compiled external-code boundary to the waiter
            try { _completion.TrySetResult(await invocation); }
            catch (Exception error) { _completion.TrySetException(error); }
        }
    }
}
