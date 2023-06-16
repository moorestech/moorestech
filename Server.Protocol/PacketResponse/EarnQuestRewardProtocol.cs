using System;
using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using Game.Quest.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.Base;

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

        public List<ToClientProtocolMessagePackBase> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<EarnQuestRewardMessagePack>(payload.ToArray());
            //クエストデータの取得
            var quest = _questDataStore.GetQuestData(data.PlayerId, data.QuestId);
            //クエストが完了してなかったら終了
            if (!quest.IsCompleted) return new List<ToClientProtocolMessagePackBase>();
            //アイテム受け取り済みなら終了
            if (quest.IsEarnedReward) return new List<ToClientProtocolMessagePackBase>();
            
            
            //全てのアイテムが追加可能かチェック
            var mainInventory = _inventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;
            //追加できなかったら終了
            if (!mainInventory.InsertionCheck(quest.QuestConfig.RewardItemStacks)) return new List<ToClientProtocolMessagePackBase>();
            
            //アイテムを追加
            mainInventory.InsertItem(quest.QuestConfig.RewardItemStacks);
            quest.EarnReward();


            return new List<ToClientProtocolMessagePackBase>();
        }
    }
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class EarnQuestRewardMessagePack : ToServerProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public EarnQuestRewardMessagePack() { }
        public EarnQuestRewardMessagePack(int playerId, string questId)
        {
            ToServerTag = EarnQuestRewardProtocol.Tag;
            PlayerId = playerId;
            QuestId = questId;
        }

        public int PlayerId { get; set; }
        public string QuestId { get; set; }
    }
}