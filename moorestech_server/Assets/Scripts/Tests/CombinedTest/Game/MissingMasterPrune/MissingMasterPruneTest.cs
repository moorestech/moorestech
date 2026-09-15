using System;
using Game.SaveLoad.Pruning;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.CombinedTest.Game.MissingMasterPrune
{
    /// <summary>ブロック・研究の除去と、除去データJSONの骨格を見る。アイテムの住所はMissingMasterItemPruneOriginTest</summary>
    /// <summary>Covers block and research removal and the pruned JSON skeleton; item addresses live in MissingMasterItemPruneOriginTest</summary>
    public class MissingMasterPruneTest
    {
        private const string MissingGuid = SaveLoadPreparerTestFixture.MissingGuid;

        [Test]
        public void マスタに無いブロックはセーブから除去されるTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            ((JArray)save["world"]).Add(SaveLoadPreparerTestFixture.MissingBlock(987654, 50));
            var worldCountBefore = ((JArray)save["world"]).Count;

            var outcome = new MissingMasterPruner().Prune(save);

            Assert.AreEqual(1, outcome.Report.RemovedBlockCount);
            Assert.AreEqual(worldCountBefore - 1, ((JArray)outcome.Save["world"]).Count);
            Assert.IsFalse(outcome.Save.ToString().Contains(MissingGuid));
        }

        // 除去したブロックの中身まで空スタックとして数えると、通知の件数が二重になる
        // Counting a removed block's contents as emptied stacks too would double the notice count
        [Test]
        public void 除去したブロックの中身は空スタック件数に入らないTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            var block = SaveLoadPreparerTestFixture.MissingBlock(987655, 51);
            block["state"] = JObject.Parse($"{{\"chest\":[{{\"itemGuid\":\"{MissingGuid}\",\"count\":3}}]}}");
            ((JArray)save["world"]).Add(block);

            var outcome = new MissingMasterPruner().Prune(save);

            Assert.AreEqual(1, outcome.Report.RemovedBlockCount);
            Assert.AreEqual(0, outcome.Report.EmptiedItemStackCount);
        }

        // base64-MessagePackの状態値（例 RailComponentStateDetail）は素通しする。壊すと復元できない
        // Base64 MessagePack state values (e.g. RailComponentStateDetail) pass through untouched; corrupting them is unrecoverable
        [Test]
        public void JSONで無い状態値は素通しされるTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            var block = (JObject)save["world"][0].DeepClone();
            block["instanceId"] = 987657;
            block["state"] = JObject.Parse("{\"rail\":\"kZPAAAA=\"}");
            ((JArray)save["world"]).Add(block);

            var outcome = new MissingMasterPruner().Prune(save);

            Assert.AreEqual(0, outcome.Report.EmptiedItemStackCount);
            Assert.AreEqual("kZPAAAA=", outcome.Save["world"].Last["state"]["rail"].Value<string>());
        }

        [Test]
        public void マスタに無い研究ノードは完了一覧から除去されるTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            save["research"] = JObject.Parse($"{{\"CompletedResearchGuids\":[\"{MissingGuid}\"]}}");

            var outcome = new MissingMasterPruner().Prune(save);

            Assert.AreEqual(1, outcome.Report.RemovedResearchCount);
            Assert.AreEqual(0, ((JArray)outcome.Save["research"]["CompletedResearchGuids"]).Count);
        }

        // 除去0件のセーブでレポートが立たないこと（毎回ロードで走るので最小構成が本番の常態）
        // A save with nothing to prune must not raise the report; this is the normal case on every load
        [Test]
        public void 除去が無いセーブではレポートもファイル要否も立たないTest()
        {
            var outcome = new MissingMasterPruner().Prune(SaveLoadPreparerTestFixture.BuildSaveJson());

            Assert.IsFalse(outcome.Report.HasRemoval);
            Assert.IsFalse(outcome.HasRemoval);
            Assert.AreEqual(0, outcome.Report.RemovedBlockCount);
            Assert.AreEqual(0, outcome.Report.EmptiedItemStackCount);
            Assert.AreEqual(0, outcome.Report.RemovedResearchCount);
        }

        [Test]
        public void 除去データのJSONは種別ごとの配列と時刻を持つTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            ((JArray)save["world"]).Add(SaveLoadPreparerTestFixture.MissingBlock(987656, 70));
            var outcome = new MissingMasterPruner().Prune(save);

            var pruned = outcome.ToPrunedJson(new DateTime(2026, 9, 13, 8, 30, 0, DateTimeKind.Utc));

            Assert.AreEqual("2026-09-13T08:30:00Z", pruned["prunedAt"].Value<string>());
            // 後日の置換・返金の入力になるので、壊す前の実体が残っていることまで見る
            // The pruned entity feeds a later replace/refund pass, so the pre-damage value itself is checked
            Assert.AreEqual(1, ((JArray)pruned["blocks"]).Count);
            Assert.AreEqual(MissingGuid, pruned["blocks"][0]["blockGuid"].Value<string>());
            Assert.AreEqual(987656, pruned["blocks"][0]["instanceId"].Value<int>());
            foreach (var emptyKind in new[] { "items", "connectionMaterials", "research", "unlockStates", "trainUnits" })
            {
                Assert.AreEqual(0, ((JArray)pruned[emptyKind]).Count, emptyKind);
            }
        }
    }
}
