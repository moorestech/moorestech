using System.Collections.Generic;

namespace Client.WebUiHost.Game.Topics
{
    public class ChallengeTreeDto
    {
        public List<ChallengeCategoryDto> Categories;
    }

    public class ChallengeCategoryDto
    {
        public string Guid;
        public string Name;
        public int IconItemId;
        public List<ChallengeNodeDto> Nodes;
    }

    public class ChallengeNodeDto
    {
        public string Guid;
        public string Title;
        public string Summary;
        public int IconItemId;
        public string State;
        public ChallengeVectorDto Position;
        public ChallengeVectorDto Scale;
        public List<string> PrevGuids;
    }

    public class ChallengeVectorDto
    {
        public double X;
        public double Y;
    }

    public class ChallengeCurrentDto
    {
        public List<CurrentChallengeDto> Challenges;
        public string CompletedChallengeGuid;
    }

    public class CurrentChallengeDto
    {
        public string Guid;
        public string Title;
        public string CategoryGuid;
    }
}
