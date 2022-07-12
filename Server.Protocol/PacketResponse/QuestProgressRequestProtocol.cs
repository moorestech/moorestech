using System;
using System.Collections.Generic;
using System.Linq;
using Game.Quest.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class QuestProgressRequestProtocol : IPacketResponse
    {
        public const string Tag = "va:requestQuestProgress";

        private readonly IQuestDataStore _questDataStore;

        public QuestProgressRequestProtocol(ServiceProvider serviceProvider)
        {
            _questDataStore = serviceProvider.GetService<IQuestDataStore>();
        }

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var playerId = MessagePackSerializer.Deserialize<QuestProgressRequestProtocolMessagePack>(payload.ToArray()).PlayerId;

            var responseQuest = new List<QuestProgress>();
            foreach (var quest in  _questDataStore.GetPlayerQuestProgress(playerId))
            {
                responseQuest.Add(new QuestProgress(quest));
            }

            var responseData = new QuestProgressResponseProtocolMessagePack(responseQuest);

            return new List<List<byte>> {MessagePackSerializer.Serialize(responseData).ToList()};
        }
    }
    
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class QuestProgressRequestProtocolMessagePack : ProtocolMessagePackBase
    {
        public QuestProgressRequestProtocolMessagePack(int playerId)
        {
            PlayerId = playerId;
            Tag = QuestProgressRequestProtocol.Tag;
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public QuestProgressRequestProtocolMessagePack() { }

        public int PlayerId { get; set; }

    }
    
    
    
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class QuestProgressResponseProtocolMessagePack : ProtocolMessagePackBase
    {
        public QuestProgressResponseProtocolMessagePack(List<QuestProgress> quests)
        {
            Tag = QuestProgressRequestProtocol.Tag;
            Quests = quests;
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public QuestProgressResponseProtocolMessagePack() { }

        public List<QuestProgress> Quests { get; set; }

    }
    
    [MessagePackObject(false)]
    public class QuestProgress
    {
        public QuestProgress(IQuest quest)
        {
            Id = quest.Quest.QuestId;
            IsCompleted = quest.IsCompleted;
            IsRewarded = quest.IsRewarded;
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public QuestProgress() { }


        [Key(0)]
        public string Id { get; set; }
        [Key(1)]
        public bool IsCompleted { get; set; }
        [Key(2)]
        public bool IsRewarded { get; set; }
        
    }
}