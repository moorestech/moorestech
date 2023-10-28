using System.Collections.Generic;
using Core.Item;
using Core.Util;

namespace Game.Quest.Interface
{
    public class QuestConfigData
    {
        public QuestConfigData(string modId, string questId, List<QuestConfigData> prerequisiteQuests, string questCategory, QuestPrerequisiteType questPrerequisiteType, string questType, string questName, string questDescription, CoreVector2 uiPosition, List<IItemStack> rewardItemStacks, string questParameter)
        {
            ModId = modId;
            QuestId = questId;
            PrerequisiteQuests = prerequisiteQuests;
            QuestPrerequisiteType = questPrerequisiteType;
            QuestCategory = questCategory;
            QuestType = questType;
            QuestName = questName;
            QuestDescription = questDescription;
            UiPosition = uiPosition;
            RewardItemStacks = rewardItemStacks;
            QuestParameter = questParameter;
        }

        public string ModId { get; }
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

    /// <summary>
    ///     前提クエストがAND（全てのクエストを達成してから達成できる）かOR（いずれかのクエストを達成したら達成できる）か
    /// </summary>
    public enum QuestPrerequisiteType
    {
        And,
        Or
    }
}