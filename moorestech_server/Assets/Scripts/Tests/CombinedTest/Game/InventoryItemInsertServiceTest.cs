using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.InventoryService;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class InventoryItemInsertServiceTest
    {
        /// <summary>
        ///     通常のinsert処理
        /// </summary>
        [Test]
        public void InsertTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).MainOpenableInventory;
            var grabInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).GrabInventory;
            
            //インベントリの設定
            mainInventory.SetItem(PlayerInventoryConst.HotBarSlotToInventorySlot(0), 1, 10);
            grabInventory.SetItem(0, 1, 10);
            
            //グラブからメインにid 1のアイテムを移す
            InventoryItemInsertService.Insert(grabInventory, 0, mainInventory, 5);
            
            Assert.AreEqual(15, mainInventory.GetItem(PlayerInventoryConst.HotBarSlotToInventorySlot(0)).Count);
            Assert.AreEqual(5, grabInventory.GetItem(0).Count);
        }
        
        
        /// <summary>
        ///     アイテムがいっぱいの時はinsertされないテスト
        /// </summary>
        [Test]
        public void FullItemInsert()
        {
            var (_, serviceProvider) =
                new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).MainOpenableInventory;
            var grabInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).GrabInventory;
            
            var id1MaxStack = ItemMaster.GetItemMaster(new ItemId(1)).MaxStack;
            
            //インベントリをアイテムで満たす
            for (var i = 0; i < PlayerInventoryConst.MainInventorySize; i++) mainInventory.SetItem(i, 1, id1MaxStack);
            //グラブインベントリの設定
            grabInventory.SetItem(0, 1, 10);
            
            //グラブからメインにid 1のアイテムを移す
            InventoryItemInsertService.Insert(grabInventory, 0, mainInventory, 5);
            //挿入されてないことをテスト
            Assert.AreEqual(10, grabInventory.GetItem(0).Count);
            
            
            //挿入した一部が帰ってくるテスト
            //下準備としてスロットのアイテム数を5引く
            mainInventory.SetItem(0, 1, id1MaxStack - 5);
            //グラブからメインにid 1のアイテムを全て移す
            InventoryItemInsertService.Insert(grabInventory, 0, mainInventory, 10);
            //挿入されていることをテスト
            Assert.AreEqual(5, grabInventory.GetItem(0).Count);
        }
    }
}