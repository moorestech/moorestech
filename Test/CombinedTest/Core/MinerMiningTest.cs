using System;
using System.Collections.Generic;
using Core.Block.BlockFactory;
using Core.Block.BlockFactory.BlockTemplate;
using Core.Block.BlockInventory;
using Core.Block.Blocks.Miner;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using Core.Item;
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
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            var blockConfig = serviceProvider.GetService<IBlockConfig>();
            var oreConfig = serviceProvider.GetService<IOreConfig>();
            var minerBlockConfigParam = blockConfig.GetBlockConfig(MinerId).Param as MinerBlockConfigParam;
            
            //手動で鉱石の設定を行う
            var miningTime = minerBlockConfigParam.OreSettings[0].MiningTime;
            var miningItemId = oreConfig.OreIdToItemId(minerBlockConfigParam.OreSettings[0].OreId);
            
            var miner = new VanillaMiner(MinerId,10,100,miningItemId,miningTime,10,itemStackFactory);
            
            var dummyInventory = new DummyBlockInventory();
            ((IBlockInventory)miner).AddOutputConnector(dummyInventory);
            
            DateTime MineEndTime = DateTime.Now.AddMilliseconds(miningTime);
            
            //鉱石1個分の採掘時間待機
            while (MineEndTime.AddSeconds(0.2).CompareTo(DateTime.Now) == 1)
            {
                GameUpdate.Update();
            }
            
            //鉱石1個が出力されているかチェック
            Assert.AreEqual(miningItemId,dummyInventory.InsertedItems[0].Id);
            Assert.AreEqual(1,dummyInventory.InsertedItems[0].Count);
            
            //コネクターを外す
            ((IBlockInventory)miner).RemoveOutputConnector(dummyInventory);
            
            //鉱石2個分の採掘時間待機
            MineEndTime = DateTime.Now.AddMilliseconds(miningTime * 2);
            while (MineEndTime.AddSeconds(0.2).CompareTo(DateTime.Now) == 1)
            {
                GameUpdate.Update();
            }
            
            //鉱石2個が残っているかチェック
            var outputSlot = (List<IItemStack>)typeof(VanillaMiner).GetField("_outputSlot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(miner);
            Assert.AreEqual(miningItemId,outputSlot[0].Id);
            Assert.AreEqual(2,outputSlot[0].Count);
            
            //またコネクターをつなげる
            ((IBlockInventory)miner).AddOutputConnector(dummyInventory);
            
            //コネクターにアイテムを入れるためのアップデート
            GameUpdate.Update();
            //アイテムがさらに2個入っているかチェック
            Assert.AreEqual(miningItemId,dummyInventory.InsertedItems[0].Id);
            Assert.AreEqual(3,dummyInventory.InsertedItems[0].Count);
            
        } 
    }
}