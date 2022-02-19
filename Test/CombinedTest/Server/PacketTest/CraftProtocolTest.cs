using System.Collections.Generic;
using Game.Crafting.Interface;
using Game.PlayerInventory.Interface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Server;
using Server.Util;

namespace Test.CombinedTest.Server.PacketTest
{
    [TestClass]
    public class CraftProtocolTest
    {
        private const short PacketId = 14;
        private const int PlayerId = 1;
        
        [TestMethod]
        public void CraftTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create();
            //クラフトインベントリの作成
            var craftInventory =
                serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).CraftingInventory;
            
            //CraftConfigの作成
            var craftConfig = serviceProvider.GetService<ICraftingConfig>().GetCraftingConfigList()[0];
            
            //craftingInventoryにアイテムを入れる
            for (int i = 0; i < craftConfig.Items.Count; i++)
            {
                craftInventory.SetItem(i,craftConfig.Items[i]);
            }
            
            
            
            //プロトコルでクラフト実行
            var payLoad = new List<byte>();
            payLoad.AddRange(ToByteList.Convert(PacketId));
            payLoad.AddRange(ToByteList.Convert(PlayerId));
            packet.GetPacketResponse(payLoad);
            
            
            //クラフト結果がResultSlotにアイテムが入っているかチェック
            Assert.AreEqual(craftConfig.Result,craftInventory.GetItem(PlayerInventoryConst.CraftingInventorySize - 1 ));
        }
    }
}