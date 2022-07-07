using System;
using MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class ReceiveQuestRewardItemProtocol
    {
        
    }
        
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class ReceiveQuestRewardItemProtocolMessagePack : ProtocolMessagePackBase
    {
        public ReceiveQuestRewardItemProtocolMessagePack(int playerId, string questId)
        {
            PlayerId = playerId;
            QuestId = questId;
            Tag = QuestProgressRequestProtocol.Tag;
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ReceiveQuestRewardItemProtocolMessagePack() { }

        public int PlayerId { get; set; }
        public string QuestId { get; set; }

    }
}