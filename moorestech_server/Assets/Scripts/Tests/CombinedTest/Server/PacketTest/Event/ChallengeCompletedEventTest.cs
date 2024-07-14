using System.Linq;
using Core.Update;
using Game.Block.Interface;
using Game.Challenge;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class ChallengeCompletedEventTest
    {
        private const int PlayerId = 0;
        private const int CraftRecipeId = 1;
        
        [Test]
        // アイテムを作成し、そのチャレンジが完了したイベントを受け取ることを確認するテスト
        // Test to ensure that the item is created and that the challenge receives a completed event
        public void CreateItemChallengeClearTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.GetOrCreateChallengeInfo(PlayerId);
         
            ClearCraftChallenge(packet,serviceProvider);
            
            // イベントを受け取り、テストする
            // Receive and test the event
            var response = packet.GetPacketResponse(EventTestUtil.EventRequestData(0));
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());
            var challengeCompleted = eventMessagePack.Events.First(e => e.Tag == CompletedChallengeEventPacket.EventTag);
            var completedChallenge = MessagePackSerializer.Deserialize<CompletedChallengeEventMessage>(challengeCompleted.Payload);
            
            Assert.AreEqual(1000, completedChallenge.CompletedChallengeId);
        }
        
        public static void ClearCraftChallenge(PacketResponseCreator packet, ServiceProvider serviceProvider)
        {
            // クラフトの素材をインベントリに追加
            // Add crafting materials to the inventory
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            foreach (var craftInfo in ServerContext.CraftingConfig.GetCraftingConfigData(CraftRecipeId).CraftRequiredItemInfos)
            {
                var requiredItem = craftInfo.ItemStack;
                playerInventoryData.MainOpenableInventory.InsertItem(requiredItem);
            }
            
            // クラフトを実行
            // Execute the craft
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RequestOneClickCraftProtocolMessagePack(PlayerId, CraftRecipeId)).ToList());
        }
        
        [Test]
        // インベントリにアイテムがあることでチャレンジが完了したイベントを受け取ることを確認するテスト
        // Test to ensure that the challenge receives a completed event when an item is in the inventory
        public void InInventoryChallengeClearTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.GetOrCreateChallengeInfo(PlayerId);
            
            // インベントリに別々にアイテムを追加
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var item1 = ServerContext.ItemStackFactory.Create("Test Author:forUniTest", "Test1", 2);
            playerInventoryData.MainOpenableInventory.SetItem(1, item1);
            var item2 = ServerContext.ItemStackFactory.Create("Test Author:forUniTest", "Test1", 1);
            playerInventoryData.MainOpenableInventory.SetItem(2, item2);
            
            // アップデートしてチャレンジをコンプリートする
            GameUpdater.UpdateWithWait();
            
            // イベントを受け取り、テストする
            // Receive and test the event
            var response = packet.GetPacketResponse(EventTestUtil.EventRequestData(0));
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());
            var challengeCompleted = eventMessagePack.Events.First(e => e.Tag == CompletedChallengeEventPacket.EventTag);
            var completedChallenge = MessagePackSerializer.Deserialize<CompletedChallengeEventMessage>(challengeCompleted.Payload);
            
            Assert.AreEqual(1010, completedChallenge.CompletedChallengeId);
        }
        
        [Test]
        public void BlockPlaceChallengeClearTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.GetOrCreateChallengeInfo(PlayerId);
            
            // ブロックを設置
            ServerContext.WorldBlockDatastore.TryAddBlock(1, new Vector3Int(0,0,0), BlockDirection.East, out _);
            
            // イベントを受け取り、テストする
            // Receive and test the event
            var response = packet.GetPacketResponse(EventTestUtil.EventRequestData(0));
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());
            var challengeCompleted = eventMessagePack.Events.First(e => e.Tag == CompletedChallengeEventPacket.EventTag);
            var completedChallenge = MessagePackSerializer.Deserialize<CompletedChallengeEventMessage>(challengeCompleted.Payload);
            
            Assert.AreEqual(1020, completedChallenge.CompletedChallengeId);
        }
    }
}