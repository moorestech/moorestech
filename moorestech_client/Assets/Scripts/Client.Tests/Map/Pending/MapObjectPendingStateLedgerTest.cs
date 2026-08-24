using Client.Game.InGame.Map.MapObject.Pending;
using NUnit.Framework;

namespace Client.Tests.Map.Pending
{
    /// <summary>
    ///     未生成個体宛イベントの保留台帳を検証
    ///     Verifies the pending-state ledger for events addressed to not-yet-instantiated objects
    /// </summary>
    public class MapObjectPendingStateLedgerTest
    {
        [Test]
        public void 未記録のinstanceIdはfalse()
        {
            var ledger = new MapObjectPendingStateLedger();
            Assert.IsFalse(ledger.TryConsume(1, out _));
        }

        [Test]
        public void 破壊とHPは同一instanceIdへ合成される()
        {
            var ledger = new MapObjectPendingStateLedger();
            ledger.RecordHp(1, 30);
            ledger.RecordDestroy(1);

            Assert.IsTrue(ledger.TryConsume(1, out var state));
            Assert.IsTrue(state.IsDestroyed);
            Assert.IsTrue(state.HasHp);
            Assert.AreEqual(30, state.Hp);
        }

        [Test]
        public void HPは最新値で上書きされる()
        {
            var ledger = new MapObjectPendingStateLedger();
            ledger.RecordHp(1, 30);
            ledger.RecordHp(1, 10);

            Assert.IsTrue(ledger.TryConsume(1, out var state));
            Assert.AreEqual(10, state.Hp);
        }

        [Test]
        public void 消費すると台帳から消える()
        {
            var ledger = new MapObjectPendingStateLedger();
            ledger.RecordDestroy(1);

            Assert.IsTrue(ledger.TryConsume(1, out _));
            Assert.IsFalse(ledger.TryConsume(1, out _));
        }

        [Test]
        public void 別instanceIdへは波及しない()
        {
            var ledger = new MapObjectPendingStateLedger();
            ledger.RecordDestroy(1);
            Assert.IsFalse(ledger.TryConsume(2, out _));
        }
    }
}
