#if NET6_0
using System.Linq;
using Core.Const;
using Game.PlayerInventory.Interface;
using Game.Quest.Interface;
using Game.Quest.QuestEntity;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Test.Module.TestMod;

namespace Test.CombinedTest.Server.PacketTest
{
    /// <summary>
    ///     
    /// </summary>
    public class EarnQuestRewardProtocolTest
    {
        private const int PlayerId = 1;
        private const int RewardQuestIndex = 1;


        ///     

        [Test]
        public void NormalEarnTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var quest = (ItemCraftQuest)serviceProvider.GetService<IQuestDataStore>().GetPlayerQuestProgress(PlayerId)[RewardQuestIndex];
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);


            
            
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new EarnQuestRewardMessagePack(PlayerId, quest.QuestConfig.QuestId)).ToList());
            
            Assert.AreEqual(false, quest.IsEarnedReward);
            
            Assert.AreEqual(ItemConst.EmptyItemId, playerInventory.MainOpenableInventory.Items[0].Id);


            
            
            typeof(ItemCraftQuest).GetProperty("IsCompleted").SetValue(quest, true);

            
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new EarnQuestRewardMessagePack(PlayerId, quest.QuestConfig.QuestId)).ToList());

            
            Assert.AreEqual(quest.QuestConfig.RewardItemStacks[0], playerInventory.MainOpenableInventory.Items[0]);
            
            Assert.AreEqual(true, quest.IsEarnedReward);


            
            
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new EarnQuestRewardMessagePack(PlayerId, quest.QuestConfig.QuestId)).ToList());
            
            Assert.AreEqual(quest.QuestConfig.RewardItemStacks[0], playerInventory.MainOpenableInventory.Items[0]);
        }



        ///     

        [Test]
        public void ItemFullNotEarnTest()
        {
            var (packet, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var quest = (ItemCraftQuest)serviceProvider.GetService<IQuestDataStore>().GetPlayerQuestProgress(PlayerId)[RewardQuestIndex];
            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;

            
            typeof(ItemCraftQuest).GetProperty("IsCompleted").SetValue(quest, true);

            
            for (var i = 0; i < mainInventory.GetSlotSize(); i++)
            {
                var differItemId = quest.QuestConfig.RewardItemStacks[0].Id + 1;
                mainInventory.SetItem(i, differItemId, 1);
            }


            
            packet.GetPacketResponse(MessagePackSerializer.Serialize(new EarnQuestRewardMessagePack(PlayerId, quest.QuestConfig.QuestId)).ToList());
            
            Assert.AreEqual(false, quest.IsEarnedReward);
        }
    }
}
#endif