using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Sync.State;

namespace Game.Block.Blocks.BeltConveyor.Sync.Replica
{
    // 全量からCoreのsegmentを生成し、形の接続どおりに配線して中身を載せる。サーバーのBeltTransportAssemblerと同じ手順をワールド無しで行う
    // Creates Core segments from the full state, wires them as the shapes say and loads the contents; the server's BeltTransportAssembler procedure without a world
    public static class BeltTransportReplicaAssembler
    {
        public static BeltTransportReplica Assemble(BeltTransportFullState fullState)
        {
            var states = fullState.Segments;
            var shapes = new BeltSegmentShape[states.Length];
            var segments = new BeltConveyorSegment[states.Length];
            for (var i = 0; i < states.Length; i++)
            {
                shapes[i] = states[i].Shape;
                segments[i] = Create(states[i]);
            }

            var machineReceivers = new Dictionary<(int, BeltDirection), BeltReplicaMachineReceiver>();
            for (var i = 0; i < states.Length; i++) WireOutputs(i, segments[i]);
            for (var i = 0; i < states.Length; i++) Restore(states[i], segments[i]);
            return new BeltTransportReplica(shapes, segments, machineReceivers);

            #region Internal

            void WireOutputs(int index, BeltConveyorSegment segment)
            {
                // 通常はsegment自身、合流・分岐はbufferが搬出する。相手が機械なら複製の受け手を作る
                // A normal segment outputs itself; merges and branches output through their buffer. Machines get a replica receiver
                foreach (var link in shapes[index].Outputs)
                {
                    IBeltReceiver target;
                    if (link.IsMachine)
                    {
                        var receiver = new BeltReplicaMachineReceiver();
                        machineReceivers.Add((index, link.Direction), receiver);
                        target = receiver;
                    }
                    else
                    {
                        target = segments[link.PartnerSegmentIndex];
                    }
                    if (segment is BeltNormalSegment normal) normal.ConnectTo(target, link.Direction, link.EntryDirection);
                    else ((BeltBufferedSegment)segment).Buffer.ConnectTo(target, link.Direction, link.EntryDirection);
                }
            }

            void Restore(BeltSegmentState state, BeltConveyorSegment segment)
            {
                // 走行中は出口に近い順のまま載せ、buffer内は合流・分岐のbufferへ戻す
                // Running items are loaded in exit order; a buffered item goes back into the merge or branch buffer
                var items = new BeltItemState[state.Items.Length];
                for (var i = 0; i < items.Length; i++) items[i] = state.Items[i].ToState();
                segment.RestoreItems(items);
                if (state.HasBufferItem) ((BeltBufferedSegment)segment).Buffer.RestoreItem(state.BufferItem.ToItem());
            }

            #endregion
        }

        private static BeltConveyorSegment Create(BeltSegmentState state)
        {
            // 優先順は全量の値をそのまま使う。合流は搬入順、分岐はbufferの搬出順
            // The priority order comes straight from the full state: input order for a merge, buffer output order for a branch
            var shape = state.Shape;
            switch (shape.Kind)
            {
                case BeltSegmentKind.Merge:
                    return new BeltMergeSegment(shape.Speed, state.PriorityOrder, shape.Forward);
                case BeltSegmentKind.Branch:
                    return new BeltBranchSegment(shape.Capacity, shape.Speed, state.PriorityOrder, shape.Forward);
                default:
                    return new BeltNormalSegment(shape.Capacity, shape.Speed);
            }
        }
    }
}
