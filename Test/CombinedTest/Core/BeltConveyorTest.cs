using System;
using Core.Block.BeltConveyor.Generally;
using Core.Block.BlockFactory;
using Core.Block.Config;
using Core.Block.Config.LoadConfig.Param;
using Core.Item;
using Core.Item.Config;
using Core.Update;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Test.Module;
using Test.Util;

namespace Test.CombinedTest.Core
{
    /// <summary>
    /// コンフィグが変わったらこのテストを変更に応じて変更してください
    /// </summary>
    public class BeltConveyorTest
    {
        private ItemStackFactory _itemStackFactory;
        [SetUp]
        public void Setup()
        {
            _itemStackFactory = new ItemStackFactory(new TestItemConfig());
        }
        
        
        //一定個数以上アイテムが入らないテストした後、正しく次に出力されるかのテスト
        [Test]
        public void FullInsertAndChangeConnectorBeltConveyorTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            
            var blockConfig = serviceProvider.GetService<IBlockConfig>();
            var config = (BeltConveyorConfigParam)blockConfig.GetBlockConfig(3).Param;
            var blockFactory = serviceProvider.GetService<BlockFactory>();
            
            var random = new Random(4123);
            for (int i = 0; i < 5; i++)
            {
                int id = random.Next(0, 10);
                
                var item = _itemStackFactory.Create(id, config.BeltConveyorItemNum + 1);
                var beltConveyor = (NormalBeltConveyor)blockFactory.Create(3, Int32.MaxValue);

                var endTime = DateTime.Now.AddMilliseconds(config.TimeOfItemEnterToExit);
                while ( DateTime.Now < endTime.AddSeconds(0.2))
                { 
                    item = beltConveyor.InsertItem(item);
                    GameUpdate.Update();
                }
                Assert.AreEqual(item.Count,1);

                var dummy = new DummyBlockInventory();
                beltConveyor.AddConnector(dummy);
                GameUpdate.Update();
                
                Assert.AreEqual(_itemStackFactory.Create(id,1).ToString(),dummy.InsertedItems[0].ToString());
            }
        }
        
        //一個のアイテムが入って正しく搬出されるかのテスト
        [Test]
        public void InsertBeltConveyorTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            
            var blockConfig = serviceProvider.GetService<IBlockConfig>();
            var config = (BeltConveyorConfigParam)blockConfig.GetBlockConfig(3).Param;
            var blockFactory = serviceProvider.GetService<BlockFactory>();
            
            
            var random = new Random(4123);
            for (int i = 0; i < 5; i++)
            {
                int id = random.Next(1, 11);
                int count = random.Next(1, 10);
                var item = _itemStackFactory.Create(id, count);
                var dummy = new DummyBlockInventory(1);
                var beltConveyor = (NormalBeltConveyor)blockFactory.Create(3, Int32.MaxValue);
                beltConveyor.AddConnector(dummy);


                var expectedEndTime = DateTime.Now.AddMilliseconds(
                    config.TimeOfItemEnterToExit);
                var outputItem = beltConveyor.InsertItem(item);
                while (!dummy.IsItemExists)
                {
                    GameUpdate.Update();
                }
                Assert.True(DateTime.Now <= expectedEndTime.AddSeconds(0.2));
                Assert.True(expectedEndTime.AddSeconds(-0.2) <= DateTime.Now);
                
                Assert.True(outputItem.Equals(_itemStackFactory.Create(id,count-1)));
                var tmp = _itemStackFactory.Create(id, 1);
                Console.WriteLine($"{tmp} {dummy.InsertedItems[0]}");
                Assert.AreEqual(tmp.ToString(),dummy.InsertedItems[0].ToString());
            }
        }
        //ベルトコンベアのインベントリをフルにするテスト
        [Test]
        public void FullInsertBeltConveyorTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            
            var blockConfig = serviceProvider.GetService<IBlockConfig>();
            var config = (BeltConveyorConfigParam)blockConfig.GetBlockConfig(3).Param;
            var blockFactory = serviceProvider.GetService<BlockFactory>();
            
            var random = new Random(4123);
            for (int i = 0; i < 5; i++)
            {
                int id = random.Next(1, 11);
                var item = _itemStackFactory.Create(id, config.BeltConveyorItemNum + 1);
                var dummy = new DummyBlockInventory(config.BeltConveyorItemNum);
                var beltConveyor = (NormalBeltConveyor)blockFactory.Create(3, Int32.MaxValue);
                beltConveyor.AddConnector(dummy);

                while (!dummy.IsItemExists)
                { 
                    item = beltConveyor.InsertItem(item);
                    GameUpdate.Update();
                }
                
                Assert.True(item.Equals(_itemStackFactory.Create(id,0)));
                var tmp = _itemStackFactory.Create(id, config.BeltConveyorItemNum);
                Assert.True(dummy.InsertedItems[0].Equals(tmp));
            }
        }
        //二つのアイテムが入ったとき、一方しか入らないテスト
        [Test]
        public void Insert2ItemBeltConveyorTest()
        {
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            var blockFactory = serviceProvider.GetService<BlockFactory>();
            
            var random = new Random(4123);
            for (int i = 0; i < 5; i++)
            {
                //必要な変数を作成
                var item1 = _itemStackFactory.Create(random.Next(1,11), random.Next(1,10));
                var item2 = _itemStackFactory.Create(random.Next(1,11), random.Next(1,10));

                var beltConveyor = (NormalBeltConveyor) blockFactory.Create(3, Int32.MaxValue);

                var item1Out = beltConveyor.InsertItem(item1);
                var item2Out = beltConveyor.InsertItem(item2);

                Assert.True(item1Out.Equals(item1.SubItem(1)));
                Assert.True(item2Out.Equals(item2));
            }
        }
    }
}