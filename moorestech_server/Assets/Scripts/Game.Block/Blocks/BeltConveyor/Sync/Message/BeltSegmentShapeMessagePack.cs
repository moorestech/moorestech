using System;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using MessagePack;

namespace Game.Block.Blocks.BeltConveyor.Sync.Message
{
    // 全量に載せるsegment1本の形の通信形
    // Wire form of one segment's shape in the full state
    [MessagePackObject]
    public class BeltSegmentShapeMessagePack
    {
        [Key(0)] public BeltSegmentKind Kind { get; set; }
        [Key(1)] public bool IsInternal { get; set; }
        [Key(2)] public BeltCellShapeMessagePack[] Cells { get; set; }
        [Key(3)] public int Speed { get; set; }
        [Key(4)] public BeltDirection Forward { get; set; }
        [Key(5)] public BeltLinkShapeMessagePack[] Inputs { get; set; }
        [Key(6)] public BeltLinkShapeMessagePack[] Outputs { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BeltSegmentShapeMessagePack() { }

        public BeltSegmentShapeMessagePack(BeltSegmentShape shape)
        {
            Kind = shape.Kind;
            IsInternal = shape.IsInternal;
            Cells = new BeltCellShapeMessagePack[shape.Cells.Length];
            for (var i = 0; i < Cells.Length; i++) Cells[i] = new BeltCellShapeMessagePack(shape.Cells[i]);
            Speed = shape.Speed;
            Forward = shape.Forward;
            Inputs = FromLinks(shape.Inputs);
            Outputs = FromLinks(shape.Outputs);
        }

        public BeltSegmentShape ToShape()
        {
            var cells = new BeltCellShape[Cells.Length];
            for (var i = 0; i < cells.Length; i++) cells[i] = Cells[i].ToShape();
            return new BeltSegmentShape(Kind, IsInternal, cells, Speed, Forward, ToLinks(Inputs), ToLinks(Outputs));
        }

        private static BeltLinkShapeMessagePack[] FromLinks(BeltLinkShape[] links)
        {
            var messages = new BeltLinkShapeMessagePack[links.Length];
            for (var i = 0; i < messages.Length; i++) messages[i] = new BeltLinkShapeMessagePack(links[i]);
            return messages;
        }

        private static BeltLinkShape[] ToLinks(BeltLinkShapeMessagePack[] messages)
        {
            var links = new BeltLinkShape[messages.Length];
            for (var i = 0; i < links.Length; i++) links[i] = messages[i].ToShape();
            return links;
        }
    }
}
