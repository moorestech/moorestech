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
using static Tests.Module.TestMod.ForUnitTestItemId;


namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class UnlockedEventPacketTest
    {
        private const int PlayerId = 1;
        
        [Test]
        public void UnlockedEventTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            // イベントがないことを確認する
            // Make sure there are no events
            var response = packet.GetPacketResponse(EventTestUtil.EventRequestData(PlayerId));
            var eventMessagePack = MessagePackSerializer.Deserialize<EventProtocol.ResponseEventProtocolMessagePack>(response[0]);
            Assert.AreEqual(0, eventMessagePack.Events.Count);
            
            // レシピのアンロック状態を変更
            // Change the unlock state of the recipe
            var unlockStateDatastore = serviceProvider.GetService<IGameUnlockStateDataController>();
            unlockStateDatastore.UnlockCraftRecipe(Craft4);
            
            // アイテムのアンロック状態を変更
            // 
            unlockStateDatastore.UnlockItem(ItemId4);
            
            // イベントを受け取り、テストする
            // Receive and test the event
            response = packet.GetPacketResponse(EventTestUtil.EventRequestData(PlayerId));
            eventMessagePack = MessagePackSerializer.Deserialize<EventProtocol.ResponseEventProtocolMessagePack>(response[0]);
            
            // イベントがあることを確認する
            // Make sure there are events
            Assert.AreEqual(2, eventMessagePack.Events.Count);
            
            // Craft4のレシピがアンロックされたことを確認する
            // Make sure the recipe for Craft3 is unlocked
            var data = MessagePackSerializer.Deserialize<UnlockEventMessagePack>(eventMessagePack.Events[0].Payload);
            Assert.AreEqual(UnlockEventType.CraftRecipe, data.UnlockEventType);
            Assert.AreEqual(Craft4.ToString(), data.UnlockedCraftRecipeGuidStr);
            
            // Item4のアイテムアンロックされたことを確認する
            data = MessagePackSerializer.Deserialize<UnlockEventMessagePack>(eventMessagePack.Events[1].Payload);
            Assert.AreEqual(UnlockEventType.Item, data.UnlockEventType);
            Assert.AreEqual(ItemId4, data.UnlockedItemId);
        }
        
        /// <summary>
        /// チャレンジがクリアされたらアンロックされるレシピのテスト
        /// </summary>
        [Test]
        public void ClearedChallengeToUnlockCraftRecipeEventTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            // 初期チャレンジを設定
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();
            
            // EventProtocolProviderにプレイヤーIDを登録するため、一度イベントを取得
            packet.GetPacketResponse(EventTestUtil.EventRequestData(PlayerId));
            
            // インベントリに別々にアイテムを追加
            const int itemId = 1;
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var item1 = ServerContext.ItemStackFactory.Create(new ItemId(itemId), 2);
            playerInventoryData.MainOpenableInventory.SetItem(1, item1);
            var item2 = ServerContext.ItemStackFactory.Create(new ItemId(itemId), 1);
            playerInventoryData.MainOpenableInventory.SetItem(2, item2);
            
            // アップデートしてチャレンジをコンプリートする
            GameUpdater.UpdateOneTick();
            
            // イベントを受け取り、テストする
            // Receive and test the event
            var response = packet.GetPacketResponse(EventTestUtil.EventRequestData(PlayerId));
            var eventMessagePack = MessagePackSerializer.Deserialize<EventProtocol.ResponseEventProtocolMessagePack>(response[0]);
            
            // レシピアンロックのイベントを取得
            // Get the recipe unlock event
            var unlockedCraftRecipeEventPacket = eventMessagePack.Events.Where(e => e.Tag == UnlockedEventPacket.EventTag).ToList()[0];
            var unlockCraftRecipeEvent = MessagePackSerializer.Deserialize<UnlockEventMessagePack>(unlockedCraftRecipeEventPacket.Payload);
            
            // Craft2のレシピがアンロックされたことを確認する
            // Make sure the recipe for Craft2 is unlocked
            Assert.AreEqual(Craft3, unlockCraftRecipeEvent.UnlockedCraftRecipeGuid);
            Assert.AreEqual(UnlockEventType.CraftRecipe, unlockCraftRecipeEvent.UnlockEventType);
            
            // アイテムアンロックのイベントを取得
            //
            var unlockedItemEventPacket = eventMessagePack.Events.Where(e => e.Tag == UnlockedEventPacket.EventTag).ToList()[1];
            var unlockItemEvent = MessagePackSerializer.Deserialize<UnlockEventMessagePack>(unlockedItemEventPacket.Payload);

            // ItemId3がアンロックされたことを確認する
            // 
            Assert.AreEqual(ItemId3, unlockItemEvent.UnlockedItemId);
            Assert.AreEqual(UnlockEventType.Item, unlockItemEvent.UnlockEventType);
        }
    }
}