using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Client.Game.InGame.BeltSegment.Gpu;
using Game.BeltSegment;
using Server.Util.MessagePack.BeltSegment;
using UniRx;
using UnityEngine;
namespace Client.Game.InGame.BeltSegment.Model
{
    internal sealed class ClientBeltWorld
    {
        private readonly ComputeShader shader;
        private readonly BeltFrameBuffer frames = new();
        private readonly Subject<BeltWorldSnapshot> snapshotApplied = new();
        private readonly Subject<BeltReplayTick> tickApplied = new();
        private readonly Subject<string> recoveryRequested = new();
        private readonly Subject<Exception> applyFailed = new();
        private readonly UniTaskCompletionSource initial = new();
        private BeltReplaySimulation cpu;
        private BeltReplaySnapshot topology;
        internal IObservable<BeltWorldSnapshot> OnBeltWorldSnapshotApplied => snapshotApplied;
        internal IObservable<BeltReplayTick> OnBeltWorldTickApplied => tickApplied;
        internal IObservable<string> OnRecoveryRequested => recoveryRequested;
        internal IObservable<Exception> OnBeltWorldApplyFailed => applyFailed;
        internal BeltStreamStatus Status { get; private set; } = BeltStreamStatus.WaitingSnapshot;
        internal GpuBeltSimulation Simulation { get; private set; }
        internal BeltRoute[] Routes { get; private set; } = Array.Empty<BeltRoute>();
        internal BeltStreamPosition Position { get; private set; }
        internal ulong Generation { get; private set; }
        internal ClientBeltWorld(ComputeShader shader) => this.shader = shader;
        internal UniTask WaitForInitialApplyAsync() => initial.Task;
        internal void CancelInitialApply(CancellationToken token) => initial.TrySetCanceled(token);
        internal void ReceiveSnapshot(BeltWorldSnapshot snapshot)
        {
            if (Status == BeltStreamStatus.Failed) return;
            if (cpu != null && (snapshot.Generation < Generation || BeltFrameBuffer.Compare(snapshot.Position, Position) < 0 ||
                snapshot.Generation == Generation && BeltFrameBuffer.Compare(snapshot.Position, Position) == 0 && Status == BeltStreamStatus.Running)) return;
            GpuBeltSimulation replacement = null;
            bool applied = false;
            // 内部例外は再要求へ変換せず、finallyで非描画と初期待機の失敗を確定する。
            // Internal exceptions propagate; finally marks the world non-drawing and faults startup.
            try
            {
                var nextCpu = new BeltReplaySimulation(snapshot.Simulation);
                replacement = new GpuBeltSimulation(snapshot.Simulation, shader);
                var old = Simulation;
                cpu = nextCpu; Simulation = replacement; replacement = null;
                topology = snapshot.Simulation; Routes = snapshot.Routes;
                Position = snapshot.Position; Generation = snapshot.Generation;
                Status = BeltStreamStatus.Running;
                old?.Dispose();
                frames.DiscardCovered(Generation, Position);
                snapshotApplied.OnNext(snapshot);
                Drain();
                applied = true;
                if (Status == BeltStreamStatus.Running) initial.TrySetResult();
            }
            finally
            {
                try { replacement?.Dispose(); }
                finally { if (!applied) Fail(new InvalidOperationException("Local snapshot construction, GPU binding, or subscriber failed.")); }
            }
        }
        internal void ReceiveFrame(BeltWorldFrame frame)
        {
            if (Status == BeltStreamStatus.Failed) return;
            if (cpu != null && (frame.Generation < Generation || BeltFrameBuffer.Compare(frame.Position, Position) <= 0)) return;
            if (!frames.Add(frame)) { Recover("Frame buffer exceeded 256 entries."); return; }
            if (Status == BeltStreamStatus.Running) Drain();
        }
        private void Drain()
        {
            bool completed = false;
            // 明示拒否と内部例外を分け、CPU成功後だけGPUと購読者へ進める。
            // Separate explicit rejection from internal failures; update GPU and subscribers only after CPU success.
            try
            {
                while (Status == BeltStreamStatus.Running && frames.TryTake(Generation, Position, out var frame))
                {
                    if (!BeltWireCodec.TryValidateFrame(frame, topology, out var reason) ||
                        !cpu.TryApplyTick(frame.Replay, frame.PreviousHash, false, out reason))
                    { completed = true; Recover(reason); return; }
                    Simulation.ApplyTick(frame.Replay);
                    Position = frame.Position;
                    tickApplied.OnNext(frame.Replay);
                }
                if (frames.HasPending) Recover("Missing frame chain or replacement generation.");
                completed = true;
            }
            finally { if (!completed) Fail(new InvalidOperationException("Local CPU/GPU tick or subscriber failed.")); }
        }
        internal void Recover(string reason)
        {
            if (Status == BeltStreamStatus.Failed) return;
            Status = BeltStreamStatus.Recovering;
            Debug.LogWarning($"[BeltWorld] Recovering: {reason}");
            recoveryRequested.OnNext(reason);
        }
        private void Fail(Exception exception)
        {
            if (Status == BeltStreamStatus.Failed) return;
            Status = BeltStreamStatus.Failed;
            initial.TrySetException(exception);
            Debug.LogError($"[BeltWorld] Local state apply failed: {exception}");
            applyFailed.OnNext(exception);
        }
        internal BeltReplaySnapshot CaptureCpuState() => cpu.CaptureSnapshot();
    }
}
