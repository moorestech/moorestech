using Client.Game.InGame.BugReport.Playtest;
using Client.PlaytestReceiver.Launch;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestLaunchProfileTest
    {
        [TearDown]
        public void ResetProfile()
        {
            PlaytestLaunchProfile.ResetForTest();
        }

        [Test]
        public void EditorWithoutBuildInfoResolvesToDeveloperModeAndLeavesTheIdentityEmpty()
        {
            // 配布版の印が無いEditorでは開発者モードの理由を残す
            // An Editor without the distribution marker retains the developer-mode reason
            PlaytestLaunchProfile.ResetForTest();
            Assert.AreEqual(PlaytestLaunchKind.DeveloperMode, PlaytestLaunchProfile.Resolve());
            Assert.AreEqual(EmptyPlaytestSessionIdentity.DeveloperModeReason, PlaytestSessionIdentityProvider.Current.SteamIdAbsenceReason);
        }

        [Test]
        public void DistributionWithLocalSteamIdSetsTheIdentity()
        {
            PlaytestLaunchProfile.SetForTest(PlaytestLaunchKind.Distribution, "76561198000000001");
            Assert.AreEqual(PlaytestLaunchKind.Distribution, PlaytestLaunchProfile.Resolve());
            Assert.AreEqual("76561198000000001", PlaytestSessionIdentityProvider.Current.SteamId);
        }

        [Test]
        public void DistributionWithoutReadableSteamIdLeavesAReasonedEmptyIdentity()
        {
            PlaytestLaunchProfile.SetForTest(PlaytestLaunchKind.Distribution, "");
            Assert.AreEqual(PlaytestLaunchKind.Distribution, PlaytestLaunchProfile.Resolve());
            Assert.IsNull(PlaytestSessionIdentityProvider.Current.SteamId);
            StringAssert.Contains("SteamUser.GetSteamID", PlaytestSessionIdentityProvider.Current.SteamIdAbsenceReason);
        }

        [Test]
        public void ResetRemovesThePreviousLaunchIdentity()
        {
            // Editorの再生跨ぎで前回のSteamIDを記録に残さない
            // Never carry the prior SteamID into records from the next Editor play session
            PlaytestLaunchProfile.SetForTest(PlaytestLaunchKind.Distribution, "76561198000000001");
            PlaytestLaunchProfile.ResetForTest();
            Assert.IsNull(PlaytestSessionIdentityProvider.Current.SteamId);
        }
    }
}
