using System;
using Client.Game.InGame.Hotbar;
using Game.Hotbar;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.Hotbar
{
    /// <summary>
    ///     Web由来選択要求の消費契約と割当適用通知の回帰試験
    ///     Regression tests for the web-originated select request's consumption contract and the assignment change notification
    /// </summary>
    public class ClientHotbarDatastoreTest
    {
        private const int RequestedSlot = 3;

        [Test]
        public void SelectRequestIsConsumedExactlyOnce()
        {
            var datastore = new ClientHotbarDatastore();

            datastore.EnqueueSelectRequest(RequestedSlot);

            Assert.IsTrue(datastore.TryConsumeSelectRequest(out var slot));
            Assert.AreEqual(RequestedSlot, slot);
            Assert.IsFalse(datastore.TryConsumeSelectRequest(out _), "選択要求は1度だけ消費される");
        }

        [Test]
        public void ClearingDiscardsAnUnconsumedSelectRequest()
        {
            var datastore = new ClientHotbarDatastore();

            // 消費されないままUIStateが遷移すると、復帰後に古いクリックが建築モードを暴発させる
            // An unconsumed request would otherwise fire build mode from a stale click after returning to the screen
            datastore.EnqueueSelectRequest(RequestedSlot);
            datastore.ClearPendingSelectRequest();

            Assert.IsFalse(datastore.TryConsumeSelectRequest(out _), "遷移をまたいだ要求は持ち越さない");
        }

        [Test]
        public void ApplyingAssignmentsNotifiesSubscribers()
        {
            var datastore = new ClientHotbarDatastore();
            var assignments = new Guid[HotbarAssignmentDatastore.SlotCount];
            assignments[1] = Guid.Parse("70000000-0000-4000-8000-000000000001");

            var changedCount = 0;
            using (datastore.OnAssignmentsChanged.Subscribe(_ => changedCount++))
            {
                datastore.ApplyAssignments(assignments);
            }

            Assert.AreEqual(1, changedCount);
            Assert.AreEqual(assignments[1], datastore.Assignments[1]);
        }
    }
}
