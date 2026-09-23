using System;
using System.Runtime.InteropServices;
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
            bool initialized = false;
            try
            {
                Topology = Create<GpuBeltTopology>(layout.Topology.Length);
                InputPorts = Create<GpuBeltPort>(layout.InputPorts.Length);
                OutputPorts = Create<GpuBeltPort>(layout.OutputPorts.Length);
                ExternalInputs = Create<GpuBeltPort>(layout.ExternalInputs.Length);
                NormalLinks = Create<GpuBeltNormalLink>(layout.NormalLinks.Length);
                States = Create<GpuBeltState>(initial.States.Length);
                Buffers = Create<GpuBeltBufferState>(initial.Buffers.Length);
                Gaps = Create<int>(initial.Gaps.Length);
                Blocks = Create<int>(initial.Blocks.Length);
                Items = Create<int>(initial.Items.Length);
                Speeds = Create<int>(initial.Speeds.Length);
                Reservations = Create<int>(layout.Topology.Length);
                ExternalReady = Create<int>(layout.ExternalInputs.Length);
                ExternalSuccess = Create<int>(layout.OutputCount);
                NormalStates = Create<GpuBeltNormalState>(layout.NormalLinks.Length);
                Events = Create<GpuBeltEvent>(eventCapacity);

                // 不変配線・復元状態は生成時のみ転送。
                // Upload immutable wiring and restored state only at construction.
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
                initialized = true;
            }
            finally
            {
                if (!initialized) Dispose();
            }

            #region Internal

            static GraphicsBuffer Create<T>(int count) where T : struct
                => new GraphicsBuffer(GraphicsBuffer.Target.Structured, Math.Max(1, count), Marshal.SizeOf<T>());

            static void Upload<T>(GraphicsBuffer buffer, T[] values) where T : struct
            {
                if (values.Length != 0) buffer.SetData(values);
            }

            #endregion
        }

        public void Dispose()
        {
            Topology?.Dispose(); InputPorts?.Dispose(); OutputPorts?.Dispose(); ExternalInputs?.Dispose();
            NormalLinks?.Dispose(); States?.Dispose(); Buffers?.Dispose(); Gaps?.Dispose(); Blocks?.Dispose();
            Items?.Dispose(); Speeds?.Dispose(); Reservations?.Dispose(); ExternalReady?.Dispose();
            ExternalSuccess?.Dispose(); NormalStates?.Dispose(); Events?.Dispose();
        }
    }
}
