using System;
using System.IO;
using System.Linq;
using Game.Block.Blocks.PowerGenerator;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Interface;
using Microsoft.Extensions.DependencyInjection;
using Server.Boot;
using Tests.Module.TestMod;
using Tests.Util;
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
        private string _root;
        [TearDown]
        public void Cleanup() { if (_root != null && Directory.Exists(_root)) Directory.Delete(_root, true); }

        [TestCase("Item")]
        [TestCase("Fluid")]
        [TestCase(null)]
        public void LegacyFuelLoadsThroughDiskMigrationChain(string kind)
        {
            var (_, original) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.GeneratorId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            var save = JObject.Parse(SaveLoadJsonTestHelper.AssembleSaveJson(original));
            save["worldVersion"] = 7;
            var fuel = (JObject)save["world"].Single(block => block["state"]?[Key] != null)["state"][Key];
            if (kind == null) fuel.Property("activeFuelType").Remove();
            else fuel["activeFuelType"] = kind;
            fuel["remainingFuelSeconds"] = 5;
            fuel["currentFuelItemGuid"] = JValue.CreateNull();
            fuel["currentFuelFluidGuid"] = JValue.CreateNull();

            // 実ディスクの旧形式をLoadOrInitializeから開き、移行連鎖と厳格復元を通す
            // Open the legacy disk save through LoadOrInitialize, exercising migration and strict restore
            _root = Path.Combine(Path.GetTempPath(), "legacy-fuel-" + Guid.NewGuid());
            var directory = WorldDataDirectory.FromWorldRoot(Path.Combine(_root, "world"));
            Directory.CreateDirectory(Path.GetDirectoryName(directory.SaveJsonFilePath));
            File.WriteAllText(directory.SaveJsonFilePath, save.ToString());
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, directory.SaveJsonFilePath),
            };
            var (_, loaded) = new MoorestechServerDIContainerGenerator().Create(options);
            loaded.GetRequiredService<IWorldSaveDataLoader>().LoadOrInitialize();
            var generator = ServerContext.WorldBlockDatastore.GetBlock(Vector3Int.zero).GetComponent<VanillaElectricGeneratorComponent>();
            var restored = (VanillaElectricGeneratorSaveJsonObject)generator.GetSaveState();
            Assert.AreEqual("None", restored.ActiveFuelType);
            Assert.AreEqual(0, restored.RemainingFuelSeconds);
            Assert.IsTrue(File.Exists(Path.Combine(_root, "world", "backup", "7", "save.json")));
        }

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
