using System.Collections.Generic;
using Core.Item;
using Core.Util;

namespace Game.Quest
{
    public class QuestParameter
    {
        public QuestParameter(string questId, List<QuestParameter> prerequisiteQuests, string questCategory, string questType, string questName, string questDescription, CoreVector2 uiPosition, List<IItemStack> rewardItemStacks, PrerequisiteType prerequisiteType)
        {
            QuestId = questId;
            PrerequisiteQuests = prerequisiteQuests;
            QuestCategory = questCategory;
            QuestType = questType;
            QuestName = questName;
            QuestDescription = questDescription;
            UiPosition = uiPosition;
            RewardItemStacks = rewardItemStacks;
            PrerequisiteType = prerequisiteType;
        }

        public string QuestId { get; }
        public List<QuestParameter> PrerequisiteQuests { get; }
        public PrerequisiteType PrerequisiteType { get; }
        public string QuestCategory { get; }
        public string QuestType { get; }
        public string QuestName { get; }
        public string QuestDescription { get; }
        public CoreVector2 UiPosition { get; }
        public List<IItemStack> RewardItemStacks { get; }
    }

    public enum PrerequisiteType
    {
        And,
        Or
    }
}