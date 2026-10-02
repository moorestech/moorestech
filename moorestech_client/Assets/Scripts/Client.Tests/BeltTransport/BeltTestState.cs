using System;
using Core.BeltTransport;
using Client.Game.InGame.Train.Network;
using Client.Game.InGame.Train.Network.Diagnostics;
using Client.Game.InGame.Train.Unit;
using UnityEngine;
namespace Client.Tests.BeltTransport
{
    internal static class BeltTestState
    {
        internal static readonly Guid Identity = new("00000001-0000-0000-0000-000000000000");
        internal static BeltNetworkSnapshot Snapshot(int progress, int speed, bool hasItem)
        {
            var cells = new[] { new BeltNetworkCell(1, -2, 3, 4, speed, "fixed:1", BeltDirection.Front, new BeltCellSurfaceProfile(0, 0)) };
            var items = hasItem ? new[] { new BeltCellItemState(1, progress, BeltDirection.Back, 0, new BeltItem(Identity, 1), false) } : Array.Empty<BeltCellItemState>();
            return new BeltNetworkSnapshot(cells, Array.Empty<BeltNetworkConnection>(), items, new[] { new BeltCellPriority(1, 0) });
        }
        internal static BeltTickDifference Tick(ulong tick) => new(tick, Array.Empty<BeltBoundaryChange>(), Array.Empty<BeltOutputResult>(), Array.Empty<BeltBoundaryChange>());
        internal static TrainUnitFutureMessageBuffer Buffer(out TrainUnitTickState state)
        {
            state = new TrainUnitTickState();
            return new TrainUnitFutureMessageBuffer(state, new TrainSynchronizationDiagnostics(state, new TrainSynchronizationDiagnosticWriter(Application.temporaryCachePath)));
        }
        internal static void FlushTick(TrainUnitFutureMessageBuffer buffer, TrainUnitTickState state, uint tick)
        {
            state.RecordAppliedTickUnifiedId(tick, 0);
            while (buffer.TryFlushEvent(state.GetAppliedTickUnifiedId() + 1)) { }
        }
    }
}
