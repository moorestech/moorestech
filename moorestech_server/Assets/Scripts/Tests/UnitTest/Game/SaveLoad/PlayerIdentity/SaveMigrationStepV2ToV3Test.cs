using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad.PlayerIdentity
{
    public class SaveMigrationStepV2ToV3Test
    {
        // テスターのセーブ相当: 幽霊1（装備のみ・スポーン）と本人1623179277（原木612）
        // Tester-like save: ghost 1 (equipment only, at spawn) and the owner 1623179277 (612 logs)
        private const string TesterLikeSave = @"{
          ""setting"": {""SpawnX"":500,""SpawnY"":13.4,""SpawnZ"":500},
          ""playerInventory"": [
            {""PlayerId"":1,""MainInventoryItems"":[],""GrabInventoryItems"":{""itemGuid"":""00000000-0000-0000-0000-000000000000"",""count"":0},""EquipmentInventoryItems"":[{""itemGuid"":""4c5fefbd-60a4-42ea-b70a-38a83b96e25e"",""count"":1}],""SelectedEquipmentIndex"":0},
            {""PlayerId"":1623179277,""MainInventoryItems"":[{""itemGuid"":""aafce615-6c30-48c4-a29e-3c5b3266748f"",""count"":612}],""GrabInventoryItems"":{""itemGuid"":""00000000-0000-0000-0000-000000000000"",""count"":0},""EquipmentInventoryItems"":[{""itemGuid"":""4c5fefbd-60a4-42ea-b70a-38a83b96e25e"",""count"":1}],""SelectedEquipmentIndex"":0}
          ],
          ""entities"": [
            {""InstanceId"":1,""Type"":""va:Player"",""X"":500,""Y"":13.4,""Z"":500},
            {""InstanceId"":1623179277,""Type"":""va:Player"",""X"":485,""Y"":13.4,""Z"":442}
          ],
          ""playerRidingStates"": [{""PlayerId"":1623179277,""RidableType"":""train"",""IdentifierState"":""{}"",""SeatIndex"":0}],
          ""hotbarAssignments"": [{""PlayerId"":1623179277,""Assignments"":[]}],
          ""remainingPlacementCounts"": [{""PlayerId"":1,""Entries"":[]}],
          ""constructionPayers"": [{""BlockInstanceId"":7,""PlayerId"":1623179277}],
          ""miningCooldowns"": [{""playerId"":1623179277,""lastAttackTick"":10}]
        }";

        [Test]
        public void 旧IDは昇順で1からの連番に振り直され全節が書き換わるTest()
        {
            var save = Convert(JObject.Parse(TesterLikeSave));

            Assert.AreEqual(1, (int)save["playerInventory"][0]["PlayerId"]);
            Assert.AreEqual(2, (int)save["playerInventory"][1]["PlayerId"]);
            Assert.AreEqual(2L, (long)save["entities"][1]["InstanceId"]);
            Assert.AreEqual(2, (int)save["playerRidingStates"][0]["PlayerId"]);
            Assert.AreEqual(2, (int)save["hotbarAssignments"][0]["PlayerId"]);
            Assert.AreEqual(1, (int)save["remainingPlacementCounts"][0]["PlayerId"]);
            Assert.AreEqual(2, (int)save["constructionPayers"][0]["PlayerId"]);
            Assert.AreEqual(2, (int)save["miningCooldowns"][0]["playerId"]);
        }

        [Test]
        public void 全員持ち主未定で持ち物総数最大が候補になるTest()
        {
            var players = Convert(JObject.Parse(TesterLikeSave))["players"];

            Assert.AreEqual(3, (int)players["nextPlayerId"]);
            Assert.AreEqual(2, (int)players["claimCandidatePlayerId"]);
            Assert.AreEqual(2, ((JArray)players["entries"]).Count);
            foreach (var entry in (JArray)players["entries"]) Assert.AreEqual(JTokenType.Null, entry["identity"].Type);
        }

        [Test]
        public void 持ち物数が多ければスポーン上でも遠いプレイヤーより優先するTest()
        {
            var save = JObject.Parse(TesterLikeSave);
            save["playerInventory"][0]["MainInventoryItems"] = new JArray(new JObject { ["itemGuid"] = "aafce615-6c30-48c4-a29e-3c5b3266748f", ["count"] = 613 });

            Assert.AreEqual(1, (int)Convert(save)["players"]["claimCandidatePlayerId"]);
        }

        [Test]
        public void 持ち物が同数ならスポーンから遠い方が候補Test()
        {
            var save = JObject.Parse(TesterLikeSave);
            save["playerInventory"][1]["MainInventoryItems"] = new JArray();
            var players = Convert(save)["players"];

            Assert.AreEqual(2, (int)players["claimCandidatePlayerId"], "同数(装備1)なら遠い旧1623179277");
        }

        [Test]
        public void 持ち物も距離も同じならIDが小さい方が候補Test()
        {
            var save = JObject.Parse(TesterLikeSave);
            save["playerInventory"][1]["MainInventoryItems"] = new JArray();
            save["entities"][1]["X"] = 500;
            save["entities"][1]["Z"] = 500;

            Assert.AreEqual(1, (int)Convert(save)["players"]["claimCandidatePlayerId"]);
        }

        [Test]
        public void プレイヤーが居ないセーブは空の表で候補無しTest()
        {
            var players = Convert(JObject.Parse(@"{""setting"":{""SpawnX"":0,""SpawnY"":0,""SpawnZ"":0},""playerInventory"":[],""entities"":[]}"))["players"];

            Assert.AreEqual(1, (int)players["nextPlayerId"]);
            Assert.AreEqual(JTokenType.Null, players["claimCandidatePlayerId"].Type);
            Assert.AreEqual(0, ((JArray)players["entries"]).Count);
        }

        [Test]
        public void 既にplayers節があるセーブは変換不能として返るTest()
        {
            var save = JObject.Parse(TesterLikeSave);
            save["players"] = new JObject();
            var result = new SaveMigrationStepV2ToV3().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains("players", result.FailureReason);
        }

        [Test]
        public void 節が配列でないセーブは変換不能として返るTest()
        {
            var save = JObject.Parse(TesterLikeSave);
            save["hotbarAssignments"] = new JObject();

            Assert.IsFalse(new SaveMigrationStepV2ToV3().Migrate(save).IsConverted);
        }

        [Test]
        public void FromVersionは2であるTest()
        {
            Assert.AreEqual(2, new SaveMigrationStepV2ToV3().FromVersion);
        }

        private static JObject Convert(JObject save)
        {
            var result = new SaveMigrationStepV2ToV3().Migrate(save);
            Assert.IsTrue(result.IsConverted, result.FailureReason);
            return result.Save;
        }
    }
}
