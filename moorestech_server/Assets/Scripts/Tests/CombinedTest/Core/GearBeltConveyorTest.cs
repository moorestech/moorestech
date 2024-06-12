using System;
using System.Collections.Generic;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class GearBeltConveyorTest
    {
        // トルクの供給率が100%のとき、指定した時間でアイテムが出てくるテスト
        [Test]
        public void OutputTestWhenTorqueSuppliedRateIs100()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var blockConfig = ServerContext.BlockConfig;
            var config = (GearBeltConveyorConfigParam)blockConfig.GetBlockConfig(ForUnitTestModBlockId.GearBeltConveyor).Param;
            var blockFactory = ServerContext.BlockFactory;
            var itemStackFactory = ServerContext.ItemStackFactory;
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            const int id = 2;
            const int count = 3;
            var item = itemStackFactory.Create(id, count);
            var dummy = new DummyBlockInventory();
            
            
            // gearBeltConveyorブロックを生成
            var gearBeltConveyorPosition = new Vector3Int(0, 0, 0);
            var gearBeltConveyor = blockFactory.Create(ForUnitTestModBlockId.GearBeltConveyor, BlockInstanceId.Create(), new BlockPositionInfo(gearBeltConveyorPosition, BlockDirection.North, Vector3Int.one));
            worldBlockDatastore.AddBlock(gearBeltConveyor);
            var gearBeltConveyorComponent = gearBeltConveyor.GetComponent<GearBeltConveyorComponent>();
            var beltConveyorComponent = gearBeltConveyor.GetComponent<VanillaBeltConveyorComponent>();
            var connectInventory = (Dictionary<IBlockInventory, (IConnectOption, IConnectOption)>)gearBeltConveyor.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectTargets;
            connectInventory.Add(dummy, (null, null));
            
            // generatorブロックを作成
            var generatorPosition = new Vector3Int(1, 0, 0);
            var generator = blockFactory.Create(ForUnitTestModBlockId.SimpleGearGenerator, BlockInstanceId.Create(), new BlockPositionInfo(generatorPosition, BlockDirection.East, Vector3Int.one));
            worldBlockDatastore.AddBlock(generator);
            
            // testGearブロックを作成
            var testGearPosition = new Vector3Int(2, 0, 0);
            var testGear = blockFactory.Create(ForUnitTestModBlockId.SmallGear, BlockInstanceId.Create(), new BlockPositionInfo(testGearPosition, BlockDirection.East, Vector3Int.one));
            worldBlockDatastore.AddBlock(testGear);
            
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            IReadOnlyDictionary<GearNetworkId, GearNetwork> gearNetwork = gearNetworkDatastore.GearNetworks;
            
            
            const int torqueRate = 1;
            const int generatorRpm = 10;
            var expectedEndTime = DateTime.Now.AddSeconds(1f / (generatorRpm * torqueRate * config.BeltConveyorSpeed));
            var startTime = DateTime.Now;
            beltConveyorComponent.InsertItem(item);
            
            // for (var i = 0; i < 100; i++)
            // {
            //     GameUpdater.UpdateWithWait();
            // }
            while (!dummy.IsItemExists)
            {
                GameUpdater.UpdateWithWait();
                var elapsed = DateTime.Now - startTime;
                if (elapsed.TotalSeconds > 5) Assert.Fail();
            }
            
            Assert.True(dummy.IsItemExists);
            
            var now = DateTime.Now;
            Debug.Log($"{now} {expectedEndTime} {now - expectedEndTime}");
            Assert.True(now <= expectedEndTime.AddSeconds(0.2));
            Assert.True(expectedEndTime.AddSeconds(-0.2) <= now);
        }
    }
}