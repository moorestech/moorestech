using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MainGame.Basic.Quest;

namespace MainGame.Network.Event
{
    public class ReceiveQuestDataEvent
    {
        public event Action<QuestProgressProperties> OnReceiveQuestProgress;
        public event Action<QuestCompletedProperties> OnQuestCompleted;

        internal async UniTask  InvokeReceiveQuestProgress(QuestProgressProperties properties)
        {
            await UniTask.SwitchToMainThread();
            OnReceiveQuestProgress?.Invoke(properties);
        }
    }

    public class QuestProgressProperties
    {
        public readonly Dictionary<string, QuestProgressData> QuestProgress;

        public QuestProgressProperties(Dictionary<string, QuestProgressData> questProgress)
        {
            QuestProgress = questProgress;
        }
    }

    public class QuestCompletedProperties
    {
        
    }
}