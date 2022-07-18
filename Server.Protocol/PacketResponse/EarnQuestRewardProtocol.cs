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
        private readonly IQuestDataStore _questDataStore;
        private readonly IPlayerInventoryDataStore _inventoryDataStore;

        public EarnQuestRewardProtocol(ServiceProvider serviceProvider)
        {
            _questDataStore = serviceProvider.GetService<IQuestDataStore>();
            _inventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<EarnQuestRewardMessagePack>(payload.ToArray());
            //クエストデータの取得
            var quest = _questDataStore.GetQuestData(data.PlayerId, data.QuestId);
            //クエストが完了してなかったら終了
            if (!quest.IsCompleted) return new List<List<byte>>();
            //アイテム受け取り済みなら終了
            if (quest.IsEarnedReward) return new List<List<byte>>();
            
            
            //全てのアイテムが追加可能かチェック
            var mainInventory = _inventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;
            //追加できなかったら終了
            if (!mainInventory.InsertionCheck(quest.Quest.RewardItemStacks)) return new List<List<byte>>();
            
            //アイテムを追加
            mainInventory.InsertItem(quest.Quest.RewardItemStacks);
            quest.EarnReward();


            return new List<List<byte>>();
        }
    }
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class EarnQuestRewardMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public EarnQuestRewardMessagePack() { }
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