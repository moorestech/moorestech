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
        readonly int clearExternal, applyExternal, captureNormalOffers, collect;
        readonly int reserve, transfer, advanceNormal, commitNormal, insertExternal;
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
            shader = UnityEngine.Object.Instantiate(source);
            Buffers = new GpuBeltBuffers(layout, initial, upload.Events.Length);
            clearExternal = shader.FindKernel("ClearExternal");
            shader.SetBuffer(clearExternal, "_ExternalReady", Buffers.ExternalReady);
            shader.SetBuffer(clearExternal, "_ExternalSuccess", Buffers.ExternalSuccess);

            applyExternal = shader.FindKernel("ApplyExternal");
            shader.SetBuffer(applyExternal, "_Events", Buffers.Events);
            shader.SetBuffer(applyExternal, "_ExternalReady", Buffers.ExternalReady);
            shader.SetBuffer(applyExternal, "_ExternalSuccess", Buffers.ExternalSuccess);
            shader.SetBuffer(applyExternal, "_Speeds", Buffers.Speeds);

            captureNormalOffers = shader.FindKernel("CaptureNormalOffers");
            shader.SetBuffer(captureNormalOffers, "_Topology", Buffers.Topology);
            shader.SetBuffer(captureNormalOffers, "_States", Buffers.States);
            shader.SetBuffer(captureNormalOffers, "_Reservations", Buffers.Reservations);
            shader.SetBuffer(captureNormalOffers, "_NormalLinks", Buffers.NormalLinks);
            shader.SetBuffer(captureNormalOffers, "_NormalStates", Buffers.NormalStates);

            collect = shader.FindKernel("Collect");
            BindQueue(collect);
            shader.SetBuffer(collect, "_Buffers", Buffers.Buffers);
            shader.SetBuffer(collect, "_Speeds", Buffers.Speeds);

            reserve = shader.FindKernel("Reserve");
            shader.SetBuffer(reserve, "_Topology", Buffers.Topology);
            shader.SetBuffer(reserve, "_States", Buffers.States);
            shader.SetBuffer(reserve, "_Buffers", Buffers.Buffers);
            shader.SetBuffer(reserve, "_Gaps", Buffers.Gaps);
            shader.SetBuffer(reserve, "_Speeds", Buffers.Speeds);
            shader.SetBuffer(reserve, "_InputPorts", Buffers.InputPorts);
            shader.SetBuffer(reserve, "_OutputPorts", Buffers.OutputPorts);
            shader.SetBuffer(reserve, "_ExternalReady", Buffers.ExternalReady);
            shader.SetBuffer(reserve, "_Reservations", Buffers.Reservations);

            transfer = shader.FindKernel("Transfer");
            BindQueue(transfer);
            shader.SetBuffer(transfer, "_Buffers", Buffers.Buffers);
            shader.SetBuffer(transfer, "_Speeds", Buffers.Speeds);
            shader.SetBuffer(transfer, "_Reservations", Buffers.Reservations);
            shader.SetBuffer(transfer, "_OutputPorts", Buffers.OutputPorts);
            shader.SetBuffer(transfer, "_ExternalSuccess", Buffers.ExternalSuccess);

            advanceNormal = shader.FindKernel("AdvanceNormal");
            BindQueue(advanceNormal);
            shader.SetBuffer(advanceNormal, "_Speeds", Buffers.Speeds);
            shader.SetBuffer(advanceNormal, "_Reservations", Buffers.Reservations);
            shader.SetBuffer(advanceNormal, "_OutputPorts", Buffers.OutputPorts);
            shader.SetBuffer(advanceNormal, "_ExternalSuccess", Buffers.ExternalSuccess);
            shader.SetBuffer(advanceNormal, "_NormalStates", Buffers.NormalStates);

            commitNormal = shader.FindKernel("CommitNormal");
            BindQueue(commitNormal);
            shader.SetBuffer(commitNormal, "_Reservations", Buffers.Reservations);
            shader.SetBuffer(commitNormal, "_NormalLinks", Buffers.NormalLinks);
            shader.SetBuffer(commitNormal, "_NormalStates", Buffers.NormalStates);

            insertExternal = shader.FindKernel("InsertExternal");
            BindQueue(insertExternal);
            shader.SetBuffer(insertExternal, "_Reservations", Buffers.Reservations);
            shader.SetBuffer(insertExternal, "_Events", Buffers.Events);
            shader.SetBuffer(insertExternal, "_ExternalInputs", Buffers.ExternalInputs);
            shader.SetInt("_SegmentCount", segmentCount);
            shader.SetInt("_InputCount", inputCount);
            shader.SetInt("_OutputCount", outputCount);
            shader.SetInt("_NormalLinkCount", normalLinkCount);
        }

        void BindQueue(int kernel)
        {
            shader.SetBuffer(kernel, "_Topology", Buffers.Topology);
            shader.SetBuffer(kernel, "_States", Buffers.States);
            shader.SetBuffer(kernel, "_Gaps", Buffers.Gaps);
            shader.SetBuffer(kernel, "_Blocks", Buffers.Blocks);
            shader.SetBuffer(kernel, "_Items", Buffers.Items);
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
        }

        void Dispatch(int kernel, int logicalCount)
        {
            const int maxThreads = 65535 * 64;
            for (int offset = 0; offset < logicalCount; offset += maxThreads)
            {
                int groups = (Math.Min(logicalCount - offset, maxThreads) + 63) / 64;
                shader.SetInt("_DispatchOffset", offset);
                shader.Dispatch(kernel, groups, 1, 1);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Buffers.Dispose();
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEngine.Object.DestroyImmediate(shader);
            else UnityEngine.Object.Destroy(shader);
#else
            UnityEngine.Object.Destroy(shader);
#endif
        }
    }
}
