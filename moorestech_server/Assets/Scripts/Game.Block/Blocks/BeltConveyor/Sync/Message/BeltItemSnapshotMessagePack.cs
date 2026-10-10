using System;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using MessagePack;

namespace Game.Block.Blocks.BeltConveyor.Sync.Message
{
    // 全量に載せるアイテム1個の通信形。個体IDはフォーマッタを持たないのでlongで運ぶ
    // Wire form of one item in the full state; the instance id has no formatter, so it travels as a long
    [MessagePackObject]
    public class BeltItemSnapshotMessagePack
    {
        [Key(0)] public ItemId ItemId { get; set; }
        [Key(1)] public long ItemInstanceId { get; set; }
        [Key(2)] public BeltEntryDirection EntryDirection { get; set; }
        [Key(3)] public int DistanceToExit { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BeltItemSnapshotMessagePack() { }

        public BeltItemSnapshotMessagePack(in BeltItemSnapshot snapshot)
        {
            ItemId = snapshot.ItemId;
            ItemInstanceId = snapshot.ItemInstanceId.AsPrimitive();
            EntryDirection = snapshot.EntryDirection;
            DistanceToExit = snapshot.DistanceToExit;
        }

        public BeltItemSnapshot ToSnapshot()
        {
            return new BeltItemSnapshot(new BeltItem(ItemId, new ItemInstanceId(ItemInstanceId), EntryDirection), DistanceToExit);
        }
    }
}
