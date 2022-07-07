using System;
using MessagePack;

namespace Server.Event.EventReceive
{
    public class QuestCompletedToSendEventPacket
    {
        public const string EventTag = "va:event:questCompleted";

        public QuestCompletedToSendEventPacket()
        {
            //TODO 実装
        }
    }
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class QuestCompletedEventMessagePack : EventProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public QuestCompletedEventMessagePack() { }

        public QuestCompletedEventMessagePack(string questId)
        {
            EventTag = PlaceBlockToSetEventPacket.EventTag;
            QuestId = questId;
        }

        public string QuestId { get; set; }
    }
}