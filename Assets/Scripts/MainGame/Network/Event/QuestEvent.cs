using System;

namespace MainGame.Network.Event
{
    public class QuestEvent
    {
        public event Action<QuestProgressProperties> OnReciveQuestProgress;
        public event Action<QuestCompletedProperties> OnQuestCompleted;
    }

    public class QuestProgressProperties
    {
        
    }

    public class QuestCompletedProperties
    {
        
    }
}