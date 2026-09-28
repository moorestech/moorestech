using NUnit.Framework;
using Server.Boot.Loop;

namespace Tests.UnitTest.Server
{
    // 世代管理の純粋テスト。PlayModeを要らないためServer.Tests側に置き、Server.Boot自身がClient.Testsへ内部公開する必要をなくす
    // Pure generation-management tests that need no PlayMode; kept in Server.Tests so Server.Boot no longer needs to expose internals to Client.Tests
    public class ServerThreadActionQueueTest
    {
        private long _generation;

        [SetUp]
        public void ResetServerQueue()
        {
            _generation = ServerThreadActionQueue.CurrentGeneration;
            ServerThreadActionQueue.Stop(_generation);
        }

        [TearDown]
        public void StopOwnedGeneration()
        {
            ServerThreadActionQueue.Stop(_generation);
        }

        [Test]
        public void サーバー終了は待機処理を失敗通知して受付を閉じる()
        {
            ServerThreadActionQueue.Stop(_generation);
            ServerThreadActionQueue.Drain();
            Assert.IsFalse(ServerThreadActionQueue.HasDrainedThisLifetime, "更新スレッド開始前のtickは有効化しない");

            _generation = ServerThreadActionQueue.BeginServerThread();
            ServerThreadActionQueue.Drain();
            var ran = false;
            var stopped = false;
            Assert.IsTrue(ServerThreadActionQueue.TryEnqueue(() => ran = true, () => stopped = true));
            ServerThreadActionQueue.Stop(_generation);

            Assert.IsFalse(ran);
            Assert.IsTrue(stopped);
            Assert.IsFalse(ServerThreadActionQueue.HasDrainedThisLifetime);
            Assert.IsFalse(ServerThreadActionQueue.TryEnqueue(() => ran = true, () => stopped = true));
        }

        [Test]
        public void 旧更新スレッドの終了は新サーバーの処理を捨てない()
        {
            _generation = ServerThreadActionQueue.BeginServerThread();
            var oldGeneration = _generation;
            ServerThreadActionQueue.Stop(_generation);
            _generation = ServerThreadActionQueue.BeginServerThread();
            ServerThreadActionQueue.Drain();
            var ran = false;
            var stopped = false;
            Assert.IsTrue(ServerThreadActionQueue.TryEnqueue(() => ran = true, () => stopped = true));

            ServerThreadActionQueue.Stop(oldGeneration);
            Assert.IsTrue(ServerThreadActionQueue.HasDrainedThisLifetime);
            ServerThreadActionQueue.Drain();
            Assert.IsTrue(ran);
            Assert.IsFalse(stopped);
            ServerThreadActionQueue.Stop(_generation);
        }
    }
}
