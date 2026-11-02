using System.IO;
using Core.Master;
using Game.Block.Interface.MachineUpgrade;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.MachineUpgrade
{
    /// <summary>
    ///     継承レシピの util と検証（継承元・継承先の数、ダイシング形の出力、上限モードの混在）を確かめる
    ///     Checks the inheritance util and validation (source/target counts, dicing-shaped outputs, mixed cap modes)
    /// </summary>
    public class MachineRecipeLevelInheritanceValidatorTest
    {
        private const string TestElectricMachineGuid = "00000000-0000-0000-0000-000000000001";

        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void UtilResolvesSourceAndTargetTest()
        {
            var inherit = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestLevelInheritanceIds.InheritRecipeGuid);
            var capped = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestLevelInheritanceIds.CappedInheritRecipeGuid);
            var plain = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];

            // 継承元は入力0、継承先は出力0、レベルは変種の並び順で決まり、変種でなければ null
            // The source is input 0, the target is output 0, the level follows the variant order, and a non-variant is null
            Assert.IsTrue(MachineRecipeLevelInheritanceUtil.TryGetShape(inherit, out var shape));
            Assert.AreEqual(0, shape.SourceInputIndex);
            Assert.AreEqual(0, shape.TargetOutputIndex);
            Assert.AreEqual(3, shape.ResolveSourceLevel(ForUnitTestLevelInheritanceIds.Source(3)));
            Assert.IsNull(shape.ResolveSourceLevel(ForUnitTestItemId.ItemId1));
            Assert.AreEqual(ForUnitTestLevelInheritanceIds.Target(2), shape.GetTargetVariant(2));
            Assert.AreEqual(3, shape.MaxLevel);

            // 入力・出力の写しは継承元・継承先だけを変種にし、固定素材は書いたまま
            // Input/output mapping turns only the source and target into variants; the fixed input stays as written
            Assert.AreEqual(ForUnitTestLevelInheritanceIds.Source(3), MachineRecipeLevelInheritanceUtil.ResolveInputItemId(inherit, 0, 3));
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(inherit.InputItems[1].ItemGuid), MachineRecipeLevelInheritanceUtil.ResolveInputItemId(inherit, 1, 3));
            Assert.AreEqual(ForUnitTestLevelInheritanceIds.Target(2), MachineRecipeLevelInheritanceUtil.ResolveOutputItemId(inherit, 0, 2));

            // 上限の有無と鍵（継承先の基準GUID）
            // Cap presence and its key (the target's base GUID)
            Assert.IsFalse(shape.HasCraftCap);
            Assert.IsTrue(MachineRecipeLevelInheritanceUtil.TryGetShape(capped, out var cappedShape));
            Assert.IsTrue(cappedShape.HasCraftCap);
            Assert.AreEqual(ForUnitTestLevelInheritanceIds.CappedTargetBaseGuid, cappedShape.TargetBaseItemGuid);

            // 継承しないレシピには形が無い
            // A non-inheritance recipe has no shape
            Assert.IsFalse(MachineRecipeLevelInheritanceUtil.TryGetShape(plain, out _));
        }

        [Test]
        public void InheritedLevelValidityFollowsRecipeTest()
        {
            var inherit = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestLevelInheritanceIds.InheritRecipeGuid);
            var plain = MasterHolder.MachineRecipesMaster.MachineRecipes.Data[0];

            // 継承レシピは 1..3 の値だけ、継承しないレシピは値なしだけを正とする（キー欠けの 0 や null を通さない）
            // An inheritance recipe accepts only 1..3 and a plain recipe only no value (a missing key's 0 or null never passes)
            Assert.IsTrue(MachineRecipeLevelInheritanceUtil.IsValidInheritedLevel(inherit, 1));
            Assert.IsTrue(MachineRecipeLevelInheritanceUtil.IsValidInheritedLevel(inherit, 3));
            Assert.IsFalse(MachineRecipeLevelInheritanceUtil.IsValidInheritedLevel(inherit, null));
            Assert.IsFalse(MachineRecipeLevelInheritanceUtil.IsValidInheritedLevel(inherit, 0));
            Assert.IsFalse(MachineRecipeLevelInheritanceUtil.IsValidInheritedLevel(inherit, 4));
            Assert.IsTrue(MachineRecipeLevelInheritanceUtil.IsValidInheritedLevel(plain, null));
            Assert.IsFalse(MachineRecipeLevelInheritanceUtil.IsValidInheritedLevel(plain, 1));
        }

        [Test]
        public void TestMasterPassesValidationTest()
        {
            Assert.IsTrue(new MachineRecipesMaster(LoadJson()).Validate(out var logs), logs);
        }

        [Test]
        public void TwoFamilySourcesFailTest()
        {
            var json = LoadJson();
            var recipe = FindRecipe(json, ForUnitTestLevelInheritanceIds.InheritRecipeGuid.ToString());

            // 2つ目の入力も継承元ファミリーの基準にすると継承元が2つになる
            // Making the second input another family base yields two sources
            recipe["inputItems"][1]["itemGuid"] = ForUnitTestLevelInheritanceIds.SourceBaseGuid.ToString();

            Assert.IsFalse(new MachineRecipesMaster(json).Validate(out var logs));
            StringAssert.Contains("must have exactly one level-family source input", logs);
        }

        [Test]
        public void DicingShapedOutputsOnQualityMachinePassTest()
        {
            var json = LoadJson();
            var recipe = FindRecipe(json, ForUnitTestLevelInheritanceIds.InheritRecipeGuid.ToString());

            // 品質可の機械（出力3）へ移し、出力を「継承先の基準＋継承先の Lv2＋ファミリー外」にする（ダイシングと同じ形）
            // Move it to the quality-enabled machine (3 outputs) with outputs "target base + target Lv2 + out-of-family" (the dicing shape)
            recipe["blockGuid"] = TestElectricMachineGuid;
            recipe["outputItems"] = JArray.Parse("[" +
                "{\"percent\": 1, \"count\": 1, \"itemGuid\": \"00000000-0000-0000-c0de-000000000021\"}," +
                "{\"percent\": 1, \"count\": 1, \"itemGuid\": \"00000000-0000-0000-c0de-000000000022\"}," +
                "{\"percent\": 1, \"count\": 1, \"itemGuid\": \"00000000-0000-0000-1234-000000000002\"}]");

            Assert.IsTrue(new MachineRecipesMaster(json).Validate(out var logs), logs);
        }

        [Test]
        public void MixedCapModeForSameTargetFailsTest()
        {
            var json = LoadJson();
            var capped = FindRecipe(json, ForUnitTestLevelInheritanceIds.CappedInheritRecipeGuid.ToString());

            // 上限つきレシピの継承先を上限なしレシピと同じファミリーにすると混在になる
            // Pointing the capped recipe at the uncapped recipe's family mixes the modes
            capped["outputItems"][0]["itemGuid"] = ForUnitTestLevelInheritanceIds.TargetBaseGuid.ToString();

            Assert.IsFalse(new MachineRecipesMaster(json).Validate(out var logs));
            StringAssert.Contains("mixes Inherit and InheritUpToUnlocked", logs);
        }

        private static JObject LoadJson()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "machineRecipes.json");
            return JObject.Parse(File.ReadAllText(path));
        }

        private static JObject FindRecipe(JObject json, string recipeGuid)
        {
            foreach (var recipe in (JArray)json["data"])
            {
                if (recipe["machineRecipeGuid"].Value<string>() == recipeGuid) return (JObject)recipe;
            }
            Assert.Fail($"recipe not found: {recipeGuid}");
            return null;
        }
    }
}
