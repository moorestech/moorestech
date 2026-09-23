using System;
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
        private readonly Subject<BeltWorldSnapshot> rebuilt = new();
        private readonly Subject<Unit> advanced = new();
        private readonly Subject<string> recoveryRequested = new();
        private readonly Subject<Exception> failed = new();
        private BeltReplaySimulation cpu;
        private BeltReplaySnapshot topology;
        internal IObservable<BeltWorldSnapshot> OnRebuilt => rebuilt;
        internal IObservable<Unit> OnAdvanced => advanced;
        internal IObservable<string> OnRecoveryRequested => recoveryRequested;
        internal IObservable<Exception> OnFailed => failed;
        internal BeltStreamStatus Status { get; private set; } = BeltStreamStatus.WaitingSnapshot;
        internal string RecoveryError { get; private set; }
        internal GpuBeltSimulation Simulation { get; private set; }
        internal BeltRoute[] Routes { get; private set; } = Array.Empty<BeltRoute>();
        internal BeltStreamPosition Position { get; private set; }
        internal ulong Generation { get; private set; }
        internal ClientBeltWorld(ComputeShader shader) => this.shader = shader;
        internal void ReceiveSnapshot(BeltWorldSnapshot snapshot)
        {
            if (cpu != null && (snapshot.Generation < Generation || BeltFrameBuffer.Compare(snapshot.Position, Position) < 0 ||
                (snapshot.Generation == Generation && BeltFrameBuffer.Compare(snapshot.Position, Position) == 0 && Status == BeltStreamStatus.Running)))
            { Debug.Log("[BeltWorld] Discarded covered snapshot."); return; }
            GpuBeltSimulation replacement = null;
            // ネットワーク状態の構築・適用境界。両方の構築成功まで旧所有を保つ。
            // Network-state apply boundary: retain old ownership until both constructions succeed.
            try
            {
                var nextCpu = new BeltReplaySimulation(snapshot.Simulation);
                replacement = new GpuBeltSimulation(snapshot.Simulation, shader);
                var old = Simulation;
                cpu = nextCpu; Simulation = replacement; replacement = null;
                topology = snapshot.Simulation; Routes = snapshot.Routes;
                Position = snapshot.Position; Generation = snapshot.Generation;
                Status = BeltStreamStatus.Running; RecoveryError = null;
                old?.Dispose();
                frames.DiscardCovered(Generation, Position);
                rebuilt.OnNext(snapshot);
                Drain();
            }
            catch (Exception exception) { Fail(exception); }
            finally { replacement?.Dispose(); }
        }
        internal void ReceiveFrame(BeltWorldFrame frame)
        {
            if (cpu != null && (frame.Generation < Generation || BeltFrameBuffer.Compare(frame.Position, Position) <= 0))
            { Debug.Log("[BeltWorld] Discarded covered frame."); return; }
            if (!frames.Add(frame)) { Recover("Frame buffer exceeded 256 entries."); return; }
            if (Status == BeltStreamStatus.Running) Drain();
        }
        private void Drain()
        {
            // 確定位置に直接連なるframeだけをCPU→GPUの順で一度適用する。
            // Apply only directly chained frames, once each, in CPU-then-GPU order.
            try
            {
                while (Status == BeltStreamStatus.Running && frames.TryTake(Generation, Position, out var frame))
                {
                    BeltWireCodec.ValidateFrame(frame, topology);
                    if (frame.PreviousHash != cpu.ComputeStateHash()) { Recover("Previous state hash mismatch."); return; }
                    cpu.ApplyTick(frame.Replay, false);
                    Simulation.ApplyTick(frame.Replay);
                    Position = frame.Position;
                    advanced.OnNext(Unit.Default);
                }
                if (frames.HasPending) Recover("Missing frame chain or replacement generation.");
            }
            catch (Exception exception) { Fail(exception); }
        }
        internal void Recover(string reason)
        {
            Status = BeltStreamStatus.Recovering; RecoveryError = reason;
            Debug.LogWarning($"[BeltWorld] Recovering: {reason}");
            recoveryRequested.OnNext(reason);
        }
        internal void Fail(Exception exception)
        {
            Debug.LogError($"[BeltWorld] Network state apply failed: {exception}");
            failed.OnNext(exception);
            Recover(exception.Message);
        }
#if UNITY_EDITOR
        internal BeltReplaySnapshot CaptureCpuState() => cpu.CaptureSnapshot();
#endif
    }
}
