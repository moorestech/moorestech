using System;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using MessagePack;
using Server.Util.MessagePack;

namespace Game.Block.Blocks.BeltConveyor.Sync.Message
{
    // 全量に載せるマス1つの通信形
    // Wire form of one cell in the full state
    [MessagePackObject]
    public class BeltCellShapeMessagePack
    {
        [Key(0)] public Vector3IntMessagePack Position { get; set; }
        [Key(1)] public BeltDirection Forward { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BeltCellShapeMessagePack() { }

        public BeltCellShapeMessagePack(in BeltCellShape cell)
        {
            Position = new Vector3IntMessagePack(cell.Position);
            Forward = cell.Forward;
        }

        public BeltCellShape ToShape()
        {
            return new BeltCellShape(Position.Vector3Int, Forward);
        }
    }
}
