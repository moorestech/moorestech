using System;
using System.Collections.Generic;
using System.Linq;
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

            // ギアネットワークを確立するための更新サイクルを実行
            // Run update cycle to establish gear network
            GameUpdater.RunFrames(1);

            const int torqueRate = 1;
            const int generatorRpm = 10;
            var gearBeltConveyorBlockParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor).BlockParam as GearBeltConveyorBlockParam;
            var duration = 1f / (generatorRpm * torqueRate * gearBeltConveyorBlockParam.BeltConveyorSpeed);

            // 期待されるtick数を計算
            // Calculate expected tick count
            var expectedTicks = (int)(duration * GameUpdater.TicksPerSecond);
            beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);

            // tick数でループ制御（タイムアウト付き）
            // Loop controlled by tick count (with timeout)
            var elapsedTicks = 0;
            var maxTicks = (int)(20 * GameUpdater.TicksPerSecond); // 20秒でタイムアウト
            while (!dummy.IsItemExists && elapsedTicks < maxTicks)
            {
                elapsedTicks++;
                GameUpdater.RunFrames(1);
            }

            Assert.True(dummy.IsItemExists, "Item should have been output");

            // 期待したtick数近辺でアイテムが到達したことを確認
            // Verify item arrived around expected tick count
            var tickTolerance = (int)(0.4 * GameUpdater.TicksPerSecond); // 0.4秒の許容誤差
            Debug.Log($"Expected ticks: {expectedTicks}, Elapsed ticks: {elapsedTicks}, Duration: {duration}");
            Assert.True(elapsedTicks <= expectedTicks + tickTolerance, $"Item should arrive within tolerance. Expected: {expectedTicks}, Actual: {elapsedTicks}");
            Assert.True(elapsedTicks >= expectedTicks - tickTolerance, $"Item should not arrive too early. Expected: {expectedTicks}, Actual: {elapsedTicks}");
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
            GameUpdater.RunFrames(GameUpdater.SecondsToTicks(0.1));

            Assert.True(gearBeltConveyorComponent.CurrentRpm.AsPrimitive() > 0f);

            // 出力を止めてRPMを0にする
            // Stop output to force RPM to 0
            generator.SetGenerateTorque(0f);
            GameUpdater.RunFrames(GameUpdater.SecondsToTicks(0.1));
            
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
            for (var i = 0; i < updateCount; i++) GameUpdater.RunFrames(GameUpdater.SecondsToTicks(0.1));
            
            Assert.False(dummy.IsItemExists);
        }

        // 停止中にアイテムを投入し、速度復帰後に正常に搬送されることのテスト
        // Test that items inserted while stopped are transported normally after speed recovery
        [Test]
        public void ItemInsertedWhileStoppedShouldTransportAfterSpeedRecovery()
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

            // 歯車ジェネレーターを配置し、一度稼働させてから停止する
            // Place a gear generator, run once, then stop
            var generatorPosition = new Vector3Int(1, 0, 0);
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, generatorPosition, BlockDirection.East, Array.Empty<BlockCreateParam>(), out var generatorBlock);
            var generator = generatorBlock.GetComponent<global::Game.Block.Blocks.Gear.SimpleGearGeneratorComponent>();
            generator.SetGenerateRpm(10f);
            generator.SetGenerateTorque(1f);
            GameUpdater.RunFrames(GameUpdater.SecondsToTicks(0.1));

            Assert.True(gearBeltConveyorComponent.CurrentRpm.AsPrimitive() > 0f, "Belt should be running initially");

            // 出力を止めてRPMを0にする
            // Stop output to force RPM to 0
            generator.SetGenerateTorque(0f);
            GameUpdater.RunFrames(GameUpdater.SecondsToTicks(0.1));

            Assert.AreEqual(0f, gearBeltConveyorComponent.CurrentRpm.AsPrimitive(), "Belt should be stopped");

            // 停止中にアイテムを挿入する
            // Insert an item while the belt is stopped
            var item = itemStackFactory.Create(new ItemId(2), 1);
            beltConveyorComponent.InsertItem(item, InsertItemContext.Empty);

            // アイテムがベルトに載っていることを確認
            // Verify the item is on the belt
            Assert.AreEqual(1, beltConveyorComponent.BeltConveyorItems.Count(i => i != null), "Item should be on the belt");

            // 速度を復帰させる
            // Restore speed
            generator.SetGenerateTorque(1f);
            GameUpdater.RunFrames(GameUpdater.SecondsToTicks(0.1));

            Assert.True(gearBeltConveyorComponent.CurrentRpm.AsPrimitive() > 0f, "Belt should be running again");

            // アイテムが搬送されるまで十分な時間待機する（1tickずつ進める）
            // Wait enough time for the item to be transported (advance 1 tick at a time)
            var gearBeltParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor).BlockParam as GearBeltConveyorBlockParam;
            var speed = 10f * 1f * gearBeltParam.BeltConveyorSpeed;
            var timeOfItemEnterToExit = 1f / speed;
            var waitTicks = GameUpdater.SecondsToTicks(timeOfItemEnterToExit * 2); // 2倍の時間待つ
            for (uint i = 0; i < waitTicks; i++)
            {
                GameUpdater.RunFrames(1);
            }

            // アイテムが出力先に到達していることを確認
            // Verify the item has reached the output
            Assert.True(dummy.IsItemExists, "Item should have been transported after speed recovery");
        }
    }
}
