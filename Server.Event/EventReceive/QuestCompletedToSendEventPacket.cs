using System;
using System.Linq;
using Game.Quest.Interface.Event;
using MessagePack;

namespace Server.Event.EventReceive
{
    public class QuestCompletedToSendEventPacket
    {
        public const string EventTag = "va:event:questCompleted";
        
        private readonly EventProtocolProvider _eventProtocolProvider;
        public QuestCompletedToSendEventPacket(IQuestCompletedEvent questCompletedEvent, EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            questCompletedEvent.SubscribeCompletedId(OnQuestCompleted);
        }

        private void OnQuestCompleted((int playerId, string questId) args)
        {
            _eventProtocolProvider.AddEvent(args.playerId,new QuestCompletedEventMessagePack(args.questId));
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