using System;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad.BeltTransport
{
    public class SaveMigrationStepV3ToV4Test
    {
        private const string Key = "Game.Block.Blocks.BeltConveyor.VanillaBeltConveyorComponent";
        [Test]
        public void LegacySlotPayloadAndConnectorIdentityArePreservedTest()
        {
            var item = new JObject { ["itemStack"] = new JObject { ["itemGuid"] = Guid.NewGuid().ToString(), ["count"] = 1 },
                ["remainingSeconds"] = 0.35, ["sourceConnectorGuid"] = Guid.NewGuid().ToString(), ["goalConnectorGuid"] = Guid.NewGuid().ToString() };
            var slots = new JArray(item.ToString(Formatting.None), JValue.CreateNull(), item.ToString(Formatting.None));
            var save = World(slots);
            var result = new SaveMigrationStepV3ToV4().Migrate(save);
            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(slots, save["world"][0]["state"][Key]["legacyItems"]));
            Assert.IsTrue(new SaveMigrationStepV3ToV4().Migrate(save).IsConverted);
        }

        [Test]
        public void MalformedLegacyItemBlocksMigrationWithoutChangingPayloadTest()
        {
            var save = World(new JArray("not-json"));
            var original = save.DeepClone();
            var result = new SaveMigrationStepV3ToV4().Migrate(save);
            Assert.IsFalse(result.IsConverted);
            Assert.IsNotEmpty(result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(original, save));
        }

        private static JObject World(JArray items) => new JObject { ["worldVersion"] = 3,
            ["world"] = new JArray(new JObject { ["instanceId"] = 4, ["state"] = new JObject { [Key] = items } }) };
    }
}
