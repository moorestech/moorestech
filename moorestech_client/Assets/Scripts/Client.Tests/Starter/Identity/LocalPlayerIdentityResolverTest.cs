using Client.PlaytestReceiver.Steam;
using Client.Starter.Identity;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Starter
{
    public class LocalPlayerIdentityResolverTest
    {
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
            var result = LocalPlayerIdentityResolver.Resolve(true, new FakeSteamReader("76561198319362448"), "abc");
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("steam:76561198319362448", result.Identity);
        }

        [Test]
        public void Steam配布ビルドでSteamIDが読めなければ端末値へ落ちず拒否Test()
        {
            var result = LocalPlayerIdentityResolver.Resolve(true, new FakeSteamReader(null), "abc");
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(LocalizationKeys.Ui.Loading.SteamIdentityUnavailable.Key, result.RefusalLocalizationKey.Key);
        }

        [Test]
        public void それ以外は端末値のSHA256小文字16進になるTest()
        {
            var result = LocalPlayerIdentityResolver.Resolve(false, new FakeSteamReader("1"), "abc");
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("device:ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", result.Identity);
        }

        [Test]
        public void 未対応端末識別子は拒否Test()
        {
            var result = LocalPlayerIdentityResolver.Resolve(false, new FakeSteamReader("1"), SystemInfo.unsupportedIdentifier);
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(LocalizationKeys.Ui.Loading.DeviceIdentityUnavailable.Key, result.RefusalLocalizationKey.Key);
        }

        [TestCase("")]
        [TestCase(null)]
        public void 端末値が取れなければ拒否Test(string device)
        {
            var result = LocalPlayerIdentityResolver.Resolve(false, new FakeSteamReader("1"), device);
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(LocalizationKeys.Ui.Loading.DeviceIdentityUnavailable.Key, result.RefusalLocalizationKey.Key);
        }
    }
}
