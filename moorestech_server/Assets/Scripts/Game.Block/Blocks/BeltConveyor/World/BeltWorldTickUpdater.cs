using System;
using System.Collections.Generic;
using Game.BeltSegment;
namespace Game.Block.Blocks.BeltConveyor
{
    internal sealed class BeltWorldTickUpdater
    {
        private BeltStreamPosition _previous;
        private uint _previousHash;
        private int[] _ready;
        private ulong _tick;
        private readonly List<BeltReplayInsertion> _insertions = new();
        internal bool InWindow { get; private set; }
        internal void Begin(ulong tick, BeltStreamPosition previous, BeltSimulationGraph graph, BeltMachinePortTable ports)
        {
            if (InWindow || tick <= previous.Tick) throw new InvalidOperationException("Belt physical ticks must advance once.");
            _tick = tick; _previous = previous; _previousHash = graph.ComputeStateHash();
            _insertions.Clear(); _ready = ports.Freeze();
            graph.Tick(false);
            InWindow = true;
        }
        internal void Insert(BeltReplayInsertion insertion) => _insertions.Add(insertion);
        internal BeltWorldFrame Complete(ulong generation, BeltMachinePortTable ports)
        {
            if (!InWindow) throw new InvalidOperationException("No belt tick is open.");
            InWindow = false;
            return new BeltWorldFrame(_previous, new BeltStreamPosition(_tick, 1), generation, _previousHash,
                new BeltReplayTick(Array.Empty<BeltReplaySpeedChange>(), _ready, ports.Consumed(), _insertions.ToArray()));
        }
    }
}
