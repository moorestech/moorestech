using System;
using System.Collections.Generic;
using Core.EnergySystem;
using Core.Item.Interface;
using Core.Item.Config;
using Core.Update;
using Game.Block.BlockInventory;
using Game.Block.Blocks.Miner;
using Game.Block.Component;
using Game.Block.Component.IOConnector;
using Game.Block.Config.LoadConfig.Param;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.World.Interface.Util;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class MinerMiningTest
    {
        private const int MinerId = UnitTestModBlockId.MinerId;

        //一定時間たったら鉱石が出るテスト
        [Test]
        public void MiningTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            GameUpdater.ResetUpdate();

            var itemStackFactory = serviceProvider.GetService<IItemStackFactory>();
            var blockConfig = serviceProvider.GetService<IBlockConfig>();
            var itemConfig = serviceProvider.GetService<IItemConfig>();
            var componentFactory = serviceProvider.GetService<ComponentFactory>();

            var minerBlockConfigParam = blockConfig.GetBlockConfig(MinerId).Param as MinerBlockConfigParam;

            //手動で鉱石の設定を行う
            var outputCount = minerBlockConfigParam.OutputSlot;
            var miningSetting = minerBlockConfigParam.MineItemSettings[0];
            var miningTime = miningSetting.MiningTime;
            var miningItemId = miningSetting.ItemId;

            var posInfo = new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one);
            var miner = new VanillaElectricMiner((MinerId, CreateBlockEntityId.Create(), 1, 100, outputCount, itemStackFactory, new BlockOpenableInventoryUpdateEvent(), posInfo, componentFactory));
            miner.SetMiningItem(miningItemId, miningTime);

            var dummyInventory = new DummyBlockInventory(itemStackFactory);
            //接続先ブロックの設定
            //本当はダメなことしているけどテストだから許してヒヤシンス
            var minerConnectors = (List<IBlockInventory>)miner.ComponentManager.GetComponent<InputConnectorComponent>().ConnectInventory;
            minerConnectors.Add(dummyInventory);

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
            minerConnectors.Remove(dummyInventory);

            //鉱石2個分の採掘時間待機
            mineEndTime = DateTime.Now.AddMilliseconds(miningTime * 2);
            while (mineEndTime.AddSeconds(0.02).CompareTo(DateTime.Now) == 1) GameUpdater.UpdateWithWait();

            //鉱石2個が残っているかチェック
            var outputSlot = miner.Items[0];
            Assert.AreEqual(miningItemId, outputSlot.Id);
            Assert.AreEqual(2, outputSlot.Count);

            //またコネクターをつなげる
            minerConnectors.Add(dummyInventory);

            //コネクターにアイテムを入れるためのアップデート
            GameUpdater.UpdateWithWait();

            //アイテムがさらに2個追加で入っているかチェック
            Assert.AreEqual(miningItemId, dummyInventory.InsertedItems[0].Id);
            Assert.AreEqual(3, dummyInventory.InsertedItems[0].Count);
        }
    }
}