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
        private readonly BeltSimulation simulation;
        public int SegmentCount => segments.Length;

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
            for (int i = 0; i < outputs.Length; i++)
                Connect(outputs[i].SourceSegmentId, new GraphOutputReceiver(outputReceivers[i]), outputs[i].OutputDirection);
            for (int i = 0; i < inputs.Length; i++) segments[inputs[i].TargetSegmentId].AttachInput(inputSources[i], inputs[i].InputDirection);
            // 配線を確定してから列とbufferを既存Coreへ復元する。
            // Restore queues and buffers through the Core after fixing the wiring.
            for (int i = 0; i < segments.Length; i++)
            {
                var state = snapshot.Segments[i];
                segments[i].RestoreItems(state.Items);
                if (state.BufferedItem.HasValue) segments[i].Buffer.RestoreItem(state.BufferedItem.Value);
            }
            simulation = new BeltSimulation(segments);

            #region Internal
            void Connect(int sourceId, IBeltReceiver target, BeltDirection direction)
            {
                var source = segments[sourceId];
                if (source.Kind == BeltSegmentKind.Normal) source.ConnectTo(target, direction);
                else source.Buffer.ConnectTo(target, direction);
            }
            #endregion
        }

        // 可変Coreを公開せず、tick境界の操作をGraphへ集める。
        // Keep mutable Core private and route tick-boundary operations through the graph.
        public int GetSpeed(int segmentId) => segments[segmentId].Speed;
        public void SetSpeed(int segmentId, int speed) => segments[segmentId].SetSpeed(speed);
        public void Tick(bool parallel) => simulation.Tick(parallel);

        public int GetInputOffer(int inputId)
        {
            var input = inputs[inputId];
            return segments[input.TargetSegmentId].GetOffer(input.InputDirection);
        }

        public bool TryInsert(int inputId, int length, in BeltItem item)
        {
            // 外部搬入の長さ契約と配線解決を一箇所で守る。
            // Enforce the external insertion length and resolve its wiring in one place.
            if (length <= 0 || BeltConstants.ItemWidth < length)
                throw new ArgumentOutOfRangeException(nameof(length), "Insertion length must be 1..256.");
            var input = inputs[inputId];
            return segments[input.TargetSegmentId].TryReceive(input.InputDirection, length, item);
        }

        public uint ComputeStateHash()
        {
            uint hash = BeltStateHash.Add(BeltStateHash.Initial, segments.Length);
            foreach (var segment in segments) hash = segment.ComputeStateHash(hash);
            hash = BeltStateHash.Add(hash, links.Length);
            foreach (var link in links)
                hash = BeltStateHash.Add(BeltStateHash.Add(BeltStateHash.Add(hash,
                    link.SourceSegmentId), link.TargetSegmentId), (int)link.OutputDirection);
            hash = BeltStateHash.Add(hash, inputs.Length);
            foreach (var input in inputs)
                hash = BeltStateHash.Add(BeltStateHash.Add(hash, input.TargetSegmentId), (int)input.InputDirection);
            hash = BeltStateHash.Add(hash, outputs.Length);
            foreach (var output in outputs)
                hash = BeltStateHash.Add(BeltStateHash.Add(hash, output.SourceSegmentId), (int)output.OutputDirection);
            return hash;
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

        private sealed class GraphOutputReceiver : IBeltReceiver
        {
            private readonly IBeltReceiver receiver;
            internal GraphOutputReceiver(IBeltReceiver receiver) => this.receiver = receiver;

            // 外部だけを包み、内部Normal接続の実体による判定を保つ。
            // Wrap only external outputs so internal Normal links retain concrete Core identity.
            public void AttachInput(IBeltSource source, BeltDirection inputDirection)
                => receiver.AttachInput(new GraphSource(source), inputDirection);
            public int GetOffer(BeltDirection inputDirection) => receiver.GetOffer(inputDirection);
            public bool TryReceive(BeltDirection inputDirection, int length, in BeltItem item)
                => receiver.TryReceive(inputDirection, length, item);
        }

        private sealed class GraphSource : IBeltSource
        {
            private readonly IBeltSource source;
            internal GraphSource(IBeltSource source) => this.source = source;

            // 保持した参照からも配線変更できず、供給照会は最新のCoreへ届く。
            // Retained references cannot rewire the graph, while supply queries reach the live Core.
            public bool TryGetOutput(BeltDirection inputDirection) => source.TryGetOutput(inputDirection);
        }
    }
}
