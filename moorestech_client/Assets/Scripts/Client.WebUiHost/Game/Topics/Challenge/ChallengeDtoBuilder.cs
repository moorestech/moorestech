using System.Collections.Generic;
using Client.Network.API;
using Core.Master;

namespace Client.WebUiHost.Game.Topics
{
    public static class ChallengeDtoBuilder
    {
        public static ChallengeTreeDto BuildTree(List<ChallengeCategoryResponse> responses)
        {
            var result = new ChallengeTreeDto { Categories = new List<ChallengeCategoryDto>() };
            foreach (var response in responses)
            {
                if (!response.IsUnlocked) continue;
                var category = new ChallengeCategoryDto
                {
                    Guid = response.Category.CategoryGuid.ToString(),
                    Name = response.Category.CategoryName,
                    IconItemId = MasterHolder.ItemMaster.GetItemId(response.Category.IconItem).AsPrimitive(),
                    Nodes = new List<ChallengeNodeDto>(),
                };
                foreach (var challenge in response.Category.Challenges)
                {
                    var state = response.CompletedChallenges.Contains(challenge)
                        ? "completed"
                        : response.CurrentChallenges.Contains(challenge) ? "current" : "locked";
                    var node = new ChallengeNodeDto
                    {
                        Guid = challenge.ChallengeGuid.ToString(),
                        Title = challenge.Title,
                        Summary = challenge.Summary,
                        IconItemId = MasterHolder.ItemMaster.GetItemId(challenge.DisplayListParam.IconItem).AsPrimitive(),
                        State = state,
                        Position = new ChallengeVectorDto
                        {
                            X = challenge.DisplayListParam.UIPosition.x,
                            Y = challenge.DisplayListParam.UIPosition.y,
                        },
                        Scale = new ChallengeVectorDto
                        {
                            X = challenge.DisplayListParam.UIScale.x,
                            Y = challenge.DisplayListParam.UIScale.y,
                        },
                        PrevGuids = new List<string>(),
                    };
                    foreach (var previous in challenge.PrevChallengeGuids) node.PrevGuids.Add(previous.ToString());
                    category.Nodes.Add(node);
                }
                result.Categories.Add(category);
            }
            return result;
        }

        public static ChallengeCurrentDto BuildCurrent(List<ChallengeCategoryResponse> responses, string completedGuid)
        {
            var result = new ChallengeCurrentDto
            {
                Challenges = new List<CurrentChallengeDto>(),
                CompletedChallengeGuid = completedGuid,
            };
            foreach (var response in responses)
            {
                foreach (var challenge in response.CurrentChallenges)
                {
                    result.Challenges.Add(new CurrentChallengeDto
                    {
                        Guid = challenge.ChallengeGuid.ToString(),
                        Title = challenge.Title,
                        CategoryGuid = response.Category.CategoryGuid.ToString(),
                    });
                }
            }
            return result;
        }
    }
}
