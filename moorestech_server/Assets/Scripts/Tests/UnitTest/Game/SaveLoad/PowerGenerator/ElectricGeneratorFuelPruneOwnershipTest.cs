using Game.SaveLoad.Pruning.Sections;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.CombinedTest.Game;

namespace Tests.UnitTest.Game.SaveLoad.PowerGenerator
{
    public class ElectricGeneratorFuelPruneOwnershipTest
    {
        private const string GeneratorKey = "Game.Block.Blocks.PowerGenerator.VanillaElectricGeneratorComponent";

        [TestCase(GeneratorKey, true)]
        [TestCase(GeneratorKey, false)]
        [TestCase("belt", true)]
        [TestCase("Game.Block.Blocks.Gear.FuelGearGeneratorComponent", true)]
        public void OnlyRemovedRootFuelOfElectricGeneratorStopsCombustion(string stateKey, bool missing)
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            var fuel = new JObject
            {
                ["activeFuelType"] = "Item",
                ["currentFuelItemGuid"] = missing ? SaveLoadPreparerTestFixture.MissingGuid : JValue.CreateNull(),
                ["currentFuelFluidGuid"] = "00000000-0000-0000-1234-000000000003",
                ["remainingFuelSeconds"] = 0.5,
            };
            var block = (JObject)save["world"][0];
            block["state"] = new JObject { [stateKey] = fuel };
            var result = new WorldSectionPruner().Prune(save["world"]);

            // 裸GUIDの綴りだけでは電気発電機と判定しない
            // Never infer an electric generator solely from a bare GUID field spelling
            var stopped = missing && stateKey == GeneratorKey;
            Assert.AreEqual(stopped ? "None" : "Item", (string)fuel["activeFuelType"]);
            Assert.AreEqual(stopped ? 0d : 0.5d, (double)fuel["remainingFuelSeconds"]);
            Assert.AreEqual(stopped ? JTokenType.Null : JTokenType.String, fuel["currentFuelFluidGuid"].Type);
            Assert.AreEqual(missing ? 1 : 0, result.EmptiedItemStacks.Count);
            Assert.AreEqual(JTokenType.Null, fuel["currentFuelItemGuid"].Type);
        }

        [Test]
        public void MissingNestedReferenceDoesNotRepairAnInvalidRootFuel()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            var fuel = new JObject
            {
                ["activeFuelType"] = "Item", ["currentFuelItemGuid"] = JValue.CreateNull(), ["remainingFuelSeconds"] = 0.5,
                ["nested"] = new JObject { ["currentFuelItemGuid"] = SaveLoadPreparerTestFixture.MissingGuid },
            };
            save["world"][0]["state"] = new JObject { [GeneratorKey] = fuel };
            var result = new WorldSectionPruner().Prune(save["world"]);
            Assert.AreEqual(1, result.EmptiedItemStacks.Count);
            Assert.AreEqual("Item", (string)fuel["activeFuelType"]);
            Assert.AreEqual(0.5, (double)fuel["remainingFuelSeconds"]);
        }
    }
}
