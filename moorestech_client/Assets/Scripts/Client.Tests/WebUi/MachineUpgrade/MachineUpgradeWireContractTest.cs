using System.Collections.Generic;
using System.IO;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Client.WebUiHost.Game.Topics.MachineUpgrade;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.WebUi.MachineUpgrade
{
    /// <summary>
    ///     machine_upgrade.current と継承つき機械レシピの wire 形状を WireFixtures と一致させる
    ///     Keeps the wire shape of machine_upgrade.current and an inheritance machine recipe equal to WireFixtures
    /// </summary>
    public class MachineUpgradeWireContractTest
    {
        [Test]
        public void MachineUpgradeFixtureMatchesDto()
        {
            AssertMatchesFixture(new MachineUpgradeDto { ModuleSlotStackLimit = 6 }, "machine_upgrade.json");
        }

        [Test]
        public void LevelInheritanceRecipeFixtureMatchesDto()
        {
            var dto = new MachineRecipeDto
            {
                RecipeGuid = "84000000-0000-4000-8000-000000000003",
                BlockGuid = "85000000-0000-4000-8000-000000000001",
                BlockId = 10,
                Time = 4,
                InputItems = new List<RecipeItemDto> { new() { ItemId = 11, Count = 2 }, new() { ItemId = 5, Count = 1 } },
                OutputItems = new List<RecipeItemDto> { new() { ItemId = 21, Count = 1 } },
                InputFluids = new List<RecipeFluidDto>(),
                OutputFluids = new List<RecipeFluidDto>(),
                LevelInheritance = new MachineRecipeLevelInheritanceDto
                {
                    Kind = MachineRecipeLevelInheritanceDtoBuilder.KindInheritUpToUnlocked,
                    SourceInputIndex = 0,
                    SourceItemIds = new List<int> { 11, 12, 13 },
                    TargetOutputIndex = 0,
                    TargetItemIds = new List<int> { 21, 22, 23 },
                    MaxCraftableLevel = 2,
                },
            };
            AssertMatchesFixture(new MachineRecipesDto { Recipes = new List<MachineRecipeDto> { dto } }, "machine_recipe_level_inheritance.json");
        }

        private static void AssertMatchesFixture(object dto, string fixtureName)
        {
            var actual = JToken.Parse(WebUiJson.Serialize(dto));
            var path = Path.Combine(Application.dataPath, "Scripts/Client.Tests/WebUi/WireFixtures", fixtureName);
            var expected = JToken.Parse(File.ReadAllText(path));
            Assert.IsTrue(JToken.DeepEquals(expected, actual), $"fixture mismatch: {fixtureName}\nexpected: {expected}\nactual: {actual}");
        }
    }
}
