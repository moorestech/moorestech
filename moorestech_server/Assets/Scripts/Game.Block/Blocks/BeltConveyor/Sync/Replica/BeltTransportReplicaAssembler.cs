using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Block.Blocks.BeltConveyor.Transport;

namespace Game.Block.Blocks.BeltConveyor.Sync.Replica
{
    // 全量からCoreのsegmentを生成し、形の接続どおりに配線して中身を載せる。生成と配線はサーバーのBeltTransportAssemblerと同じ関数を使う
    // Creates Core segments from the full state, wires them as the shapes say and loads the contents; creation and wiring use the same functions as the server's BeltTransportAssembler
    public static class BeltTransportReplicaAssembler
    {
        public static BeltTransportReplica Assemble(BeltTransportFullState fullState)
        {
            // 優先順は全量の値をそのまま使う。合流は搬入順、分岐はbufferの搬出順
            // The priority order comes straight from the full state: input order for a merge, buffer output order for a branch
            var states = fullState.Segments;
            var shapes = new BeltSegmentShape[states.Length];
            var segments = new BeltConveyorSegment[states.Length];
            for (var i = 0; i < states.Length; i++)
            {
                var shape = states[i].Shape;
                shapes[i] = shape;
                segments[i] = BeltTransportAssembler.CreateSegment(shape.Kind, shape.Capacity, shape.Speed, states[i].PriorityOrder, shape.Forward);
            }

            var machineReceivers = new Dictionary<(int, BeltDirection), BeltReplicaMachineReceiver>();
            for (var i = 0; i < states.Length; i++) WireOutputs(i, segments[i]);
            for (var i = 0; i < states.Length; i++) Restore(states[i], segments[i]);
            return new BeltTransportReplica(shapes, segments, machineReceivers);

            #region Internal

            void WireOutputs(int index, BeltConveyorSegment segment)
            {
                // 相手が機械なら複製の受け手を作る
                // A machine partner gets a replica receiver
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
                    BeltTransportAssembler.ConnectOutput(segment, target, link.Direction, link.EntryDirection);
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
    }
}
