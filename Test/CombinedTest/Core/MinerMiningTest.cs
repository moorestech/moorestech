using System;
using Core.Block.BlockFactory;
using Core.Block.BlockInventory;
using Core.Block.Blocks.Miner;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using Core.Ore;
using Core.Update;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Test.Module;

namespace Test.CombinedTest.Core
{
    public class MinerMiningTest
    {
        private int MinerId = 1;
        
        //TODO マイナーを保存するテスト
        //一定時間たったら鉱石が出るテスト
        [Test]
        public void MiningTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var blockFactory = serviceProvider.GetService<BlockFactory>();
            var blockConfig = serviceProvider.GetService<IBlockConfig>();
            var oreConfig = serviceProvider.GetService<IOreConfig>();
            //TODo minerの鉱石チェックサービスをテスト用モジュールにする
            var miner = new VanillaMiner(MinerId,10);
            
            var dummyInventory = new DummyBlockInventory();
            ((IBlockInventory)miner).AddOutputConnector(dummyInventory);
            var minerBlockConfigParam = blockConfig.GetBlockConfig(MinerId).Param as MinerBlockConfigParam;
            var oreId = minerBlockConfigParam.OreSettings[0].OreId;
            
            DateTime MineEndTime = DateTime.Now.AddMilliseconds(minerBlockConfigParam.OreSettings[0].MiningTime);
            
            //鉱石1個分の採掘時間待機
            while (MineEndTime.AddSeconds(0.2).CompareTo(DateTime.Now) == 1)
            {
                GameUpdate.Update();
            }
            
            //鉱石1個が出力されているかチェック
            Assert.AreEqual(oreConfig.OreIdToItemId(oreId),dummyInventory.InsertedItems[0].Id);
            Assert.AreEqual(1,dummyInventory.InsertedItems[0].Count);
            
            //コネクターを外す
            ((IBlockInventory)miner).RemoveOutputConnector(dummyInventory);
            
            //鉱石2個分の採掘時間待機
            MineEndTime = DateTime.Now.AddMilliseconds(minerBlockConfigParam.OreSettings[0].MiningTime * 2);
            while (MineEndTime.AddSeconds(0.2).CompareTo(DateTime.Now) == 1)
            {
                GameUpdate.Update();
            }
            
            //鉱石2個が残っているかチェック
            //TODO リフレクションを使ってチェック
            
            //またコネクターをつなげる
            ((IBlockInventory)miner).AddOutputConnector(dummyInventory);
            
            //コネクターにアイテムを入れるためのアップデート
            GameUpdate.Update();
            //アイテムがさらに2個入っているかチェック
            Assert.AreEqual(oreConfig.OreIdToItemId(oreId),dummyInventory.InsertedItems[0].Id);
            Assert.AreEqual(3,dummyInventory.InsertedItems[0].Count);
            
        } 
    }
}