using System;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Core.Master;
using Game.Context;
using Game.Gear.Common;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.GearChain;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class ChainEnergyTransmissionTest
    {
        [Test]
        public void GeneratorPowerTravelsThroughChain()
        {
            // テスト用DIコンテナを立ち上げる
            // Initialize test DI container
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            const int playerId = 0;
            var chainItemId = global::Core.Master.MasterHolder.ItemMaster.GetItemId(ChainConstants.ChainItemGuid);

            // ブロックを配置してギアネットワークを構築する
            // Place blocks to build gear network
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(0, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleA);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearChainPole, new Vector3Int(6, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var poleB);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, new Vector3Int(7, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var targetGear);

            // プレイヤーのインベントリにチェーンを追加する
            // Grant chain item to player inventory
            var inventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;
            inventory.SetItem(0, ServerContext.ItemStackFactory.Create(chainItemId, 5));

            // チェーン接続を確立する
            // Establish chain connection
            var connected = GearChainSystemUtil.TryConnect(new Vector3Int(1, 0, 0), new Vector3Int(6, 0, 0), playerId, chainItemId, out var error);
            Assert.True(connected);
            Assert.IsEmpty(error ?? string.Empty);

            // ギアネットワークを更新する
            // Update gear networks
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            foreach (var network in gearNetworkDatastore.GearNetworks.Values) network.ManualUpdate();

            // 発電機とターゲットギアの回転が一致することを確認する
            // Ensure generator and target gear share rpm and direction
            var generator = generatorBlock.GetComponent<IGearGenerator>();
            var gear = targetGear.GetComponent<IGear>();
            Assert.AreEqual(generator.CurrentRpm.AsPrimitive(), gear.CurrentRpm.AsPrimitive(), 0.001f);
            Assert.AreEqual(generator.IsCurrentClockwise, gear.IsCurrentClockwise);
        }
    }
}
