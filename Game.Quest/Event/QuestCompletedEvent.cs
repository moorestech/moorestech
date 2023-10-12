using System;
using Game.Quest.Interface.Event;

namespace Game.Quest.Event
{
    public class QuestCompletedEvent : IQuestCompletedEvent
    {
        public void SubscribeCompletedId(Action<(int playerId, string questId)> questCompleted)
        {
            OnQuestCompleted += questCompleted;
        }

        public event Action<(int playerId, string questId)> OnQuestCompleted;

        public void InvokeQuestCompleted(int playerId, string questId)
        {
            OnQuestCompleted?.Invoke((playerId, questId));
        }
    }
}