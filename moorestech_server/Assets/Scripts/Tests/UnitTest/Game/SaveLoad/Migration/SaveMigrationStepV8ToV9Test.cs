using System.Text.RegularExpressions;
using Game.Block.Blocks.CleanRoom.Machine.YieldPreview;
using Game.SaveLoad.Json.WorldVersions;
using Game.SaveLoad.Migration;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad.Migration
{
    public class SaveMigrationStepV8ToV9Test
    {
        private const string VanillaKey = "Game.Block.Blocks.Machine.VanillaMachineSaveComponent";
        private const string CleanRoomKey = "Game.Block.Blocks.CleanRoom.Machine.CleanRoomMachineProcessorComponent";

        // v8 マスタの GUID（トランジスタ設置済みウェハG1/G3・ダイシング・パッケージング）
        // v8 master GUIDs (transistor-placed wafer G1/G3, dicing, packaging)
        private const string TransistorWaferG1 = "019e3c14-b270-716f-84e5-c0b06a97592b";
        private const string TransistorWaferG3 = "01a17356-2aff-701f-9a1c-173e586a97c8";
        private const string DicingRecipe = "01a17363-05b5-72d8-8a5d-a0c5f18cd451";
        private const string PackagingRecipe = "01a17363-05b5-72d8-8a5d-a0c6e92df431";

        [Test]
        public void グレード付きウェハはG1へ寄り個数を保つTest()
        {
            var result = new SaveMigrationStepV8ToV9().Migrate(MasterV8Save());

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            var outputSlot = result.Save["world"][0]["state"][VanillaKey]["outputSlot"][0];
            Assert.AreEqual(TransistorWaferG1, outputSlot["itemGuid"].Value<string>());
            Assert.AreEqual(5, outputSlot["count"].Value<int>());
            Assert.AreEqual(TransistorWaferG1, result.Save["world"][1]["state"][CleanRoomKey]["consumedInputItemGuids"][0].Value<string>());
            Assert.AreEqual(8, result.Save["worldVersion"].Value<int>());
        }

        [Test]
        public void 解放状態のグレード付きウェハはG1へ寄せないTest()
        {
            // 未解放の G3 を G1 へ寄せると、後勝ちの読み込みで解放済みの G1 が未解放に戻る
            // Folding a locked G3 into G1 would relock the unlocked G1 through last-wins loading
            var result = new SaveMigrationStepV8ToV9().Migrate(MasterV8Save());

            var unlocks = (JArray)result.Save["gameUnlockState"]["itemUnlockStateInfos"];
            Assert.AreEqual(TransistorWaferG1, unlocks[0]["guid"].Value<string>());
            Assert.AreEqual(TransistorWaferG3, unlocks[1]["guid"].Value<string>());
        }

        [TestCase("{\"kind\":\"waferGrade\",\"maxGrade\":2,\"promotionChance\":0.3}", true)]
        [TestCase("{\"kind\":\"dicing\",\"dieLossRate\":0.15}", true)]
        [TestCase("{\"kind\":\"layerExposure\",\"layerFailRate\":0.1}", false)]
        [TestCase("{\"kind\":\"dicing\",\"dieLossRate\":0.15,\"qualityTier\":2,\"levelRates\":[0.1,0.7,0.1],\"failedRate\":0.1}", false)]
        public void 版9で読めない抽選記録だけ捨てるTest(string drawRecordJson, bool dropped)
        {
            var save = MasterV8Save();
            var original = JObject.Parse(drawRecordJson);
            Pending(save)["drawRecord"] = original.DeepClone();

            var result = new SaveMigrationStepV8ToV9().Migrate(save);

            // 残った記録は版9の復元で読める
            // A kept record is readable by the version-9 restore
            Assert.IsTrue(result.IsConverted, result.FailureReason);
            var migrated = Pending(result.Save)["drawRecord"];
            if (dropped) Assert.AreEqual(JTokenType.Null, migrated.Type);
            else Assert.IsTrue(JToken.DeepEquals(original, migrated));
            Assert.DoesNotThrow(() => CleanRoomYieldPreviewResult.RestoreDrawRecord(migrated as JObject));
        }

        [Test]
        public void 継承をやめたレシピの継承レベルだけ捨てるTest()
        {
            var result = new SaveMigrationStepV8ToV9().Migrate(MasterV8Save());

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(JTokenType.Null, result.Save["world"][1]["state"][CleanRoomKey]["inheritedLevel"].Type);
            Assert.AreEqual(JTokenType.Null, Pending(result.Save)["inheritedLevel"].Type);
            Assert.AreEqual(2, result.Save["world"][2]["state"][CleanRoomKey]["inheritedLevel"].Value<int>());
            Assert.AreEqual(2, result.Save["world"][2]["state"][CleanRoomKey]["pendingRealization"]["inheritedLevel"].Value<int>());
        }

        // 旧マスタ(5c5ccfe)の items.json から書き起こした期待値。本番の表とは独立に持ち、表の誤記を検出する
        // Expectations transcribed from the old master (5c5ccfe) items.json, held independently of the production table to catch typos in it
        private static readonly string[][] OldGradedWaferGuidsToG1 =
        {
            new[] { "019e3c14-b270-716f-84e5-c0b06a97592b", "01a17356-2aff-701f-9a1c-173dd8d81157", "01a17356-2aff-701f-9a1c-173e586a97c8", "01a17356-2aff-701f-9a1c-173f4e56a08c", "01a17356-2aff-701f-9a1c-17400907fcdb" },
            new[] { "019e3c25-78f1-70ec-be1b-2c2cdd9297bb", "01a17356-2aff-701f-9a1c-1741f04fc54a", "01a17356-2aff-701f-9a1c-1742daa8f03b", "01a17356-2aff-701f-9a1c-1743a3e0faa8", "01a17356-2aff-701f-9a1c-17440811ab88" },
            new[] { "01a17356-2aff-701f-9a1c-1745ca409137", "01a17356-2aff-701f-9a1c-17463137c0e6", "01a17356-2aff-701f-9a1c-1747727e8dd7", "01a17356-2aff-701f-9a1c-17489944bcb8", "01a17356-2aff-701f-9a1c-1749778cc017" },
            new[] { "019e3b02-b9fd-73c7-9538-ef13b0f755f1", "01a17356-2aff-701f-9a1c-174aa306c5ed", "01a17356-2aff-701f-9a1c-174b4e200c10", "01a17356-2aff-701f-9a1c-174cedcce469", "01a17356-2aff-701f-9a1c-174dce4a2f06" },
        };

        private static readonly string[] OldInheritanceRecipeGuids =
        {
            "01a17363-05b5-72d8-8a5d-a0c245c7ebb6", "01a17363-05b5-72d8-8a5d-a0c31f51aa60", "01a17363-05b5-72d8-8a5d-a0c41f603b85", "01a17363-05b5-72d8-8a5d-a0c5f18cd451",
        };

        [Test]
        public void 凍結した移行表の全行が旧マスタと一致するTest()
        {
            // 旧G2〜G5の16個を出力スロットに置き、継承をやめた4レシピの機械に継承レベルを置いて移行する
            // Put the 16 old G2-G5 wafers in output slots and a machine per dropped-inheritance recipe with an inherited level, then migrate
            var slots = new JArray();
            foreach (var family in OldGradedWaferGuidsToG1)
                foreach (var oldGuid in family) slots.Add(new JObject { ["itemGuid"] = oldGuid, ["count"] = 1 });
            var world = new JArray(new JObject { ["blockGuid"] = "a", ["state"] = new JObject { [VanillaKey] = new JObject { ["outputSlot"] = slots } } });
            foreach (var recipeGuid in OldInheritanceRecipeGuids)
                world.Add(new JObject { ["blockGuid"] = "r", ["state"] = new JObject { [CleanRoomKey] = new JObject { ["cycleCount"] = 1, ["state"] = 0, ["recipeGuid"] = recipeGuid, ["inheritedLevel"] = 2 } } });

            var result = new SaveMigrationStepV8ToV9().Migrate(new JObject { ["worldVersion"] = 8, ["world"] = world });

            // 全グレードが同じ工程の G1（各行の先頭）へ寄り、継承レベルは4レシピとも捨てられる
            // Every grade folds into its process's G1 (the first of each row) and the inherited level is dropped for all four recipes
            Assert.IsTrue(result.IsConverted, result.FailureReason);
            var migratedSlots = (JArray)result.Save["world"][0]["state"][VanillaKey]["outputSlot"];
            var slotIndex = 0;
            foreach (var family in OldGradedWaferGuidsToG1)
                foreach (var oldGuid in family) Assert.AreEqual(family[0], migratedSlots[slotIndex++]["itemGuid"].Value<string>(), $"移行元 {oldGuid}");
            for (var i = 0; i < OldInheritanceRecipeGuids.Length; i++)
                Assert.AreEqual(JTokenType.Null, result.Save["world"][i + 1]["state"][CleanRoomKey]["inheritedLevel"].Type, OldInheritanceRecipeGuids[i]);
        }

        [Test]
        public void 再実行しても変化しないTest()
        {
            var first = new SaveMigrationStepV8ToV9().Migrate(MasterV8Save());
            Assert.IsTrue(first.IsConverted, first.FailureReason);
            var once = first.Save.DeepClone();

            var second = new SaveMigrationStepV8ToV9().Migrate(first.Save);

            Assert.IsTrue(second.IsConverted, second.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(once, second.Save));
        }

        [Test]
        public void 読めない形なら何も書き換えず拒否するTest()
        {
            // ウェハを寄せる部品が通っても、抽選記録の形が壊れていれば何も変えない
            // Even when the wafer part passes, a broken draw-record shape changes nothing
            var save = MasterV8Save();
            Pending(save)["drawRecord"] = 5;
            var original = save.DeepClone();
            LogAssert.Expect(LogType.Warning, new Regex("版8から版9へ変換できません"));

            var result = new SaveMigrationStepV8ToV9().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains("drawRecord", result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(original, save));
        }

        [Test]
        public void 変換元の版は8Test()
        {
            Assert.AreEqual(8, new SaveMigrationStepV8ToV9().FromVersion);
        }

        [Test]
        public void 版8のセーブは連鎖で現在版まで変換されるTest()
        {
            var result = SaveMigrationChain.ForCurrentVersion(new ISaveMigrationStep[] { new SaveMigrationStepV1ToV2(), new SaveMigrationStepV2ToV3(), new SaveMigrationStepV3ToV4(), new SaveMigrationStepV4ToV5(), new SaveMigrationStepV5ToV6(), new SaveMigrationStepV6ToV7(), new SaveMigrationStepV7ToV8(), new SaveMigrationStepV8ToV9(), new SaveMigrationStepV9ToV10(), new SaveMigrationStepV10ToV11(), new SaveMigrationStepV11ToV12() }).Migrate(MasterV8Save());

            Assert.IsTrue(result.CanLoad, result.BlockedReason);
            Assert.AreEqual(WorldSaveAllInfo.CurrentVersion, result.Save["worldVersion"].Value<int>());
            Assert.AreEqual(JTokenType.Null, Pending(result.Save)["drawRecord"].Type);
        }

        #region TestHelper

        // master版8の実形: G3 ウェハを出力に持つ機械・ダイシング待機中の清浄室機械・パッケージング中の清浄室機械・G1/G3 の解放状態
        // The real master version-8 shape: a machine holding G3 wafers, a clean-room machine waiting on dicing, one packaging, and G1/G3 unlock states
        private static JObject MasterV8Save()
        {
            var dicing = JObject.Parse($"{{\"cycleCount\":3,\"state\":0,\"recipeGuid\":\"{DicingRecipe}\",\"inheritedLevel\":2,\"consumedInputItemGuids\":[\"{TransistorWaferG3}\"]," +
                                       $"\"pendingRealization\":{{\"recipeGuid\":\"{DicingRecipe}\",\"inheritedLevel\":2,\"outputs\":[],\"totalTicks\":20,\"powerMultiplier\":1.0,\"drawRecord\":{{\"kind\":\"dicing\",\"dieLossRate\":0.15}}}}}}");
            var packaging = JObject.Parse($"{{\"cycleCount\":1,\"state\":0,\"recipeGuid\":\"{PackagingRecipe}\",\"inheritedLevel\":2," +
                                          $"\"pendingRealization\":{{\"recipeGuid\":\"{PackagingRecipe}\",\"inheritedLevel\":2,\"outputs\":[],\"totalTicks\":20,\"powerMultiplier\":1.0,\"drawRecord\":null}}}}");
            return new JObject
            {
                ["worldVersion"] = 8,
                ["world"] = new JArray(
                    PlacedBlock(0, new JObject { [VanillaKey] = JObject.Parse($"{{\"outputSlot\":[{{\"itemGuid\":\"{TransistorWaferG3}\",\"count\":5}}]}}") }),
                    PlacedBlock(1, new JObject { [CleanRoomKey] = dicing }),
                    PlacedBlock(2, new JObject { [CleanRoomKey] = packaging })),
                ["gameUnlockState"] = JObject.Parse($"{{\"itemUnlockStateInfos\":[{{\"guid\":\"{TransistorWaferG1}\",\"isUnlocked\":true}},{{\"guid\":\"{TransistorWaferG3}\",\"isUnlocked\":false}}]}}"),
            };
        }

        // 水平北・座標・guidを持つ最小ブロック（guidは架空）
        // Minimal block with flat north, position, guid (guid is fictional)
        private static JObject PlacedBlock(int x, JObject state)
        {
            return new JObject { ["blockGuid"] = $"00000000-0000-0000-0000-00000000000{x}", ["instanceId"] = x, ["X"] = x, ["Y"] = 0, ["Z"] = 0, ["direction"] = 4, ["state"] = state };
        }

        private static JObject Pending(JObject save)
        {
            return (JObject)save["world"][1]["state"][CleanRoomKey]["pendingRealization"];
        }

        #endregion
    }
}
