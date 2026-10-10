using System;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using MessagePack;

namespace Game.Block.Blocks.BeltConveyor.Sync.Message
{
    // 全量に載せるsegment1本の形と中身の通信形
    // Wire form of one segment's shape and contents in the full state
    [MessagePackObject]
    public class BeltSegmentStateMessagePack
    {
        [Key(0)] public BeltSegmentShapeMessagePack Shape { get; set; }
        [Key(1)] public int PriorityOrder { get; set; }
        [Key(2)] public BeltItemSnapshotMessagePack[] Items { get; set; }
        // 合流・分岐のbufferにアイテムがある時だけ載り、無ければnull
        // Present only when a merge or branch buffer holds an item; null otherwise
        [Key(3)] public BeltItemSnapshotMessagePack BufferItem { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BeltSegmentStateMessagePack() { }

        public BeltSegmentStateMessagePack(BeltSegmentState state)
        {
            Shape = new BeltSegmentShapeMessagePack(state.Shape);
            PriorityOrder = state.PriorityOrder;
            Items = new BeltItemSnapshotMessagePack[state.Items.Length];
            for (var i = 0; i < Items.Length; i++) Items[i] = new BeltItemSnapshotMessagePack(state.Items[i]);
            BufferItem = state.HasBufferItem ? new BeltItemSnapshotMessagePack(state.BufferItem) : null;
        }

        public BeltSegmentState ToState()
        {
            var items = new BeltItemSnapshot[Items.Length];
            for (var i = 0; i < items.Length; i++) items[i] = Items[i].ToSnapshot();
            var hasBufferItem = BufferItem != null;
            return new BeltSegmentState(Shape.ToShape(), PriorityOrder, items, hasBufferItem, hasBufferItem ? BufferItem.ToSnapshot() : default);
        }
    }
}
