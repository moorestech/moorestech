using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace MainGame.Network.Event
{
    public class ReceiveQuestDataEvent
    {
        public event Action<QuestProgressProperties> OnReceiveQuestProgress;
        public event Action<QuestCompletedProperties> OnQuestCompleted;

        public async UniTask  InvokeReceiveQuestProgress(QuestProgressProperties properties)
        {
            await UniTask.SwitchToMainThread();
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