using System;
using System.Collections.Generic;

namespace MainGame.Network.Event
{
    public class ReceiveQuestDataEvent
    {
        public event Action<QuestProgressProperties> OnReceiveQuestProgress;
        public event Action<QuestCompletedProperties> OnQuestCompleted;

        public virtual void InvokeReceiveQuestProgress(QuestProgressProperties properties)
        {
            OnReceiveQuestProgress?.Invoke(properties);
        }
    }

    public class QuestProgressProperties
    {
        public readonly Dictionary<string, (bool IsCompleted, bool IsRewarded)> QuestProgress;

        public QuestProgressProperties(Dictionary<string, (bool IsCompleted, bool IsRewarded)> questProgress)
        {
            QuestProgress = questProgress;
        }
    }

    public class QuestCompletedProperties
    {
        
    }
}