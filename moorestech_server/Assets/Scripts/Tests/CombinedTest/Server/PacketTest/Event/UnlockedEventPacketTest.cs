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
using Server.Protocol;


namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class UnlockedEventPacketTest
    {
        private const int PlayerId = 1;
        
        [Test]
        public void UnlockedEventTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            
            // イベントがないことを確認する
            // Make sure there are no events
            Assert.AreEqual(0, sink.TakeAll().Count);
            
            // レシピのアンロック状態を変更
            // Change the unlock state of the recipe
            var unlockStateDatastore = serviceProvider.GetService<IGameUnlockStateDataController>();
            unlockStateDatastore.UnlockCraftRecipe(Craft4);
            
            // アイテムのアンロック状態を変更
            // 
            unlockStateDatastore.UnlockItem(ItemId4);
            
            // イベントを受け取り、テストする
            // Receive and test the event
            var events = sink.TakeAll();
            
            // イベントがあることを確認する
            // Make sure there are events
            Assert.AreEqual(2, events.Count);
            
            // Craft4のレシピがアンロックされたことを確認する
            // Make sure the recipe for Craft4 is unlocked
            var data = MessagePackSerializer.Deserialize<UnlockEventMessagePack>(events[0].Payload);
            Assert.AreEqual(UnlockEventType.CraftRecipe, data.UnlockEventType);
            Assert.AreEqual(Craft4.ToString(), data.UnlockedCraftRecipeGuidStr);
            
            // Item4のアイテムアンロックされたことを確認する
            // Make sure the item Item4 is unlocked
            data = MessagePackSerializer.Deserialize<UnlockEventMessagePack>(events[1].Payload);
            Assert.AreEqual(UnlockEventType.Item, data.UnlockEventType);
            Assert.AreEqual(ItemId4, data.UnlockedItemId);
        }
        
        [Test]
        public void UnlockConnectToolEventTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);

            // イベントがないことを確認する
            // Make sure there are no events
            Assert.AreEqual(0, sink.TakeAll().Count);

            // 接続ツールを解放する
            // Unlock a connect tool
            var electricWireGuid = Guid.Parse("c0000000-0000-0000-0000-000000000001");
            var unlockStateDatastore = serviceProvider.GetService<IGameUnlockStateDataController>();
            unlockStateDatastore.UnlockConnectTool(electricWireGuid);

            // 解放イベントを受け取り検証する
            // Receive and verify the unlock event
            var events = sink.TakeAll();
            Assert.AreEqual(1, events.Count);

            var data = MessagePackSerializer.Deserialize<UnlockEventMessagePack>(events[0].Payload);
            Assert.AreEqual(UnlockEventType.ConnectTool, data.UnlockEventType);
            Assert.AreEqual(electricWireGuid, data.UnlockedConnectToolGuid);
        }

        /// <summary>
        /// チャレンジがクリアされたらアンロックされるレシピのテスト
        /// </summary>
        [Test]
        public void ClearedChallengeToUnlockCraftRecipeEventTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            
            // 初期チャレンジを設定
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();
            
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
            var events = sink.TakeAll();
            
            // レシピアンロックのイベントを取得
            // Get the recipe unlock event
            var unlockedCraftRecipeEventPacket = events.Where(e => e.Tag == UnlockedEventPacket.EventTag).ToList()[0];
            var unlockCraftRecipeEvent = MessagePackSerializer.Deserialize<UnlockEventMessagePack>(unlockedCraftRecipeEventPacket.Payload);
            
            // Craft2のレシピがアンロックされたことを確認する
            // Make sure the recipe for Craft2 is unlocked
            Assert.AreEqual(Craft3, unlockCraftRecipeEvent.UnlockedCraftRecipeGuid);
            Assert.AreEqual(UnlockEventType.CraftRecipe, unlockCraftRecipeEvent.UnlockEventType);
            
            // アイテムアンロックのイベントを取得
            // Take the item unlock event
            var unlockedItemEventPacket = events.Where(e => e.Tag == UnlockedEventPacket.EventTag).ToList()[1];
            var unlockItemEvent = MessagePackSerializer.Deserialize<UnlockEventMessagePack>(unlockedItemEventPacket.Payload);

            // ItemId3がアンロックされたことを確認する
            // 
            Assert.AreEqual(ItemId3, unlockItemEvent.UnlockedItemId);
            Assert.AreEqual(UnlockEventType.Item, unlockItemEvent.UnlockEventType);
        }
    }
}