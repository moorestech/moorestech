using System;

namespace Game.Quest.Interface.Event
{
    public interface IQuestCompletedEvent
    {
        public void SubscribeCompletedId(Action<(int playerId, string questId)> questCompleted);
    }
}