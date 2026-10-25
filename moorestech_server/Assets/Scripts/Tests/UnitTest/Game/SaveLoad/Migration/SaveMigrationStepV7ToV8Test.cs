using System.Text.RegularExpressions;
using Game.SaveLoad.Json.WorldVersions;
using Game.SaveLoad.Migration;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad.Migration
{
    public class SaveMigrationStepV7ToV8Test
    {
        private const string VanillaKey = "Game.Block.Blocks.Machine.VanillaMachineSaveComponent";
        private const string CleanRoomKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";
        private const string PipeKey = "Game.Block.Blocks.Fluid.FluidPipeSaveComponent";

        [Test]
        public void 版7のセーブに全部品の変換が入るTest()
        {
            var result = new SaveMigrationStepV7ToV8().Migrate(MasterV7Save());

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            var save = result.Save;
            Assert.AreEqual(JTokenType.Null, save["world"][0]["state"][VanillaKey]["processor"]["inheritedLevel"].Type);
            var cleanRoom = save["world"][1]["state"][CleanRoomKey];
            Assert.AreEqual(JTokenType.Null, cleanRoom["inheritedLevel"].Type);
            Assert.IsTrue(JToken.DeepEquals(new JArray(), cleanRoom["inputFluidSlot"]));
            Assert.IsTrue(JToken.DeepEquals(new JArray(), cleanRoom["outputFluidSlot"]));
            Assert.IsFalse(save["rocketHasLaunchedOnce"].Value<bool>());
            Assert.AreEqual(0, save["spaceElevatorCablePool"]["cableCount"].Value<int>());
            Assert.AreEqual(7, save["worldVersion"].Value<int>());
        }

        [Test]
        public void 後ろの部品が拒否すると前の部品も書き換えないTest()
        {
            // 継承レベルを足すべき機械があっても、プールの節が壊れていれば何も変えない
            // Even with machines awaiting an inherited level, a broken pool section changes nothing
            var save = MasterV7Save();
            save["spaceElevatorCablePool"] = 5;
            var original = save.DeepClone();
            LogAssert.Expect(LogType.Warning, new Regex("版7から版8へ変換できません"));

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains("spaceElevatorCablePool", result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(original, save));
        }

        [Test]
        public void 再実行しても変化しないTest()
        {
            var first = new SaveMigrationStepV7ToV8().Migrate(MasterV7Save());
            Assert.IsTrue(first.IsConverted, first.FailureReason);
            var once = first.Save.DeepClone();

            var second = new SaveMigrationStepV7ToV8().Migrate(first.Save);

            Assert.IsTrue(second.IsConverted, second.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(once, second.Save));
        }

        [Test]
        public void 変換元の版は7で現在版は8Test()
        {
            Assert.AreEqual(7, new SaveMigrationStepV7ToV8().FromVersion);
            Assert.AreEqual(8, WorldSaveAllInfo.CurrentVersion);
        }

        [Test]
        public void 版6のセーブは流体変換を経て現在版まで変換されるTest()
        {
            var save = JObject.Parse($"{{\"worldVersion\":6,\"world\":[{{\"state\":{{\"{PipeKey}\":{{\"amount\":1.5}}}}}}]}}");

            var result = CurrentVersionChain().Migrate(save);

            Assert.IsTrue(result.CanLoad, result.BlockedReason);
            Assert.AreEqual(WorldSaveAllInfo.CurrentVersion, result.Save["worldVersion"].Value<int>());
            Assert.AreEqual(1_500_000_000L, result.Save["world"][0]["state"][PipeKey]["amountNano"].Value<long>());
            Assert.IsFalse(result.Save["rocketHasLaunchedOnce"].Value<bool>());
            Assert.AreEqual(0, result.Save["spaceElevatorCablePool"]["cableCount"].Value<int>());
        }

        [Test]
        public void 部品が拒否すると連鎖は版を刻まず止まるTest()
        {
            var save = MasterV7Save();
            save["world"][0]["state"][VanillaKey] = 5;
            LogAssert.Expect(LogType.Error, new Regex("セーブをV7からV8へ変換できませんでした"));

            var result = CurrentVersionChain().Migrate(save);

            Assert.IsFalse(result.CanLoad);
            Assert.AreEqual(7, save["worldVersion"].Value<int>());
        }

        #region TestHelper

        // master版7の実形: 継承レベルの無い通常機械・液体キーの無い清浄室機械・履歴とプールの節なし
        // The real master version-7 shape: a normal machine without an inherited level, a clean-room machine without fluid keys, no history or pool sections
        private static JObject MasterV7Save()
        {
            return new JObject
            {
                ["worldVersion"] = 7,
                ["world"] = new JArray(
                    new JObject { ["blockGuid"] = "a", ["state"] = new JObject { [VanillaKey] = JObject.Parse("{\"inputSlot\":[],\"processor\":{\"state\":1,\"recipeGuid\":\"r\"}}") } },
                    new JObject { ["blockGuid"] = "b", ["state"] = new JObject { [CleanRoomKey] = JObject.Parse("{\"cycleCount\":3,\"state\":0}") } }),
                // 旧マスタでコスト0だった金属超精密加工を完了し、金属3Dプリンタのレシピ表示を解放済み（Task 7 で使う）
                // Completed the formerly zero-cost metal precision research with the metal 3D printer recipe view unlocked (used by Task 7)
                ["research"] = JObject.Parse("{\"CompletedResearchGuids\":[\"019e3ad9-46e2-73fd-9555-8c1d0245d26d\",\"019e3af5-f2e6-76de-a80f-5e66e79fa35d\"]}"),
                ["gameUnlockState"] = JObject.Parse("{\"itemUnlockStateInfos\":[{\"guid\":\"019e3af5-e750-71ae-ab09-2b98f3db9913\",\"isUnlocked\":true}],\"blockUnlockStateInfos\":[]}"),
            };
        }

        // 本番と同じ配列（SaveAndEventServiceRegistration）
        // The same array as production (SaveAndEventServiceRegistration)
        private static SaveMigrationChain CurrentVersionChain()
        {
            return SaveMigrationChain.ForCurrentVersion(new ISaveMigrationStep[] { new SaveMigrationStepV1ToV2(), new SaveMigrationStepV2ToV3(), new SaveMigrationStepV3ToV4(), new SaveMigrationStepV4ToV5(), new SaveMigrationStepV5ToV6(), new SaveMigrationStepV6ToV7(), new SaveMigrationStepV7ToV8() });
        }

        #endregion
    }
}
