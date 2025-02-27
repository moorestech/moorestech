using System;
using System.Linq;
using Core.Master;
using Core.Update;
using Game.Challenge;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Interface;
using Game.UnlockState;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using static Tests.Module.TestMod.ForUnitTestCraftRecipeId;


namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class UnlockedEventPacketTest
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
            var unlockStateDatastore = serviceProvider.GetService<IGameUnlockStateDataController>();
            unlockStateDatastore.UnlockCraftRecipe(Craft4);
            
            // イベントを受け取り、テストする
            // Receive and test the event
            response = packet.GetPacketResponse(EventTestUtil.EventRequestData(PlayerId));
            eventMessagePack = MessagePackSerializer.Deserialize<EventProtocol.ResponseEventProtocolMessagePack>(response[0].ToArray());
            
            // イベントがあることを確認する
            // Make sure there are events
            Assert.AreEqual(1, eventMessagePack.Events.Count);
            
            // Craft3のレシピがアンロックされたことを確認する
            // Make sure the recipe for Craft3 is unlocked
            var data = MessagePackSerializer.Deserialize<UnlockCraftRecipeEventMessagePack>(eventMessagePack.Events[0].Payload);
            Assert.AreEqual(Craft4.ToString(), data.UnlockedCraftRecipeGuidStr);
        }
        
        /// <summary>
        /// チャレンジがクリアされたらアンロックされるレシピのテスト
        /// </summary>
        [Test]
        public void ClearedChallengeToUnlockEventTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.GetOrCreateChallengeInfo(PlayerId);
            
            // インベントリに別々にアイテムを追加
            const int itemId = 1;
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var item1 = ServerContext.ItemStackFactory.Create(new ItemId(itemId), 2);
            playerInventoryData.MainOpenableInventory.SetItem(1, item1);
            var item2 = ServerContext.ItemStackFactory.Create(new ItemId(itemId), 1);
            playerInventoryData.MainOpenableInventory.SetItem(2, item2);
            
            // アップデートしてチャレンジをコンプリートする
            GameUpdater.UpdateWithWait();
            
            // イベントを受け取り、テストする
            // Receive and test the event
            var response = packet.GetPacketResponse(EventTestUtil.EventRequestData(PlayerId));
            var eventMessagePack = MessagePackSerializer.Deserialize<EventProtocol.ResponseEventProtocolMessagePack>(response[0].ToArray());
            
            // レシピアンロックのイベントを取得
            // Get the recipe unlock event
            var unlockedCraftRecipeEvent = eventMessagePack.Events.First(e => e.Tag == UnlockedEventPacket.EventTag);
            var unlockCraftRecipeEvent = MessagePackSerializer.Deserialize<UnlockCraftRecipeEventMessagePack>(unlockedCraftRecipeEvent.Payload);
            
            // Craft2のレシピがアンロックされたことを確認する
            // Make sure the recipe for Craft2 is unlocked
            Assert.AreEqual(Craft3, unlockCraftRecipeEvent.UnlockedCraftRecipeGuid);
        }
    }
}