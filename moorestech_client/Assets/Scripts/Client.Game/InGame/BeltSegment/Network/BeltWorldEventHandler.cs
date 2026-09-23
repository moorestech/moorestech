using System;
using System.Threading;
using Client.Game.Common;
using Client.Game.InGame.BeltSegment.Model;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack.BeltSegment;
using UniRx;
using VContainer.Unity;
namespace Client.Game.InGame.BeltSegment.Network
{
    internal sealed class BeltWorldEventHandler : IInitializable, IInitialEventApplyWaitTarget
    {
        private readonly IVanillaApiEvent events;
        private readonly ClientBeltWorld world;
        private readonly BeltWorldRecovery recovery;
        private readonly CancellationToken cancellation;
        private readonly UniTaskCompletionSource initial = new();
        public BeltWorldEventHandler(IVanillaApiEvent events, ClientBeltWorld world, BeltWorldRecovery recovery, CancellationToken cancellation)
        { this.events = events; this.world = world; this.recovery = recovery; this.cancellation = cancellation; }
        public UniTask WaitForInitialApplyAsync() => initial.Task;
        public void Initialize()
        {
            world.OnRebuilt.Subscribe(_ => initial.TrySetResult());
            world.OnFailed.Subscribe(exception => initial.TrySetException(exception));
            world.OnRecoveryRequested.Subscribe(_ => recovery.Request());
            // 初期要求より先に両購読を登録し、同期イベントreplayの競合も同じ所有者へ渡す。
            // Subscribe before the initial request, routing synchronous event-replay races through the same owner.
            events.SubscribeEventResponse(BeltWorldEventPacket.SnapshotTag, ReceiveSnapshot);
            events.SubscribeEventResponse(BeltWorldEventPacket.FrameTag, ReceiveFrame);
            cancellation.Register(() => initial.TrySetCanceled(cancellation));
            recovery.Request();
        }
        private void ReceiveSnapshot(byte[] payload)
        {
            if (cancellation.IsCancellationRequested) return;
            // 同期InitializeDispatchを止めないネットワーク入力境界。
            // Network-input boundary that must not interrupt synchronous InitializeDispatch.
            try { world.ReceiveSnapshot(BeltWireCodec.Decode(MessagePackSerializer.Deserialize<BeltWorldSnapshotMessagePack>(payload))); }
            catch (Exception exception) { world.Fail(exception); }
        }
        private void ReceiveFrame(byte[] payload)
        {
            if (cancellation.IsCancellationRequested) return;
            // 確定frameの検証失敗は復旧状態へ折り畳む。
            // Fold invalid completed frames into explicit recovery state.
            try { world.ReceiveFrame(BeltWireCodec.Decode(MessagePackSerializer.Deserialize<BeltWorldFrameMessagePack>(payload))); }
            catch (Exception exception) { world.Fail(exception); }
        }
    }
}
