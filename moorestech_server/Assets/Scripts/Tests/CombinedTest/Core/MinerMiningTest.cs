using System;
using System.Collections.Generic;
using System.Reflection;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.Miner;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Game.Map.Interface.Vein;
using Mooresmaster.Model.BlockConnectInfoModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class MinerMiningTest
    {
        //一定時間たったら鉱石が出るテスト
        [Test]
        public void MiningTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var blockFactory = ServerContext.BlockFactory;
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            
            //手動で鉱石の設定を行う
            var (mapVein, pos) = GetMapVein();
            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ElectricMinerId, pos, BlockDirection.North, out _);
            var miner = worldBlockDatastore.GetBlock(pos);
            var minerComponent = miner.GetComponent<VanillaMinerProcessorComponent>();
            
            var miningItems = (List<IItemStack>)typeof(VanillaMinerProcessorComponent).GetField("_miningItems", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(minerComponent);
            var miningItemId = miningItems[0].Id;
            var miningTime = (float)typeof(VanillaMinerProcessorComponent).GetField("_defaultMiningTime", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(minerComponent);
            
            
            var dummyInventory = new DummyBlockInventory();
            //接続先ブロックの設定
            //本当はダメなことしているけどテストだから許してヒヤシンス
            var minerConnectors = (Dictionary<IBlockInventory, ConnectedInfo>)miner.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            minerConnectors.Add(dummyInventory, new ConnectedInfo());
            
            //電力の設定
            var segment = new EnergySegment();
            segment.AddEnergyConsumer(miner.GetComponent<IElectricConsumer>());
            segment.AddGenerator(new TestElectricGenerator(new ElectricPower(10000), new BlockInstanceId(10)));
            
            var mineEndTime = DateTime.Now.AddSeconds(miningTime);
            
            
            //テストコードの準備完了
            //鉱石1個分の採掘時間待機
            while (mineEndTime.AddSeconds(0.05).CompareTo(DateTime.Now) == 1) GameUpdater.UpdateWithWait();
            
            //鉱石1個が出力されているかチェック
            Assert.AreEqual(miningItemId, dummyInventory.InsertedItems[0].Id);
            Assert.AreEqual(1, dummyInventory.InsertedItems[0].Count);
            
            //コネクターを外す
            minerConnectors.Remove(dummyInventory);
            
            //鉱石2個分の採掘時間待機
            mineEndTime = DateTime.Now.AddSeconds(miningTime * 2);
            while (mineEndTime.AddSeconds(0.05).CompareTo(DateTime.Now) == 1) GameUpdater.UpdateWithWait();
            
            //鉱石2個が残っているかチェック
            var outputSlot = miner.GetComponent<VanillaMinerProcessorComponent>().InventoryItems[0];
            Assert.AreEqual(miningItemId, outputSlot.Id);
            Assert.AreEqual(2, outputSlot.Count);
            
            //またコネクターをつなげる
            minerConnectors.Add(dummyInventory, new ConnectedInfo());
            
            //コネクターにアイテムを入れるためのアップデート
            GameUpdater.UpdateWithWait();
            
            //アイテムがさらに2個追加で入っているかチェック
            Assert.AreEqual(miningItemId, dummyInventory.InsertedItems[0].Id);
            Assert.AreEqual(3, dummyInventory.InsertedItems[0].Count);
        }
        
        public static (IMapVein mapVein, Vector3Int pos) GetMapVein()
        {
            var pos = new Vector3Int(0, 0);
            for (var i = 0; i < 500; i++)
            for (var j = 0; j < 500; j++)
            {
                List<IMapVein> veins = ServerContext.MapVeinDatastore.GetOverVeins(new Vector3Int(i, j));
                if (veins.Count == 0) continue;
                
                return (veins[0], new Vector3Int(i, j));
            }
            
            return (null, pos);
        }
    }
}