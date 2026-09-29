using Client.PlaytestReceiver.Steam;
using Client.Game.InGame.BugReport.BuildOrigin;
using Client.Starter.Identity;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Starter.Identity
{
    public class LocalPlayerIdentityResolverTest
    {
        [Test]
        public void ビルド情報の無い実行ファイルは端末値へ落とさず拒否Test()
        {
            var origin = BuildOriginReading.WithoutInfo("build-info.json missing");
            var result = LocalPlayerIdentityResolver.ResolveForBuildOrigin(origin, new FakeSteamReader("1"), "device-value");
            Assert.IsTrue(result.Refusal.HasValue);
            Assert.AreEqual(LocalizationKeys.Ui.Loading.BuildOriginUnavailable.Key, result.Refusal.Value.Key.Key);
            StringAssert.Contains("build-info.json missing", result.Refusal.Value.LogReason);
        }

        private sealed class FakeSteamReader : IPlaytestLocalSteamIdReader
        {
            private readonly string _steamId;
            public FakeSteamReader(string steamId)
            {
                _steamId = steamId;
            }

            public bool TryRead(out string steamId, out string failureReason)
            {
                steamId = _steamId;
                failureReason = _steamId == null ? "no steam" : null;
                return _steamId != null;
            }
        }

        [Test]
        public void Steam配布ビルドはsteam身元になるTest()
        {
            var result = LocalPlayerIdentityResolver.Resolve(PlayerIdentitySource.SteamDistribution, new FakeSteamReader("76561198319362448"), "abc");
            Assert.IsFalse(result.Refusal.HasValue);
            Assert.AreEqual("steam:76561198319362448", result.Identity);
        }

        [Test]
        public void Steam配布ビルドでSteamIDが読めなければ端末値へ落ちず拒否Test()
        {
            var result = LocalPlayerIdentityResolver.Resolve(PlayerIdentitySource.SteamDistribution, new FakeSteamReader(null), "abc");
            Assert.IsTrue(result.Refusal.HasValue);
            Assert.AreEqual(LocalizationKeys.Ui.Loading.SteamIdentityUnavailable.Key, result.Refusal.Value.Key.Key);
        }

        [Test]
        public void それ以外は端末値のSHA256小文字16進になるTest()
        {
            var result = LocalPlayerIdentityResolver.Resolve(PlayerIdentitySource.Device, new FakeSteamReader("1"), "abc");
            Assert.IsFalse(result.Refusal.HasValue);
            Assert.AreEqual("device:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", result.Identity);
        }

        [Test]
        public void 未対応端末識別子は拒否Test()
        {
            var result = LocalPlayerIdentityResolver.Resolve(PlayerIdentitySource.Device, new FakeSteamReader("1"), SystemInfo.unsupportedIdentifier);
            Assert.IsTrue(result.Refusal.HasValue);
            Assert.AreEqual(LocalizationKeys.Ui.Loading.DeviceIdentityUnavailable.Key, result.Refusal.Value.Key.Key);
        }

        [TestCase("")]
        [TestCase(null)]
        public void 端末値が取れなければ拒否Test(string device)
        {
            var result = LocalPlayerIdentityResolver.Resolve(PlayerIdentitySource.Device, new FakeSteamReader("1"), device);
            Assert.IsTrue(result.Refusal.HasValue);
            Assert.AreEqual(LocalizationKeys.Ui.Loading.DeviceIdentityUnavailable.Key, result.Refusal.Value.Key.Key);
        }

        // 配布判定そのものを通す経路で検査する。Resolve直呼びだけでは分岐の入れ替えが検出できない
        // Exercise the path that decides the distribution kind; calling Resolve directly cannot catch a flipped branch
        [Test]
        public void Steamラベル付きの焼き込みビルドはsteam身元になるTest()
        {
            var origin = BuildOriginReading.Baked(new BuildInfo { SteamBuildLabel = "playtest-20260929" });
            var result = LocalPlayerIdentityResolver.ResolveForBuildOrigin(origin, new FakeSteamReader("76561198319362448"), "abc");
            Assert.IsFalse(result.Refusal.HasValue);
            Assert.AreEqual("steam:76561198319362448", result.Identity);
        }

        [Test]
        public void Steamラベルの無い焼き込みビルドは端末身元になるTest()
        {
            var origin = BuildOriginReading.Baked(new BuildInfo { SteamBuildLabel = null });
            var result = LocalPlayerIdentityResolver.ResolveForBuildOrigin(origin, new FakeSteamReader("76561198319362448"), "abc");
            Assert.IsFalse(result.Refusal.HasValue);
            Assert.AreEqual("device:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", result.Identity);
        }

        [Test]
        public void エディタ実行は端末身元になるTest()
        {
            var result = LocalPlayerIdentityResolver.ResolveForBuildOrigin(BuildOriginReading.Editor(), new FakeSteamReader("76561198319362448"), "abc");
            Assert.IsFalse(result.Refusal.HasValue);
            Assert.AreEqual("device:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", result.Identity);
        }
    }
}
