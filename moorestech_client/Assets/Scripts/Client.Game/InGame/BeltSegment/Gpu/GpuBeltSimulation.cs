using System;
using Game.BeltSegment;
using UnityEngine;

namespace Client.Game.InGame.BeltSegment.Gpu
{
    internal sealed class GpuBeltSimulation : IDisposable
    {
        internal readonly GpuBeltBuffers Buffers;
        readonly ComputeShader shader;
        readonly GpuBeltTickUpload upload;
        readonly int segmentCount, inputCount, outputCount, normalLinkCount;
        readonly (int Id, int ThreadCountX) clearExternal, applyExternal, captureNormalOffers, collect;
        readonly (int Id, int ThreadCountX) reserve, transfer, advanceNormal, commitNormal, insertExternal;
        bool disposed;

        internal GpuBeltSimulation(BeltReplaySnapshot snapshot, ComputeShader source)
        {
            var layout = new GpuBeltLayout(snapshot);
            var initial = new GpuBeltInitialState(snapshot, layout);
            segmentCount = layout.Topology.Length;
            inputCount = layout.ExternalInputs.Length;
            outputCount = layout.OutputCount;
            normalLinkCount = layout.NormalLinks.Length;
            upload = new GpuBeltTickUpload(inputCount, outputCount, segmentCount);
            bool initialized = false;
            try
            {
                shader = UnityEngine.Object.Instantiate(source);
                Buffers = new GpuBeltBuffers(layout, initial, upload.Events.Length);
                clearExternal = FindKernel("ClearExternal");
                shader.SetBuffer(clearExternal.Id, "_ExternalReady", Buffers.ExternalReady);
                shader.SetBuffer(clearExternal.Id, "_ExternalSuccess", Buffers.ExternalSuccess);

                applyExternal = FindKernel("ApplyExternal");
                shader.SetBuffer(applyExternal.Id, "_Events", Buffers.Events);
                shader.SetBuffer(applyExternal.Id, "_ExternalReady", Buffers.ExternalReady);
                shader.SetBuffer(applyExternal.Id, "_ExternalSuccess", Buffers.ExternalSuccess);
                shader.SetBuffer(applyExternal.Id, "_Speeds", Buffers.Speeds);

                captureNormalOffers = FindKernel("CaptureNormalOffers");
                shader.SetBuffer(captureNormalOffers.Id, "_Topology", Buffers.Topology);
                shader.SetBuffer(captureNormalOffers.Id, "_States", Buffers.States);
                shader.SetBuffer(captureNormalOffers.Id, "_Reservations", Buffers.Reservations);
                shader.SetBuffer(captureNormalOffers.Id, "_NormalLinks", Buffers.NormalLinks);
                shader.SetBuffer(captureNormalOffers.Id, "_NormalStates", Buffers.NormalStates);

                collect = FindKernel("Collect");
                BindQueue(collect.Id);
                shader.SetBuffer(collect.Id, "_Buffers", Buffers.Buffers);
                shader.SetBuffer(collect.Id, "_Speeds", Buffers.Speeds);

                reserve = FindKernel("Reserve");
                shader.SetBuffer(reserve.Id, "_Topology", Buffers.Topology);
                shader.SetBuffer(reserve.Id, "_States", Buffers.States);
                shader.SetBuffer(reserve.Id, "_Buffers", Buffers.Buffers);
                shader.SetBuffer(reserve.Id, "_Gaps", Buffers.Gaps);
                shader.SetBuffer(reserve.Id, "_Speeds", Buffers.Speeds);
                shader.SetBuffer(reserve.Id, "_InputPorts", Buffers.InputPorts);
                shader.SetBuffer(reserve.Id, "_OutputPorts", Buffers.OutputPorts);
                shader.SetBuffer(reserve.Id, "_ExternalReady", Buffers.ExternalReady);
                shader.SetBuffer(reserve.Id, "_Reservations", Buffers.Reservations);

                transfer = FindKernel("Transfer");
                BindQueue(transfer.Id);
                shader.SetBuffer(transfer.Id, "_Buffers", Buffers.Buffers);
                shader.SetBuffer(transfer.Id, "_Speeds", Buffers.Speeds);
                shader.SetBuffer(transfer.Id, "_Reservations", Buffers.Reservations);
                shader.SetBuffer(transfer.Id, "_OutputPorts", Buffers.OutputPorts);
                shader.SetBuffer(transfer.Id, "_ExternalSuccess", Buffers.ExternalSuccess);

                advanceNormal = FindKernel("AdvanceNormal");
                BindQueue(advanceNormal.Id);
                shader.SetBuffer(advanceNormal.Id, "_Speeds", Buffers.Speeds);
                shader.SetBuffer(advanceNormal.Id, "_Reservations", Buffers.Reservations);
                shader.SetBuffer(advanceNormal.Id, "_OutputPorts", Buffers.OutputPorts);
                shader.SetBuffer(advanceNormal.Id, "_ExternalSuccess", Buffers.ExternalSuccess);
                shader.SetBuffer(advanceNormal.Id, "_NormalStates", Buffers.NormalStates);

                commitNormal = FindKernel("CommitNormal");
                BindQueue(commitNormal.Id);
                shader.SetBuffer(commitNormal.Id, "_Reservations", Buffers.Reservations);
                shader.SetBuffer(commitNormal.Id, "_NormalLinks", Buffers.NormalLinks);
                shader.SetBuffer(commitNormal.Id, "_NormalStates", Buffers.NormalStates);

                insertExternal = FindKernel("InsertExternal");
                BindQueue(insertExternal.Id);
                shader.SetBuffer(insertExternal.Id, "_Reservations", Buffers.Reservations);
                shader.SetBuffer(insertExternal.Id, "_Events", Buffers.Events);
                shader.SetBuffer(insertExternal.Id, "_ExternalInputs", Buffers.ExternalInputs);
                shader.SetInt("_SegmentCount", segmentCount);
                shader.SetInt("_InputCount", inputCount);
                shader.SetInt("_OutputCount", outputCount);
                shader.SetInt("_NormalLinkCount", normalLinkCount);
                initialized = true;
            }
            finally
            {
                if (!initialized)
                {
                    Buffers?.Dispose();
                    DestroyShader(shader);
                }
            }

            #region Internal

            (int Id, int ThreadCountX) FindKernel(string name)
            {
                int kernel = shader.FindKernel(name);
                shader.GetKernelThreadGroupSizes(kernel, out uint threadCountX, out _, out _);
                return (kernel, (int)threadCountX);
            }

            void BindQueue(int kernel)
            {
                shader.SetBuffer(kernel, "_Topology", Buffers.Topology);
                shader.SetBuffer(kernel, "_States", Buffers.States);
                shader.SetBuffer(kernel, "_Gaps", Buffers.Gaps);
                shader.SetBuffer(kernel, "_Blocks", Buffers.Blocks);
                shader.SetBuffer(kernel, "_Items", Buffers.Items);
            }

            #endregion
        }

