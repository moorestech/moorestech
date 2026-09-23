using Server.Event.EventReceive.BeltSegment;
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
        public BeltWorldEventHandler(IVanillaApiEvent events, ClientBeltWorld world, BeltWorldRecovery recovery, CancellationToken cancellation)
        { this.events = events; this.world = world; this.recovery = recovery; this.cancellation = cancellation; }
        public void Initialize()
        {
            world.OnRecoveryRequested.Subscribe(_ => recovery.Request());
            // 初期要求より先に両購読を登録し、同期イベントreplayの競合も同じ所有者へ渡す。
            // Subscribe before the initial request, routing synchronous event-replay races through the same owner.
            events.SubscribeEventResponse(BeltWorldEventPacket.SnapshotTag, ReceiveSnapshot);
            events.SubscribeEventResponse(BeltWorldEventPacket.FrameTag, ReceiveFrame);
            cancellation.Register(() => world.CancelInitialApply(cancellation));
            recovery.Request();
            #region Internal
            void ReceiveSnapshot(byte[] payload)
            {
                if (cancellation.IsCancellationRequested) return;
                BeltWorldSnapshotMessagePack wire;
                // MessagePackは外部byte列のパースのみをこの境界で隔離する。
                // Isolate only parsing external MessagePack bytes at this input boundary.
                try { wire = MessagePackSerializer.Deserialize<BeltWorldSnapshotMessagePack>(payload); }
                catch (Exception exception) { world.Recover($"Snapshot decode failed: {exception.Message}"); return; }
                if (!BeltWireCodec.TryDecode(wire, out var snapshot, out var reason)) { world.Recover(reason); return; }
                world.ReceiveSnapshot(snapshot);
            }
            void ReceiveFrame(byte[] payload)
            {
                if (cancellation.IsCancellationRequested) return;
                BeltWorldFrameMessagePack wire;
                // 外部byteパース後の検証・ローカル適用はcatchの外で行う。
                // Validate and apply locally outside the catch after parsing external bytes.
                try { wire = MessagePackSerializer.Deserialize<BeltWorldFrameMessagePack>(payload); }
                catch (Exception exception) { world.Recover($"Frame decode failed: {exception.Message}"); return; }
                if (!BeltWireCodec.TryDecode(wire, out var frame, out var reason)) { world.Recover(reason); return; }
                world.ReceiveFrame(frame);
            }
            #endregion
        }
        public UniTask WaitForInitialApplyAsync() => world.WaitForInitialApplyAsync();
    }
}
