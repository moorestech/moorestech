using System.Collections.Generic;

namespace Game.Challenge
{
    public class ChallengeInfo
    {
        public const string BackgroundSkitType = "backgroundSkit";
        
        public readonly int Id; // TODO 将来的にintはやめたい
        public readonly List<int> NextIds;
        public readonly int PreviousId;
        
        public readonly string Summary;
        
        public readonly string TaskCompletionType;
        public readonly IChallengeTaskParam TaskParam;
        
        public readonly string FireSkitType;
        public readonly string FireSkitName;
        
        public List<TutorialConfig> Tutorials;
        
        public ChallengeInfo(TmpChallengeInfo tmpChallengeInfo, List<int> nextIds)
        {
            Id = tmpChallengeInfo.Id;
            PreviousId = tmpChallengeInfo.PreviousId;
            NextIds = nextIds;
            TaskCompletionType = tmpChallengeInfo.TaskCompletionType;
            TaskParam = tmpChallengeInfo.TaskParam;
            Summary = tmpChallengeInfo.Summary;
            FireSkitType = tmpChallengeInfo.FireSkitType;
            FireSkitName = tmpChallengeInfo.FireSkitName;
            Tutorials = tmpChallengeInfo.Tutorials;
        }
    }
    
    public class TmpChallengeInfo
    {
        public int Id;
        public int PreviousId;
        
        public string Summary;
        
        public string TaskCompletionType;
        public IChallengeTaskParam TaskParam;
        
        public string FireSkitType;
        public string FireSkitName;
        
        public List<TutorialConfig> Tutorials;
    }
}