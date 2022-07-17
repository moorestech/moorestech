using System;
using System.Collections.Generic;
using MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class EarnQuestRewardProtocol : IPacketResponse
    {
        public const string Tag = "va:earnReward";
        
        public List<List<byte>> GetResponse(List<byte> payload)
        {
            return new List<List<byte>>();
        }
    }
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class EarnQuestRewardMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public EarnQuestRewardMessagePack() { }
        public EarnQuestRewardMessagePack(int playerId, int questId)
        {
            Tag = EarnQuestRewardProtocol.Tag;
            PlayerId = playerId;
            QuestId = questId;
        }

        public int PlayerId { get; set; }
        public int QuestId { get; set; }
    }
}