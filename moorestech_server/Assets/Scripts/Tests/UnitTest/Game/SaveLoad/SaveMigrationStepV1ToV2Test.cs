using System;
using System.Text.RegularExpressions;
using Core.Update;
using Game.SaveLoad.Migration.Steps;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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

            var migrated = new SaveMigrationStepV1ToV2().Migrate(save);

            var state = (JObject)migrated["world"][0]["state"];
            Assert.AreEqual(JTokenType.Object, state["machine"].Type);
            Assert.AreEqual(3, state["machine"]["remain"].Value<int>());
        }

        [Test]
        public void stateが無い要素には空オブジェクトが入るTest()
        {
            var save = JObject.Parse("{\"world\":[{\"blockGuid\":\"x\"}]}");

            var migrated = new SaveMigrationStepV1ToV2().Migrate(save);

            Assert.AreEqual(JTokenType.Object, migrated["world"][0]["state"].Type);
            Assert.AreEqual(0, ((JObject)migrated["world"][0]["state"]).Count);
        }

        // 3項目は版2で必須になった。欠けたままロードするとtickが巻き戻り乱数復元が例外になる
        // The three fields became required in version 2; loading without them rewinds the tick and breaks random restore
        [Test]
        public void 欠損した3項目が補われるTest()
        {
            var save = JObject.Parse("{\"world\":[]}");

            var migrated = new SaveMigrationStepV1ToV2().Migrate(save);

            Assert.AreEqual(0UL, migrated["currentTick"].Value<ulong>());
            Assert.AreEqual(GameRandom.StateFromSeed(0UL), migrated["randomState"].ToObject<ulong[]>());
            Assert.AreEqual(JTokenType.Array, migrated["miningCooldowns"].Type);
            Assert.AreEqual(0, ((JArray)migrated["miningCooldowns"]).Count);
        }

        // 既に値がある版1セーブを補填で塗り潰すと、進んでいた時刻や乱数列が無音で消える
        // Overwriting values that a version 1 save already has would silently erase its tick and random sequence
        [Test]
        public void 既に値がある項目は上書きされないTest()
        {
            var save = JObject.Parse("{\"world\":[],\"currentTick\":42,\"randomState\":[1,2,3,4],\"miningCooldowns\":[{\"playerId\":1}]}");

            var migrated = new SaveMigrationStepV1ToV2().Migrate(save);

            Assert.AreEqual(42UL, migrated["currentTick"].Value<ulong>());
            Assert.AreEqual(new ulong[] { 1, 2, 3, 4 }, migrated["randomState"].ToObject<ulong[]>());
            Assert.AreEqual(1, ((JArray)migrated["miningCooldowns"]).Count);
        }

        // 二重エンコードは1回の展開では文字列のまま残り、移行済みに見えてロード時に落ちる
        // A double-encoded value stays a string after one expansion; it would look migrated yet break the load
        [Test]
        public void 二重エンコードされたstateは例外になるTest()
        {
            var save = JObject.Parse("{\"world\":[{\"state\":{\"machine\":\"\\\"{\\\\\\\"remain\\\\\\\":3}\\\"\"}}]}");

            // 理由は開発者が読めるログにも出す設計なので、期待するエラーログとして受け取る
            // The reason is also written to a developer-readable log by design, so the expected error log is consumed here
            LogAssert.Expect(LogType.Error, new Regex("二重エンコード"));
            Assert.Throws<InvalidOperationException>(() => new SaveMigrationStepV1ToV2().Migrate(save));
        }

        // base64-MessagePackのようにJSONとして読めない値は、壊さずそのまま残す（理由はログへ）
        // A value that is not JSON, such as base64 MessagePack, is left untouched and the reason is logged
        [Test]
        public void JSONとして読めないstate値はそのまま残るTest()
        {
            var save = JObject.Parse("{\"world\":[{\"state\":{\"blob\":\"AAECAw==\"}}]}");

            var migrated = new SaveMigrationStepV1ToV2().Migrate(save);

            Assert.AreEqual("AAECAw==", migrated["world"][0]["state"]["blob"].Value<string>());
        }

        // stateが非オブジェクト(文字列等)のとき、無音で{}へ潰すと元データが破棄されてしまう
        // If a non-object state were silently collapsed to {}, the original data would be discarded
        [Test]
        public void stateが非オブジェクトの要素は元の値を残したまま展開をとばすTest()
        {
            var save = JObject.Parse("{\"world\":[{\"state\":\"not-an-object\"}]}");

            LogAssert.Expect(LogType.Error, new Regex("stateがオブジェクトではありません"));
            var migrated = new SaveMigrationStepV1ToV2().Migrate(save);

            Assert.AreEqual("not-an-object", migrated["world"][0]["state"].Value<string>());
        }

        [Test]
        public void FromVersionは1であるTest()
        {
            Assert.AreEqual(1, new SaveMigrationStepV1ToV2().FromVersion);
        }
    }
}
