using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
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

    internal sealed class RemoteExecServerInvocation : IDisposable
    {
        private readonly MethodInfo _entry;
        private readonly CancellationToken _cancellationToken;
        private readonly CancellationTokenRegistration _registration;
        private readonly UniTaskCompletionSource<object> _completion = new();
        private int _admissionState;

        internal bool HasStarted => Volatile.Read(ref _admissionState) == 1;

        internal RemoteExecServerInvocation(MethodInfo entry, CancellationToken cancellationToken)
        {
            _entry = entry;
            _cancellationToken = cancellationToken;
            _registration = cancellationToken.Register(CancelBeforeStart);
        }

        internal UniTask<object> Completion => _completion.Task;

        internal void StartOnServerThread()
        {
            // 開始と取消を排他にし、実行中の取消では直列化を解かない
            // Arbitrate start against cancellation; cancellation after start must not release serialization
            if (Interlocked.CompareExchange(ref _admissionState, 1, 0) != 0) return;

            // 外部から送られ動的に実行するコードの同期例外を待機者へ渡す
            // Forward synchronous exceptions at the dynamically compiled external-code boundary
            try
            {
                var invocation = (UniTask<object>)_entry.Invoke(null, null);
                CompleteAsync(invocation).Forget();
            }
            catch (Exception error)
            {
                _completion.TrySetException(error);
            }
        }

        internal void StopBeforeStart()
        {
            // 停止と開始を排他にし、実行済みの要求の結果を上書きしない
            // Arbitrate stop against start without replacing the outcome of an invocation already running
            if (Interlocked.CompareExchange(ref _admissionState, 3, 0) != 0) return;
            Debug.LogWarning("[RemoteExec] 内蔵サーバー停止により実行前の要求を拒否しました");
            _completion.TrySetException(new RemoteExecServerStoppedBeforeStartException());
        }

        public void Dispose()
        {
            _registration.Dispose();
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
