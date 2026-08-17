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
            var proprieties = InitializeProprieties.CreateLocalServer(ServerConst.DefaultPlayerId);

            // ローカルは接続試行しないモードとして固定する
            // Pin local play as the mode that never probes an existing server
            Assert.That(proprieties.IsRemoteConnection, Is.False);
            Assert.That(proprieties.ServerIp, Is.EqualTo(ServerConst.LocalServerIp));
            Assert.That(proprieties.PlayerId, Is.EqualTo(ServerConst.DefaultPlayerId));
        }

        [Test]
        public void CreateRemoteConnection_リモートモードで指定IPとポートを保持する()
        {
            var proprieties = InitializeProprieties.CreateRemoteConnection("192.168.1.10", 25000, 5);

            // 外部接続は明示指定の宛先だけを運ぶことを固定する
            // Pin that remote connection carries only the explicitly specified destination
            Assert.That(proprieties.IsRemoteConnection, Is.True);
            Assert.That(proprieties.ServerIp, Is.EqualTo("192.168.1.10"));
            Assert.That(proprieties.ServerPort, Is.EqualTo(25000));
            Assert.That(proprieties.PlayerId, Is.EqualTo(5));
        }
    }
}
