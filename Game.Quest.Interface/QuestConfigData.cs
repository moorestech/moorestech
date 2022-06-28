using System.Collections.Generic;
using Core.Item;
using Core.Util;

namespace Game.Quest.Interface
{
    public class QuestConfigData
    {
        
        public QuestConfigData(string questId, List<QuestConfigData> prerequisiteQuests, string questCategory, string questType, string questName, string questDescription, CoreVector2 uiPosition, List<IItemStack> rewardItemStacks, QuestPrerequisiteType questPrerequisiteType, string questParameter)
        {
            QuestId = questId;
            PrerequisiteQuests = prerequisiteQuests;
            QuestCategory = questCategory;
            QuestType = questType;
            QuestName = questName;
            QuestDescription = questDescription;
            UiPosition = uiPosition;
            RewardItemStacks = rewardItemStacks;
            QuestPrerequisiteType = questPrerequisiteType;
            QuestParameter = questParameter;
        }

        public string QuestId { get; }
        public List<QuestConfigData> PrerequisiteQuests { get; }
        public QuestPrerequisiteType QuestPrerequisiteType { get; }
        public string QuestCategory { get; }
        public string QuestType { get; }
        public string QuestName { get; }
        public string QuestDescription { get; }
        public CoreVector2 UiPosition { get; }
        public List<IItemStack> RewardItemStacks { get; }
        public string QuestParameter { get; }
    }
    
    public enum QuestPrerequisiteType
    {
        And,
        Or
    }
}