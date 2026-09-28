using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.PlayerIdentity;
using Newtonsoft.Json;
using NUnit.Framework;
using Tests.Util.PlayerIdentity;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.UnitTest.Game.PlayerIdentity
{
    public class PlayerIdentityRegistrySaveLoadTest
    {
        [Test]
        public void JSONを経由して既知の身元と候補と次のIDを復元するTest()
        {
            var original = new PlayerIdentityRegistry();
            original.Load(new PlayersSaveJsonObject(5, 3, new List<PlayerIdentityEntryJsonObject>
            {
                new(1, "steam:1"),
                new(2, null),
                new(3, null),
            }));

            // JSONの往復後も既知の接続は候補を消費しない
            // A known connection does not consume the candidate after a JSON round trip
            var json = JsonConvert.SerializeObject(original.GetSaveJsonObject());
            var restored = new PlayerIdentityRegistry();
            restored.Load(JsonConvert.DeserializeObject<PlayersSaveJsonObject>(json));
            Assert.AreEqual(1, PlayerIdentityTestHelper.Register(restored, "steam:1").PlayerId);
            Assert.AreEqual(3, restored.GetSaveJsonObject().ClaimCandidatePlayerId);

            // 最初の未知身元だけが候補を受け取り、次は保存済みの次番号を使う
            // Only the first unknown identity claims the candidate; the next uses the saved next id
            Assert.AreEqual(3, PlayerIdentityTestHelper.Register(restored, "steam:2").PlayerId);
            Assert.AreEqual(5, PlayerIdentityTestHelper.Register(restored, "steam:3").PlayerId);
            Assert.IsNull(restored.GetSaveJsonObject().Entries[1].Identity);
            Assert.IsNull(restored.GetSaveJsonObject().ClaimCandidatePlayerId);
        }

        [TestCase(1)]
        [TestCase(99)]
        public void 持ち主未定でない候補は理由付きで復元を拒否するTest(int candidateId)
        {
            var registry = new PlayerIdentityRegistry();
            var invalid = new PlayersSaveJsonObject(3, candidateId, new List<PlayerIdentityEntryJsonObject>
            {
                new(1, "steam:1"),
                new(2, null),
            });

            LogAssert.Expect(LogType.Error, new Regex("持ち主未定の一覧にありません"));
            Assert.Throws<InvalidOperationException>(() => registry.Load(invalid));
            Assert.IsEmpty(registry.GetSaveJsonObject().Entries);
        }

        [Test]
        public void 新規ワールドの初期化は前の身元と候補を消すTest()
        {
            var registry = new PlayerIdentityRegistry();
            registry.Load(new PlayersSaveJsonObject(3, 2, new List<PlayerIdentityEntryJsonObject>
            {
                new(1, "steam:1"),
                new(2, null),
            }));
            registry.InitializeForNewWorld();

            Assert.IsEmpty(registry.GetSaveJsonObject().Entries);
            Assert.IsEmpty(registry.GetSaveJsonObject().Entries);
            Assert.IsNull(registry.GetSaveJsonObject().ClaimCandidatePlayerId);
            Assert.AreEqual(1, PlayerIdentityTestHelper.Register(registry, "steam:2").PlayerId);
        }

        [TestCase(0, 2)]
        [TestCase(-1, 2)]
        [TestCase(1, 0)]
        [TestCase(1, 1)]
        public void 不正IDや既存ID以下の次番号は復元せずログを出すTest(int playerId, int nextPlayerId)
        {
            var registry = new PlayerIdentityRegistry();
            PlayerIdentityTestHelper.Register(registry, "steam:9");
            var invalid = new PlayersSaveJsonObject(nextPlayerId, null, new List<PlayerIdentityEntryJsonObject>
            {
                new(playerId, "steam:1"),
            });

            // 破損セーブの拒否で現在の身元を失わない
            // Rejecting a corrupt save must not lose the currently registered identity
            LogAssert.Expect(LogType.Error, new Regex("players 節を復元できません"));
            Assert.Throws<InvalidOperationException>(() => registry.Load(invalid));
            Assert.AreEqual("steam:9", registry.GetSaveJsonObject().Entries[0].Identity);
            Assert.AreEqual(1, registry.GetSaveJsonObject().Entries[0].PlayerId);
        }

        [TestCase(1, "steam:2")]
        [TestCase(2, "steam:1")]
        [TestCase(2, "invalid")]
        public void 重複IDや重複身元や不正書式の身元は復元を拒否するTest(int secondId, string secondIdentity)
        {
            var registry = new PlayerIdentityRegistry();
            var invalid = new PlayersSaveJsonObject(3, null, new List<PlayerIdentityEntryJsonObject>
            {
                new(1, "steam:1"),
                new(secondId, secondIdentity),
            });

            LogAssert.Expect(LogType.Error, new Regex("players 節を復元できません"));
            Assert.Throws<InvalidOperationException>(() => registry.Load(invalid));
            Assert.IsEmpty(registry.GetSaveJsonObject().Entries);
        }

        [Test]
        public void 採番上限では負数へ周回せず拒否するTest()
        {
            var registry = new PlayerIdentityRegistry();
            registry.Load(new PlayersSaveJsonObject(int.MaxValue, null, new List<PlayerIdentityEntryJsonObject>()));

            LogAssert.Expect(LogType.Error, new Regex("採番上限"));
            Assert.Throws<InvalidOperationException>(() => registry.PreviewAssignment("steam:1"));
            Assert.IsEmpty(registry.GetSaveJsonObject().Entries);
            Assert.AreEqual(int.MaxValue, registry.GetSaveJsonObject().NextPlayerId);
        }


    }
}
