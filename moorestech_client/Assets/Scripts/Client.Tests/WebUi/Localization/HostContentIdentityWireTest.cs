using System.Linq;
using System.Reflection;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Topics;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.WebUi.Localization
{
    public class HostContentIdentityWireTest
    {
        [Test]
        public void BlockInventoryCarriesGuidWithoutSourceName()
        {
            var dto = new BlockInventoryDto { Open = true };
            var blockGuidField = typeof(BlockInventoryDto).GetField("BlockGuid");
            Assert.That(blockGuidField, Is.Not.Null);
            blockGuidField.SetValue(dto, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

            SetLegacyNameWhenPresent(dto, "Source chest");
            var actual = JObject.Parse(WebUiJson.Serialize(dto));

            Assert.That(actual.Properties().Select(property => property.Name),
                Is.EquivalentTo(new[] { "open", "blockGuid" }));
            Assert.That(actual["blockGuid"]?.Value<string>(),
                Is.EqualTo("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        }

        [Test]
        public void MachineRecipeCarriesGuidWithoutSourceName()
        {
            var dto = new MachineRecipeDto
            {
                RecipeGuid = "11111111-2222-3333-4444-555555555555",
                BlockGuid = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                BlockId = 7,
                Time = 2.5,
            };

            SetLegacyNameWhenPresent(dto, "Source machine");
            var actual = JObject.Parse(WebUiJson.Serialize(dto));

            Assert.That(actual.Properties().Select(property => property.Name), Is.EquivalentTo(new[]
            {
                "recipeGuid", "blockGuid", "blockId", "time",
            }));
        }

        [Test]
        public void ResearchAndChallengeDtosExposeIdentityWithoutDisplayText()
        {
            AssertPublicFields<ResearchNodeDto>(
                "Guid", "State", "IconItemId", "Position", "PrevGuids",
                "ConsumeItems", "RewardItems", "UnlockItemIds");
            AssertPublicFields<ChallengeCategoryDto>("Guid", "IconItemId", "Nodes");
            AssertPublicFields<ChallengeNodeDto>(
                "Guid", "IconItemId", "State", "Position", "Scale", "PrevGuids");
            AssertPublicFields<CurrentChallengeDto>("Guid", "CategoryGuid");
        }

        private static void SetLegacyNameWhenPresent(object dto, string sourceName)
        {
            dto.GetType().GetField("BlockName", BindingFlags.Public | BindingFlags.Instance)
                ?.SetValue(dto, sourceName);
        }

        private static void AssertPublicFields<T>(params string[] expectedNames)
        {
            var actualNames = typeof(T)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(field => field.Name);
            Assert.That(actualNames, Is.EquivalentTo(expectedNames));
        }
    }
}