        internal void ApplyTick(BeltReplayTick tick)
        {
            int count = upload.Prepare(tick);
            if (count != 0) Buffers.Events.SetData(upload.Events, 0, 0, count);
            shader.SetInt("_EventCount", count);
            // 有効graphではNormal/Branch入力は単一、Merge入力は予約方向のみ。
            // Valid graphs have one Normal/Branch input and one reserved Merge direction.
            // bufferと走行列を分離し、Normal間だけ別dispatchで搬入する。
            // Buffers own separate state; Normal links commit in a separate dispatch.
            Dispatch(clearExternal, Math.Max(inputCount, outputCount));
            Dispatch(applyExternal, count);
            Dispatch(captureNormalOffers, normalLinkCount);
            Dispatch(collect, segmentCount);
            Dispatch(reserve, segmentCount);
            Dispatch(transfer, segmentCount);
            Dispatch(advanceNormal, segmentCount);
            Dispatch(commitNormal, normalLinkCount);
            Dispatch(insertExternal, count);

            #region Internal

            void Dispatch((int Id, int ThreadCountX) kernel, int logicalCount)
            {
                int maxThreads = 65535 * kernel.ThreadCountX;
                for (int offset = 0; offset < logicalCount; offset += maxThreads)
                {
                    int groups = (Math.Min(logicalCount - offset, maxThreads) + kernel.ThreadCountX - 1)
                        / kernel.ThreadCountX;
                    shader.SetInt("_DispatchOffset", offset);
                    shader.Dispatch(kernel.Id, groups, 1, 1);
                }
            }

            #endregion
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Buffers.Dispose();
            DestroyShader(shader);
        }

#if UNITY_EDITOR
        static void DestroyShader(ComputeShader shader)
        {
            if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(shader);
            else UnityEngine.Object.Destroy(shader);
        }
#else
        static void DestroyShader(ComputeShader shader) => UnityEngine.Object.Destroy(shader);
#endif
    }
}
