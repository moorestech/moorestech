using System;
using System.Threading;
using industrialization.Config.BeltConveyor;
using industrialization.GameSystem;
using industrialization.Installation;
using industrialization.Installation.BeltConveyor;
using industrialization.Installation.BeltConveyor.Generally;
using industrialization.Item;
using Microsoft.VisualBasic;
using NUnit.Framework;

namespace industrialization.Test
{
    public class BeltConveyorTest
    {
        //一個のアイテムが入って正しく搬出されるかのテスト
        [Test]
        public void InsertBeltConveyorTest()
        {
            var random = new Random(4123);
            for (int i = 0; i < 20; i++)
            {
                //必要な変数を作成
                int speed = random.Next(50, 500);
                int num = random.Next(1, 5);
                BeltConveyorConfig.TestSetBeltConveyorNum(speed,num);
                
                int id = random.Next(0, 10);
                int amount = random.Next(1, 10);
                var item = ItemStackFactory.NewItemStack(id, amount);
                var dummy = new DummyInstallationInventory(1);
                var beltconveyor = new GenericBeltConveyor(0,Guid.Empty, new GenericBeltConveyorInventory(new GenericBeltConveyorConnector(dummy)));


                var outputItem = beltconveyor.InsertItem(item);

                while (!DummyInstallationInventory.IsFinish)
                {
                    GameUpdate.Update();
                }
                
                Assert.True(outputItem.Equals(ItemStackFactory.NewItemStack(id,amount-1)));
                var tmp = ItemStackFactory.NewItemStack(id, 1);
                Console.WriteLine($"{tmp} {dummy.insertedItems[0]}");
                Assert.True(dummy.insertedItems[0].Equals(tmp));
            }
        }
        //二つのアイテムが入ったとき、一方しか入らないテスト
        [Test]
        public void Insert2ItemBeltConveyorTest()
        {
            var random = new Random(4123);
            for (int i = 0; i < 100; i++)
            {
                BeltConveyorConfig.TestSetBeltConveyorNum(200,5);
                //必要な変数を作成
                var item1 = ItemStackFactory.NewItemStack(random.Next(0,10), random.Next(1,10));
                var item2 = ItemStackFactory.NewItemStack(random.Next(0,10), random.Next(1,10));

                var beltconveyor = new GenericBeltConveyor(0,Guid.Empty, new GenericBeltConveyorInventory(new GenericBeltConveyorConnector(new DummyInstallationInventory())));

                var item1out = beltconveyor.InsertItem(item1);
                var item2out = beltconveyor.InsertItem(item2);

                Assert.True(item1out.Equals(item1.SubItem(1)));
                Assert.True(item2.Equals(item2out));
            }
        }
        //ランダムなアイテムを搬入し、搬出を確かめるテスト
        [Test]
        public void RandomItemInsertTest()
        {
            var random = new Random(4123);
            for (int i = 0; i < 100; i++)
            {
                BeltConveyorConfig.TestSetBeltConveyorNum(200,5);
                //必要な変数を作成
                var item1 = ItemStackFactory.NewItemStack(random.Next(0,10), random.Next(1,10));
                var item2 = ItemStackFactory.NewItemStack(random.Next(0,10), random.Next(1,10));

                var beltconveyor = new GenericBeltConveyor(0,Guid.Empty, new GenericBeltConveyorInventory(new GenericBeltConveyorConnector(new DummyInstallationInventory())));

                var item1out = beltconveyor.InsertItem(item1);
                var item2out = beltconveyor.InsertItem(item2);

                Assert.True(item1out.Equals(item1.SubItem(1)));
                Assert.True(item2.Equals(item2out));
            }
        }
    }
}