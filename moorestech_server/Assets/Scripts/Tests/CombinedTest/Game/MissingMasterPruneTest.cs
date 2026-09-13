using System;
using System.Linq;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.SaveLoad.Json;
using Game.SaveLoad.Pruning;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Game
{
    public class MissingMasterPruneTest
    {
        // マスタに絶対に無いguid。テストmodのguidと衝突しない固定値を使う
        // A guid guaranteed absent from any master; a fixed value that cannot collide with the test mod
        private const string MissingGuid = "ffffffff-ffff-ffff-ffff-ffffffffffff";

        [Test]
        public void マスタに無いブロックはセーブから除去されるTest()
        {
            var save = BuildSaveJson();
            ((JArray)save["world"]).Add(JObject.Parse($"{{\"blockGuid\":\"{MissingGuid}\",\"direction\":0,\"instanceId\":987654,\"state\":{{}},\"X\":50,\"Y\":0,\"Z\":50}}"));
            var worldCountBefore = ((JArray)save["world"]).Count;

            var outcome = new MissingMasterPruner().Prune(save);

            Assert.AreEqual(1, outcome.Report.RemovedBlockCount);
            Assert.AreEqual(worldCountBefore - 1, ((JArray)outcome.Save["world"]).Count);
            Assert.IsFalse(outcome.Save.ToString().Contains(MissingGuid));
        }

        [Test]
        public void マスタに無いアイテムは空スタックへ落とされるTest()
        {
            var save = BuildSaveJson();
            var mainItems = (JArray)save["playerInventory"][0]["MainInventoryItems"];
            mainItems[0]["itemGuid"] = MissingGuid;
            mainItems[0]["count"] = 5;

            var outcome = new MissingMasterPruner().Prune(save);

            Assert.AreEqual(1, outcome.Report.EmptiedItemStackCount);
            var pruned = (JArray)outcome.Save["playerInventory"][0]["MainInventoryItems"];
            Assert.AreEqual(Guid.Empty.ToString(), pruned[0]["itemGuid"].Value<string>());
            Assert.AreEqual(0, pruned[0]["count"].Value<int>());
        }

        // チェスト等の中身はworld[].stateのJSON文字列の中にある。ここを見ないとロードで例外が出る
        // Chest contents live inside the JSON string in world[].state; skipping it would still throw at load
        [Test]
        public void ブロック内部stateのアイテムも空スタックへ落とされるTest()
        {
            var save = BuildSaveJson();
            ((JArray)save["world"]).Add(JObject.Parse(
                $"{{\"blockGuid\":\"{FirstBlockGuid(save)}\",\"direction\":0,\"instanceId\":987655,\"state\":{{\"inventory\":\"{{\\\"items\\\":[{{\\\"itemGuid\\\":\\\"{MissingGuid}\\\",\\\"count\\\":3}}]}}\"}},\"X\":60,\"Y\":0,\"Z\":60}}"));

            var outcome = new MissingMasterPruner().Prune(save);

            Assert.AreEqual(1, outcome.Report.EmptiedItemStackCount);
            var stateText = outcome.Save["world"].Last["state"]["inventory"].Value<string>();
            StringAssert.Contains(Guid.Empty.ToString(), stateText);
            Assert.IsFalse(stateText.Contains(MissingGuid));
        }

        // base64-MessagePackの状態値（例 RailComponentStateDetail）は素通しする。壊すと復元できない
        // Base64 MessagePack state values (e.g. RailComponentStateDetail) pass through untouched; corrupting them is unrecoverable
        [Test]
        public void JSONで無い状態値は素通しされるTest()
        {
            var save = BuildSaveJson();
            ((JArray)save["world"]).Add(JObject.Parse(
                $"{{\"blockGuid\":\"{FirstBlockGuid(save)}\",\"direction\":0,\"instanceId\":987657,\"state\":{{\"rail\":\"kZPAAAA=\"}},\"X\":61,\"Y\":0,\"Z\":61}}"));

            var outcome = new MissingMasterPruner().Prune(save);

            Assert.AreEqual(0, outcome.Report.EmptiedItemStackCount);
            Assert.AreEqual("kZPAAAA=", outcome.Save["world"].Last["state"]["rail"].Value<string>());
        }

        [Test]
        public void マスタに無い研究ノードは完了一覧から除去されるTest()
        {
            var save = BuildSaveJson();
            save["research"] = JObject.Parse($"{{\"CompletedResearchGuids\":[\"{MissingGuid}\"]}}");

            var outcome = new MissingMasterPruner().Prune(save);

            Assert.AreEqual(1, outcome.Report.RemovedResearchCount);
            Assert.AreEqual(0, ((JArray)outcome.Save["research"]["CompletedResearchGuids"]).Count);
        }

        // 除去0件のセーブでレポートが立たないこと（毎回ロードで走るので最小構成が本番の常態）
        // A save with nothing to prune must not raise the report; this is the normal case on every load
        [Test]
        public void 除去が無いセーブではレポートが立たないTest()
        {
            var outcome = new MissingMasterPruner().Prune(BuildSaveJson());

            Assert.IsFalse(outcome.Report.HasRemoval);
            Assert.AreEqual(0, outcome.Report.RemovedBlockCount);
            Assert.AreEqual(0, outcome.Report.EmptiedItemStackCount);
            Assert.AreEqual(0, outcome.Report.RemovedResearchCount);
        }

        [Test]
        public void 除去データのJSONは3つの配列と時刻を持つTest()
        {
            var save = BuildSaveJson();
            ((JArray)save["world"]).Add(JObject.Parse($"{{\"blockGuid\":\"{MissingGuid}\",\"direction\":0,\"instanceId\":987656,\"state\":{{}},\"X\":70,\"Y\":0,\"Z\":70}}"));
            var outcome = new MissingMasterPruner().Prune(save);

            var pruned = outcome.ToPrunedJson(new DateTime(2026, 9, 13, 8, 30, 0, DateTimeKind.Utc));

            Assert.AreEqual("2026-09-13T08:30:00Z", pruned["prunedAt"].Value<string>());
            Assert.AreEqual(1, ((JArray)pruned["blocks"]).Count);
            Assert.AreEqual(0, ((JArray)pruned["items"]).Count);
            Assert.AreEqual(0, ((JArray)pruned["research"]).Count);
        }

        // 実DIで作った本物のセーブを土台にする。手書きJSONだと形の食い違いに気づけない
        // Build on a real save from the DI container; a hand-written JSON would hide shape drift
        // 何も配置しない直後のDIはplayerInventory/worldが空になるため、プレイヤーとブロックを1つ用意する
        // A fresh DI has empty playerInventory/world until something is placed, so seed one player and one block
        private static JObject BuildSaveJson()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(1);
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            return JObject.Parse(serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson());
        }

        private static string FirstBlockGuid(JObject save)
        {
            var world = (JArray)save["world"];
            return world.Count > 0 ? world[0]["blockGuid"].Value<string>() : MissingGuid;
        }
    }
}
