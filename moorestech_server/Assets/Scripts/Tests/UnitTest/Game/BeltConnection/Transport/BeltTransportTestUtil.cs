using System.Linq;
using Core.BeltTransport;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Topology;
using Game.Block.Blocks.BeltConveyor.Topology.Layout;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Blocks.BeltConveyor.Transport.Rebuild;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.InventoryConnectsModule;
using NUnit.Framework;
using Tests.Module;
using Tests.UnitTest.Game.BeltConnection.Machine;
using UnityEngine;

namespace Tests.UnitTest.Game.BeltConnection.Transport
{
    internal static class BeltTransportTestUtil
    {
        internal static void InstallMachinePorts(Vector3Int[] outputDirections, Vector3Int[] inputDirections)
        {
            // 1マス機械(チェスト)に、方向ごとの出力・入力ポートを付ける。全機械が同じポートを持つ
            // Give the one-cell machine (chest) one port per direction; every machine shares the same ports
            var outputs = outputDirections.Select(d => MachinePortTestTemplate.Output(Vector3Int.zero, new[] { d }, null)).ToArray();
            var inputs = inputDirections.Select(d => MachinePortTestTemplate.Input(Vector3Int.zero, new[] { d }, null)).ToArray();
            MachinePortTestTemplate.Install(new InventoryConnects(inputs, outputs), Vector3Int.one);
        }

        internal static BeltTransportAssembly Assemble(IWorldBlockDatastore world)
        {
            var layouts = BeltSegmentLayoutBuilder.Build(BeltTopologyBuilder.Build(world));
            return BeltTransportAssembler.Assemble(layouts);
        }

        // 旧構成のアイテムを取り出し、現在のワールドから作り直した新構成へ復元する(BeltTransportDatastoreと同じ手順)
        // Capture the old assembly's items and restore them into a new assembly built from the current world, as BeltTransportDatastore does
        internal static BeltTransportAssembly Rebuild(BeltTransportAssembly old, IWorldBlockDatastore world)
        {
            var snapshot = BeltTransportSnapshot.Capture(old);
            var next = Assemble(world);
            BeltTransportRestorer.Restore(snapshot, next);
            return next;
        }

        internal static BeltItem NewItem(ItemId itemId, BeltEntryDirection entryDirection)
        {
            return new BeltItem(itemId, ItemInstanceId.Create(), entryDirection);
        }

        internal static BeltConveyorSegment SegmentAt(BeltTransportAssembly assembly, Vector3Int position)
        {
            return assembly.Segments[SegmentIndexAt(assembly, position)];
        }

        // segmentの走行列が、出口に近い順に(アイテム, 出口までの距離)と完全一致するか。個体・種類・進入方向まで比べる
        // Whether a segment's run equals the given (item, distance to exit) pairs in exit order, comparing instance, kind and entry direction
        internal static void AssertRun(BeltConveyorSegment segment, params (BeltItem item, int distance)[] expected)
        {
            var actual = segment.CaptureItems();
            Assert.AreEqual(expected.Length, actual.Length, "item count on the segment");
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].distance, actual[i].DistanceToExit, $"distance of item {i}");
                AssertSameItem(expected[i].item, actual[i].Item);
            }
        }

        internal static void AssertSameItem(BeltItem expected, BeltItem actual)
        {
            Assert.AreEqual(expected.ItemInstanceId, actual.ItemInstanceId, "item instance");
            Assert.AreEqual(expected.ItemId, actual.ItemId, "item id");
            Assert.AreEqual(expected.EntryDirection, actual.EntryDirection, "entry direction");
        }

        internal static bool Push(BeltTransportAssembly assembly, IBlock belt, IBlock machine, ItemId itemId)
        {
            // 機械からの入力リンクの接続をそのまま使い、実機の押し込みと同じ文脈を作る
            // Reuse the machine input link's connection to build the same context a real machine push carries
            var link = MachineInputLink(assembly, machine);
            var context = new InsertItemContext(machine.BlockInstanceId, link.Connection.SourceConnector, link.Connection.TargetConnector);
            return assembly.TrySupplyFromMachine(belt.BlockInstanceId, context, itemId, ItemInstanceId.Create());
        }

        internal static BeltSegmentLayoutLink MachineInputLink(BeltTransportAssembly assembly, IBlock machine)
        {
            var links = assembly.Layouts.SelectMany(layout => layout.Inputs).Where(link => link.IsMachine && ReferenceEquals(link.Connection.PartnerBlock, machine)).ToList();
            Assert.AreEqual(1, links.Count, $"machine input links of {machine.BlockPositionInfo.OriginalPos}");
            return links[0];
        }

        internal static int SegmentIndexAt(BeltTransportAssembly assembly, Vector3Int position)
        {
            return assembly.Layouts.Single(layout => layout.Cells.Any(cell => cell.Position == position)).Index;
        }

        internal static int InternalSegmentIndex(BeltTransportAssembly assembly)
        {
            return assembly.Layouts.Single(layout => layout.IsInternal).Index;
        }

        // 接続テスト用の機械(MachinePortTestTemplate)はDummyBlockInventoryを持つ
        // Machines of the connection tests (MachinePortTestTemplate) carry a DummyBlockInventory
        internal static DummyBlockInventory Inventory(IBlock block)
        {
            return block.ComponentManager.GetComponent<DummyBlockInventory>();
        }

        internal static int CountOf(DummyBlockInventory inventory, ItemId itemId)
        {
            var count = 0;
            for (var i = 0; i < inventory.GetSlotSize(); i++)
                if (inventory.GetItem(i).Id == itemId) count += inventory.GetItem(i).Count;
            return count;
        }

        internal static int TotalCount(DummyBlockInventory inventory)
        {
            var count = 0;
            for (var i = 0; i < inventory.GetSlotSize(); i++)
                if (inventory.GetItem(i).Id != ItemMaster.EmptyItemId) count += inventory.GetItem(i).Count;
            return count;
        }

        internal static void FillWith(DummyBlockInventory inventory, ItemId itemId)
        {
            // 別アイテムで全スロットを埋め、以後の搬入を拒否させる
            // Fill every slot with another item so later inserts are rejected
            for (var i = 0; i < inventory.GetSlotSize(); i++) inventory.SetItem(i, ServerContext.ItemStackFactory.Create(itemId, 1));
        }

        internal static void Tick(BeltTransportAssembly assembly, int count)
        {
            for (var i = 0; i < count; i++) assembly.Simulation.Tick();
        }
    }
}
