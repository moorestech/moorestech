using System;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.PlayerIdentity;
using Game.UnlockState;
using Tests.Util.PlayerIdentity;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    // 電線口なし燃料機械へは全経路で接続不可
    // A wireless fuel machine is never wired by any path
    public class WirelessFuelMachineWireTest : ElectricWireAutoConnectPlaceTestBase
    {
        private static readonly Vector3Int PolePosition = new(0, 0, 0);
        private static readonly Vector3Int MachinePosition = new(1, 0, 0);

        [Test]
        public void 電柱を置いても範囲内の燃料機械へ自動接続しない()
        {
            var (packet, serviceProvider) = CreateServer();
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.WirelessFuelMachineId, MachinePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fuelMachine);

            SetupWire(serviceProvider, 10);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.ElectricPoleId);
            PlaceBlock(packet, ForUnitTestModBlockId.ElectricPoleId, PolePosition);

            // 双方未接続で電線も減らない
            // Neither side is wired and no wire is spent
            var pole = ServerContext.WorldBlockDatastore.GetBlock(PolePosition);
            Assert.IsNotNull(pole);
            Assert.AreEqual(0, pole.GetComponent<IElectricWireConnector>().WireConnections.Count);
            Assert.AreEqual(0, fuelMachine.GetComponent<IElectricWireConnector>().WireConnections.Count);
            Assert.AreEqual(10, GetWireCount(serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory));
        }

        [Test]
        public void 燃料機械を置いても範囲内の電柱へ自動接続しない()
        {
            var (packet, serviceProvider) = CreateServer();
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, PolePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole);

            SetupWire(serviceProvider, 10);
            UnlockBlock(serviceProvider, ForUnitTestModBlockId.WirelessFuelMachineId);
            PlaceBlock(packet, ForUnitTestModBlockId.WirelessFuelMachineId, MachinePosition);

            var fuelMachine = ServerContext.WorldBlockDatastore.GetBlock(MachinePosition);
            Assert.IsNotNull(fuelMachine);
            Assert.AreEqual(0, fuelMachine.GetComponent<IElectricWireConnector>().WireConnections.Count);
            Assert.AreEqual(0, pole.GetComponent<IElectricWireConnector>().WireConnections.Count);
        }

        [Test]
        public void 電柱から燃料機械へ手動でつなげない()
        {
            var (_, serviceProvider) = CreateServer();
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, PolePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pole);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.WirelessFuelMachineId, MachinePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var fuelMachine);
            SetupWire(serviceProvider, 10);

            // どちら向きに張っても接続上限で拒否される
            // Either direction is rejected by the connection limit
            Assert.IsFalse(ElectricWireSystemUtil.TryConnect(PolePosition, MachinePosition, PlayerId, ElectricWireConnectToolGuid, false, out var reasonPoleToMachine));
            Assert.IsFalse(ElectricWireSystemUtil.TryConnect(MachinePosition, PolePosition, PlayerId, ElectricWireConnectToolGuid, false, out var reasonMachineToPole));
            Assert.AreEqual(ElectricWirePlacementFailureReason.ConnectionLimit, reasonPoleToMachine);
            Assert.AreEqual(ElectricWirePlacementFailureReason.ConnectionLimit, reasonMachineToPole);
            Assert.AreEqual(0, pole.GetComponent<IElectricWireConnector>().WireConnections.Count);
            Assert.AreEqual(0, fuelMachine.GetComponent<IElectricWireConnector>().WireConnections.Count);
        }

        [Test]
        public void 配線済みの機械が燃料機械に変わったセーブをロードすると電柱側にも接続が残らない()
        {
            // 旧マスタの配線済みワールドを再現
            // Reproduce a world wired under the old master
            var (_, saveServiceProvider) = CreateServer();
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, PolePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, MachinePosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            // セーブの身元検証を通すため、登録済みプレイヤーの電線で張る
            // Wire with a registered player's items so the save passes the identity validation
            var playerId = PlayerIdentityTestHelper.Register(saveServiceProvider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;
            saveServiceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(ElectricWireConnectToolGuid);
            var inventory = saveServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(WireItemGuid), 10));
            Assert.IsTrue(ElectricWireSystemUtil.TryConnect(PolePosition, MachinePosition, playerId, ElectricWireConnectToolGuid, false, out var error), error.ToString());
            var saveJson = saveServiceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            // ブロックのみ差し替え新マスタ再現
            // Swap only the block to reproduce the new master
            var machineGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.MachineId).BlockGuid.ToString();
            var fuelMachineGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.WirelessFuelMachineId).BlockGuid.ToString();
            var save = JObject.Parse(saveJson);
            var swappedCount = 0;
            foreach (var block in save.SelectTokens("$..blockGuid"))
            {
                if (block.Value<string>() != machineGuid) continue;
                ((JValue)block).Value = fuelMachineGuid;
                swappedCount++;
            }
            Assert.AreEqual(1, swappedCount);

            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(save.ToString());

            // 燃料機械も電柱も、互いへの接続を持たない
            // Neither the fuel machine nor the pole keeps a connection to the other
            var loadedPole = ServerContext.WorldBlockDatastore.GetBlock(PolePosition).GetComponent<IElectricWireConnector>();
            var loadedFuelMachine = ServerContext.WorldBlockDatastore.GetBlock(MachinePosition).GetComponent<IElectricWireConnector>();
            Assert.AreEqual(0, loadedFuelMachine.WireConnections.Count);
            Assert.AreEqual(0, loadedPole.WireConnections.Count);
        }
    }
}
