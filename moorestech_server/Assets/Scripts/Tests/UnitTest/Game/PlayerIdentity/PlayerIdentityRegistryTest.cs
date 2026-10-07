using System.Collections.Generic;
using Game.PlayerIdentity;
using NUnit.Framework;
using Tests.Util.PlayerIdentity;

namespace Tests.UnitTest.Game.PlayerIdentity
{
    public class PlayerIdentityRegistryTest
    {
        private const string SteamA = "steam:1";
        private const string SteamB = "steam:2";
        private const string SteamC = "steam:3";

        [Test]
        public void 新しいワールドでは1から連番で払い出すTest()
        {
            var registry = new PlayerIdentityRegistry();
            registry.InitializeForNewWorld();

            Assert.AreEqual(1, PlayerIdentityTestHelper.Register(registry, SteamA).PlayerId);
            Assert.AreEqual(2, PlayerIdentityTestHelper.Register(registry, SteamB).PlayerId);
            Assert.AreEqual(PlayerIdAssignmentKind.NewlyAssigned, PlayerIdentityTestHelper.Register(registry, SteamC).Kind);
        }

        [Test]
        public void 既知の身元には同じIDを返すTest()
        {
            var registry = new PlayerIdentityRegistry();
            registry.InitializeForNewWorld();
            PlayerIdentityTestHelper.Register(registry, SteamA);

            var again = PlayerIdentityTestHelper.Register(registry, SteamA);
            Assert.AreEqual(1, again.PlayerId);
            Assert.AreEqual(PlayerIdAssignmentKind.Known, again.Kind);
            Assert.AreEqual(SteamA, registry.GetSaveJsonObject().Entries[0].Identity);
            Assert.AreEqual(1, registry.GetSaveJsonObject().Entries[0].PlayerId);
            Assert.AreEqual(1, registry.GetSaveJsonObject().Entries.Count);
        }

        [Test]
        public void 欠番は再利用しないTest()
        {
            var registry = new PlayerIdentityRegistry();
            registry.Load(new PlayersSaveJsonObject(5, null, new List<PlayerIdentityEntryJsonObject>
            {
                new(1, SteamA),
            }));

            Assert.AreEqual(5, PlayerIdentityTestHelper.Register(registry, SteamB).PlayerId);
        }

        [Test]
        public void 候補は最初の未知の身元だけに結びつき以後は新規IDTest()
        {
            var registry = new PlayerIdentityRegistry();
            registry.Load(new PlayersSaveJsonObject(3, 2, new List<PlayerIdentityEntryJsonObject>
            {
                new(1, null),
                new(2, null),
            }));

            var first = PlayerIdentityTestHelper.Register(registry, SteamA);
            Assert.AreEqual(2, first.PlayerId);
            Assert.AreEqual(PlayerIdAssignmentKind.ClaimedCandidate, first.Kind);

            var second = PlayerIdentityTestHelper.Register(registry, SteamB);
            Assert.AreEqual(3, second.PlayerId);
            Assert.AreEqual(PlayerIdAssignmentKind.NewlyAssigned, second.Kind);

            var save = registry.GetSaveJsonObject();
            Assert.IsNull(save.ClaimCandidatePlayerId);
            Assert.AreEqual(3, save.Entries.Count);
            Assert.IsNull(save.Entries[0].Identity, "候補でない持ち主未定は残る");
            Assert.AreEqual(SteamA, save.Entries[1].Identity);
        }

        [Test]
        public void セーブ出力はプレイヤーID昇順Test()
        {
            var registry = new PlayerIdentityRegistry();
            registry.Load(new PlayersSaveJsonObject(4, null, new List<PlayerIdentityEntryJsonObject>
            {
                new(3, SteamC),
                new(1, SteamA),
            }));
            PlayerIdentityTestHelper.Register(registry, SteamB);

            var entries = registry.GetSaveJsonObject().Entries;
            CollectionAssert.AreEqual(new[] { 1, 3, 4 }, entries.ConvertAll(e => e.PlayerId));
            Assert.AreEqual(5, registry.GetSaveJsonObject().NextPlayerId);
        }


    }
}
