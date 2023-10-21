#if NET6_0
using System;
using Core.EnergySystem;
using Core.Item;
using Core.Ore;
using Core.Update;
using Game.Block.BlockInventory;
using Game.Block.Blocks.Miner;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Event;
using Game.Block.Interface.BlockConfig;
using Game.World.Interface.Util;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Test.Module;
using Test.Module.TestMod;

namespace Test.CombinedTest.Core
{
    public class MinerMiningTest
    {
        private readonly int MinerId = UnitTestModBlockId.MinerId;

        
        [Test]
        public void MiningTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var blockConfig = serviceProvider.GetService<IBlockConfig>();
            var oreConfig = serviceProvider.GetService<IOreConfig>();
            var minerBlockConfigParam = blockConfig.GetBlockConfig(MinerId).Param as MinerBlockConfigParam;

            
            var outputCount = minerBlockConfigParam.OutputSlot;
            var miningTime = minerBlockConfigParam.OreSettings[0].MiningTime;
            var miningItemId = oreConfig.OreIdToItemId(minerBlockConfigParam.OreSettings[0].OreId);

            var miner = new VanillaElectricMiner((MinerId, CreateBlockEntityId.Create(), 1, 100, outputCount, itemStackFactory, new BlockOpenableInventoryUpdateEvent()));
            miner.SetMiningItem(miningItemId, miningTime);

            var dummyInventory = new DummyBlockInventory(itemStackFactory);
            
            ((IBlockInventory)miner).AddOutputConnector(dummyInventory);
            
            var segment = new EnergySegment();
            segment.AddEnergyConsumer(miner);
            segment.AddGenerator(new TestElectricGenerator(10000, 10));

            var MineEndTime = DateTime.Now.AddMilliseconds(miningTime);


            
            //1
            while (MineEndTime.AddSeconds(0.2).CompareTo(DateTime.Now) == 1) GameUpdater.Update();

            //1
            Assert.AreEqual(miningItemId, dummyInventory.InsertedItems[0].Id);
            Assert.AreEqual(1, dummyInventory.InsertedItems[0].Count);

            
            ((IBlockInventory)miner).RemoveOutputConnector(dummyInventory);

            //2
            MineEndTime = DateTime.Now.AddMilliseconds(miningTime * 2);
            while (MineEndTime.AddSeconds(0.02).CompareTo(DateTime.Now) == 1) GameUpdater.Update();

            miner.Update();
            //2
            var outputSlot = miner.Items[0];
            Assert.AreEqual(miningItemId, outputSlot.Id);
            Assert.AreEqual(2, outputSlot.Count);

            
            ((IBlockInventory)miner).AddOutputConnector(dummyInventory);

            
            GameUpdater.Update();
            //2
            Assert.AreEqual(miningItemId, dummyInventory.InsertedItems[0].Id);
            Assert.AreEqual(3, dummyInventory.InsertedItems[0].Count);
        }
    }
}
#endif