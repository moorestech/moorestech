using System;
using UnityEngine;

namespace Client.Game.InGame.BeltSegment.Gpu
{
    internal sealed class GpuBeltBuffers : IDisposable
    {
        internal readonly GraphicsBuffer Topology, InputPorts, OutputPorts, ExternalInputs, NormalLinks;
        internal readonly GraphicsBuffer States, Buffers, Gaps, Blocks, Items, Speeds;
        internal readonly GraphicsBuffer Reservations, ExternalReady, ExternalSuccess, NormalStates, Events;

        internal GpuBeltBuffers(GpuBeltLayout layout, GpuBeltInitialState initial, int eventCapacity)
        {
            Topology = Create(layout.Topology.Length, 32);
            InputPorts = Create(layout.InputPorts.Length, 16);
            OutputPorts = Create(layout.OutputPorts.Length, 16);
            ExternalInputs = Create(layout.ExternalInputs.Length, 16);
            NormalLinks = Create(layout.NormalLinks.Length, 16);
            States = Create(initial.States.Length, 16);
            Buffers = Create(initial.Buffers.Length, 16);
            Gaps = Create(initial.Gaps.Length, 4);
            Blocks = Create(initial.Blocks.Length, 4);
            Items = Create(initial.Items.Length, 4);
            Speeds = Create(initial.Speeds.Length, 4);
            Reservations = Create(layout.Topology.Length, 4);
            ExternalReady = Create(layout.ExternalInputs.Length, 4);
            ExternalSuccess = Create(layout.OutputCount, 4);
            NormalStates = Create(layout.NormalLinks.Length, 16);
            Events = Create(eventCapacity, 16);

            // 不変配線と復元状態はowner生成時にだけ転送する。
            // Upload immutable wiring and restored state only at owner construction.
            Upload(Topology, layout.Topology);
            Upload(InputPorts, layout.InputPorts);
            Upload(OutputPorts, layout.OutputPorts);
            Upload(ExternalInputs, layout.ExternalInputs);
            Upload(NormalLinks, layout.NormalLinks);
            Upload(States, initial.States);
            Upload(Buffers, initial.Buffers);
            Upload(Gaps, initial.Gaps);
            Upload(Blocks, initial.Blocks);
            Upload(Items, initial.Items);
            Upload(Speeds, initial.Speeds);
            int[] reservations = new int[layout.Topology.Length];
            Array.Fill(reservations, -1);
            Upload(Reservations, reservations);
            Upload(ExternalReady, new int[layout.ExternalInputs.Length]);
            Upload(ExternalSuccess, new int[layout.OutputCount]);
            Upload(NormalStates, new GpuBeltNormalState[layout.NormalLinks.Length]);
        }

        static GraphicsBuffer Create(int count, int stride)
            => new GraphicsBuffer(GraphicsBuffer.Target.Structured, Math.Max(1, count), stride);

        static void Upload<T>(GraphicsBuffer buffer, T[] values) where T : struct
        {
            if (values.Length != 0) buffer.SetData(values);
        }

        public void Dispose()
        {
            Topology.Dispose(); InputPorts.Dispose(); OutputPorts.Dispose(); ExternalInputs.Dispose();
            NormalLinks.Dispose(); States.Dispose(); Buffers.Dispose(); Gaps.Dispose(); Blocks.Dispose();
            Items.Dispose(); Speeds.Dispose(); Reservations.Dispose(); ExternalReady.Dispose();
            ExternalSuccess.Dispose(); NormalStates.Dispose(); Events.Dispose();
        }
    }
}
