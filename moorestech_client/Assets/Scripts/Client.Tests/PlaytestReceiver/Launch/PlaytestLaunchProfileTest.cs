using System.Threading;
using Client.Game.InGame.BugReport.Playtest;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Launch;
using Client.PlaytestReceiver.Steam;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestLaunchProfileTest
    {
        [TearDown]
        public void ResetProfile()
        {
            PlaytestLaunchProfile.ResetOnPlayMode();
        }

        // Decideはbuild-infoとSteam起動の2条件だけを見る純関数。片方だけ崩れても開発者モードへ倒れることをmutationで固定する（C4）
        // Decide is a pure function over the two conditions alone; mutating either one still folds to developer mode (C4)
        [Test]
        public void buildinfo有りSteam無しは開発者モード()
        {
            Assert.AreEqual(PlaytestLaunchKind.DeveloperMode, PlaytestLaunchProfile.Decide(true, new SteamRunningFake(false)));
        }

        [Test]
        public void buildinfo無しSteam有りは開発者モード()
        {
            Assert.AreEqual(PlaytestLaunchKind.DeveloperMode, PlaytestLaunchProfile.Decide(false, new SteamRunningFake(true)));
        }

        [Test]
        public void 両方有りで読めたら配布版でSteamIDを識別へ差し込む()
        {
            var kind = PlaytestLaunchProfile.ResolveWith(true, new SteamRunningFake(true), new FakeLocalSteamIdReader("76561198000000001", ""));
            Assert.AreEqual(PlaytestLaunchKind.Distribution, kind);
            Assert.AreEqual("76561198000000001", PlaytestSessionIdentityProvider.Current.SteamId);
        }

        [Test]
        public void 両方有りで読めなければ理由付きの空識別を残す()
        {
            var kind = PlaytestLaunchProfile.ResolveWith(true, new SteamRunningFake(true), new FakeLocalSteamIdReader(null, "fake-unreadable"));
            Assert.AreEqual(PlaytestLaunchKind.Distribution, kind);
            Assert.IsNull(PlaytestSessionIdentityProvider.Current.SteamId);
            var reason = PlaytestSessionIdentityProvider.Current.SteamIdAbsenceReason;
            StringAssert.StartsWith(EmptyPlaytestSessionIdentity.LocalSteamIdUnreadableReason, reason);
            StringAssert.Contains("fake-unreadable", reason);
        }

        [Test]
        public void ResolveWithは一度確定すると再判定しない()
        {
            var first = PlaytestLaunchProfile.ResolveWith(false, new SteamRunningFake(false), new FakeLocalSteamIdReader(null, "unreadable"));
            Assert.AreEqual(PlaytestLaunchKind.DeveloperMode, first);
            Assert.AreEqual(PlaytestLaunchKind.DeveloperMode, PlaytestLaunchProfile.Resolve());
        }

        [Test]
        public void ResetはSteamID識別を取り消す()
        {
            // Editorの再生跨ぎで前回のSteamIDを記録に残さない
            // Never carry the prior SteamID into records from the next Editor play session
            PlaytestLaunchProfile.Apply(PlaytestLaunchKind.Distribution, new LocalSteamSessionIdentity("76561198000000001"));
            PlaytestLaunchProfile.ResetOnPlayMode();
            Assert.IsNull(PlaytestSessionIdentityProvider.Current.SteamId);
        }

        private sealed class SteamRunningFake : IPlaytestSteamTicketProvider
        {
            private readonly bool _steamRunning;
            public SteamRunningFake(bool steamRunning) => _steamRunning = steamRunning;
            public bool IsSteamRunning() => _steamRunning;
            public UniTask<string> RequestWebApiTicketHexAsync(CancellationToken token) => UniTask.FromResult<string>(null);
            public void ReleaseWebApiTicket() { }
        }

        private sealed class FakeLocalSteamIdReader : IPlaytestLocalSteamIdReader
        {
            private readonly string _steamId;
            private readonly string _failureReason;
            public FakeLocalSteamIdReader(string steamId, string failureReason)
            {
                _steamId = steamId;
                _failureReason = failureReason;
            }

            public bool TryRead(out string steamId, out string failureReason)
            {
                steamId = _steamId;
                failureReason = _failureReason;
                return _steamId != null;
            }
        }
    }
}
