using System;
using System.Linq;
using Game.Block.Blocks.Chest;
using Game.SaveLoad.Pruning;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.CombinedTest.Game.MissingMasterPrune
{
    /// <summary>除去したアイテムが持ち主・格納先・スロットつきで退避されることを見る。後日の返金を元の場所へ戻す入力になる</summary>
    /// <summary>Checks that pruned items are archived with owner, container and slot; this is the input for returning a later refund to its place</summary>
    public class MissingMasterItemPruneOriginTest
    {
        private const string MissingGuid = SaveLoadPreparerTestFixture.MissingGuid;

        [Test]
        public void プレイヤー所持品はプレイヤーIDと格納先とスロットつきで退避されるTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            var player = (JObject)save["playerInventory"][0];
            player["MainInventoryItems"][2]["itemGuid"] = MissingGuid;
            player["MainInventoryItems"][2]["count"] = 5;

            var outcome = new MissingMasterPruner().Prune(save);

            Assert.AreEqual(1, outcome.Report.EmptiedItemStackCount);
            Assert.AreEqual(Guid.Empty.ToString(), outcome.Save["playerInventory"][0]["MainInventoryItems"][2]["itemGuid"].Value<string>());
            Assert.AreEqual(0, outcome.Save["playerInventory"][0]["MainInventoryItems"][2]["count"].Value<int>());

            var item = SinglePrunedItem(outcome);
            Assert.AreEqual("itemStack", item["kind"].Value<string>());
            Assert.AreEqual(MissingGuid, item["itemGuid"].Value<string>());
            Assert.AreEqual(5, item["count"].Value<int>());
            Assert.AreEqual("playerInventory", item["origin"]["section"].Value<string>());
            Assert.AreEqual(player["PlayerId"].Value<int>(), item["origin"]["playerId"].Value<int>());
            Assert.AreEqual("MainInventoryItems", item["origin"]["container"].Value<string>());
            Assert.AreEqual(2, item["origin"]["slot"].Value<int>());
        }

        // 実DIのチェストのstateはSaveKeyの下にスタック配列を直接持つ。住所はブロックIDとSaveKeyとスロット
        // A real chest's state holds the stack array directly under its SaveKey; the address is block id, SaveKey and slot
        [Test]
        public void ブロックstate内のアイテムはブロックIDとstateキーとスロットつきで退避されるTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            var chestKey = typeof(VanillaChestComponent).FullName;
            var chestBlock = ((JArray)save["world"]).OfType<JObject>().First(block => block["state"]?[chestKey] != null);
            var chestSlots = (JArray)chestBlock["state"][chestKey];
            Assert.Greater(chestSlots.Count, 1, "テストの土台のチェストのスロットが足りません");
            chestSlots[1]["itemGuid"] = MissingGuid;
            chestSlots[1]["count"] = 3;

            var outcome = new MissingMasterPruner().Prune(save);

            var item = SinglePrunedItem(outcome);
            Assert.AreEqual(3, item["count"].Value<int>());
            Assert.AreEqual("world", item["origin"]["section"].Value<string>());
            Assert.AreEqual(chestBlock["instanceId"].Value<int>(), item["origin"]["blockInstanceId"].Value<int>());
            Assert.AreEqual(chestKey, item["origin"]["stateKey"].Value<string>());
            Assert.AreEqual(1, item["origin"]["slot"].Value<int>());
            Assert.IsNull(item["origin"]["playerId"]);
        }

        // 文字列に埋め込まれたJSON（ベルトコンベア等）でも、外側の添字をスロットとしてブロックIDつきで残す
        // JSON embedded in strings (belt conveyors and the like) still records the outer index as the slot along with the block id
        [Test]
        public void 埋め込みJSON文字列内のアイテムは外側のスロットとブロックIDを持つTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            var beltItem = new JObject { ["itemStack"] = new JObject { ["itemGuid"] = MissingGuid, ["count"] = 1 } }.ToString(Newtonsoft.Json.Formatting.None);
            ((JArray)save["world"]).Add(StateBlock(save, 987670, new JArray("{}", beltItem)));

            var outcome = new MissingMasterPruner().Prune(save);

            var item = SinglePrunedItem(outcome);
            Assert.AreEqual(987670, item["origin"]["blockInstanceId"].Value<int>());
            Assert.AreEqual("belt", item["origin"]["stateKey"].Value<string>());
            Assert.AreEqual(1, item["origin"]["slot"].Value<int>());
            Assert.AreEqual("itemStack", item["origin"]["container"].Value<string>());
            StringAssert.DoesNotContain(MissingGuid, outcome.Save["world"].Last["state"]["belt"][1].Value<string>());
        }

        // 裸guidの燃料項目を残すとItemMaster.GetItemIdが例外を投げ、そのワールドは永久に起動不能になる
        // Leaving a bare-guid fuel field makes ItemMaster.GetItemId throw and that world can never be loaded again
        [TestCase("currentFuelItemGuid", TestName = "燃料の裸guidはnullへ落とされ個数を捏造せず項目名つきで退避されるTest_JsonProperty綴り")]
        [TestCase("CurrentFuelItemGuidStr", TestName = "燃料の裸guidはnullへ落とされ個数を捏造せず項目名つきで退避されるTest_フィールド名綴り")]
        public void 燃料の裸guidはnullへ落とされ個数を捏造せず項目名つきで退避されるTest(string fuelPropertyName)
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            ((JArray)save["world"]).Add(StateBlock(save, 987658, new JObject { [fuelPropertyName] = MissingGuid }));

            var outcome = new MissingMasterPruner().Prune(save);

            Assert.AreEqual(1, outcome.Report.EmptiedItemStackCount);
            Assert.AreEqual(JTokenType.Null, outcome.Save["world"].Last["state"]["belt"][fuelPropertyName].Type);
            var item = SinglePrunedItem(outcome);
            Assert.AreEqual("bareGuid", item["kind"].Value<string>());
            Assert.AreEqual(MissingGuid, item["itemGuid"].Value<string>());
            Assert.IsNull(item["count"], "裸guidに個数を捏造してはいけません");
            Assert.AreEqual(fuelPropertyName, item["origin"]["field"].Value<string>());
            Assert.AreEqual(987658, item["origin"]["blockInstanceId"].Value<int>());
        }

        // 接続コスト素材は在庫ではない。空スタックとして数えると「取り除いたアイテム」の通知が嘘になる
        // Connection materials are not inventory; counting them as emptied stacks would make the "items removed" notice false
        [Test]
        public void 接続コスト素材は住所つきで別配列に退避され空スタック件数に入らないTest()
        {
            var save = SaveLoadPreparerTestFixture.BuildSaveJson();
            var connections = JObject.Parse($"{{\"connections\":[{{\"targetBlockInstanceId\":1,\"materials\":[{{\"itemGuid\":\"{MissingGuid}\",\"count\":2}}]}}]}}");
            ((JArray)save["world"]).Add(StateBlock(save, 987659, connections));

            var outcome = new MissingMasterPruner().Prune(save);

            Assert.AreEqual(0, outcome.Report.EmptiedItemStackCount);
            Assert.IsTrue(outcome.HasRemoval);
            StringAssert.DoesNotContain(MissingGuid, outcome.Save["world"].Last["state"].ToString());
            var material = (JObject)outcome.ToPrunedJson(DateTime.UtcNow)["connectionMaterials"].Single();
            Assert.AreEqual(2, material["count"].Value<int>());
            Assert.AreEqual(987659, material["origin"]["blockInstanceId"].Value<int>());
            Assert.AreEqual("materials", material["origin"]["container"].Value<string>());
            Assert.AreEqual(0, material["origin"]["slot"].Value<int>());
        }

        private static JObject SinglePrunedItem(MissingMasterPruneOutcome outcome)
        {
            var items = (JArray)outcome.ToPrunedJson(DateTime.UtcNow)["items"];
            Assert.AreEqual(1, items.Count);
            return (JObject)items[0];
        }

        // 実在するブロックに、stateキー"belt"で任意の中身を持たせる。walkerの経路だけを通す
        // A real block carrying arbitrary contents under the state key "belt", driving only the walker path
        private static JObject StateBlock(JObject save, int instanceId, JToken stateContents)
        {
            var block = (JObject)save["world"][0].DeepClone();
            block["instanceId"] = instanceId;
            block["state"] = new JObject { ["belt"] = stateContents };
            return block;
        }
    }
}
