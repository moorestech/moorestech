using Core.Update;
using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Game.UnlockState;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class ElectricWireAutoConnectPlaceTest
    {
        private const int PlayerId = 5;

        private static readonly Guid WireItemGuid = Guid.Parse("00000000-0000-0000-4649-000000000001");

        [Test]
        public void 機械設置時に最寄り電柱1本へ自動接続される()
        {
            // 近い電柱(距離1)と遠い電柱(距離2)を先に設置し、機械をプロトコル経由で置く
            // Place a near pole (distance 1) and a far pole (distance 2), then place a machine via the protocol
            var (packet, serviceProvider) = CreateServer();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var nearPole);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, new Vector3Int(0, 0, 2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var farPole);

            var inventory = SetupWire(serviceProvider, 5);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.MachineId);
            PlaceBlock(packet, ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0, 0));

            var machine = worldBlockDatastore.GetBlock(new Vector3Int(0, 0, 0));
            var nearConnector = nearPole.GetComponent<IElectricWireConnector>();
            var farConnector = farPole.GetComponent<IElectricWireConnector>();
            var machineConnector = machine.GetComponent<IElectricWireConnector>();

            Assert.IsTrue(nearConnector.ContainsWireConnection(machineConnector.BlockInstanceId));
            Assert.IsFalse(farConnector.ContainsWireConnection(machineConnector.BlockInstanceId));
            Assert.AreEqual(1, machineConnector.WireConnections.Count);

            // 距離1のワイヤーコストは1(consumptionPerLength=1)
            // Wire cost for distance 1 is 1 (consumptionPerLength=1)
            Assert.AreEqual(4, GetWireCount(inventory));
        }

        [Test]
        public void 電柱設置時に未接続機械が全部接続される()
        {
            // 未接続機械2台と接続済み機械1台を用意し、電柱をプロトコル経由で置く
            // Prepare two unconnected machines and one already-connected machine, then place a pole via the protocol
            var (packet, serviceProvider) = CreateServer();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var unconnectedA);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0, 1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var unconnectedB);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(-1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var alreadyConnected);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.InfinityGeneratorId, new Vector3Int(100, 0, 100), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ElectricWireTestUtil.Connect(new Vector3Int(-1, 0, 0), new Vector3Int(100, 0, 100));

            SetupWire(serviceProvider, 5);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.ElectricPoleId);
            PlaceBlock(packet, ForUnitTestModBlockId.ElectricPoleId, new Vector3Int(0, 0, 0));

            var pole = worldBlockDatastore.GetBlock(new Vector3Int(0, 0, 0));
            var poleConnector = pole.GetComponent<IElectricWireConnector>();

            Assert.IsTrue(poleConnector.ContainsWireConnection(unconnectedA.GetComponent<IElectricWireConnector>().BlockInstanceId));
            Assert.IsTrue(poleConnector.ContainsWireConnection(unconnectedB.GetComponent<IElectricWireConnector>().BlockInstanceId));
            Assert.IsFalse(poleConnector.ContainsWireConnection(alreadyConnected.GetComponent<IElectricWireConnector>().BlockInstanceId));
            Assert.AreEqual(2, poleConnector.WireConnections.Count);
        }

        [Test]
        public void 電線不足時は設置自体が失敗し状態が変化しない()
        {
            // 電柱の近くに機械を置くが電線を1つも持たない
            // Attempt to place a machine near a pole while holding zero wire items
            var (packet, serviceProvider) = CreateServer();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole);

            SetupWire(serviceProvider, 0);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.MachineId);
            var segmentDatastore = serviceProvider.GetService<IElectricWireNetworkDatastore>();
            // 事前配置分のトポロジを反映してから基準値を取る
            // Flush the pre-placed topology before taking the baseline
            GameUpdater.UpdateOneTick();
            var segmentCountBefore = segmentDatastore.SegmentCount;

            PlaceBlock(packet, ForUnitTestModBlockId.MachineId, new Vector3Int(0, 0, 0));

            Assert.IsFalse(worldBlockDatastore.Exists(new Vector3Int(0, 0, 0)));
            Assert.AreEqual(0, pole.GetComponent<IElectricWireConnector>().WireConnections.Count);
            // 設置失敗後の状態確認もtick反映後に行う
            // Verify the unchanged state after a tick so pending commands are settled
            GameUpdater.UpdateOneTick();
            Assert.AreEqual(segmentCountBefore, segmentDatastore.SegmentCount);
        }

        [Test]
        public void 範囲内に接続先が無ければ電線消費なしで孤立設置される()
        {
            // 周囲に電気系ブロックが無い状態で機械を置く。電線ゼロでも成功する
            // Place a machine with no electric blocks nearby; succeeds even with zero wire items
            var (packet, serviceProvider) = CreateServer();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            SetupWire(serviceProvider, 0);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.MachineId);
            PlaceBlock(packet, ForUnitTestModBlockId.MachineId, new Vector3Int(50, 0, 50));

            Assert.IsTrue(worldBlockDatastore.Exists(new Vector3Int(50, 0, 50)));

            var machine = worldBlockDatastore.GetBlock(new Vector3Int(50, 0, 50));
            Assert.AreEqual(0, machine.GetComponent<IElectricWireConnector>().WireConnections.Count);
        }

        #region TestUtil

        private static (PacketResponseCreator packet, ServiceProvider serviceProvider) CreateServer()
        {
            return new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        private static IOpenableInventory SetupWire(ServiceProvider serviceProvider, int wireCount)
        {
            // 電線アイテムだけをインベントリへ置く（設置ブロックは建設コスト方式のため所持不要）
            // Put only wire items into the inventory (placement no longer consumes a block item)
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            if (0 < wireCount) inventory.SetItem(10, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(WireItemGuid), wireCount));

            return inventory;
        }

        private static void GrantRequiredItems(ServiceProvider serviceProvider, BlockId blockId)
        {
            // 建設コスト1セット分をインベントリへ投入する
            // Insert one construction-cost set into the inventory
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            foreach (var requiredItem in MasterHolder.BlockMaster.GetBlockMaster(blockId).RequiredItems)
            {
                inventory.InsertItem(MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid), (int)requiredItem.Count);
            }
        }

        private static void UnlockBlock(ServiceProvider serviceProvider, BlockId blockId)
        {
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid;
            serviceProvider.GetService<IGameUnlockStateDataController>().UnlockBlock(blockGuid);
        }

        private static void PlaceBlock(PacketResponseCreator packet, BlockId blockId, Vector3Int position)
        {
            var placeInfo = new List<PlaceInfo>
            {
                new()
                {
                    Position = position,
                    Direction = BlockDirection.North,
                    VerticalDirection = BlockVerticalDirection.Horizontal,
                    BlockId = blockId,
                },
            };

            var payload = MessagePackSerializer.Serialize(new PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack(PlayerId, placeInfo));
            packet.GetPacketResponse(payload, new PacketResponseContext());
        }

        private static int GetWireCount(IOpenableInventory inventory)
        {
            return GetItemCount(inventory, WireItemGuid);
        }

        private static int GetItemCount(IOpenableInventory inventory, Guid itemGuid)
        {
            var itemId = MasterHolder.ItemMaster.GetItemId(itemGuid);
            var total = 0;
            foreach (var itemStack in inventory.InventoryItems)
                if (itemStack.Id == itemId)
                    total += itemStack.Count;

            return total;
        }

        #endregion
    }
}
