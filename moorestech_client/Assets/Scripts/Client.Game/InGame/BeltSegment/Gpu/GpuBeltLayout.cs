using System.Collections.Generic;
using Game.BeltSegment;

namespace Client.Game.InGame.BeltSegment.Gpu
{
    internal sealed class GpuBeltLayout
    {
        internal readonly GpuBeltTopology[] Topology;
        internal readonly GpuBeltPort[] InputPorts;
        internal readonly GpuBeltPort[] OutputPorts;
        internal readonly GpuBeltPort[] ExternalInputs;
        internal readonly GpuBeltNormalLink[] NormalLinks;
        internal readonly int TotalCapacity;
        internal readonly int OutputCount;

        internal GpuBeltLayout(BeltReplaySnapshot snapshot)
        {
            int segmentCount = snapshot.Segments.Length;
            Topology = new GpuBeltTopology[segmentCount];
            var inputsBySegment = new List<GpuBeltPort>[segmentCount];
            var outputsBySegment = new List<GpuBeltPort>[segmentCount];
            var normalLinks = new List<GpuBeltNormalLink>();
            int offset = 0;

            // segment indexを維持し、列の領域を容量で連結する。
            // Keep segment indices and concatenate lanes by capacity.
            for (int i = 0; i < segmentCount; i++)
            {
                var segment = snapshot.Segments[i];
                Topology[i] = new GpuBeltTopology
                {
                    Offset = offset, Capacity = segment.Capacity, Kind = (int)segment.Kind,
                    NormalLinkIndex = -1
                };
                offset += segment.Capacity;
                inputsBySegment[i] = new List<GpuBeltPort>();
                outputsBySegment[i] = new List<GpuBeltPort>();
            }
            TotalCapacity = offset;

            // Coreと同じ登録順で、内部接続を先に両端へ登録する。
            // Register both ends of internal links first, matching Core order.
            foreach (var link in snapshot.Links)
            {
                var sourceKind = snapshot.Segments[link.SourceSegmentId].Kind;
                int inputDirection = (int)link.OutputDirection ^ 1;
                outputsBySegment[link.SourceSegmentId].Add(new GpuBeltPort
                {
                    Kind = GpuBeltData.PortSegment, Id = link.TargetSegmentId,
                    Direction = (int)link.OutputDirection
                });
                inputsBySegment[link.TargetSegmentId].Add(new GpuBeltPort
                {
                    Kind = sourceKind == BeltSegmentKind.Normal ? GpuBeltData.PortSegment : GpuBeltData.PortBuffer,
                    Id = link.SourceSegmentId, Direction = inputDirection
                });
                if (sourceKind != BeltSegmentKind.Normal ||
                    snapshot.Segments[link.TargetSegmentId].Kind != BeltSegmentKind.Normal) continue;

                var topology = Topology[link.SourceSegmentId];
                topology.NormalLinkIndex = normalLinks.Count;
                Topology[link.SourceSegmentId] = topology;
                normalLinks.Add(new GpuBeltNormalLink
                {
                    Source = link.SourceSegmentId, Target = link.TargetSegmentId,
                    InputDirection = inputDirection
                });
            }

            // 外部出力は内部linkの後、外部入力は最後に追加する。
            // Add external outputs after links and external inputs last.
            for (int i = 0; i < snapshot.Outputs.Length; i++)
            {
                var output = snapshot.Outputs[i];
                outputsBySegment[output.SourceSegmentId].Add(new GpuBeltPort
                {
                    Kind = GpuBeltData.PortExternal, Id = i, Direction = (int)output.OutputDirection
                });
            }
            ExternalInputs = new GpuBeltPort[snapshot.Inputs.Length];
            for (int i = 0; i < snapshot.Inputs.Length; i++)
            {
                var input = snapshot.Inputs[i];
                var port = new GpuBeltPort
                {
                    Kind = GpuBeltData.PortExternal, Id = i, Direction = (int)input.InputDirection
                };
                inputsBySegment[input.TargetSegmentId].Add(port);
                ExternalInputs[i] = new GpuBeltPort
                {
                    Kind = GpuBeltData.PortSegment, Id = input.TargetSegmentId,
                    Direction = (int)input.InputDirection
                };
            }

            // segment順に平坦化し、登録順の優先indexを固定する。
            // Flatten by segment while retaining each registration priority index.
            var inputPorts = new List<GpuBeltPort>();
            var outputPorts = new List<GpuBeltPort>();
            for (int i = 0; i < segmentCount; i++)
            {
                var topology = Topology[i];
                topology.FirstInput = inputPorts.Count;
                topology.InputCount = inputsBySegment[i].Count;
                topology.FirstOutput = outputPorts.Count;
                topology.OutputCount = outputsBySegment[i].Count;
                Topology[i] = topology;
                inputPorts.AddRange(inputsBySegment[i]);
                outputPorts.AddRange(outputsBySegment[i]);
            }
            InputPorts = inputPorts.ToArray();
            OutputPorts = outputPorts.ToArray();
            NormalLinks = normalLinks.ToArray();
            OutputCount = snapshot.Outputs.Length;
        }
    }
}
