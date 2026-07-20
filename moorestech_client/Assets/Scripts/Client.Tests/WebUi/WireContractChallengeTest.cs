using System.Collections.Generic;
using System.IO;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi
{
    public class WireContractChallengeTest
    {
        [Test]
        public void ChallengeFixturesMatchDtos()
        {
            var nodeGuid = "22222222-2222-2222-2222-222222222222";
            var categoryGuid = "11111111-1111-1111-1111-111111111111";
            var node = new ChallengeNodeDto
            {
                Guid = nodeGuid,
                Title = "First challenge",
                Summary = "Build a machine",
                IconItemId = 2,
                State = "current",
                Position = new ChallengeVectorDto { X = 0, Y = 100 },
                Scale = new ChallengeVectorDto { X = 1, Y = 1 },
                PrevGuids = new List<string>(),
            };
            var tree = new ChallengeTreeDto
            {
                Categories = new List<ChallengeCategoryDto>
                {
                    new() { Guid = categoryGuid, Name = "Basics", IconItemId = 1, Nodes = new List<ChallengeNodeDto> { node } },
                },
            };
            var current = new ChallengeCurrentDto
            {
                Challenges = new List<CurrentChallengeDto>
                {
                    new() { Guid = nodeGuid, Title = "First challenge", CategoryGuid = categoryGuid },
                },
                CompletedChallengeGuid = null,
            };

            AssertMatchesFixture(tree, "challenge_tree.json");
            AssertMatchesFixture(current, "challenge_current.json");
        }

        private static void AssertMatchesFixture(object dto, string fixtureName)
        {
            var actual = JToken.Parse(WebUiJson.Serialize(dto));
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", fixtureName);
            var expected = JToken.Parse(File.ReadAllText(path));
            Assert.IsTrue(JToken.DeepEquals(expected, actual), $"fixture mismatch: {fixtureName}");
        }
    }
}
