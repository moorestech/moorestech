using System;

namespace Game.Quest.Interface.Event
{
    public interface IQuestCompletedEvent
    {
        public void SubscribeCompletedId(Action<string> questCompleted);   
    }
}