using System;
using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using Game.Quest.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class EarnQuestRewardProtocol : IPacketResponse
    {
        public const string Tag = "va:earnReward";
        private readonly IPlayerInventoryDataStore _inventoryDataStore;
        private readonly IQuestDataStore _questDataStore;

        public EarnQuestRewardProtocol(ServiceProvider serviceProvider)
        {
            _questDataStore = serviceProvider.GetService<IQuestDataStore>();
            _inventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<EarnQuestRewardMessagePack>(payload.ToArray());
            
            var quest = _questDataStore.GetQuestData(data.PlayerId, data.QuestId);
            
            if (!quest.IsCompleted) return new List<List<byte>>();
            
            if (quest.IsEarnedReward) return new List<List<byte>>();


            
            var mainInventory = _inventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;
            
            if (!mainInventory.InsertionCheck(quest.QuestConfig.RewardItemStacks)) return new List<List<byte>>();

            
            mainInventory.InsertItem(quest.QuestConfig.RewardItemStacks);
            quest.EarnReward();


            return new List<List<byte>>();
        }
    }

    [MessagePackObject(true)]
    public class EarnQuestRewardMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("。。")]
        public EarnQuestRewardMessagePack()
        {
        }

        public EarnQuestRewardMessagePack(int playerId, string questId)
        {
            Tag = EarnQuestRewardProtocol.Tag;
            PlayerId = playerId;
            QuestId = questId;
        }

        public int PlayerId { get; set; }
        public string QuestId { get; set; }
    }
}