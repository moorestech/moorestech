using Core.Item;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server;
using Server.Boot;
using Server.Protocol.PacketResponse.Util;

using Test.Module.TestConfig;
using Test.Module.TestMod;

namespace Test.CombinedTest.Game
{
    public class InventoryItemMoveServiceTest
    {
        [Test]
        public void MoveTest()
        {
            int playerId = 1;

            //初期設定----------------------------------------------------------

            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var itemStackFactory = serviceProvider.GetService<ItemStackFactory>();

            //プレイヤーのインベントリの設定
            var playerInventoryData =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId);


            //アイテムの設定
            var inventory = playerInventoryData.MainOpenableInventory;
            inventory.SetItem(0, itemStackFactory.Create(1, 5));
            inventory.SetItem(1, itemStackFactory.Create(1, 1));
            inventory.SetItem(2, itemStackFactory.Create(2, 1));

            var itemMoveService = new InventoryItemMoveService();

            //実際に移動させてテスト
            //全てのアイテムを移動させるテスト
            itemMoveService.Move(itemStackFactory,inventory,
                0,inventory,3,5);
            Assert.AreEqual(inventory.GetItem(0), itemStackFactory.CreatEmpty());
            Assert.AreEqual(inventory.GetItem(3), itemStackFactory.Create(1, 5));

            //一部のアイテムを移動させるテスト
            itemMoveService.Move(itemStackFactory,inventory,
                3,inventory,0,3);
            Assert.AreEqual(inventory.GetItem(0), itemStackFactory.Create(1, 3));
            Assert.AreEqual(inventory.GetItem(3), itemStackFactory.Create(1, 2));

            //一部のアイテムを移動しようとするが他にスロットがあるため失敗するテスト
            itemMoveService.Move(itemStackFactory,inventory,
                0,inventory,2,1);
            Assert.AreEqual(inventory.GetItem(0), itemStackFactory.Create(1, 3));
            Assert.AreEqual(inventory.GetItem(2), itemStackFactory.Create(2, 1));

            //全てのアイテムを移動させるテスト
            itemMoveService.Move(itemStackFactory,inventory,
                0,inventory,2,3);
            Assert.AreEqual(inventory.GetItem(0), itemStackFactory.Create(2, 1));
            Assert.AreEqual(inventory.GetItem(2), itemStackFactory.Create(1, 3));

            //アイテムを加算するテスト
            itemMoveService.Move(itemStackFactory,inventory,
                2,inventory,1,3);
            Assert.AreEqual(inventory.GetItem(2), itemStackFactory.CreatEmpty());
            Assert.AreEqual(inventory.GetItem(1), itemStackFactory.Create(1, 4));
            
            
            //全てのアイテムを同じスロットにアイテムを移動させるテスト
            itemMoveService.Move(itemStackFactory,inventory,
                1,inventory,1,4);
            Assert.AreEqual(inventory.GetItem(1), itemStackFactory.Create(1, 4));
        }
    }
}