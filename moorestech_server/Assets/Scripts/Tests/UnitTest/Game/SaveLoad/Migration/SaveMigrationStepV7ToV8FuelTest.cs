using System;
using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad.Migration
{
    public class SaveMigrationStepV7ToV8FuelTest
    {
        private const string Key = "Game.Block.Blocks.PowerGenerator.VanillaElectricGeneratorComponent";

        [TestCase("Item", null)]
        [TestCase("Item", "")]
        [TestCase("Fluid", "00000000-0000-0000-0000-000000000000")]
        [TestCase(null, null)]
        public void MissingLegacyFuelIsNormalizedIdempotently(string type, string guid)
        {
            var fuel = new JObject { ["remainingFuelSeconds"] = 5, ["inventory"] = new JArray(), ["fluidTank"] = new JObject { ["amount"] = 3 } };
            if (type != null) fuel["activeFuelType"] = type;
            fuel[type == "Fluid" ? "currentFuelFluidGuid" : "currentFuelItemGuid"] = guid;
            var save = Save(fuel);
            Assert.IsTrue(new SaveMigrationStepV7ToV8().Migrate(save).IsConverted);
            Assert.AreEqual("None", (string)fuel["activeFuelType"]);
            Assert.AreEqual(0, (double)fuel["remainingFuelSeconds"]);
            Assert.AreEqual(3, (int)fuel["fluidTank"]["amount"]);
            var once = save.DeepClone();
            Assert.IsTrue(new SaveMigrationStepV7ToV8().Migrate(save).IsConverted);
            Assert.IsTrue(JToken.DeepEquals(once, save));
        }

        [Test]
        public void IdentifiableFuelIsPreservedAndLaterRefusalIsAtomic()
        {
            var fuel = new JObject { ["activeFuelType"] = "Item", ["currentFuelItemGuid"] = Guid.NewGuid().ToString(), ["remainingFuelSeconds"] = 4 };
            var save = Save(fuel);
            Assert.IsTrue(new SaveMigrationStepV7ToV8().Migrate(save).IsConverted);
            Assert.AreEqual("Item", (string)fuel["activeFuelType"]);

            // 後段の拒否があれば燃料も変更しない
            // A later refusal leaves the fuel unchanged too
            fuel["currentFuelItemGuid"] = JValue.CreateNull();
            save["spaceElevatorCablePool"] = 5;
            var before = save.DeepClone();
            LogAssert.Expect(LogType.Warning, new Regex("版7から版8へ変換できません"));
            Assert.IsFalse(new SaveMigrationStepV7ToV8().Migrate(save).IsConverted);
            Assert.IsTrue(JToken.DeepEquals(before, save));
        }

        private static JObject Save(JObject fuel) => new()
        {
            ["worldVersion"] = 7,
            ["world"] = new JArray(new JObject { ["state"] = new JObject { [Key] = fuel } }),
        };
    }
}
