using System.Linq;

namespace Game.Quest.Interface.Extension
{
    public static class QuestExtension
    {

        ///     

        /// <param name="quest"></param>
        /// <returns></returns>
        public static bool IsRewardEarnable(this IQuest quest)
        {
            //false
            if (quest.IsEarnedReward) return false;
            //false
            if (!quest.IsCompleted) return false;
            //true
            if (quest.PreRequestQuests.Count == 0) return true;


            
            var preRequestQuestCount = quest.PreRequestQuests.Count(q => q.IsCompleted);

            switch (quest.QuestConfig.QuestPrerequisiteType)
            {
                //ANDtrue
                case QuestPrerequisiteType.And when preRequestQuestCount == quest.PreRequestQuests.Count:
                //ORtrue
                case QuestPrerequisiteType.Or when preRequestQuestCount > 0:
                    return true;
                default:
                    return false;
            }
        }
    }
}