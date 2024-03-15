using System;
using System.Reflection;
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
using Tests.Module;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core
{
    public class MinerMiningTest
    {
        private const int MinerId = UnitTestModBlockId.MinerId;

        //一定時間たったら鉱石が出るテスト
        [Test]
        public void MiningTest()
        {
            var (_, serviceProvider) = new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            GameUpdater.ResetUpdate();

            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var blockConfig = serviceProvider.GetService<IBlockConfig>();
            var oreConfig = serviceProvider.GetService<IOreConfig>();
            var minerBlockConfigParam = blockConfig.GetBlockConfig(MinerId).Param as MinerBlockConfigParam;

            //手動で鉱石の設定を行う
            var outputCount = minerBlockConfigParam.OutputSlot;
            var miningTime = minerBlockConfigParam.OreSettings[0].MiningTime;
            var miningItemId = oreConfig.OreIdToItemId(minerBlockConfigParam.OreSettings[0].OreId);

            var miner = new VanillaElectricMiner((MinerId, CreateBlockEntityId.Create(), 1, 100, outputCount,
                itemStackFactory, new BlockOpenableInventoryUpdateEvent()));
            miner.SetMiningItem(miningItemId, miningTime);

            var dummyInventory = new DummyBlockInventory(itemStackFactory);
            //接続先ブロックの設定
            ((IBlockInventory)miner).AddOutputConnector(dummyInventory);
            //電力の設定
            var segment = new EnergySegment();
            segment.AddEnergyConsumer(miner);
            segment.AddGenerator(new TestElectricGenerator(10000, 10));

            var mineEndTime = DateTime.Now.AddMilliseconds(miningTime);


            //テストコードの準備完了
            //鉱石1個分の採掘時間待機
            while (mineEndTime.AddSeconds(0.2).CompareTo(DateTime.Now) == 1) GameUpdater.UpdateWithWait();

            //鉱石1個が出力されているかチェック
            Assert.AreEqual(miningItemId, dummyInventory.InsertedItems[0].Id);
            Assert.AreEqual(1, dummyInventory.InsertedItems[0].Count);

            //コネクターを外す
            ((IBlockInventory)miner).RemoveOutputConnector(dummyInventory);

            //鉱石2個分の採掘時間待機
            mineEndTime = DateTime.Now.AddMilliseconds(miningTime * 2);
            while (mineEndTime.AddSeconds(0.02).CompareTo(DateTime.Now) == 1) GameUpdater.UpdateWithWait();

            //鉱石2個が残っているかチェック
            var outputSlot = miner.Items[0];
            Assert.AreEqual(miningItemId, outputSlot.Id);
            Assert.AreEqual(2, outputSlot.Count);

            //またコネクターをつなげる
            ((IBlockInventory)miner).AddOutputConnector(dummyInventory);

            //コネクターにアイテムを入れるためのアップデート
            GameUpdater.UpdateWithWait();
            
            //アイテムがさらに2個追加で入っているかチェック
            Assert.AreEqual(miningItemId, dummyInventory.InsertedItems[0].Id);
            Assert.AreEqual(3, dummyInventory.InsertedItems[0].Count);
        }
    }
}