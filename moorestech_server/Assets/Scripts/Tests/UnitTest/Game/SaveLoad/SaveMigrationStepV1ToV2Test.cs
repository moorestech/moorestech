using Core.Update;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.UnitTest.Game.SaveLoad
{
    public class SaveMigrationStepV1ToV2Test
    {
        // 版1のブロックstateはJSON文字列で保存されていた。展開しないとロード時に型が合わず落ちる
        // Version 1 stored block state as JSON strings; without expansion the load fails on the type mismatch
        [Test]
        public void stateのJSON文字列がオブジェクトへ展開されるTest()
        {
            var save = JObject.Parse("{\"world\":[{\"state\":{\"machine\":\"{\\\"remain\\\":3}\"}}]}");

            var result = new SaveMigrationStepV1ToV2().Migrate(save);

            Assert.IsTrue(result.IsConverted, result.FailureReason);
            var state = (JObject)result.Save["world"][0]["state"];
            Assert.AreEqual(JTokenType.Object, state["machine"].Type);
            Assert.AreEqual(3, state["machine"]["remain"].Value<int>());
        }

        [Test]
        public void stateが無い要素には空オブジェクトが入るTest()
        {
            var save = JObject.Parse("{\"world\":[{\"blockGuid\":\"x\"}]}");

            var migrated = Convert(save);

            Assert.AreEqual(JTokenType.Object, migrated["world"][0]["state"].Type);
            Assert.AreEqual(0, ((JObject)migrated["world"][0]["state"]).Count);
        }

        // 3項目は版2で必須になった。欠けたままロードするとtickが巻き戻り乱数復元が例外になる
        // The three fields became required in version 2; loading without them rewinds the tick and breaks random restore
        [Test]
        public void 欠損した3項目が補われるTest()
        {
            var save = JObject.Parse("{\"world\":[]}");

            var migrated = Convert(save);

            Assert.AreEqual(0UL, migrated["currentTick"].Value<ulong>());
            Assert.AreEqual(GameRandom.StateFromSeed(0UL), migrated["randomState"].ToObject<ulong[]>());
            Assert.AreEqual(JTokenType.Array, migrated["miningCooldowns"].Type);
            Assert.AreEqual(0, ((JArray)migrated["miningCooldowns"]).Count);
            CollectionAssert.AreEqual(new[] { "currentTick", "randomState", "miningCooldowns" }, migrated["backfilledFields"].ToObject<string[]>());
        }

        // 既に値がある版1セーブを補填で塗り潰すと、進んでいた時刻や乱数列が無音で消える
        // Overwriting values that a version 1 save already has would silently erase its tick and random sequence
        [Test]
        public void 既に値がある項目は上書きされないTest()
        {
            var save = JObject.Parse("{\"world\":[],\"currentTick\":42,\"randomState\":[1,2,3,4],\"miningCooldowns\":[{\"playerId\":1}]}");

            var migrated = Convert(save);

            Assert.AreEqual(42UL, migrated["currentTick"].Value<ulong>());
            Assert.AreEqual(new ulong[] { 1, 2, 3, 4 }, migrated["randomState"].ToObject<ulong[]>());
            Assert.AreEqual(1, ((JArray)migrated["miningCooldowns"]).Count);
            Assert.AreEqual(0, ((JArray)migrated["backfilledFields"]).Count, "補填していない項目が補填済みとして刻まれている");
        }

        // 二重エンコードは1回の展開では文字列のまま残り、移行済みに見えてロード時に落ちる
        // A double-encoded value stays a string after one expansion; it would look migrated yet break the load
        [Test]
        public void 二重エンコードされたstateは変換不能として返るTest()
        {
            var save = JObject.Parse("{\"world\":[{\"state\":{\"machine\":\"\\\"{\\\\\\\"remain\\\\\\\":3}\\\"\"}}]}");

            var result = new SaveMigrationStepV1ToV2().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains("二重エンコード", result.FailureReason);
            Assert.IsNull(result.Save);
        }

        // base64-MessagePackのようにJSONとして読めない値を素通しすると、未変換のまま版2が刻まれる
        // Passing a non-JSON value such as base64 MessagePack would stamp version 2 onto an unconverted save
        [Test]
        public void JSONとして読めないstate値は変換不能として返るTest()
        {
            var save = JObject.Parse("{\"world\":[{\"state\":{\"blob\":\"AAECAw==\"}}]}");

            var result = new SaveMigrationStepV1ToV2().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains("JSONとして読めない", result.FailureReason);
            StringAssert.Contains("AAECAw==", result.FailureReason);
            Assert.IsNull(result.Save);
        }

        // stateが非オブジェクト(文字列等)のセーブは変換できない。とばして通すと未変換のまま版だけ上がる
        // A save whose state is a non-object cannot be converted; skipping it would raise the version on an unconverted save
        [Test]
        public void stateが非オブジェクトの要素は変換不能として返るTest()
        {
            var save = JObject.Parse("{\"world\":[{\"state\":\"not-an-object\"}]}");

            var result = new SaveMigrationStepV1ToV2().Migrate(save);

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains("stateがオブジェクトではない", result.FailureReason);
            Assert.AreEqual("not-an-object", save["world"][0]["state"].Value<string>());
        }

        // world節が壊れているセーブも、変換したことにせず理由つきで返す
        // A save with a broken world node is also returned as unconverted with a reason instead of counting as done
        [Test]
        public void worldが配列でないセーブは変換不能として返るTest()
        {
            var result = new SaveMigrationStepV1ToV2().Migrate(JObject.Parse("{\"world\":\"broken\"}"));

            Assert.IsFalse(result.IsConverted);
            StringAssert.Contains("worldが配列ではない", result.FailureReason);
        }

        [Test]
        public void FromVersionは1であるTest()
        {
            Assert.AreEqual(1, new SaveMigrationStepV1ToV2().FromVersion);
        }

        // 変換成功の分岐だけを短く書くための共通処理。失敗したらその場でテストを落とす
        // Shared shorthand for the converted branch; a failure fails the test right here
        private static JObject Convert(JObject save)
        {
            var result = new SaveMigrationStepV1ToV2().Migrate(save);
            Assert.IsTrue(result.IsConverted, result.FailureReason);
            return result.Save;
        }
    }
}
