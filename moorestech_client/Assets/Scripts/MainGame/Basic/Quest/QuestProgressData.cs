namespace MainGame.Basic.Quest
{
    public class QuestProgressData
    {
        public readonly bool IsComplete;
        public readonly bool IsRewardEarnbable;
        public readonly bool IsRewarded;

        public QuestProgressData(bool isComplete,bool isRewarded, bool isRewardEarnbable) 
        {
            IsRewarded = isRewarded;
            IsRewardEarnbable = isRewardEarnbable;
            IsComplete = isComplete;
        }
    }
}