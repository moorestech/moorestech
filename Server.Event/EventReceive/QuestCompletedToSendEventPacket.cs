using System;
using Game.Quest.Interface.Event;
using MessagePack;

namespace Server.Event.EventReceive
{
    public class QuestCompletedToSendEventPacket
    {
        private readonly IQuestCompletedEvent _questCompletedEvent;
        public const string EventTag = "va:event:questCompleted";

        public QuestCompletedToSendEventPacket(IQuestCompletedEvent questCompletedEvent)
        {
            _questCompletedEvent = questCompletedEvent;
            _questCompletedEvent.SubscribeCompletedId(OnQuestCompleted);
        }

        private void OnQuestCompleted(string obj)
        {
            
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