using System;
using System.Collections.Generic;
using Core.BeltTransport;
using NUnit.Framework;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.BeltTransport
{
    internal static class BeltTransportTestFactory
    {
        internal static BeltTransportNetwork CreateNetwork((int Capacity, int Speed)[] paths,
            (int Source, int Target, BeltDirection Direction)[] links)
        {
            var cells = new List<BeltNetworkCell>();
            var edges = new List<BeltNetworkConnection>();
            // 設定速度の異なる経路を実際のセル列から構築する。
            // Build real cell chains with distinct configured speed profiles.
            for (int path = 0; path < paths.Length; path++)
            for (int cell = 0; cell < paths[path].Capacity; cell++)
            {
                int id = path * 100 + cell + 1;
                cells.Add(new BeltNetworkCell(id, path, 0, cell, paths[path].Speed, $"profile-{path}",
                    BeltDirection.Front, new BeltCellSurfaceProfile(0, 0)));
                if (cell != 0) edges.Add(new BeltNetworkConnection(id - 1, id, true, true, BeltDirection.Front, 0));
            }
            foreach (var link in links)
                edges.Add(new BeltNetworkConnection(link.Source * 100 + paths[link.Source].Capacity,
                    link.Target * 100 + 1, true, 0 <= link.Target, link.Direction, 0));
            var network = RestoreNetwork(new BeltNetworkSnapshot(cells.ToArray(), edges.ToArray(),
                Array.Empty<BeltCellItemState>(), Array.Empty<BeltCellPriority>()));
            return network;
        }

        internal static BeltTransportNetwork RestoreNetwork(BeltNetworkSnapshot snapshot)
        {
            var boundary = new BlockedBoundary();
            var network = new BeltTransportNetwork(boundary, boundary);
            network.Restore(snapshot);
            return network;
        }

        internal static void SetItems(BeltTransportNetwork network, BeltCellItemState[] items)
        {
            var snapshot = network.Capture();
            network.Restore(new BeltNetworkSnapshot(snapshot.Cells, snapshot.Connections, items, snapshot.Priorities));
        }
        internal static BeltCellItemState State(int cell, int progress, int identity, bool buffer) =>
            new BeltCellItemState(cell, progress, BeltDirection.Back, 0, Item(identity), buffer);
        internal static BeltItem Item(int instance) =>
            new BeltItem(new Guid(instance, 0, 0, new byte[8]), ForUnitTestItemId.ItemId1.AsPrimitive());

        // 分岐の閉じた外部搬出口を明示し、予期しない復元損失は失敗にする。
        // Model a blocked external branch outlet and fail on unexpected restoration loss.
        private sealed class BlockedBoundary : IBeltExternalReceiverFactory, IBeltReceiver, IBeltItemDropObserver
        {
            public IBeltReceiver Create(BeltNetworkConnection connection, int stage) => this;
            public int GetOffer(BeltDirection inputDirection) => 0;
            public bool TryReceive(BeltDirection inputDirection, int length, in BeltItem item) => false;
            public void OnDropped(BeltCellItemState item, string reason) => Assert.Fail(reason);
        }
    }
}
