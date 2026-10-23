using System;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.RocketLaunchPad;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.World.Interface.DataStore;
using Game.Block.Interface.State;
using Game.Context;
using Game.Orbit;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.Util.Rocket;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest.Rocket
{
    public class RocketLaunchPadRemoveRefundTest
    {
        private const int PlayerId = 3;

        [Test]
        public void 建造中の台を撤去すると積んだ資材と積荷が戻る()
        {
            var (packet, services) = CreateServer();
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.AwaitingBuildCommand, 5));
            Assert.AreEqual(RocketLaunchPadCommandResult.Accepted, pad.GetComponent<IRocketLaunchPadComponent>().RequestBuild());

            // 電線が先頭の実テンプレートで、積載ロケットも合算されることを確かめる
            // Verify the real wire-first template also refunds the loaded rocket
            var wire = pad.GetComponent<IElectricWireConnector>();
            var partnerId = wire.WireConnections.Keys.Single();
            var partner = wire.WireConnections[partnerId].Connector;
            wire.TryRemoveWireConnection(partnerId, out var record);
            partner.TryRemoveWireConnection(wire.BlockInstanceId, out _);
            var paid = new ConnectionLineRecord(record.ConnectToolGuid, new[] { new ConnectToolMaterialCost(ForUnitTestItemId.ItemId1, 4) });
            Assert.IsTrue(wire.TryAddWireConnection(partnerId, paid));
            Assert.IsTrue(partner.TryAddWireConnection(wire.BlockInstanceId, paid));
            var refundComponents = pad.ComponentManager.GetComponents<IGetRefundItemsInfo>().ToList();
            Assert.AreEqual(2, refundComponents.Count);
            Assert.IsFalse(refundComponents[0] is RocketLaunchPadProcessorComponent);
            var inventory = GetInventory(services);
            ClearInventory(inventory);
            packet.GetPacketResponse(CreateRemovePayload(Vector3Int.zero), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(Vector3Int.zero));
            Assert.AreEqual(4, GetItemCount(inventory, ForUnitTestItemId.ItemId1));
            Assert.AreEqual(2, GetItemCount(inventory, ForUnitTestItemId.RocketCaseId));
            Assert.AreEqual(3, GetItemCount(inventory, ForUnitTestItemId.RocketFuelId));
            Assert.AreEqual(1, GetItemCount(inventory, ForUnitTestItemId.RocketComputerId));
            Assert.AreEqual(1, GetItemCount(inventory, ForUnitTestItemId.SatellitePayloadId));
        }

        [Test]
        public void 出力口の成果も撤去で戻る()
        {
            var (packet, services) = CreateServer();
            ServerContext.GetService<IRocketLaunchHistory>().RecordLaunch();
            var pad = RocketLaunchPadTestUtil.PlacePoweredPad(Vector3Int.zero, 100);
            RocketLaunchPadTestUtil.FillOneLaunch(pad, ForUnitTestItemId.SatellitePayloadId, 1);
            Assert.IsTrue(RocketLaunchPadTestUtil.RunUntilPhase(pad, RocketLaunchPadPhase.Launching, 40));

            // 消費済みの資材は返さず、開ける出力口の成果だけ返す
            // Return the openable output contents without refunding consumed materials
            var inventory = GetInventory(services);
            ClearInventory(inventory);
            packet.GetPacketResponse(CreateRemovePayload(Vector3Int.zero), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(PlayerId));
            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(Vector3Int.zero));
            Assert.AreEqual(150, GetItemCount(inventory, ForUnitTestItemId.ComputeDataId));
            Assert.AreEqual(0, GetItemCount(inventory, ForUnitTestItemId.SatellitePayloadId));
        }

        private static (PacketResponseCreator packet, ServiceProvider services) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        private static IOpenableInventory GetInventory(ServiceProvider services)
        {
            return services.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
        }

        private static void ClearInventory(IOpenableInventory inventory)
        {
            for (var i = 0; i < inventory.GetSlotSize(); i++) inventory.SetItem(i, ServerContext.ItemStackFactory.CreatEmpty());
        }

        private static int GetItemCount(IOpenableInventory inventory, ItemId itemId)
        {
            return inventory.InventoryItems.Where(stack => stack.Id == itemId).Sum(stack => stack.Count);
        }

        private static byte[] CreateRemovePayload(Vector3Int position)
        {
            return MessagePackSerializer.Serialize(new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(position));
        }
    }
}
