using Client.Common;
using Client.Localization;
using Client.Starter;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.Starter
{
    public class InitializeProprietiesTest
    {
        [Test]
        public void CreateLocalServer_ローカルモードで生成しループバックIPを持つ()
        {
            var proprieties = InitializeProprieties.CreateLocalServer(null);

            // 接続試行しないモードを固定
            // Pin that local never probes
            Assert.That(proprieties.IsRemoteConnection, Is.False);
            Assert.That(proprieties.ServerIp, Is.EqualTo(ServerConst.LocalServerIp));

            // ローカルは宛先ポートを持たない。11564等の固定値へ戻す退行を落とす
            // Local carries no destination port; a regression back to a fixed value such as 11564 fails here
            Assert.That(proprieties.RemoteServerPort, Is.Null);

            // 未指定時の既定プレイヤーはInitializeProprietiesが解決する
            // InitializeProprieties resolves the default player when unspecified
            Assert.That(proprieties.PlayerId, Is.EqualTo(1));
        }

        [Test]
        public void CreateLocalServer_プレイヤーID指定時はその値を保持する()
        {
            var proprieties = InitializeProprieties.CreateLocalServer(7);

            Assert.That(proprieties.PlayerId, Is.EqualTo(7));
        }

        [Test]
        public void CreateRemoteConnection_リモートモードで指定IPとポートを保持する()
        {
            var proprieties = InitializeProprieties.CreateRemoteConnection("192.168.1.10", 25000, 5);

            // 明示指定の宛先のみを固定
            // Pin the explicit destination only
            Assert.That(proprieties.IsRemoteConnection, Is.True);
            Assert.That(proprieties.ServerIp, Is.EqualTo("192.168.1.10"));
            Assert.That(proprieties.RemoteServerPort, Is.EqualTo(25000));
            Assert.That(proprieties.PlayerId, Is.EqualTo(5));
        }

        [Test]
        public void TryCreateRemoteConnection_検証を通った入力だけリモート接続になる()
        {
            var created = InitializeProprieties.TryCreateRemoteConnection("192.168.1.10", "25000", 5, out var proprieties, out _);

            Assert.That(created, Is.True);
            Assert.That(proprieties.IsRemoteConnection, Is.True);
            Assert.That(proprieties.ServerIp, Is.EqualTo("192.168.1.10"));
            Assert.That(proprieties.RemoteServerPort, Is.EqualTo(25000));
            Assert.That(proprieties.PlayerId, Is.EqualTo(5));
        }

        [Test]
        public void TryCreateRemoteConnection_IPとポートの不正はそれぞれの文言キーで拒む()
        {
            AssertDenied("not-an-ip", "25000", LocalizationKeys.Ui.MainMenu.ConnectInvalidIp);
            AssertDenied("192.168.1.10", "twenty", LocalizationKeys.Ui.MainMenu.ConnectInvalidPort);

            #region Internal

            void AssertDenied(string ipText, string portText, LocalizationKey expectedKey)
            {
                var created = InitializeProprieties.TryCreateRemoteConnection(ipText, portText, 5, out var proprieties, out var denyReason);

                Assert.That(created, Is.False);
                Assert.That(proprieties, Is.Null);
                Assert.That(denyReason.Key.Key, Is.EqualTo(expectedKey.Key));
                Assert.That(denyReason.TextParams, Is.Empty);
            }

            #endregion
        }

        [Test]
        public void TryCreateRemoteConnection_範囲外ポートは境界値を文言へ供給して拒む()
        {
            // 文言解決は実辞書を通す
            // Resolve text through the real dictionary
            Localize.Initialize();

            AssertDeniedWithBoundary("65536", LocalizationKeys.Ui.MainMenu.ConnectPortTooLarge, "65535");
            AssertDeniedWithBoundary("1024", LocalizationKeys.Ui.MainMenu.ConnectPortTooSmall, "1024");

            #region Internal

            void AssertDeniedWithBoundary(string portText, LocalizationKey expectedKey, string expectedBoundary)
            {
                var created = InitializeProprieties.TryCreateRemoteConnection("192.168.1.10", portText, 5, out var proprieties, out var denyReason);

                Assert.That(created, Is.False);
                Assert.That(proprieties, Is.Null);
                Assert.That(denyReason.Key.Key, Is.EqualTo(expectedKey.Key));

                // 境界値は{p0}として実文言まで届く
                // The boundary value reaches the resolved wording through {p0}
                StringAssert.Contains(expectedBoundary, Localize.GetFormatted(denyReason.Key, denyReason.TextParams));
            }

            #endregion
        }

        [Test]
        public void TryCreateRemoteConnection_許容範囲の境界ポートは通る()
        {
            Assert.That(InitializeProprieties.TryCreateRemoteConnection("192.168.1.10", "1025", 5, out _, out _), Is.True);
            Assert.That(InitializeProprieties.TryCreateRemoteConnection("192.168.1.10", "65535", 5, out _, out _), Is.True);
        }
    }
}
