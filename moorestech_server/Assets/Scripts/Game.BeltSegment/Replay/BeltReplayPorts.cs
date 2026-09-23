using System;
using System.Collections.Generic;

namespace Game.BeltSegment
{
    internal sealed class BeltReplayPorts
    {
        private readonly Source[] sourcePorts;
        private readonly Receiver[] receiverPorts;
        internal readonly IReadOnlyList<IBeltSource> Sources;
        internal readonly IReadOnlyList<IBeltReceiver> Receivers;

        internal BeltReplayPorts(int inputCount, int outputCount)
        {
            sourcePorts = new Source[inputCount];
            receiverPorts = new Receiver[outputCount];
            for (int i = 0; i < inputCount; i++) sourcePorts[i] = new Source();
            for (int i = 0; i < outputCount; i++) receiverPorts[i] = new Receiver();
            Sources = sourcePorts;
            Receivers = receiverPorts;
        }

        internal void Prepare(BeltReplayTick tick)
        {
            // 記録にない候補を毎tick不可へ戻し、前tickの成功を持ち越さない。
            // Reset absent candidates every tick so previous successes cannot carry over.
            foreach (var source in sourcePorts) source.SetReady(false);
            foreach (var receiver in receiverPorts) receiver.Prepare();
            foreach (int id in tick.ReadyInputs) sourcePorts[id].SetReady(true);
            foreach (int id in tick.SuccessfulOutputs) receiverPorts[id].Enable();
        }

        internal void VerifyOutputs(BeltReplayTick tick)
        {
            foreach (int id in tick.SuccessfulOutputs)
                if (!receiverPorts[id].Consumed)
                    throw new InvalidOperationException($"Recorded external output {id} was not reproduced.");
        }

        private sealed class Source : IBeltSource
        {
            private bool ready;
            internal void SetReady(bool value) => ready = value;
            public bool TryGetOutput(BeltDirection inputDirection) => ready;
        }

        private sealed class Receiver : IBeltReceiver
        {
            private bool expected;
            internal bool Consumed { get; private set; }
            internal void Prepare()
            {
                expected = false;
                Consumed = false;
            }
            internal void Enable() => expected = true;
            public void AttachInput(IBeltSource source, BeltDirection inputDirection) { }
            public int GetOffer(BeltDirection inputDirection) => expected && !Consumed ? BeltConstants.ItemWidth : 0;
            public bool TryReceive(BeltDirection inputDirection, int length, in BeltItem item)
            {
                // 通常拒否と、確定結果の二重消費を区別する。
                // Distinguish ordinary rejection from consuming a recorded result twice.
                if (!expected) return false;
                if (Consumed) throw new InvalidOperationException("An external output was consumed twice in one tick.");
                Consumed = true;
                return true;
            }
        }
    }
}
