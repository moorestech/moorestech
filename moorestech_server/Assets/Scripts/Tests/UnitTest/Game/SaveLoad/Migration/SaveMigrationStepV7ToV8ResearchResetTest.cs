using System.Text.RegularExpressions;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.SaveLoad.Migration
{
    public class SaveMigrationStepV7ToV8ResearchResetTest
    {
        // 旧マスタでコスト0だった6ノード
        // The six nodes that cost nothing in old masters
        private const string FilterSplitter = "019e3aab-32c6-7166-a184-d761153c2498";
        private const string MachineOverclock = "019e3ae2-5005-7439-b242-9b7b3191a2c3";
        private const string MetalPrecision = "019e3af5-f2e6-76de-a80f-5e66e79fa35d";
        private const string FusionReactor = "019e3af0-297f-726e-8a02-0a8518124e6d";
        private const string AntimatterBomb = "019e3aef-a149-746b-8fad-159e0434cb51";
        private const string OrbitalElevator = "01a16291-4094-7369-a87f-5dfe2197d25e";

        // 旧マスタで上の研究が解放していたもの
        // What the research above unlocked in old masters
        private const string MetalPrinterItem = "019e3af5-e750-71ae-ab09-2b98f3db9913";
        private const string GrapheneCableItem = "01a16290-f793-748b-a0b0-91d44be0d4ab";
        private const string ElevatorBaseBlock = "01a162aa-cec0-738c-93c5-f58adc87fa7f";
        private const string LaunchPadBlock = "01a162b0-7769-7419-af24-bb35528b93d2";
        private const string FusionReactorBlock = "01a162f1-ab04-76e2-94cf-dc713edeb63b";

        // 対象外（コストのあるTier8研究と無関係な記録）
        // Out of scope (a costed Tier8 research and unrelated records)
        private const string OtherResearch = "019e3ad9-46e2-73fd-9555-8c1d0245d26d";
        private const string OtherItem = "00000000-0000-0000-1234-000000000001";
        private const string OtherBlock = "00000000-0000-0000-0000-000000000001";

        [Test]
        public void 対象6ノードの完了だけが外れ他の完了と読めない値は残るTest()
        {
            var save = Save(Completed(OtherResearch, FilterSplitter, MachineOverclock, MetalPrecision, FusionReactor, AntimatterBomb, OrbitalElevator, "broken"), Unlocks(), Unlocks());

            var result = Migrate(save);

            Assert.IsTrue(JToken.DeepEquals(Completed(OtherResearch, "broken"), result["research"]["CompletedResearchGuids"]));
        }

        [Test]
        public void 外したノードが解放した記録だけが消え他の記録は残るTest()
        {
            // 金属超精密加工と軌道エレベーターの建設を完了。核融合炉は未完了なので炉ブロックの記録は残る
            // Metal precision and orbital elevator are completed; the fusion reactor is not, so its block record stays
            var save = Save(
                Completed(OtherResearch, MetalPrecision, OrbitalElevator),
                Unlocks(MetalPrinterItem, GrapheneCableItem, OtherItem),
                Unlocks(ElevatorBaseBlock, LaunchPadBlock, FusionReactorBlock, OtherBlock));

            var result = Migrate(save);

            Assert.IsTrue(JToken.DeepEquals(Completed(OtherResearch), result["research"]["CompletedResearchGuids"]));
            Assert.IsTrue(JToken.DeepEquals(Unlocks(OtherItem), result["gameUnlockState"]["itemUnlockStateInfos"]));
            Assert.IsTrue(JToken.DeepEquals(Unlocks(FusionReactorBlock, OtherBlock), result["gameUnlockState"]["blockUnlockStateInfos"]));
        }

        [Test]
        public void 対象ノードを完了していないセーブは解放記録も触らないTest()
        {
            var save = Save(Completed(OtherResearch), Unlocks(MetalPrinterItem), Unlocks(FusionReactorBlock));
            var original = save.DeepClone();

            var result = Migrate(save);

            Assert.IsTrue(JToken.DeepEquals(original["research"], result["research"]));
            Assert.IsTrue(JToken.DeepEquals(original["gameUnlockState"], result["gameUnlockState"]));
        }

        [TestCase("{\"worldVersion\":7,\"world\":[]}")]
        [TestCase("{\"worldVersion\":7,\"world\":[],\"research\":null,\"gameUnlockState\":null}")]
        [TestCase("{\"worldVersion\":7,\"world\":[],\"research\":{\"CompletedResearchGuids\":null}}")]
        [TestCase("{\"worldVersion\":7,\"world\":[],\"research\":{\"CompletedResearchGuids\":[\"019e3af5-f2e6-76de-a80f-5e66e79fa35d\"]}}")]
        public void 研究や解放状態の節が無くても変換できるTest(string json)
        {
            var result = new SaveMigrationStepV7ToV8().Migrate(JObject.Parse(json));

            Assert.IsTrue(result.IsConverted, result.FailureReason);
        }

        [TestCase("{\"research\":[]}")]
        [TestCase("{\"research\":{\"CompletedResearchGuids\":{}}}")]
        [TestCase("{\"research\":{\"CompletedResearchGuids\":[\"019e3af5-f2e6-76de-a80f-5e66e79fa35d\"]},\"gameUnlockState\":[]}")]
        [TestCase("{\"research\":{\"CompletedResearchGuids\":[\"019e3af5-f2e6-76de-a80f-5e66e79fa35d\"]},\"gameUnlockState\":{\"itemUnlockStateInfos\":5}}")]
        [TestCase("{\"research\":{\"CompletedResearchGuids\":[\"019e3af5-f2e6-76de-a80f-5e66e79fa35d\"]},\"gameUnlockState\":{\"blockUnlockStateInfos\":[1]}}")]
        public void 形の違う節は理由つきで拒否し原本を変えないTest(string json)
        {
            var save = JObject.Parse(json);
            save["worldVersion"] = 7;
            save["world"] = new JArray();
            var original = save.DeepClone();
            LogAssert.Expect(LogType.Warning, new Regex("版7から版8へ変換できません"));

            var result = new SaveMigrationStepV7ToV8().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            Assert.IsTrue(JToken.DeepEquals(original, save));
        }

        [Test]
        public void 再実行しても変化しないTest()
        {
            var once = Migrate(Save(Completed(OtherResearch, OrbitalElevator), Unlocks(GrapheneCableItem, OtherItem), Unlocks(LaunchPadBlock)));
            var snapshot = once.DeepClone();

            var twice = Migrate(once);

            Assert.IsTrue(JToken.DeepEquals(snapshot, twice));
        }

        #region TestHelper

        private static JObject Migrate(JObject save)
        {
            var result = new SaveMigrationStepV7ToV8().Migrate(save);
            Assert.IsTrue(result.IsConverted, result.FailureReason);
            return result.Save;
        }

        private static JObject Save(JArray completed, JArray itemInfos, JArray blockInfos)
        {
            return new JObject
            {
                ["worldVersion"] = 7,
                ["world"] = new JArray(),
                ["research"] = new JObject { ["CompletedResearchGuids"] = completed },
                ["gameUnlockState"] = new JObject { ["itemUnlockStateInfos"] = itemInfos, ["blockUnlockStateInfos"] = blockInfos },
            };
        }

        private static JArray Completed(params string[] guids)
        {
            var array = new JArray();
            foreach (var guid in guids) array.Add(guid);
            return array;
        }

        private static JArray Unlocks(params string[] guids)
        {
            var array = new JArray();
            foreach (var guid in guids) array.Add(new JObject { ["guid"] = guid, ["isUnlocked"] = true });
            return array;
        }

        #endregion
    }
}
