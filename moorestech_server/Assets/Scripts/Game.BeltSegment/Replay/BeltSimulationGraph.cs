using System;
using System.Collections.Generic;

namespace Game.BeltSegment
{
    public sealed class BeltSimulationGraph
    {
        private readonly BeltConveyorSegment[] segments;
        private readonly BeltReplayLink[] links;
        private readonly BeltReplayInput[] inputs;
        private readonly BeltReplayOutput[] outputs;
        public IReadOnlyList<BeltConveyorSegment> Segments => segments;
        public BeltSimulation Simulation { get; }

        public BeltSimulationGraph(BeltReplaySnapshot snapshot,
            IReadOnlyList<IBeltSource> inputSources, IReadOnlyList<IBeltReceiver> outputReceivers)
        {
            if (inputSources.Count != snapshot.Inputs.Length || outputReceivers.Count != snapshot.Outputs.Length)
                throw new ArgumentException("External port counts must match the snapshot.");
            // 入力配列への変更が配線の正本へ戻らないよう分離する。
            // Isolate wiring ownership from later changes to input arrays.
            links = (BeltReplayLink[])snapshot.Links.Clone();
            inputs = (BeltReplayInput[])snapshot.Inputs.Clone();
            outputs = (BeltReplayOutput[])snapshot.Outputs.Clone();
            segments = new BeltConveyorSegment[snapshot.Segments.Length];
            for (int i = 0; i < segments.Length; i++)
            {
                var state = snapshot.Segments[i];
                segments[i] = new BeltConveyorSegment(state.Capacity, state.Speed, state.Kind, state.PriorityIndex);
            }
            // 登録順は合流・分岐の優先順位なので、3配列の順序を保持する。
            // Registration defines junction priority, so preserve the order of all three arrays.
            foreach (var link in links) Connect(link.SourceSegmentId, segments[link.TargetSegmentId], link.OutputDirection);
            for (int i = 0; i < outputs.Length; i++) Connect(outputs[i].SourceSegmentId, outputReceivers[i], outputs[i].OutputDirection);
            for (int i = 0; i < inputs.Length; i++) segments[inputs[i].TargetSegmentId].AttachInput(inputSources[i], inputs[i].InputDirection);
            // 配線を確定してから列とbufferを既存Coreへ復元する。
            // Restore queues and buffers through the Core after fixing the wiring.
            for (int i = 0; i < segments.Length; i++)
            {
                var state = snapshot.Segments[i];
                segments[i].RestoreItems(state.Items);
                if (state.BufferedItem.HasValue) segments[i].Buffer.RestoreItem(state.BufferedItem.Value);
            }
            Simulation = new BeltSimulation(segments);

            #region Internal
            void Connect(int sourceId, IBeltReceiver target, BeltDirection direction)
            {
                var source = segments[sourceId];
                if (source.Kind == BeltSegmentKind.Normal) source.ConnectTo(target, direction);
                else source.Buffer.ConnectTo(target, direction);
            }
            #endregion
        }

        public BeltReplaySnapshot CaptureSnapshot()
        {
            var states = new BeltReplaySegmentState[segments.Length];
            for (int i = 0; i < states.Length; i++)
            {
                var segment = segments[i];
                BeltItem? bufferedItem = segment.Buffer != null && segment.Buffer.TryGetItem(out var item) ? item : (BeltItem?)null;
                var items = segment.CaptureItems();
                // 復元と同じ種別契約で境界状態を運ぶ。
                // Carry boundary state through the same kind contracts used for restoration.
                states[i] = segment.Kind switch
                {
                    BeltSegmentKind.Normal => BeltReplaySegmentState.Normal(segment.Capacity, segment.Speed, items),
                    BeltSegmentKind.Merge => BeltReplaySegmentState.Merge(segment.Speed, segment.PriorityIndex, items, bufferedItem),
                    BeltSegmentKind.Branch => BeltReplaySegmentState.Branch(segment.Capacity, segment.Speed, segment.PriorityIndex, items, bufferedItem),
                    _ => throw new InvalidOperationException("Unknown segment kind.")
                };
            }
            return new BeltReplaySnapshot(states, (BeltReplayLink[])links.Clone(),
                (BeltReplayInput[])inputs.Clone(), (BeltReplayOutput[])outputs.Clone());
        }
    }
}
