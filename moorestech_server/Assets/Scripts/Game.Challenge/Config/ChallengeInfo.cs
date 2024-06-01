using System.Collections.Generic;

namespace Game.Challenge
{
    public class ChallengeInfo
    {
        public const string CreateItem = "createItem";
        public const string InInventoryItem = "inInventoryItem";
        
        public const string BackgroundSkitType = "backgroundSkit";
        public readonly string FireSkitName;
        
        public readonly int Id; // TODO 将来的にintはやめたい
        public readonly List<int> NextIds;
        public readonly int PreviousId;
        public readonly string SkitType;
        
        public readonly string Summary;
        
        public readonly string TaskCompletionType;
        public readonly IChallengeTaskParam TaskParam;
        
        public ChallengeInfo(TmpChallengeInfo tmpChallengeInfo, List<int> nextIds)
        {
            Id = tmpChallengeInfo.Id;
            PreviousId = tmpChallengeInfo.PreviousId;
            NextIds = nextIds;
            TaskCompletionType = tmpChallengeInfo.TaskCompletionType;
            TaskParam = tmpChallengeInfo.TaskParam;
            Summary = tmpChallengeInfo.Summary;
            SkitType = tmpChallengeInfo.SkitType;
            FireSkitName = tmpChallengeInfo.FireSkitName;
        }
    }
    
    public delegate IChallengeTaskParam ChallengeTaskParamLoader(dynamic param);
    
    public interface IChallengeTaskParam
    {
    }
}