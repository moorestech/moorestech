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
            GameUpdater.AdvanceTicks(1);

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
                GameUpdater.AdvanceTicks(1);
            }

            Assert.True(dummy.IsItemExists, "Item should have been output");

            // 期待したtick数近辺でアイテムが到達したことを確認
            // Verify item arrived around expected tick count
            var tickTolerance = (int)(0.4 * GameUpdater.TicksPerSecond); // 0.4秒の許容誤差
            Debug.Log($"Expected ticks: {expectedTicks}, Elapsed ticks: {elapsedTicks}, Duration: {duration}");
            Assert.True(elapsedTicks <= expectedTicks + tickTolerance, $"Item should arrive within tolerance. Expected: {expectedTicks}, Actual: {elapsedTicks}");
            Assert.True(elapsedTicks >= expectedTicks - tickTolerance, $"Item should not arrive too early. Expected: {expectedTicks}, Actual: {elapsedTicks}");
        }
    }
}
