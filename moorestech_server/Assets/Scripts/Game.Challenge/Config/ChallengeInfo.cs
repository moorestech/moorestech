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

        public ChallengeInfo(int id, int previousId,List<int> nextIds,string taskCompletionType, IChallengeTaskParam taskParam, string summary, string skitType, string fireSkitName)
        {
            Id = id;
            PreviousId = previousId;
            NextIds = nextIds;
            TaskCompletionType = taskCompletionType;
            TaskParam = taskParam;
            Summary = summary;
            SkitType = skitType;
            FireSkitName = fireSkitName;
        }
    }

    public delegate IChallengeTaskParam ChallengeTaskParamLoader(dynamic param, IItemConfig itemConfig);

    public interface IChallengeTaskParam
    {
    }
}