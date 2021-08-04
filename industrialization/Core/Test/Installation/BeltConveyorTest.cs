using System;
using industrialization.Core.GameSystem;
using industrialization.Core.Installation;
using industrialization.Core.Installation.BeltConveyor.Generally;
using industrialization.Core.Installation.BeltConveyor.util;
using industrialization.Core.Item;
using NUnit.Framework;

namespace industrialization.Core.Test.Installation
{
    public class BeltConveyorTest
    {
        //TODO アイテム搬出入時間のテスト
        //TODO 一定個数以上アイテムが入らないテスト
        
        //一個のアイテムが入って正しく搬出されるかのテスト
        [Test]
        public void InsertBeltConveyorTest()
        {
            var random = new Random(4123);
            for (int i = 0; i < 20; i++)
            {
                int id = random.Next(0, 10);
                int amount = random.Next(1, 10);
                var item = ItemStackFactory.NewItemStack(id, amount);
                var dummy = new DummyInstallationInventory(1);
                var beltConveyor = BeltConveyorFactory.Create(0, Int32.MaxValue,new DummyInstallationInventory());


                var outputItem = beltConveyor.InsertItem(item);

                while (!dummy.IsItemExists)
                {
                    GameUpdate.Update();
                }
                
                Assert.True(outputItem.Equals(ItemStackFactory.NewItemStack(id,amount-1)));
                var tmp = ItemStackFactory.NewItemStack(id, 1);
                Console.WriteLine($"{tmp} {dummy.InsertedItems[0]}");
                Assert.True(dummy.InsertedItems[0].Equals(tmp));
            }
        }
        //二つのアイテムが入ったとき、一方しか入らないテスト
        [Test]
        public void Insert2ItemBeltConveyorTest()
        {
            var random = new Random(4123);
            for (int i = 0; i < 100; i++)
            {
                //必要な変数を作成
                var item1 = ItemStackFactory.NewItemStack(random.Next(0,10), random.Next(1,10));
                var item2 = ItemStackFactory.NewItemStack(random.Next(0,10), random.Next(1,10));

                var beltConveyor = BeltConveyorFactory.Create(0, Int32.MaxValue,new DummyInstallationInventory());

                var item1Out = beltConveyor.InsertItem(item1);
                var item2Out = beltConveyor.InsertItem(item2);

                Assert.True(item1Out.Equals(item1.SubItem(1)));
                Assert.True(item2Out.Equals(item2));
            }
        }
        //ランダムなアイテムを搬入し、搬出を確かめるテスト
        [Test]
        public void RandomItemInsertTest()
        {
            var random = new Random(4123);
            for (int i = 0; i < 100; i++)
            {
                //必要な変数を作成
                var item1 = ItemStackFactory.NewItemStack(random.Next(0,10), random.Next(1,10));
                var item2 = ItemStackFactory.NewItemStack(random.Next(0,10), random.Next(1,10));

                var beltConveyor = BeltConveyorFactory.Create(0, Int32.MaxValue,new DummyInstallationInventory());

                var item1out = beltConveyor.InsertItem(item1);
                var item2out = beltConveyor.InsertItem(item2);

                Assert.True(item1out.Equals(item1.SubItem(1)));
                Assert.True(item2.Equals(item2out));
            }
        }
    }
}