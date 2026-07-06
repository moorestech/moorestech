using System.Collections.Generic;
using System.IO;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi
{
    /// <summary>
    /// 研究ツリーの DTO ⇔ WireFixtures の一致を C# 側から強制する
    /// Enforce DTO ⇔ WireFixtures equality for the research tree from the C# side
    /// </summary>
    public class WireContractResearchTest
    {
        // 2 ノード（completed / 前提未達）で prevGuids・consumeItems・reward/unlock を網羅する
        // Two nodes (completed / prerequisite-unmet) covering prevGuids, consumeItems, reward/unlock
        [Test]
        public void ResearchTreeFixtureMatchesDto()
        {
            var dto = new ResearchTreeDto
            {
                Nodes = new List<ResearchNodeDto>
                {
                    new()
                    {
                        Guid = "11111111-1111-1111-1111-111111111111",
                        Name = "最初の研究",
                        Description = "説明テキスト",
                        State = "completed",
                        Position = new ResearchPositionDto { X = 0, Y = 0 },
                        PrevGuids = new List<string>(),
                        ConsumeItems = new List<ResearchConsumeItemDto> { new() { ItemId = 1, Count = 5 } },
                        RewardItemIds = new List<int> { 2 },
                        UnlockItemIds = new List<int>(),
                    },
                    new()
                    {
                        Guid = "22222222-2222-2222-2222-222222222222",
                        Name = "次の研究",
                        Description = "前提つき",
                        State = "unresearchableNotEnoughPreNode",
                        Position = new ResearchPositionDto { X = 300, Y = -120 },
                        PrevGuids = new List<string> { "11111111-1111-1111-1111-111111111111" },
                        ConsumeItems = new List<ResearchConsumeItemDto>(),
                        RewardItemIds = new List<int>(),
                        UnlockItemIds = new List<int> { 3 },
                    },
                },
            };
            AssertMatchesFixture(dto, "research_tree.json");
        }

        // DTO を実運用シリアライザで直列化しフィクスチャと DeepEquals 照合する
        // Serialize the DTO with the production serializer and DeepEquals against the fixture
        private void AssertMatchesFixture(object dto, string fixtureName)
        {
            var actual = JToken.Parse(WebUiJson.Serialize(dto));
            var expected = JToken.Parse(LoadFixture(fixtureName));
            Assert.IsTrue(JToken.DeepEquals(expected, actual), $"fixture mismatch: {fixtureName}\nexpected: {expected}\nactual: {actual}");
        }

        private string LoadFixture(string fixtureName)
        {
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", fixtureName);
            return File.ReadAllText(path);
        }
    }
}
