using System;
using System.Linq;
using Core.Master;
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
using Tests.Module.TestMod;
using UnityEngine;
using static Server.Protocol.PacketResponse.EventProtocol;
using static Server.Protocol.PacketResponse.OneClickCraft;

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
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            // 初期チャレンジを設定
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();
            
            ClearCraftChallenge(packet,serviceProvider);
            
            // イベントを受け取り、テストする
            // Receive and test the event
            var response = packet.GetPacketResponse(EventTestUtil.EventRequestData(0));
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());
            var challengeCompleted = eventMessagePack.Events.First(e => e.Tag == CompletedChallengeEventPacket.EventTag);
            var completedChallenge = MessagePackSerializer.Deserialize<CompletedChallengeEventMessagePack>(challengeCompleted.Payload);
            
            var challengeId = new Guid("00000000-0000-0000-4567-000000000001");
            Assert.AreEqual(challengeId, completedChallenge.CompletedChallengeGuid);
        }
        
        public static void ClearCraftChallenge(PacketResponseCreator packet, ServiceProvider serviceProvider)
        {
            // クラフトの素材をインベントリに追加
            // Add crafting materials to the inventory
            var playerInventoryData = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var craftRecipeElement = MasterHolder.CraftRecipeMaster.CraftRecipes.Data[CraftRecipeId];
            foreach (var requiredItem in craftRecipeElement.RequiredItems)
            {
                var item = ServerContext.ItemStackFactory.Create(requiredItem.ItemGuid, requiredItem.Count);
                playerInventoryData.MainOpenableInventory.InsertItem(item);
            }
            
            // クラフトを実行
            // Execute the craft
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new RequestOneClickCraftProtocolMessagePack(PlayerId, craftRecipeElement.CraftRecipeGuid)).ToList());
        }
        
        [Test]
        // インベントリにアイテムがあることでチャレンジが完了したイベントを受け取ることを確認するテスト
        // Test to ensure that the challenge receives a completed event when an item is in the inventory
        public void InInventoryChallengeClearTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
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
            GameUpdater.UpdateWithWait();
            
            // イベントを受け取り、テストする
            // Receive and test the event
            var response = packet.GetPacketResponse(EventTestUtil.EventRequestData(0));
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());
            var challengeCompleted = eventMessagePack.Events.First(e => e.Tag == CompletedChallengeEventPacket.EventTag);
            var completedChallenge = MessagePackSerializer.Deserialize<CompletedChallengeEventMessagePack>(challengeCompleted.Payload);
            
            var challengeId = new Guid("00000000-0000-0000-4567-000000000002");
            Assert.AreEqual(challengeId, completedChallenge.CompletedChallengeGuid);
        }
        
        [Test]
        public void BlockPlaceChallengeClearTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            
            // 初期チャレンジを設定
            var challengeDatastore = serviceProvider.GetService<ChallengeDatastore>();
            challengeDatastore.InitializeCurrentChallenges();
            
            // EventProtocolProviderにプレイヤーIDを登録するため、一度イベントを取得
            packet.GetPacketResponse(EventTestUtil.EventRequestData(0));
            
            // ブロックを設置
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(0,0,0), BlockDirection.East, out _, Array.Empty<BlockCreateParam>());
            
            // アップデートを呼び出してイベントを処理
            GameUpdater.UpdateWithWait();
            
            // イベントを受け取り、テストする
            // Receive and test the event
            var response = packet.GetPacketResponse(EventTestUtil.EventRequestData(0));
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());
            
            var challengeCompleted = eventMessagePack.Events.First(e => e.Tag == CompletedChallengeEventPacket.EventTag);
            var completedChallenge = MessagePackSerializer.Deserialize<CompletedChallengeEventMessagePack>(challengeCompleted.Payload);
            
            var challengeId = new Guid("00000000-0000-0000-4567-000000000003");
            Assert.AreEqual(challengeId, completedChallenge.CompletedChallengeGuid);
        }
    }
}
