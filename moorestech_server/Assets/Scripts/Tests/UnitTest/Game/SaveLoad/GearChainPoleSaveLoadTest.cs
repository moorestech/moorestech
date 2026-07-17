using System;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class GearChainPoleSaveLoadTest
    {
        [Test]
        public void ロード後の最初のtickで保存済みチェーンから歯車網を再構築する()
        {
            var (_, saveProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var pos1 = new Vector3Int(0, 0, 0);
            var pos2 = new Vector3Int(10, 0, 0);
            var pos3 = new Vector3Int(20, 0, 0);

            // 三本の支柱を設置し、一番目から残り二本へチェーンを張る
            // Place three poles and connect the first pole to the other two by chains
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.GearChainPole, pos1, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block1));
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.GearChainPole, pos2, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block2));
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.GearChainPole, pos3, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var block3));
            var pole1 = block1.GetComponent<IGearChainPole>();
            var pole2 = block2.GetComponent<IGearChainPole>();
            var pole3 = block3.GetComponent<IGearChainPole>();
            var noCost = new GearChainConnectionCost(ItemMaster.EmptyItemId, 0);
            Assert.IsTrue(pole1.TryAddChainConnection(pole2.BlockInstanceId, noCost));
            Assert.IsTrue(pole1.TryAddChainConnection(pole3.BlockInstanceId, noCost));
            GameUpdater.UpdateOneTick();

            var saveJson = saveProvider.GetRequiredService<AssembleSaveJsonText>().AssembleSaveJson();
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var loader = (WorldLoaderFromJson)loadProvider.GetRequiredService<IWorldSaveDataLoader>();
            loader.Load(saveJson);

            // ロード直後は接続だけを復元し、歯車網は未構築のままdirtyにする
            // Restore only connections during load and leave the gear network dirty and unbuilt
            var loadedDatastore = loadProvider.GetRequiredService<GearNetworkDatastore>();
            var loadedPole1 = ServerContext.WorldBlockDatastore.GetBlock(pos1).GetComponent<IGearChainPole>();
            var loadedPole2 = ServerContext.WorldBlockDatastore.GetBlock(pos2).GetComponent<IGearChainPole>();
            var loadedPole3 = ServerContext.WorldBlockDatastore.GetBlock(pos3).GetComponent<IGearChainPole>();
            Assert.IsFalse(loadedDatastore.TryGetGearNetwork(loadedPole1.BlockInstanceId, out _));
            Assert.IsTrue(loadedPole1.ContainsChainConnection(loadedPole2.BlockInstanceId));
            Assert.IsTrue(loadedPole1.ContainsChainConnection(loadedPole3.BlockInstanceId));

            // 最初のtick先頭で三本を同じ歯車網へまとめ、その後はdirtyを解除する
            // Build one shared gear network at the first tick head and then clear the dirty state
            GameUpdater.UpdateOneTick();
            Assert.IsTrue(loadedDatastore.TryGetGearNetwork(loadedPole1.BlockInstanceId, out var network1));
            Assert.IsTrue(loadedDatastore.TryGetGearNetwork(loadedPole2.BlockInstanceId, out var network2));
            Assert.IsTrue(loadedDatastore.TryGetGearNetwork(loadedPole3.BlockInstanceId, out var network3));
            Assert.AreSame(network1, network2);
            Assert.AreSame(network1, network3);
        }
    }
}
