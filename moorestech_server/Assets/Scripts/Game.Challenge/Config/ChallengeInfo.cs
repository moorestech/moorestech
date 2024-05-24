using System.Collections.Generic;
using Core.Item.Interface.Config;

namespace Game.Challenge
{
    public class ChallengeInfo
    {
        public readonly int Id; // TODO 将来的にintはやめたい
        public readonly int PreviousId;
        public readonly List<int> NextIds;

        public readonly string TaskCompletionType;
        public readonly IChallengeTaskParam TaskParam;

        public readonly string Summary;
        public readonly string SkitType;
        public readonly string FireSkitName;

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

    public delegate IChallengeTaskParam ChallengeTaskParamLoader(dynamic param, IItemConfig itemConfig);

    public interface IChallengeTaskParam
    {
    }
}