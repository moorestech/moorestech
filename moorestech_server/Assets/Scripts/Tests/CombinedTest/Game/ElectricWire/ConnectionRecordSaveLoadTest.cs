using System;
using System.Linq;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.PlayerInventory.Interface;
using Game.PlayerIdentity;
using Tests.Util.PlayerIdentity;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using Server.Protocol.PacketResponse.Util.GearChain;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game.ElectricWire
{
    // 接続記録の種類がセーブ往復で保たれるか検証
    // Verify connection records keep their tool across save/load
    public class ConnectionRecordSaveLoadTest
    {
        private static readonly Guid WireToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");
        private static readonly Guid ChainToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000003");
        private static readonly Guid WireItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000001");
        private static readonly Guid ChainItemGuid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        [Test]
        public void 電線の種類がセーブロードで復元される()
        {
            var saveJson = BuildSave(out var posPole, out var posGenerator, out _, out _);

            // 別ワールドへロードし電線の種類を照合
            // Load into a fresh world and compare the wire's tool
            var loaded = LoadInto(saveJson);
            var pole = ServerContext.WorldBlockDatastore.GetBlock(posPole).GetComponent<IElectricWireConnector>();
            var generator = ServerContext.WorldBlockDatastore.GetBlock(posGenerator).GetComponent<IElectricWireConnector>();
            Assert.AreEqual(WireToolGuid, pole.WireConnections[generator.BlockInstanceId].Record.ConnectToolGuid);
            Assert.AreEqual(WireToolGuid, generator.WireConnections[pole.BlockInstanceId].Record.ConnectToolGuid);
            var record = pole.WireConnections[generator.BlockInstanceId].Record;
            Assert.AreEqual(2, record.TotalCount);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(WireItemGuid), record.Materials.Single().ItemId);
            CollectionAssert.AreEqual(record.Materials, generator.WireConnections[pole.BlockInstanceId].Record.Materials);
            Assert.IsNotNull(loaded);
        }

        [Test]
        public void チェーンの種類がセーブロードで復元される()
        {
            var saveJson = BuildSave(out _, out _, out var posChainA, out var posChainB);

            // 別ワールドへロードしチェーンの種類を照合
            // Load into a fresh world and compare the chain's tool
            LoadInto(saveJson);
            var poleA = ServerContext.WorldBlockDatastore.GetBlock(posChainA).GetComponent<IGearChainPole>();
            var poleB = ServerContext.WorldBlockDatastore.GetBlock(posChainB).GetComponent<IGearChainPole>();
            Assert.IsTrue(poleA.TryGetChainConnectionRecord(poleB.BlockInstanceId, out var record));
            Assert.AreEqual(ChainToolGuid, record.ConnectToolGuid);
            Assert.AreEqual(10, record.Materials.Sum(m => m.Count));
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(ChainItemGuid), record.Materials.Single().ItemId);
            Assert.IsTrue(poleB.TryGetChainConnectionRecord(poleA.BlockInstanceId, out var reverseRecord));
            Assert.AreEqual(ChainToolGuid, reverseRecord.ConnectToolGuid);
            CollectionAssert.AreEqual(record.Materials, reverseRecord.Materials);
        }

        [Test]
        public void 種類を持たない接続だけを飛ばして他の接続は復元する()
        {
            var save = JObject.Parse(BuildSave(out var posPole, out var posGenerator, out var posChainA, out var posChainB));

            // 電線の種類キーだけ消す。空Guidへ縮退させず、その接続だけを警告つきで飛ばす
            // Drop only the wire's tool key; never degrade to an empty Guid, skip just that connection with a warning
            var removedCount = 0;
            foreach (var block in (JArray)save["world"])
            {
                if (block["state"]?["ElectricWireConnectorComponent"]?["connections"] is not JArray connections) continue;
                foreach (var connection in connections)
                {
                    Assert.IsTrue(((JObject)connection).Remove("connectToolGuid"));
                    removedCount++;
                }
            }
            Assert.AreEqual(2, removedCount);

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("\\[ElectricWire\\] Saved connection without connectToolGuid skipped"));
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("\\[ElectricWire\\] Saved connection without connectToolGuid skipped"));
            LoadInto(save.ToString());
            Assert.AreEqual(0, ServerContext.WorldBlockDatastore.GetBlock(posPole).GetComponent<IElectricWireConnector>().WireConnections.Count);
            Assert.AreEqual(0, ServerContext.WorldBlockDatastore.GetBlock(posGenerator).GetComponent<IElectricWireConnector>().WireConnections.Count);
            var poleA = ServerContext.WorldBlockDatastore.GetBlock(posChainA).GetComponent<IGearChainPole>();
            var poleB = ServerContext.WorldBlockDatastore.GetBlock(posChainB).GetComponent<IGearChainPole>();
            Assert.IsTrue(poleA.TryGetChainConnectionRecord(poleB.BlockInstanceId, out var record));
            Assert.AreEqual(ChainToolGuid, record.ConnectToolGuid);
        }

        private static string BuildSave(out Vector3Int posPole, out Vector3Int posGenerator, out Vector3Int posChainA, out Vector3Int posChainB)
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(provider.GetRequiredService<PlayerIdentityRegistry>(), "steam:1").PlayerId;
            var unlock = provider.GetService<IGameUnlockStateDataController>();
            unlock.UnlockConnectTool(WireToolGuid);
            unlock.UnlockConnectTool(ChainToolGuid);

            // 本番経路で電柱-発電機とポール2本を接続
            // Connect pole-generator by wire and two poles by chain via production paths
            posPole = new Vector3Int(0, 0, 0);
            posGenerator = new Vector3Int(2, 0, 0);
            posChainA = new Vector3Int(10, 0, 0);
            posChainB = new Vector3Int(12, 0, 0);
            var world = ServerContext.WorldBlockDatastore;
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, posPole, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.GeneratorId, posGenerator, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posChainA, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.GearChainPole, posChainB, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));

            var inventory = provider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(WireItemGuid), 10));
            inventory.SetItem(1, ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(ChainItemGuid), 10));
            Assert.IsTrue(ElectricWireSystemUtil.TryConnect(posPole, posGenerator, playerId, WireToolGuid, false, out var wireError), wireError.ToString());
            Assert.IsTrue(GearChainSystemUtil.TryConnect(posChainA, posChainB, playerId, ChainToolGuid, out var chainError), chainError.ToString());

            return provider.GetService<AssembleSaveJsonText>().AssembleSaveJson();
        }

        private static ServiceProvider LoadInto(string saveJson)
        {
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ((WorldLoaderFromJson)provider.GetService<IWorldSaveDataLoader>()).Load(saveJson);
            return provider;
        }
    }
}
