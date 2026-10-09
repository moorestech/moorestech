using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.Block.Interface;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // 構成(D3)からCoreのsegmentを生成し、構成のリンクどおりに接続する
    // Creates Core segments from the layouts (D3) and wires them exactly as the layout links say
    public static class BeltTransportAssembler
    {
        public static BeltTransportAssembly Assemble(BeltSegmentLayout[] layouts)
        {
            // 番号順にsegmentを生成してから接続する。優先順は新規生成として向きから初期化する
            // Create every segment in index order, then wire; priorities start from the direction as for a fresh block
            var segments = new BeltConveyorSegment[layouts.Length];
            for (var i = 0; i < layouts.Length; i++) segments[i] = Create(layouts[i]);

            var supplyPortByFace = new Dictionary<BeltMachineSupplyKey, BeltMachineSupplyPort>();
            for (var i = 0; i < layouts.Length; i++)
            {
                WireOutputs(layouts[i], segments[i]);
                CollectSupplyPorts(layouts[i], segments[i]);
            }
            return new BeltTransportAssembly(layouts, segments, supplyPortByFace);

            #region Internal

            void WireOutputs(BeltSegmentLayout layout, BeltConveyorSegment segment)
            {
                // 通常はsegment自身、合流・分岐はbufferが搬出する。相手が機械なら受け口を作る
                // A normal segment outputs itself; merges and branches output through their buffer. Machines get a receiver
                var sourceBlock = SourceBlockOf(layout);
                foreach (var link in layout.Outputs)
                {
                    IBeltReceiver target = link.IsMachine ? new BeltMachineReceiver(sourceBlock, link.Connection) : segments[link.PartnerSegmentIndex];
                    if (segment is BeltNormalSegment normal) normal.ConnectTo(target, link.Direction, link.EntryDirection);
                    else ((BeltBufferedSegment)segment).Buffer.ConnectTo(target, link.Direction, link.EntryDirection);
                }
            }

            void CollectSupplyPorts(BeltSegmentLayout layout, BeltConveyorSegment segment)
            {
                // 機械が直接入れるのは、先頭マスの入力が機械の通常・分岐と、内部segment
                // Machines push directly into a normal or branch whose head input is a machine, and into internal segments
                foreach (var link in layout.Inputs)
                {
                    if (!link.IsMachine) continue;
                    var key = new BeltMachineSupplyKey(SupplyBlockOf(layout), link.Connection.PartnerBlock.BlockInstanceId);
                    supplyPortByFace.Add(key, new BeltMachineSupplyPort(segment, link.Direction, link.EntryDirection));
                }
            }

            BlockInstanceId SourceBlockOf(BeltSegmentLayout layout)
            {
                // 内部segmentは機械へ出さないので、マスを持つsegmentの末尾マスだけが送り元になる
                // Internal segments never output to a machine, so only the last cell of a cell-bearing segment emits
                return layout.IsInternal ? default : layout.Cells[layout.Cells.Length - 1].BlockInstanceId;
            }

            BlockInstanceId SupplyBlockOf(BeltSegmentLayout layout)
            {
                // 内部segmentへの押し込みは、その合流マスのblockへの押し込みとして届く
                // A push into an internal segment arrives as a push into its merge cell's block
                var owner = layout.IsInternal ? layouts[layout.Outputs[0].PartnerSegmentIndex] : layout;
                return owner.Cells[0].BlockInstanceId;
            }

            #endregion
        }

        private static BeltConveyorSegment Create(BeltSegmentLayout layout)
        {
            switch (layout.Kind)
            {
                case BeltSegmentKind.Merge:
                    return new BeltMergeSegment(layout.Speed, BeltPriority.InitializeFromDirection, layout.Forward);
                case BeltSegmentKind.Branch:
                    return new BeltBranchSegment(layout.Capacity, layout.Speed, BeltPriority.InitializeFromDirection, layout.Forward);
                default:
                    return new BeltNormalSegment(layout.Capacity, layout.Speed);
            }
        }
    }
}
