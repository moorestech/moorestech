using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Microsoft.Extensions.DependencyInjection;
using Mooresmaster.Model.BlockConnectInfoModule;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class GearBeltConveyorTest
    {
        // トルクの供給率が100%のとき、指定した時間でアイテムが出てくるテスト
        [Test]
        public void OutputTestWhenTorqueSuppliedRateIs100()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            const int id = 2;
            const int count = 3;
            var item = itemStackFactory.Create(new ItemId(id), count);
            var dummy = new DummyBlockInventory();
            
            
            // gearBeltConveyorブロックを生成
            var gearBeltConveyorPosition = new Vector3Int(0, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, gearBeltConveyorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gearBeltConveyor);
            var beltConveyorComponent = gearBeltConveyor.GetComponent<VanillaBeltConveyorComponent>();
            var connectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)gearBeltConveyor.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            connectInventory.Add(dummy, new ConnectedInfo());
            
            // generatorブロックを作成
            var generatorPosition = new Vector3Int(1, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.InfinityTorqueSimpleGearGenerator, generatorPosition, BlockDirection.East, Array.Empty<BlockCreateParam>(), out var generator);
            
            // testGearブロックを作成
            var testGearPosition = new Vector3Int(2, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SmallGear, testGearPosition, BlockDirection.East, Array.Empty<BlockCreateParam>(), out var testGear);
            
            var gearNetworkDatastore = serviceProvider.GetService<GearNetworkDatastore>();
            IReadOnlyDictionary<GearNetworkId, GearNetwork> gearNetwork = gearNetworkDatastore.GearNetworks;
            
            
            const int torqueRate = 1;
            const int generatorRpm = 10;
            var gearBeltConveyorBlockParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor).BlockParam as GearBeltConveyorBlockParam;
            var duration = 1f / (generatorRpm * torqueRate * gearBeltConveyorBlockParam.BeltConveyorSpeed);
            var expectedEndTime = DateTime.Now.AddSeconds(duration);
            var startTime = DateTime.Now;
            beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);
            
            // for (var i = 0; i < 100; i++)
            // {
            //     GameUpdater.UpdateWithWait();
            // }
            var c = 0;
            while (!dummy.IsItemExists)
            {
                c++;
                GameUpdater.UpdateWithWait();
                var elapsed = DateTime.Now - startTime;
                if (elapsed.TotalSeconds > 20) Assert.Fail();
            }
            
            Assert.True(dummy.IsItemExists);
            
            var now = DateTime.Now;
            Debug.Log($"{now.Second} {expectedEndTime.Second}\n{(now - startTime).TotalSeconds}\n{(expectedEndTime - now).TotalSeconds}\n{duration}\n{c}");
            Assert.True(now <= expectedEndTime.AddSeconds(0.4));
            Assert.True(expectedEndTime.AddSeconds(-0.4) <= now);
        }

        // RPMが0のときはアイテムが搬送されないことのテスト
        [Test]
        public void NoOutputWhenRpmIsZero()
        {
            // テスト用のDIコンテナを初期化する
            // Initialize the test DI container
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            var itemStackFactory = ServerContext.ItemStackFactory;
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            // 歯車ベルトコンベアと出力先を用意する
            // Prepare the gear belt conveyor and its output
            var gearBeltConveyorPosition = new Vector3Int(0, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, gearBeltConveyorPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var gearBeltConveyor);
            var connectInventory = (Dictionary<IBlockInventory, ConnectedInfo>)gearBeltConveyor.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            var dummy = new DummyBlockInventory();
            connectInventory.Add(dummy, new ConnectedInfo());
            
            var beltConveyorComponent = gearBeltConveyor.GetComponent<VanillaBeltConveyorComponent>();
            var gearBeltConveyorComponent = gearBeltConveyor.GetComponent<GearBeltConveyorComponent>();
            
            // 歯車ジェネレーターで一度稼働させ、速度を設定する
            // Run once with a gear generator to set the speed
            var generatorPosition = new Vector3Int(1, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.East, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            var generator = generatorBlock.GetComponent<global::Game.Block.Blocks.Gear.SimpleGearGeneratorComponent>();
            generator.SetGenerateRpm(10f);
            generator.SetGenerateTorque(1f);
            GameUpdater.SpecifiedDeltaTimeUpdate(0.1);
            
            Assert.True(gearBeltConveyorComponent.CurrentRpm.AsPrimitive() > 0f);
            
            // 出力を止めてRPMを0にする
            // Stop output to force RPM to 0
            generator.SetGenerateTorque(0f);
            GameUpdater.SpecifiedDeltaTimeUpdate(0.1);
            
            Assert.AreEqual(0f, gearBeltConveyorComponent.CurrentRpm.AsPrimitive());
            
            // RPM0の状態でアイテムを挿入する
            // Insert an item while RPM is zero
            var item = itemStackFactory.Create(new ItemId(2), 1);
            beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);
            
            // ベルトの速度に相当する時間を超えても搬送されないことを確認する
            // Ensure the item is not transported even after exceeding the belt travel time
            var gearBeltParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor).BlockParam as GearBeltConveyorBlockParam;
            var previousSpeed = 10f * 1f * gearBeltParam.BeltConveyorSpeed;
            var timeOfItemEnterToExit = 1f / previousSpeed;
            var updateCount = (int)Math.Ceiling(timeOfItemEnterToExit / 0.1f) + beltConveyorComponent.BeltConveyorItems.Count + 2;
            for (var i = 0; i < updateCount; i++) GameUpdater.SpecifiedDeltaTimeUpdate(0.1);
            
            Assert.False(dummy.IsItemExists);
        }
    }
}
