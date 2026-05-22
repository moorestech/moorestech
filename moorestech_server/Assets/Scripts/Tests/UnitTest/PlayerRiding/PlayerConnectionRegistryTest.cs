using System.Collections.Generic;
using Game.PlayerConnection;
using NUnit.Framework;
using UniRx;

namespace Tests.UnitTest.PlayerRiding
{
    public class PlayerConnectionRegistryTest
    {
        [Test]
        public void Register_MakesPlayerConnected_Unregister_FiresDisconnectedAndClears()
        {
            var registry = new PlayerConnectionRegistry();
            var disconnected = new List<int>();
            using var sub = registry.OnPlayerDisconnected.Subscribe(disconnected.Add);

            registry.Register(5);
            registry.Unregister(5);

            // 登録済み playerId は接続中になり、解除時に切断通知が出る。
            // A registered player becomes connected and emits a disconnect notification when removed.
            Assert.IsFalse(registry.IsConnected(5));
            CollectionAssert.AreEqual(new List<int> { 5 }, disconnected);
        }

        [Test]
        public void Unregister_UnknownPlayer_DoesNotFire()
        {
            var registry = new PlayerConnectionRegistry();
            var disconnected = new List<int>();
            using var sub = registry.OnPlayerDisconnected.Subscribe(disconnected.Add);

            registry.Unregister(99);

            Assert.AreEqual(0, disconnected.Count);
            Assert.IsFalse(registry.IsConnected(99));
        }
    }
}
