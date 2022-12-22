using System.Linq;

namespace Game.Quest.Interface.Extension
{
    public static class QuestExtension
    {
        /// <summary>
        /// そのクエストが報酬受け取り可能かどうかを判定する
        /// </summary>
        /// <param name="quest"></param>
        /// <returns></returns>
        public static bool IsRewardEarnable(this IQuest quest)
        {
            //既に報酬を受け取ったのでfalse
            if (quest.IsEarnedReward)
            {
                return false;
            }
            //まだクエストを完了していないのでfalse
            if (!quest.IsCompleted)
            {
                return false;
            }
            //完了済みで前提クエストが無ければtrue
            if (quest.PreRequestQuests.Count == 0)
            {
                return true;
            }
                
                
            //前提クエストの完了しているクエスト数を取得
            var preRequestQuestCount = quest.PreRequestQuests.Count(q => q.IsCompleted);

            switch (quest.QuestConfig.QuestPrerequisiteType)
            {
                //AND条件ですべて完了していたらtrue
                case QuestPrerequisiteType.And when preRequestQuestCount == quest.PreRequestQuests.Count:
                //OR条件でいずれか完了していたらtrue
                case QuestPrerequisiteType.Or when preRequestQuestCount > 0:
                    return true;
                default:
                    return false;
            }
        }
    }
}