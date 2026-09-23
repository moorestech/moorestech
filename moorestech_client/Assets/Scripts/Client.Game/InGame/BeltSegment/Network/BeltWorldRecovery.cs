using System;
using System.Threading;
using Client.Game.InGame.BeltSegment.Model;
using Cysharp.Threading.Tasks;
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
            if (IsPending || cancellation.IsCancellationRequested) return;
            IsPending = true;
            Run().Forget();
        }
        private async UniTask Run()
        {
            int retryDelay = 500;
            // 応答・遅延を含む1操作がゲートを所有し、イベント適用では解放しない。
            // One operation owns the gate across response and delay; event acceptance never releases it.
            try
            {
                do
                {
                    try
                    {
                        var snapshot = await requester.Request(cancellation);
                        cancellation.ThrowIfCancellationRequested();
                        world.ReceiveSnapshot(snapshot);
                    }
                    catch (TimeoutException exception)
                    {
                        if (world.Status != BeltStreamStatus.Running) world.Recover(exception.Message);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return; }
                    catch (Exception exception) { world.Fail(exception); }
                    if (world.Status == BeltStreamStatus.Running) return;
                    await UniTask.Delay(retryDelay, ignoreTimeScale: true, cancellationToken: cancellation);
                    retryDelay = Math.Min(8000, retryDelay * 2);
                } while (!cancellation.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            finally { IsPending = false; }
        }
    }
}
