using System.Linq;
using Game.SaveLoad.Interface;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using static Server.Protocol.PacketResponse.GetGameUnlockStateProtocol;
using static Tests.Module.TestMod.ForUnitTestCraftRecipeId;
using static Tests.Module.TestMod.ForUnitTestItemId;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetGameUnlockStateProtocolTest
    {
        [Test]
        public void GetUnlockStateInfo()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            // アンロック状態を取得
            // Get the unlock state
            var unlockStateDatastore = serviceProvider.GetService<IGameUnlockStateDataController>();
            var craftInfos = unlockStateDatastore.CraftRecipeUnlockStateInfos;
            Assert.True(craftInfos[Craft1].IsUnlocked);
            Assert.True(craftInfos[Craft2].IsUnlocked);
            Assert.False(craftInfos[Craft3].IsUnlocked);
            Assert.False(craftInfos[Craft4].IsUnlocked);
            
            // アイテムのアンロック状態を取得
            // 
            var itemInfos = unlockStateDatastore.ItemUnlockStateInfos;
            Assert.True(itemInfos[ItemId1].IsUnlocked);
            Assert.True(itemInfos[ItemId2].IsUnlocked);
            Assert.False(itemInfos[ItemId3].IsUnlocked);
            Assert.False(itemInfos[ItemId4].IsUnlocked);
            
            // サーバーからアンロック状態を取得
            // Get the unlock state from the server
            var messagePack = new RequestGameUnlockStateProtocolMessagePack();
            var responseBytes = packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack).ToList())[0];
            var response = MessagePackSerializer.Deserialize<ResponseGameUnlockStateProtocolMessagePack>(responseBytes.ToArray());
            
            Assert.True(response.UnlockCraftRecipeGuids.Contains(Craft1));
            Assert.True(response.UnlockCraftRecipeGuids.Contains(Craft2));
            Assert.True(response.LockedCraftRecipeGuids.Contains(Craft3));
            Assert.True(response.LockedCraftRecipeGuids.Contains(Craft4));
            
            Assert.True(response.UnlockItemIds.Contains(ItemId1));
            Assert.True(response.UnlockItemIds.Contains(ItemId2));
            Assert.True(response.LockedItemIds.Contains(ItemId3));
            Assert.True(response.LockedItemIds.Contains(ItemId4));
            
            
            
            // アンロック状態を変更
            // Change the unlock state
            unlockStateDatastore.UnlockCraftRecipe(Craft3);
            unlockStateDatastore.UnlockItem(ItemId3);
            
            // 再びサーバーからアンロック状態を取得
            // Get the unlock state from the server again
            responseBytes = packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack).ToList())[0];
            response = MessagePackSerializer.Deserialize<ResponseGameUnlockStateProtocolMessagePack>(responseBytes.ToArray());
            
            Assert.True(response.UnlockCraftRecipeGuids.Contains(Craft1));
            Assert.True(response.UnlockCraftRecipeGuids.Contains(Craft2));
            Assert.True(response.UnlockCraftRecipeGuids.Contains(Craft3));
            Assert.True(response.LockedCraftRecipeGuids.Contains(Craft4));
            
            Assert.True(response.UnlockItemIds.Contains(ItemId1));
            Assert.True(response.UnlockItemIds.Contains(ItemId2));
            Assert.True(response.UnlockItemIds.Contains(ItemId3));
            Assert.True(response.LockedItemIds.Contains(ItemId4));
        }
    }
}