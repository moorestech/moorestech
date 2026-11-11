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
            var commands = new FakeTrainTimetableCommands();
            var trigger = new TrainTimetableFetchTrigger(commands);

            // 持ち物タブの読込中では依頼しない
            // Loading on the inventory tab does not ask
            trigger.ReceiveState(TrainTimetableViewState.Loading);
            Assert.AreEqual(0, commands.OpenRequestCount);
            trigger.SelectTab(TrainInventoryTab.Timetable);
            Assert.AreEqual(1, commands.OpenRequestCount);
            trigger.SelectTab(TrainInventoryTab.Timetable);
            Assert.AreEqual(1, commands.OpenRequestCount);

            // 時刻表タブは読込中で依頼、用意済みは不要
            // Timetable tab: loading requests, ready does not
            trigger.ReceiveState(TrainTimetableViewState.Loading);
            Assert.AreEqual(2, commands.OpenRequestCount);
            trigger.ReceiveState(TrainTimetableViewState.Ready(TrainTestData.NorthOnly(TrainTestData.Unit1)));
            trigger.SelectTab(TrainInventoryTab.Inventory);
            Assert.AreEqual(2, commands.OpenRequestCount);
        }
    }
}
