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
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;
using Random = System.Random;

namespace Tests.CombinedTest.Core
{
    /// <summary>
    ///     コンフィグが変わったらこのテストを変更に応じて変更してください
    /// </summary>
    public class BeltConveyorTest
    {
        //一定個数以上アイテムが入らないテストした後、正しく次に出力されるかのテスト
        [Test]
        public void FullInsertAndChangeConnectorBeltConveyorTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var blockConfig = ServerContext.BlockConfig;
            var config = (BeltConveyorConfigParam)blockConfig.GetBlockConfig(3).Param;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var random = new Random(4123);
            for (var i = 0; i < 2; i++) //あまり深い意味はないが取りあえずテストは2回実行する
            {
                var id = random.Next(0, 10);
                
                var item = itemStackFactory.Create(id, config.BeltConveyorItemNum + 1);
                var beltConveyor = ServerContext.BlockFactory.Create(ForUnitTestModBlockId.BeltConveyorId, new BlockInstanceId(int.MaxValue), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
                var beltConveyorComponent = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();
                
                var endTime = DateTime.Now.AddSeconds(config.TimeOfItemEnterToExit);
                
                while (DateTime.Now < endTime.AddSeconds(0.05))
                {
                    item = beltConveyorComponent.InsertItem(item);
                    GameUpdater.UpdateWithWait();
                }
                
                Assert.AreEqual(item.Count, 1);
                
                var dummy = new DummyBlockInventory();
                
                var connectInventory = (Dictionary<IBlockInventory, (IConnectOption, IConnectOption)>)beltConveyor.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
                connectInventory.Add(dummy, (null, null));
                GameUpdater.UpdateWithWait();
                
                Assert.AreEqual(itemStackFactory.Create(id, 1).ToString(), dummy.InsertedItems[0].ToString());
            }
        }
        
        //一個のアイテムが入って正しく搬出されるかのテスト
        [Test]
        public void InsertBeltConveyorTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var blockConfig = ServerContext.BlockConfig;
            var config = (BeltConveyorConfigParam)blockConfig.GetBlockConfig(3).Param;
            var blockFactory = ServerContext.BlockFactory;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            
            const int id = 2;
            const int count = 3;
            var item = itemStackFactory.Create(id, count);
            var dummy = new DummyBlockInventory();
            
            // アイテムを挿入
            var beltConveyor = blockFactory.Create(3, new BlockInstanceId(int.MaxValue), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
            var beltConveyorComponent = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();
            
            var connectInventory = (Dictionary<IBlockInventory, (IConnectOption, IConnectOption)>)beltConveyor.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
            connectInventory.Add(dummy, (null, null));
            
            var expectedEndTime = DateTime.Now.AddSeconds(config.TimeOfItemEnterToExit);
            var outputItem = beltConveyorComponent.InsertItem(item);
            
            //5秒以上経過したらループを抜ける 
            while (!dummy.IsItemExists) GameUpdater.UpdateWithWait();
            
            
            //チェック
            Assert.True(DateTime.Now <= expectedEndTime.AddSeconds(0.1));
            Assert.True(expectedEndTime.AddSeconds(-0.1) <= DateTime.Now);
            
            Debug.Log($"{(DateTime.Now - expectedEndTime).TotalSeconds}");
            
            Assert.True(outputItem.Equals(itemStackFactory.Create(id, count - 1)));
            var tmp = itemStackFactory.Create(id, 1);
            Debug.Log($"{tmp} {dummy.InsertedItems[0]}");
            Assert.AreEqual(tmp.ToString(), dummy.InsertedItems[0].ToString());
        }
        
        //ベルトコンベアのインベントリをフルにするテスト
        [Test]
        public void FullInsertBeltConveyorTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            
            var blockConfig = ServerContext.BlockConfig;
            var config = (BeltConveyorConfigParam)blockConfig.GetBlockConfig(3).Param;
            var blockFactory = ServerContext.BlockFactory;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var random = new Random(4123);
            for (var i = 0; i < 2; i++) //あまり深い意味はないが取りあえずテストは2回実行する
            {
                var id = random.Next(1, 11);
                var item = itemStackFactory.Create(id, config.BeltConveyorItemNum + 1);
                var dummy = new DummyBlockInventory(config.BeltConveyorItemNum);
                var beltConveyor = blockFactory.Create(3, new BlockInstanceId(int.MaxValue), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
                var beltConveyorComponent = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();
                
                var connectInventory = (Dictionary<IBlockInventory, (IConnectOption, IConnectOption)>)beltConveyor.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
                connectInventory.Add(dummy, (null, null));
                
                while (!dummy.IsItemExists)
                {
                    item = beltConveyorComponent.InsertItem(item);
                    GameUpdater.UpdateWithWait();
                }
                
                Assert.True(item.Equals(itemStackFactory.Create(id, 0)));
                var tmp = itemStackFactory.Create(id, config.BeltConveyorItemNum);
                Assert.True(dummy.InsertedItems[0].Equals(tmp));
            }
        }
        
        //二つのアイテムが入ったとき、一方しか入らないテスト
        [Test]
        public void Insert2ItemBeltConveyorTest()
        {
            var (_, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var blockFactory = ServerContext.BlockFactory;
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            var random = new Random(4123);
            for (var i = 0; i < 2; i++) //あまり深い意味はないが取りあえずテストは2回実行する
            {
                //必要な変数を作成
                var item1 = itemStackFactory.Create(random.Next(1, 11), random.Next(1, 10));
                var item2 = itemStackFactory.Create(random.Next(1, 11), random.Next(1, 10));
                
                var beltConveyor = blockFactory.Create(3, new BlockInstanceId(int.MaxValue), new BlockPositionInfo(Vector3Int.one, BlockDirection.North, Vector3Int.one));
                var beltConveyorComponent = beltConveyor.GetComponent<VanillaBeltConveyorComponent>();
                
                var item1Out = beltConveyorComponent.InsertItem(item1);
                var item2Out = beltConveyorComponent.InsertItem(item2);
                
                Assert.True(item1Out.Equals(item1.SubItem(1)));
                Assert.True(item2Out.Equals(item2));
            }
        }
    }
}