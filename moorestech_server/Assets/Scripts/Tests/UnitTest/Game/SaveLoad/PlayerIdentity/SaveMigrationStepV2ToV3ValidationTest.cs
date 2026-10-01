using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad.PlayerIdentity
{
    public class SaveMigrationStepV2ToV3ValidationTest
    {
        [TestCase("{\"playerInventory\":[null]}")]
        [TestCase("{\"playerInventory\":[{\"PlayerId\":\"abc\"}]}")]
        [TestCase("{\"playerInventory\":[{\"PlayerId\":99999999999999999999}]}")]
        [TestCase("{\"playerInventory\":[{\"PlayerId\":0}]}")]
        [TestCase("{\"entities\":[{\"Type\":{},\"InstanceId\":1}]}")]
        [TestCase("{\"playerInventory\":[{\"PlayerId\":7},{\"PlayerId\":7}]}")]
        [TestCase("{\"playerInventory\":[{\"PlayerId\":7,\"MainInventoryItems\":{}}]}")]
        [TestCase("{\"playerInventory\":[{\"PlayerId\":7,\"GrabInventoryItems\":{\"count\":\"bad\"}}]}")]
        [TestCase("{\"playerInventory\":[{\"PlayerId\":7,\"EquipmentInventoryItems\":[{\"count\":-1}]}]}")]
        [TestCase("{\"setting\":{\"SpawnX\":\"bad\"},\"playerInventory\":[{\"PlayerId\":7}]}")]
        [TestCase("{\"entities\":[{\"Type\":\"va:Player\",\"InstanceId\":7,\"X\":{}}]}")]
        public void 壊れたJSONは例外を投げず原本を変えずに拒否するTest(string json)
        {
            var save = JObject.Parse(json);
            var original = save.DeepClone();

            var result = new SaveMigrationStepV2ToV3().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            Assert.IsNotEmpty(result.FailureReason);
            Assert.IsTrue(JToken.DeepEquals(original, save), "拒否前にIDが書き換わっている");
        }

        [Test]
        public void 参照だけのプレイヤーも振り直しプレイヤー以外のエンティティを保持するTest()
        {
            var save = JObject.Parse(@"{
                'constructionPayers':[{'PlayerId':90},{'PlayerId':90}],
                'miningCooldowns':[{'playerId':80}],
                'entities':[{'Type':'va:Item','InstanceId':90,'X':1}]
            }");
            var entity = save["entities"].DeepClone();

            var result = new SaveMigrationStepV2ToV3().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(2, (int)save["constructionPayers"][0]["PlayerId"]);
            Assert.AreEqual(2, (int)save["constructionPayers"][1]["PlayerId"]);
            Assert.AreEqual(1, (int)save["miningCooldowns"][0]["playerId"]);
            Assert.AreEqual(1, (int)save["players"]["claimCandidatePlayerId"]);
            Assert.IsTrue(JToken.DeepEquals(entity, save["entities"]));
        }

        [Test]
        public void 高さの距離も持ち物同数の候補選択に使うTest()
        {
            var save = JObject.Parse(@"{
                'setting':{'SpawnX':0,'SpawnY':10,'SpawnZ':0},
                'entities':[
                    {'Type':'va:Player','InstanceId':10,'X':0,'Y':10,'Z':0},
                    {'Type':'va:Player','InstanceId':20,'X':0,'Y':20,'Z':0}]
            }");

            var result = new SaveMigrationStepV2ToV3().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(2, (int)save["players"]["claimCandidatePlayerId"]);
        }

        [Test]
        public void 候補の持ち物総数にはつかみ中と装備を含むTest()
        {
            var save = JObject.Parse(@"{
                'playerInventory':[
                    {'PlayerId':10,'MainInventoryItems':[{'count':5}]},
                    {'PlayerId':20,'GrabInventoryItems':{'count':3},'EquipmentInventoryItems':[{'count':3}]}]
            }");

            var result = new SaveMigrationStepV2ToV3().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            Assert.AreEqual(2, (int)save["players"]["claimCandidatePlayerId"]);
        }
    }
}
