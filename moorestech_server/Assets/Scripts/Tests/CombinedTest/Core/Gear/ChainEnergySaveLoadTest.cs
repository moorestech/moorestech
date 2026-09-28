using System;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Core.Master;
using Game.Context;
using Game.Gear.Common;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Tests.Util.PlayerIdentity;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.GearChain;
using Tests.Module;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core.Gear
{
    public class ChainEnergySaveLoadTest
    {
        private static readonly Guid ConnectToolGuid = Guid.Parse("c0000000-0000-0000-0000-000000000003");
        private static readonly Guid ChainMaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        [Test]
        public void SaveLoadRestoresChainPoleNetworkConnection()
        {
            // 保存前ワールドにチェーン経由のギアネットワークを構築する
            // Build a gear network through chain poles before saving
            var (_, saveServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerId = PlayerIdentityTestHelper.Register(saveServiceProvider, "steam:1");
            var chainItemId = MasterHolder.ItemMaster.GetItemId(ChainMaterialGuid);
            saveServiceProvider.GetService<IGameUnlockStateDataController>().UnlockConnectTool(ConnectToolGuid);

            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleA);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, new Vector3Int(6, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleB);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, new Vector3Int(7, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var targetGear);

            // チェーン接続を確立して保存対象のインスタンスIDを控える
            // Establish the chain connection and keep instance ids for load assertions
            var inventory = saveServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(chainItemId, 20));
            var connected = GearChainSystemUtil.TryConnect(new Vector3Int(1, 0, 0), new Vector3Int(6, 0, 0), playerId, ConnectToolGuid, out var error);
            Assert.True(connected);
            Assert.IsEmpty(error ?? string.Empty);

            var generatorId = generatorBlock.BlockInstanceId;
            var poleAId = poleA.BlockInstanceId;
            var poleBId = poleB.BlockInstanceId;
            var targetGearId = targetGear.BlockInstanceId;
            var saveJson = SaveLoadJsonTestHelper.AssembleSaveJson(saveServiceProvider);

            // 別DIコンテナへロードして、セーブ復元後のネットワーク所属を検証する
            // Load into another DI container and verify network membership after restore
            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            SaveLoadJsonTestHelper.LoadFromJson(loadServiceProvider, saveJson);
            GameUpdater.UpdateOneTick();

            worldBlockDatastore = ServerContext.WorldBlockDatastore;
            var loadedPoleA = worldBlockDatastore.GetBlock(poleAId).GetComponent<IGearChainPole>();
            var loadedPoleB = worldBlockDatastore.GetBlock(poleBId).GetComponent<IGearChainPole>();
            Assert.IsTrue(loadedPoleA.ContainsChainConnection(poleBId));
            Assert.IsTrue(loadedPoleB.ContainsChainConnection(poleAId));

            // チェーン接続先を含めた単一ネットワークへ統合されていることを確認する
            // Ensure the chain-connected endpoints are merged into one network
            Assert.True(ServerContext.GetService<IGearNetworkDatastore>().TryGetGearNetwork(generatorId, out var generatorNetwork));
            Assert.True(ServerContext.GetService<IGearNetworkDatastore>().TryGetGearNetwork(poleAId, out var poleANetwork));
            Assert.True(ServerContext.GetService<IGearNetworkDatastore>().TryGetGearNetwork(poleBId, out var poleBNetwork));
            Assert.True(ServerContext.GetService<IGearNetworkDatastore>().TryGetGearNetwork(targetGearId, out var targetGearNetwork));
            Assert.AreSame(generatorNetwork, poleANetwork);
            Assert.AreSame(generatorNetwork, poleBNetwork);
            Assert.AreSame(generatorNetwork, targetGearNetwork);

            // ロード後の更新で発電機の回転がチェーン先へ届くことを確認する
            // Verify generator rotation reaches the far side after load update
            var generator = worldBlockDatastore.GetBlock(generatorId).GetComponent<IGearGenerator>();
            var gear = worldBlockDatastore.GetBlock(targetGearId).GetComponent<IGear>();
            Assert.AreEqual(generator.CurrentRpm.AsPrimitive(), gear.CurrentRpm.AsPrimitive(), 0.001f);
            Assert.AreEqual(generator.IsCurrentClockwise, gear.IsCurrentClockwise);
        }
    }
}
