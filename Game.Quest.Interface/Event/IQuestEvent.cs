using System;

namespace Game.Quest.Interface.Event
{
    public interface IQuestEvent
    {
        public void SubscribeCompletedId(Action<string> questCompleted);   
    }
}