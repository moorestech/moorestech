using System;
using Client.Game.InGame.BeltSegment.Gpu;
using UnityEngine;
namespace Client.Game.InGame.BeltSegment.Rendering
{
    internal sealed class BeltDrawDispatch : IDisposable
    {
        private readonly ComputeShader shader;
        private readonly int clear, build, prefix, scatter, capacity, segments, kinds;
        internal BeltDrawDispatch(ComputeShader source, GpuBeltBuffers simulation, BeltDrawBuffers draw,
            int capacity, int segments, int kinds, Vector3 centerOffset)
        {
            this.capacity = capacity; this.segments = segments; this.kinds = kinds;
            shader = UnityEngine.Object.Instantiate(source);
            bool initialized = false;
            try
            {
            clear = shader.FindKernel("Clear"); build = shader.FindKernel("BuildPositions");
            prefix = shader.FindKernel("PrefixKinds"); scatter = shader.FindKernel("Scatter");
            shader.SetInt("_Capacity", capacity); shader.SetInt("_SegmentCount", segments); shader.SetInt("_KindCount", kinds);
            shader.SetVector("_CenterOffset", centerOffset);
            shader.SetBuffer(clear, "_RawPositions", draw.RawPositions);
            shader.SetBuffer(clear, "_Counts", draw.Counts); shader.SetBuffer(clear, "_Cursors", draw.Cursors);
            // 確定走行列を直接参照。
            // Read accepted queue buffers directly.
            shader.SetBuffer(build, "_Topology", simulation.Topology); shader.SetBuffer(build, "_States", simulation.States);
            shader.SetBuffer(build, "_Gaps", simulation.Gaps); shader.SetBuffer(build, "_Items", simulation.Items);
            shader.SetBuffer(build, "_Cells", draw.Cells); shader.SetBuffer(build, "_Entries", draw.Entries);
            shader.SetBuffer(build, "_Routes", draw.Routes); shader.SetBuffer(build, "_Kinds", draw.Kinds);
            shader.SetBuffer(build, "_RawPositions", draw.RawPositions); shader.SetBuffer(build, "_Counts", draw.Counts);
            shader.SetBuffer(prefix, "_Counts", draw.Counts); shader.SetBuffer(prefix, "_Offsets", draw.Offsets);
            shader.SetBuffer(prefix, "_Arguments", draw.Arguments);
            shader.SetBuffer(scatter, "_RawPositions", draw.RawPositions); shader.SetBuffer(scatter, "_Positions", draw.Positions);
            shader.SetBuffer(scatter, "_Offsets", draw.Offsets); shader.SetBuffer(scatter, "_Cursors", draw.Cursors);
            initialized = true;
            }
            finally { if (!initialized) BeltItemMaterials.DestroyResource(shader); }
        }
        internal void Recompute()
        {
            Dispatch(clear, Math.Max(capacity, kinds), false);
            Dispatch(build, segments, true);
            Dispatch(prefix, 1, false);
            Dispatch(scatter, capacity, false);

            #region Internal
            void Dispatch(int kernel, int count, bool oneGroupPerElement)
            {
                shader.GetKernelThreadGroupSizes(kernel, out uint threads, out _, out _);
                int width = oneGroupPerElement ? 1 : (int)threads;
                int limit = 65535 * width;
                for (int offset = 0; offset < count; offset += limit)
                {
                    shader.SetInt("_DispatchOffset", offset);
                    shader.Dispatch(kernel, (Math.Min(count - offset, limit) + width - 1) / width, 1, 1);
                }
            }
            #endregion
        }

        public void Dispose() => BeltItemMaterials.DestroyResource(shader);
    }
}
