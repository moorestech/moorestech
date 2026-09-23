using System;
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.BeltSegment.Model;
using Client.Game.InGame.BeltSegment.Network;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.BeltSegment;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UniRx;
namespace Client.Tests.BeltSegment.Network
{
    internal static class BeltNetworkFixture
    {
        internal static void LoadMaster() => new MoorestechServerDIContainerGenerator().Create(
            new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        internal static ClientBeltWorld World() => new(Resources.Load<ComputeShader>("BeltSegment/BeltGpuReplay"));
        internal static BeltReplayTick EmptyTick() => new(Array.Empty<BeltReplaySpeedChange>(), Array.Empty<int>(),
            Array.Empty<int>(), Array.Empty<BeltReplayInsertion>());
        internal static BeltWorldSnapshot Empty(ulong tick, uint sequence, ulong generation) => new(new(tick, sequence), generation,
            new(Array.Empty<BeltReplaySegmentState>(), Array.Empty<BeltReplayLink>(), Array.Empty<BeltReplayInput>(), Array.Empty<BeltReplayOutput>()),
            Array.Empty<BeltRoute>());
        internal static BeltWorldSnapshot Single(ulong tick, uint sequence, ulong generation)
        {
            var item = new BeltItem { Guid = Guid.NewGuid(), ItemId = ForUnitTestItemId.ItemId1.AsPrimitive(), AcceptedInput = BeltDirection.Left };
            var route = new BeltRoute(new[] { new BeltRouteCell(new(0, 0, 0), BeltEntryDirection.FromBack, 0, 0) }, new BeltRouteCell[4]);
            return new(new(tick, sequence), generation, new(new[] { BeltReplaySegmentState.Normal(1, 16, new[] { new BeltItemState(item, 240) }) },
                Array.Empty<BeltReplayLink>(), new[] { new BeltReplayInput(0, BeltDirection.Back) }, Array.Empty<BeltReplayOutput>()), new[] { route });
        }
        internal static BeltWorldFrame Frame(BeltWorldSnapshot snapshot, BeltReplayTick tick) => new(snapshot.Position,
            new(snapshot.Position.Tick + 1, 1), snapshot.Generation, new BeltReplaySimulation(snapshot.Simulation).ComputeStateHash(), tick);
    }
    internal sealed class ControlledBeltRequester : IBeltSnapshotRequester
    {
        internal readonly List<UniTaskCompletionSource<BeltWorldSnapshot>> Calls = new();
        public UniTask<BeltWorldSnapshot> Request(CancellationToken cancellationToken)
        {
            var source = new UniTaskCompletionSource<BeltWorldSnapshot>(); Calls.Add(source);
            return source.Task.AttachExternalCancellation(cancellationToken);
        }
    }
    internal sealed class BeltTestEvents : IVanillaApiEvent
    {
        private readonly Dictionary<string, Subject<byte[]>> events = new();
        public IDisposable SubscribeEventResponse(string tag, Action<byte[]> responseAction)
        {
            if (!events.TryGetValue(tag, out var subject)) events.Add(tag, subject = new());
            return subject.Subscribe(responseAction);
        }
        internal void Send(string tag, byte[] payload) => events[tag].OnNext(payload);
    }
}
