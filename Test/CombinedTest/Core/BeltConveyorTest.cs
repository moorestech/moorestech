using System;
using Core.Block;
using Core.Block.BeltConveyor.util;
using Core.Config.BeltConveyor;
using Core.GameSystem;
using Core.Item;
using NUnit.Framework;
using Test.Util;

namespace Test.CombinedTest.Core
{
    public class BeltConveyorTest
    {
        //一定個数以上アイテムが入らないテストした後、正しく次に出力されるかのテスト
        [Test]
        public void FullInsertAndChangeConnectorBeltConveyorTest()
        {
            var random = new Random(4123);
            for (int i = 0; i < 5; i++)
            {
                int id = random.Next(0, 10);
                var conf = BeltConveyorConfig.GetBeltConveyorData(0);
                var item = ItemStackFactory.NewItemStack(id, conf.BeltConveyorItemNum + 1);
                var beltConveyor = BeltConveyorFactory.Create(1, Int32.MaxValue,new NullIBlockInventory());

                var endTime = DateTime.Now.AddMilliseconds(conf.TimeOfItemEnterToExit);
                while ( DateTime.Now < endTime.AddSeconds(0.2))
                { 
                    item = beltConveyor.InsertItem(item);
                    GameUpdate.Update();
                }
                Assert.AreEqual(item.Amount,1);

                var dummy = new DummyBlockInventory();
                beltConveyor.ChangeConnector(dummy);
                GameUpdate.Update();
                
                Assert.AreEqual(ItemStackFactory.NewItemStack(id,1).ToString(),dummy.InsertedItems[0].ToString());
            }
        }
        
        //一個のアイテムが入って正しく搬出されるかのテスト
        [Test]
        public void InsertBeltConveyorTest()
        {
            var random = new Random(4123);
            for (int i = 0; i < 5; i++)
            {
                int id = random.Next(0, 10);
                int amount = random.Next(1, 10);
                var item = ItemStackFactory.NewItemStack(id, amount);
                var dummy = new DummyBlockInventory(1);
                var beltConveyor = BeltConveyorFactory.Create(0, Int32.MaxValue,dummy);

                var expectedEndTime = DateTime.Now.AddMilliseconds(
                    BeltConveyorConfig.GetBeltConveyorData(0).TimeOfItemEnterToExit);
                var outputItem = beltConveyor.InsertItem(item);
                while (!dummy.IsItemExists)
                {
                    GameUpdate.Update();
                }
                Assert.True(DateTime.Now <= expectedEndTime.AddSeconds(0.2));
                Assert.True(expectedEndTime.AddSeconds(-0.2) <= DateTime.Now);
                
                Assert.True(outputItem.Equals(ItemStackFactory.NewItemStack(id,amount-1)));
                var tmp = ItemStackFactory.NewItemStack(id, 1);
                Console.WriteLine($"{tmp} {dummy.InsertedItems[0]}");
                Assert.AreEqual(tmp.ToString(),dummy.InsertedItems[0].ToString());
            }
        }
        //ベルトコンベアのインベントリをフルにするテスト
        [Test]
        public void FullInsertBeltConveyorTest()
        {
            var random = new Random(4123);
            for (int i = 0; i < 5; i++)
            {
                int id = random.Next(0, 10);
                var conf = BeltConveyorConfig.GetBeltConveyorData(0);
                var item = ItemStackFactory.NewItemStack(id, conf.BeltConveyorItemNum + 1);
                var dummy = new DummyBlockInventory(conf.BeltConveyorItemNum);
                var beltConveyor = BeltConveyorFactory.Create(0, Int32.MaxValue,dummy);

                while (!dummy.IsItemExists)
                { 
                    item = beltConveyor.InsertItem(item);
                    GameUpdate.Update();
                }
                
                Assert.True(item.Equals(ItemStackFactory.NewItemStack(id,0)));
                var tmp = ItemStackFactory.NewItemStack(id, conf.BeltConveyorItemNum);
                Assert.True(dummy.InsertedItems[0].Equals(tmp));
            }
        }
        //二つのアイテムが入ったとき、一方しか入らないテスト
        [Test]
        public void Insert2ItemBeltConveyorTest()
        {
            var random = new Random(4123);
            for (int i = 0; i < 5; i++)
            {
                //必要な変数を作成
                var item1 = ItemStackFactory.NewItemStack(random.Next(0,10), random.Next(1,10));
                var item2 = ItemStackFactory.NewItemStack(random.Next(0,10), random.Next(1,10));

                var beltConveyor = BeltConveyorFactory.Create(0, Int32.MaxValue,new DummyBlockInventory());

                var item1Out = beltConveyor.InsertItem(item1);
                var item2Out = beltConveyor.InsertItem(item2);

                Assert.True(item1Out.Equals(item1.SubItem(1)));
                Assert.True(item2Out.Equals(item2));
            }
        }
    }
}