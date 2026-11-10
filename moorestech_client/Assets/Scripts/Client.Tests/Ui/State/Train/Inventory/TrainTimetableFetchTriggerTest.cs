using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.State.Train.Inventory;
using Client.Ui.State.Train.Timetable;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Train.Inventory
{
    public class TrainTimetableFetchTriggerTest
    {
        [Test]
        public void 時刻表タブへ移った瞬間と時刻表タブで読込中が届いたときだけ依頼する()
        {
            var trigger = new TrainTimetableFetchTrigger();

            // 持ち物タブの読込中では依頼しない
            // Loading on the inventory tab does not ask
            Assert.IsFalse(trigger.ReceiveState(TrainTimetableViewState.Loading));
            Assert.IsTrue(trigger.SelectTab(TrainInventoryTab.Timetable));
            Assert.IsFalse(trigger.SelectTab(TrainInventoryTab.Timetable));

            // 時刻表タブでは読込中が届くたびに依頼し、用意済みでは依頼しない
            // On the timetable tab every loading arrival asks, ready does not
            Assert.IsTrue(trigger.ReceiveState(TrainTimetableViewState.Loading));
            Assert.IsFalse(trigger.ReceiveState(TrainTimetableViewState.Ready(TrainTestData.NorthOnly(TrainTestData.Unit1))));
            Assert.IsFalse(trigger.SelectTab(TrainInventoryTab.Inventory));
        }
    }
}
