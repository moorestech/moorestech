using Client.Common;
using Client.Starter;
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
    }
}
