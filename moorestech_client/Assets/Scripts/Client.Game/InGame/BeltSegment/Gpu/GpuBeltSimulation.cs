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
            clearExternal = Kernel("ClearExternal", b =>
            {
                b("_ExternalReady", Buffers.ExternalReady); b("_ExternalSuccess", Buffers.ExternalSuccess);
            });
            applyExternal = Kernel("ApplyExternal", b =>
            {
                b("_Events", Buffers.Events); b("_ExternalReady", Buffers.ExternalReady);
                b("_ExternalSuccess", Buffers.ExternalSuccess); b("_Speeds", Buffers.Speeds);
            });
            captureNormalOffers = Kernel("CaptureNormalOffers", b =>
            {
                b("_Topology", Buffers.Topology); b("_States", Buffers.States);
                b("_Reservations", Buffers.Reservations); b("_NormalLinks", Buffers.NormalLinks);
                b("_NormalStates", Buffers.NormalStates);
            });
            collect = Kernel("Collect", b =>
            {
                BindQueue(b); b("_Buffers", Buffers.Buffers); b("_Speeds", Buffers.Speeds);
            });
            reserve = Kernel("Reserve", b =>
            {
                b("_Topology", Buffers.Topology); b("_States", Buffers.States);
                b("_Buffers", Buffers.Buffers); b("_Gaps", Buffers.Gaps);
                b("_Speeds", Buffers.Speeds); b("_InputPorts", Buffers.InputPorts);
                b("_OutputPorts", Buffers.OutputPorts); b("_ExternalReady", Buffers.ExternalReady);
                b("_Reservations", Buffers.Reservations);
            });
            transfer = Kernel("Transfer", b =>
            {
                BindQueue(b); b("_Buffers", Buffers.Buffers); b("_Speeds", Buffers.Speeds);
                b("_Reservations", Buffers.Reservations); b("_OutputPorts", Buffers.OutputPorts);
                b("_ExternalSuccess", Buffers.ExternalSuccess);
            });
            advanceNormal = Kernel("AdvanceNormal", b =>
            {
                BindQueue(b); b("_Speeds", Buffers.Speeds); b("_Reservations", Buffers.Reservations);
                b("_OutputPorts", Buffers.OutputPorts); b("_ExternalSuccess", Buffers.ExternalSuccess);
                b("_NormalStates", Buffers.NormalStates);
            });
            commitNormal = Kernel("CommitNormal", b =>
            {
                BindQueue(b); b("_Reservations", Buffers.Reservations);
                b("_NormalLinks", Buffers.NormalLinks); b("_NormalStates", Buffers.NormalStates);
            });
            insertExternal = Kernel("InsertExternal", b =>
            {
                BindQueue(b); b("_Reservations", Buffers.Reservations);
                b("_Events", Buffers.Events); b("_ExternalInputs", Buffers.ExternalInputs);
            });
            shader.SetInt("_SegmentCount", segmentCount);
            shader.SetInt("_InputCount", inputCount);
            shader.SetInt("_OutputCount", outputCount);
            shader.SetInt("_NormalLinkCount", normalLinkCount);
        }

        int Kernel(string name, Action<Action<string, GraphicsBuffer>> bind)
        {
            int id = shader.FindKernel(name);
            bind((property, buffer) => shader.SetBuffer(id, property, buffer));
            return id;
        }

        void BindQueue(Action<string, GraphicsBuffer> bind)
        {
            bind("_Topology", Buffers.Topology); bind("_States", Buffers.States);
            bind("_Gaps", Buffers.Gaps); bind("_Blocks", Buffers.Blocks); bind("_Items", Buffers.Items);
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
