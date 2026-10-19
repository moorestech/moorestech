using System;
using Client.Game.InGame.UI.UIState.State.DragDelete.Confirm;
using NUnit.Framework;

namespace Client.Tests.UIState.DragDelete.Confirm
{
    public class DeleteHoldDurationTest
    {
        [TestCase(1, 0.75f)]
        [TestCase(4, 0.75f)]
        [TestCase(5, 1.25f)]
        [TestCase(19, 1.25f)]
        [TestCase(20, 1.75f)]
        [TestCase(500, 1.75f)]
        public void 件数の段ごとに満了時間が決まる(int targetCount, float expectedSeconds)
        {
            Assert.AreEqual(expectedSeconds, DeleteHoldDuration.SecondsFor(targetCount), 0.0001f);
        }

        [Test]
        public void 対象0件の満了時間は求めない()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DeleteHoldDuration.SecondsFor(0));
        }
    }
}
