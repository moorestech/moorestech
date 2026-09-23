using System;
using System.Threading;
using Client.Game.InGame.BeltSegment.Model;
using Cysharp.Threading.Tasks;
using Game.BeltSegment;
namespace Client.Game.InGame.BeltSegment.Network
{
    internal sealed class BeltWorldRecovery
    {
        private readonly ClientBeltWorld world;
        private readonly IBeltSnapshotRequester requester;
        private readonly CancellationToken cancellation;
        internal bool IsPending { get; private set; }
        internal BeltWorldRecovery(ClientBeltWorld world, IBeltSnapshotRequester requester, CancellationToken cancellation)
        { this.world = world; this.requester = requester; this.cancellation = cancellation; }
        internal void Request()
        {
            if (IsPending || cancellation.IsCancellationRequested || world.Status == BeltStreamStatus.Failed) return;
            IsPending = true;
            Run().Forget();
            #region Internal
            async UniTask Run()
            {
                int retryDelay = 500;
                try
                {
                    while (!cancellation.IsCancellationRequested)
                    {
                        if (world.Status is BeltStreamStatus.Running or BeltStreamStatus.Failed) return;
                        BeltWorldSnapshot snapshot = null;
                        // 実ネットワーク要求だけを隔離し、ローカル適用例外は再試行しない。
                        // Isolate only the real network request; local application failures never retry.
                        try { snapshot = await requester.Request(cancellation); }
                        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return; }
                        catch (Exception exception)
                        {
                            if (world.Status != BeltStreamStatus.Running) world.Recover(exception.Message);
                        }
                        if (cancellation.IsCancellationRequested) return;
                        if (snapshot != null) world.ReceiveSnapshot(snapshot);
                        if (world.Status is BeltStreamStatus.Running or BeltStreamStatus.Failed) return;
                        bool canceled = await UniTask.Delay(retryDelay, ignoreTimeScale: true, cancellationToken: cancellation).SuppressCancellationThrow();
                        if (canceled) return;
                        retryDelay = Math.Min(8000, retryDelay * 2);
                    }
                }
                finally { IsPending = false; }
            }
            #endregion
        }
    }
}
