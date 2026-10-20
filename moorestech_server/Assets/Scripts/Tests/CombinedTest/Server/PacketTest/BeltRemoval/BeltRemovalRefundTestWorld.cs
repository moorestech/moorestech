using System.Linq;
using System.Collections.Generic;
using Core.BeltTransport;
using Core.Inventory;
using Core.Item;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Construction;
using Game.Context;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.Util;
using Tests.UnitTest.Game.BeltConnection.Transport;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    internal sealed class BeltRemovalRefundTestWorld
    {
        internal readonly ServiceProvider Services;
        internal readonly IOpenableInventory Inventory;
        internal readonly BeltTransportDatastore Transport;
        private readonly RemoveBlockProtocol _protocol;
        private IReadOnlyList<IItemStack> _lastRefund;

        private BeltRemovalRefundTestWorld(ServiceProvider services)
        {
            Services = services;
            Inventory = services.GetService<IPlayerInventoryDataStore>().GetInventoryData(1).MainOpenableInventory;
            Transport = services.GetService<BeltTransportDatastore>();
            _protocol = new RemoveBlockProtocol(services);
        }

        internal static BeltRemovalRefundTestWorld CreateServer()
        {
            var (_, services) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return new BeltRemovalRefundTestWorld(services);
        }

        internal IBlock Place(BlockId id, Vector3Int position)
        {
            return BeltWorldTestUtil.Place(id, position, BlockDirection.North);
        }

        internal IBlock CreateMerge()
        {
            // 本流・横流で合流と内部区間を構築
            // Build a merge and internal segments from main and side inputs
            for (var z = -1; z <= 2; z++) Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z));
            BeltWorldTestUtil.Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(1, 0, 1), BlockDirection.West);
            BeltWorldTestUtil.EnterMachinePushWindow();
            return ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(0, 0, 1));
        }

        internal static BeltItem Item()
        {
            return new BeltItem(ForUnitTestItemId.ItemId1, ItemInstanceId.Create(), BeltEntryDirection.FromBack);
        }

        internal IBlock CreateConnectionMerge(bool beltInput, bool rightSource)
        {
            // 非ベルト入力を左右から与える
            // Feed the merge from non-belt inputs on either side
            BeltTransportTestUtil.InstallMachinePorts(new[] { Vector3Int.left, Vector3Int.right }, new[] { Vector3Int.back });
            if (beltInput) Place(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.zero);
            var merge = Place(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.forward);
            Place(ForUnitTestModBlockId.BeltConveyorId, Vector3Int.forward * 2);
            Place(ForUnitTestModBlockId.ChestId, new Vector3Int(-1, 0, 1));
            if (rightSource) Place(ForUnitTestModBlockId.ChestId, new Vector3Int(1, 0, 1));
            BeltWorldTestUtil.EnterMachinePushWindow();
            return merge;
        }

        internal BeltItem RestoreInternal(BeltDirection direction)
        {
            var layout = Transport.Assembly.Layouts.Single(entry => entry.IsInternal && entry.Inputs[0].Direction == direction);
            var item = Item();
            Transport.Assembly.Segments[layout.Index].RestoreItems(new[] { new BeltItemState(item, 100) });
            return item;
        }

        internal int CarriedCount()
        {
            return Transport.Assembly.Segments.Sum(segment => segment.Count + (segment is BeltBufferedSegment buffered && buffered.Buffer.HasItem ? 1 : 0));
        }

        internal void Restore(Vector3Int position, BeltItemState[] states)
        {
            BeltWorldTestUtil.EnterMachinePushWindow();
            BeltWorldTestUtil.SegmentAt(position).RestoreItems(states);
        }

        internal RemoveBlockProtocol.RemoveBlockResponseMessagePack Remove(Vector3Int position)
        {
            // 挿入時にIDを採番する手持ちとは別に、返却契約の個体IDを観測する
            // Observe refund-contract identity before the inventory assigns stack identities
            var block = ServerContext.WorldBlockDatastore.GetBlock(position);
            if (block != null) _lastRefund = Transport.GetRefundItems(block.BlockInstanceId)
                .Concat(Transport.GetConnectionRemovalRefundItems(block.BlockInstanceId)).ToArray();
            var bytes = MessagePackSerializer.Serialize(new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(position));
            return (RemoveBlockProtocol.RemoveBlockResponseMessagePack)_protocol.GetResponse(bytes, 1);
        }

        internal int Count(ItemId id)
        {
            return Inventory.InventoryItems.Where(item => item.Id == id).Sum(item => item.Count);
        }

        internal void AssertRefund(BeltItem item)
        {
            Assert.IsTrue(_lastRefund.Any(stack => stack.Id == item.ItemId && stack.ItemInstanceId == item.ItemInstanceId && stack.Count == 1));
        }

        internal void FillInventory()
        {
            var filler = ForUnitTestItemId.ItemId2;
            for (var i = 0; i < Inventory.GetSlotSize(); i++)
                Inventory.SetItem(i, ServerContext.ItemStackFactory.Create(filler, ItemStackLevelDataStore.Instance.GetMaxStack(filler)));
        }

        internal void ClearInventory()
        {
            for (var i = 0; i < Inventory.GetSlotSize(); i++) Inventory.SetItem(i, ServerContext.ItemStackFactory.CreatEmpty());
        }

        internal int Wallet(BlockId id)
        {
            return Services.GetService<IRemainingPlacementCountLookup>().GetReader(1).GetRemainingCount(id);
        }
    }
}
