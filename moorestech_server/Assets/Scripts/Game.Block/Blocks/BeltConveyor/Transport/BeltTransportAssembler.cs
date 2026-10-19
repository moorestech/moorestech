using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Save;
using Game.Block.Blocks.BeltConveyor.Sync.Diff;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.Block.Interface;

namespace Game.Block.Blocks.BeltConveyor.Transport
{
    // 構成(D3)からCoreのsegmentを生成し、構成のリンクどおりに接続する
    // Creates Core segments from the layouts (D3) and wires them exactly as the layout links say
    // segmentの生成と出力の配線はクライアントの複製組立(BeltTransportReplicaAssembler)と共有する。片側だけ変えると複製の構成がずれて同期が止まる
    // Segment creation and output wiring are shared with the client replica assembler; changing only one side would diverge the replica and stop sync
    public static class BeltTransportAssembler
    {
        // savedPriorityOrdersはロード直後だけ中身を持つ。載っている合流・分岐はその優先順で、それ以外は向きから初期化する
        // savedPriorityOrders has entries only right after a load; listed merges and branches start from them, everything else from the direction
        // diffRecorderは機械との搬送の成立を書く先。生成した受け手と受け口がsegment番号つきで記録する
        // diffRecorder receives settled machine handoffs; the created receivers and supply ports record them with segment numbers
        public static BeltTransportAssembly Assemble(BeltSegmentLayout[] layouts, IReadOnlyDictionary<BlockInstanceId, int> savedPriorityOrders, BeltTransportDiffRecorder diffRecorder)
        {
            var segments = new BeltConveyorSegment[layouts.Length];
            for (var i = 0; i < layouts.Length; i++) segments[i] = CreateSegment(layouts[i].Kind, layouts[i].Capacity, layouts[i].Speed, PriorityOrderOf(layouts[i]), layouts[i].Forward);

            var supplyPortByFace = new Dictionary<BeltMachineSupplyKey, BeltMachineSupplyPort>();
            for (var i = 0; i < layouts.Length; i++)
            {
                WireOutputs(layouts[i], segments[i]);
                CollectSupplyPorts(layouts[i], segments[i]);
            }
            return new BeltTransportAssembly(layouts, segments, supplyPortByFace, diffRecorder);

            #region Internal

            void WireOutputs(BeltSegmentLayout layout, BeltConveyorSegment segment)
            {
                // 相手が機械なら受け手を作り、配線は複製と共有のConnectOutputで行う
                // A machine partner gets a receiver; the wiring itself goes through ConnectOutput shared with the replica
                var sourceBlock = SourceBlockOf(layout);
                foreach (var link in layout.Outputs)
                {
                    IBeltReceiver target = link.IsMachine
                        ? new BeltMachineReceiver(sourceBlock, link.Connection, layout.Index, link.Direction, diffRecorder)
                        : segments[link.PartnerSegmentIndex];
                    ConnectOutput(segment, target, link.Direction, link.EntryDirection);
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
                    supplyPortByFace.Add(key, new BeltMachineSupplyPort(segment, layout.Index, link.Direction, link.EntryDirection));
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

            int PriorityOrderOf(BeltSegmentLayout layout)
            {
                // 合流の優先順は先頭(唯一)のマス、分岐は末尾マスのblockに属する。保存値は搬出・搬入方向を除いた並べ替えのときだけ使う
                // A merge's order belongs to its only cell and a branch's to its last cell; a saved value is used only when it permutes the directions other than the output or input
                switch (layout.Kind)
                {
                    case BeltSegmentKind.Merge:
                        return BeltSavedPriorityOrder.Resolve(savedPriorityOrders, layout.Cells[0].BlockInstanceId, layout.Forward);
                    case BeltSegmentKind.Branch:
                        var lastCell = layout.Cells[layout.Cells.Length - 1].BlockInstanceId;
                        return BeltSavedPriorityOrder.Resolve(savedPriorityOrders, lastCell, BeltDirections.Opposite(layout.Forward));
                    default:
                        return BeltPriority.InitializeFromDirection;
                }
            }

            #endregion
        }

        // 種類ごとにCoreのsegmentを1本作る。通常segmentは優先順を持たないのでpriorityOrderを見ない
        // Creates one Core segment for the kind; a normal segment has no priority order and ignores priorityOrder
        public static BeltConveyorSegment CreateSegment(BeltSegmentKind kind, int capacity, int speed, int priorityOrder, BeltDirection forward)
        {
            switch (kind)
            {
                case BeltSegmentKind.Merge:
                    return new BeltMergeSegment(speed, priorityOrder, forward);
                case BeltSegmentKind.Branch:
                    return new BeltBranchSegment(capacity, speed, priorityOrder, forward);
                default:
                    return new BeltNormalSegment(capacity, speed);
            }
        }

        // 出力1本を配線する。通常はsegment自身、合流・分岐はbufferが搬出する
        // Wires one output: a normal segment outputs itself, merges and branches output through their buffer
        public static void ConnectOutput(BeltConveyorSegment segment, IBeltReceiver target, BeltDirection outputDirection, BeltEntryDirection entryDirection)
        {
            if (segment is BeltNormalSegment normal) normal.ConnectTo(target, outputDirection, entryDirection);
            else ((BeltBufferedSegment)segment).Buffer.ConnectTo(target, outputDirection, entryDirection);
        }
    }
}
