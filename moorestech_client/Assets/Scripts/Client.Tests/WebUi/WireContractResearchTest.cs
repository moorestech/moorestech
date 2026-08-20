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
                        Guid = "11111111-1111-4111-8111-111111111111",
                        State = "completed",
                        IconItemId = 2,
                        Position = new ResearchPositionDto { X = 0, Y = 0 },
                        PrevGuids = new List<string>(),
                        ConsumeItems = new List<ResearchConsumeItemDto> { new() { ItemId = 1, Count = 5 } },
                        RewardItems = new List<ResearchRewardItemDto> { new() { ItemId = 2, Count = 4 } },
                        UnlockItemRecipeViewItemIds = new List<int>(),
                        UnlockBlocks = new List<ResearchUnlockBlockDto>(),
                        UnlockMachineRecipes = new List<ResearchUnlockMachineRecipeDto>(),
                        UnlockConnectToolGuids = new List<string>(),
                        UnlockTrainCarGuids = new List<string>(),
                    },
                    new()
                    {
                        Guid = "22222222-2222-4222-8222-222222222222",
                        State = "unresearchableNotEnoughPreNode",
                        IconItemId = 3,
                        Position = new ResearchPositionDto { X = 300, Y = -120 },
                        PrevGuids = new List<string> { "11111111-1111-4111-8111-111111111111" },
                        ConsumeItems = new List<ResearchConsumeItemDto>(),
                        RewardItems = new List<ResearchRewardItemDto>(),
                        UnlockItemRecipeViewItemIds = new List<int> { 3 },
                        UnlockBlocks = new List<ResearchUnlockBlockDto> { new() { BlockId = 7, BlockGuid = "44444444-4444-4444-8444-444444444444" } },
                        UnlockMachineRecipes = new List<ResearchUnlockMachineRecipeDto>
                        {
                            new()
                            {
                                RecipeGuid = "88888888-8888-4888-8888-888888888888",
                                OutputItemIds = new List<int> { 9 },
                                OutputFluids = new List<ResearchUnlockFluidDto>
                                {
                                    new() { FluidId = 1, FluidGuid = "99999999-9999-4999-8999-999999999999", Amount = 100 },
                                },
                            },
                        },
                        UnlockConnectToolGuids = new List<string> { "55555555-5555-4555-8555-555555555555" },
                        UnlockTrainCarGuids = new List<string> { "66666666-6666-4666-8666-666666666666" },
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
