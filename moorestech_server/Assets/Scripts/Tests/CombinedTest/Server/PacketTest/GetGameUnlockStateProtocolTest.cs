using System.Linq;
using Game.SaveLoad.Interface;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using static Server.Protocol.PacketResponse.GetGameUnlockStateProtocol;
using static Tests.Module.TestMod.ForUnitTest.ForUnitTestCraftRecipeId;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetGameUnlockStateProtocolTest
    {
        [Test]
        public void GetUnlockStateInfo()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            // レシピのアンロック状態を取得
            // Get the unlock state of the recipe
            var unlockStateDatastore = serviceProvider.GetService<IGameUnlockStateDataController>();
            var infos = unlockStateDatastore.CraftRecipeUnlockStateInfos;
            Assert.True(infos[Craft0].IsUnlocked);
            Assert.True(infos[Craft1].IsUnlocked);
            Assert.False(infos[Craft2].IsUnlocked);
            Assert.False(infos[Craft3].IsUnlocked);
            
            // サーバーからレシピのアンロック状態を取得
            // Get the unlock state of the recipe from the server
            var messagePack = new RequestGameUnlockStateProtocolMessagePack();
            var responseBytes = packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack).ToList())[0];
            var response = MessagePackSerializer.Deserialize<ResponseGameUnlockStateProtocolMessagePack>(responseBytes.ToArray());
            
            Assert.True(response.UnlockCraftRecipeGuids.Contains(Craft0));
            Assert.True(response.UnlockCraftRecipeGuids.Contains(Craft1));
            Assert.True(response.LockedCraftRecipeGuids.Contains(Craft2));
            Assert.True(response.LockedCraftRecipeGuids.Contains(Craft3));
            
            // レシピのアンロック状態を変更
            // Change the unlock state of the recipe
            unlockStateDatastore.UnlockCraftRecipe(Craft2);
            
            // 再びサーバーからレシピのアンロック状態を取得
            // Get the unlock state of the recipe from the server again
            responseBytes = packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack).ToList())[0];
            response = MessagePackSerializer.Deserialize<ResponseGameUnlockStateProtocolMessagePack>(responseBytes.ToArray());
            
            Assert.True(response.UnlockCraftRecipeGuids.Contains(Craft0));
            Assert.True(response.UnlockCraftRecipeGuids.Contains(Craft1));
            Assert.True(response.UnlockCraftRecipeGuids.Contains(Craft2));
            Assert.True(response.LockedCraftRecipeGuids.Contains(Craft3));
        }
    }
}