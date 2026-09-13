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

        // 裸guidの燃料項目を残すとItemMaster.GetItemIdが例外を投げ、そのワールドは永久に起動不能になる
        // Leaving a bare-guid fuel field makes ItemMaster.GetItemId throw and that world can never be loaded again
        [TestCase("currentFuelItemGuid", TestName = "マスタに無い燃料アイテムはnullへ落とされるTest_JsonProperty綴り")]
        [TestCase("CurrentFuelItemGuidStr", TestName = "マスタに無い燃料アイテムはnullへ落とされるTest_フィールド名綴り")]
        public void マスタに無い燃料アイテムはnullへ落とされるTest(string fuelPropertyName)
        {
            var save = BuildSaveJson();
            ((JArray)save["world"]).Add(StateBlock(save, 987658, $"{{\\\"{fuelPropertyName}\\\":\\\"{MissingGuid}\\\"}}"));

            var outcome = new MissingMasterPruner().Prune(save);
            Assert.AreEqual(1, outcome.Report.EmptiedItemStackCount);

            var stateText = outcome.Save["world"].Last["state"]["generator"].Value<string>();
            Assert.AreEqual(JTokenType.Null, JObject.Parse(stateText)[fuelPropertyName].Type);
        }

        // pruned/itemsは後日の置換・返金の入力になるため、裸guid由来も在庫スタック由来もitemGuid/countの両方を持つ必要がある
        // pruned/items feeds a later replace/refund pass, so both bare-guid and inventory-stack entries must carry itemGuid and count
        [Test]
        public void pruned_items内の裸guid由来と在庫スタック由来は同じキーを持つTest()
        {
            var save = BuildSaveJson();
            var mainItems = (JArray)save["playerInventory"][0]["MainInventoryItems"];
            mainItems[0]["itemGuid"] = MissingGuid;
            mainItems[0]["count"] = 5;
            ((JArray)save["world"]).Add(StateBlock(save, 987660, $"{{\\\"currentFuelItemGuid\\\":\\\"{MissingGuid}\\\"}}"));

            var outcome = new MissingMasterPruner().Prune(save);
            var items = (JArray)outcome.ToPrunedJson(DateTime.UtcNow)["items"];

            Assert.AreEqual(2, items.Count);
            var stackEntry = items.Single(item => item["field"] == null);
            var bareGuidEntry = items.Single(item => item["field"] != null);
            Assert.AreEqual(MissingGuid, stackEntry["itemGuid"].Value<string>());
            Assert.AreEqual(5, stackEntry["count"].Value<int>());
            Assert.AreEqual(MissingGuid, bareGuidEntry["itemGuid"].Value<string>());
            Assert.AreEqual(1, bareGuidEntry["count"].Value<int>());
            Assert.AreEqual("currentFuelItemGuid", bareGuidEntry["field"].Value<string>());
        }

        // 接続コスト素材は在庫ではない。空スタックとして数えると「枠を空けた」という通知が嘘になる
        // Connection materials are not inventory; counting them as emptied stacks would make the "slots emptied" notice false
        [Test]
        public void 接続コスト素材は空にされるが空スタック件数には入らないTest()
        {
            var save = BuildSaveJson();
            var connections = $"{{\\\"connections\\\":[{{\\\"targetBlockInstanceId\\\":1,\\\"materials\\\":[{{\\\"itemGuid\\\":\\\"{MissingGuid}\\\",\\\"count\\\":2}}]}}]}}";
            ((JArray)save["world"]).Add(StateBlock(save, 987659, connections));

            var outcome = new MissingMasterPruner().Prune(save);

            Assert.AreEqual(0, outcome.Report.EmptiedItemStackCount);
            Assert.IsTrue(outcome.HasRemoval);
            var stateText = outcome.Save["world"].Last["state"]["generator"].Value<string>();
            Assert.IsFalse(stateText.Contains(MissingGuid));
            Assert.AreEqual(2, outcome.ToPrunedJson(DateTime.UtcNow)["connectionMaterials"][0]["count"].Value<int>());
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
        public void 除去データのJSONは種別ごとの配列と時刻を持つTest()
        {
            var save = BuildSaveJson();
            ((JArray)save["world"]).Add(JObject.Parse($"{{\"blockGuid\":\"{MissingGuid}\",\"direction\":0,\"instanceId\":987656,\"state\":{{}},\"X\":70,\"Y\":0,\"Z\":70}}"));
            var outcome = new MissingMasterPruner().Prune(save);

            var pruned = outcome.ToPrunedJson(new DateTime(2026, 9, 13, 8, 30, 0, DateTimeKind.Utc));

            Assert.AreEqual("2026-09-13T08:30:00Z", pruned["prunedAt"].Value<string>());
            Assert.AreEqual(1, ((JArray)pruned["blocks"]).Count);
            // 後日の置換・返金の入力になるので、壊す前の実体が残っていることまで見る
            // The pruned entity feeds a later replace/refund pass, so the pre-damage value itself is checked
            Assert.AreEqual(MissingGuid, pruned["blocks"][0]["blockGuid"].Value<string>());
            Assert.AreEqual(987656, pruned["blocks"][0]["instanceId"].Value<int>());
            Assert.AreEqual(0, ((JArray)pruned["items"]).Count);
            Assert.AreEqual(0, ((JArray)pruned["connectionMaterials"]).Count);
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
            var placed = ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, Vector3Int.zero, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            Assert.IsTrue(placed, "テストの土台となるチェストの設置に失敗しました");

            // 種付けが効かないと0件系テストが空セーブの検証へ静かに退化するので、土台の中身まで固定する
            // If the seeding stops working the zero-removal tests silently decay into empty-save checks, so pin the seeded content
            var save = JObject.Parse(serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson());
            Assert.Greater(((JArray)save["world"]).Count, 0, "テストの土台のworldが空です");
            Assert.Greater(((JArray)save["playerInventory"]).Count, 0, "テストの土台のplayerInventoryが空です");
            return save;
        }

        // stateにJSON文字列を1つだけ持つブロック。埋め込みJSONの中まで降りる経路を通す
        // A block whose state holds a single JSON string, so the walk goes through the embedded-JSON path
        private static JObject StateBlock(JObject save, int instanceId, string embeddedStateJson)
        {
            return JObject.Parse(
                $"{{\"blockGuid\":\"{FirstBlockGuid(save)}\",\"direction\":0,\"instanceId\":{instanceId},\"state\":{{\"generator\":\"{embeddedStateJson}\"}},\"X\":62,\"Y\":0,\"Z\":62}}");
        }

        private static string FirstBlockGuid(JObject save)
        {
            // フォールバックは置かない。空ならBuildSaveJsonのアサートで既に落ちている
            // No fallback here; an empty world already fails the assert inside BuildSaveJson
            return ((JArray)save["world"])[0]["blockGuid"].Value<string>();
        }
    }
}
