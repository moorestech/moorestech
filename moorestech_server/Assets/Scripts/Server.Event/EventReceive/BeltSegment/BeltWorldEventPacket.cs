using Game.Block.Blocks.BeltConveyor;
using Game.Context;
using MessagePack;
using Server.Util.MessagePack.BeltSegment;
using UniRx;
namespace Server.Event.EventReceive.BeltSegment
{
    public sealed class BeltWorldEventPacket : IBootInitializable
    {
        public const string SnapshotTag = "va:event:beltWorldSnapshot";
        public const string FrameTag = "va:event:beltWorldFrame";
        private readonly IBeltWorldLookup world;
        private readonly EventProtocolProvider provider;
        private bool loaded;
        public BeltWorldEventPacket(IBeltWorldLookup world, EventProtocolProvider provider)
        { this.world = world; this.provider = provider; }
        public void Load()
        {
            if (loaded) return;
            loaded = true;
            // 再構築と確定tickを同じ既存イベント配信へ一度だけ登録する。
            // Subscribe once to replacements and completed ticks on the existing event stream.
            world.OnBeltWorldRebuilt.Subscribe(value => provider.AddBroadcastEvent(SnapshotTag,
                MessagePackSerializer.Serialize(new BeltWorldSnapshotMessagePack(value))));
            world.OnFrame.Subscribe(value => provider.AddBroadcastEvent(FrameTag,
                MessagePackSerializer.Serialize(new BeltWorldFrameMessagePack(value))));
        }
    }
}
