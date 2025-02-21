using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using static Tests.Module.TestMod.ForUnitTest.ForUnitTestCraftRecipeId;


namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class UnlockedCraftRecipeEventPacketTest
    {
        private const int PlayerId = 1;
        
        [Test]
        public void UnlockedEventTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            // イベントがないことを確認する
            // Make sure there are no events
            var response = packet.GetPacketResponse(EventTestUtil.EventRequestData(PlayerId));
            var eventMessagePack = MessagePackSerializer.Deserialize<EventProtocol.ResponseEventProtocolMessagePack>(response[0].ToArray());
            Assert.AreEqual(0, eventMessagePack.Events.Count);
            
            // レシピのアンロック状態を変更
            // Change the unlock state of the recipe
            var unlockStateDatastore = serviceProvider.GetService<IGameUnlockStateDatastore>();
            unlockStateDatastore.UnlockCraftRecipe(Craft3);
            
            // イベントがあることを確認する
            // Make sure there are events
            Assert.AreEqual(1, eventMessagePack.Events.Count);
            
            // Craft3のレシピがアンロックされたことを確認する
            // Make sure the recipe for Craft3 is unlocked
            var data = MessagePackSerializer.Deserialize<UnlockCraftRecipeEventMessagePack>(eventMessagePack.Events[0].Payload);
            Assert.AreEqual(Craft3.ToString(), data.UnlockedCraftRecipeGuidStr);
        }
        
        [Test]
        public void ClearedChallengeToUnlockEventTest()
        {
            Assert.Fail();
        }
    }
}