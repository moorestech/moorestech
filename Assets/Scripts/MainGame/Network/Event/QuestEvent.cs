using System;
using System.Collections.Generic;

namespace MainGame.Network.Event
{
    public class QuestEvent
    {
        public event Action<QuestProgressProperties> OnReciveQuestProgress;
        public event Action<QuestCompletedProperties> OnQuestCompleted;
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