namespace MainGame.Basic.Quest
{
    public class QuestProgress
    {
        public readonly bool IsComplete;
        public readonly bool IsRewardEarnbable;
        public readonly bool IsRewarded;

        public QuestProgress(bool isComplete,bool isRewarded, bool isRewardEarnbable) 
        {
            IsRewarded = isRewarded;
            IsRewardEarnbable = isRewardEarnbable;
            IsComplete = isComplete;
        }
    }
}