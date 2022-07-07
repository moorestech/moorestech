using System;
using System.Collections.Generic;
using MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class QuestProgressRequestProtocol : IPacketResponse
    {
        public const string Tag = "va:requestQuestProgress";
        public List<List<byte>> GetResponse(List<byte> payload)
        {
            throw new System.NotImplementedException();
        }
    }
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class QuestProgressRequestProtocolMessagePack : ProtocolMessagePackBase
    {
        public QuestProgressRequestProtocolMessagePack(List<QuestProgress> quests)
        {
            Tag = QuestProgressRequestProtocol.Tag;
            Quests = quests;
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public QuestProgressRequestProtocolMessagePack() { }

        public List<QuestProgress> Quests { get; set; }

    }
    
    [MessagePackObject(false)]
    public class QuestProgress
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public QuestProgress(string id, bool isCompleted, bool acquiredReward)
        {
            Id = id;
            IsCompleted = isCompleted;
            AcquiredReward = acquiredReward;
        }


        [Key(0)]
        public string Id { get; set; }
        [Key(1)]
        public bool IsCompleted { get; set; }
        [Key(2)]
        public bool AcquiredReward { get; set; }
        
    }
}