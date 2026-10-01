using System;
using System.Linq;
using Core.BeltTransport;
using MessagePack;
namespace Server.Util.MessagePack.BeltTransport
{
    [MessagePackObject]
    public sealed class BeltSnapshotMessagePack
    {
        [Key(0)] public ulong Tick { get; }
        [Key(1)] public BeltCellMessagePack[] Cells { get; }
        [Key(2)] public BeltConnectionMessagePack[] Connections { get; }
        [Key(3)] public BeltCellItemMessagePack[] Items { get; }
        [Key(4)] public BeltPriorityMessagePack[] Priorities { get; }
        [SerializationConstructor]
        public BeltSnapshotMessagePack(ulong tick, BeltCellMessagePack[] cells, BeltConnectionMessagePack[] connections, BeltCellItemMessagePack[] items, BeltPriorityMessagePack[] priorities)
        {
            Tick = tick;
            Cells = cells;
            Connections = connections;
            Items = items;
            Priorities = priorities;
        }
        [Obsolete("Reserved for MessagePack.")]
        public BeltSnapshotMessagePack() { }
        public BeltSnapshotMessagePack(BeltCommittedSnapshot value)
        {
            // 確定tickと同じ境界の状態を一括で符号化する。
            // Encode the committed tick and its matching boundary together.
            Tick = value.Tick;
            Cells = value.Snapshot.Cells.Select(v => new BeltCellMessagePack(v)).ToArray();
            Connections = value.Snapshot.Connections.Select(v => new BeltConnectionMessagePack(v)).ToArray();
            Items = value.Snapshot.Items.Select(v => new BeltCellItemMessagePack(v)).ToArray();
            Priorities = value.Snapshot.Priorities.Select(v => new BeltPriorityMessagePack(v)).ToArray();
        }
        public BeltCommittedSnapshot ToCore() => new(Tick, new BeltNetworkSnapshot(
            Cells.Select(v => v.ToCore()).ToArray(), Connections.Select(v => v.ToCore()).ToArray(),
            Items.Select(v => v.ToCore()).ToArray(), Priorities.Select(v => v.ToCore()).ToArray()));
    }
}
