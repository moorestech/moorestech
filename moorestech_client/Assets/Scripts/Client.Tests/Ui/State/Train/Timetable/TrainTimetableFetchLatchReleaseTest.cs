using Client.Game.InGame.Train.Timetable;
using Client.Ui.State.Train;
using Client.Tests.Ui.State.Train.Fakes;
using Game.Train.Unit;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.Ui.State.Train.Timetable
{
    public class TrainTimetableFetchLatchReleaseTest
    {
        [Test]
        public void 取得中に時刻表が届くとラッチを下ろして購読側へ知らせる()
        {
            var datastore = new ClientTrainTimetableDatastore();
            var sender = new FakeTrainTimetableRequestSender();
            var fetcher = new TrainTimetableFetcher(datastore, datastore, sender);
            var releasedCount = 0;
            fetcher.OnFetchLatchReleased.Subscribe(_ => releasedCount++);

            // 偽の送信口は応答を返さないので、取得中のまま 1 回だけ送られる
            // The fake sender never responds, so the fetch stays in flight after exactly one send
            fetcher.RequestForOpenedTab(TrainTestData.Unit1);
            Assert.AreEqual(1, sender.SentTrainUnitInstanceIds.Count);
            Assert.AreEqual(TrainTestData.Unit1, sender.SentTrainUnitInstanceIds[0]);
            Assert.IsTrue(fetcher.IsFetchInFlight(TrainTestData.Unit1));
            Assert.AreEqual(0, releasedCount);

            // 応答より先に push で届いたら下ろす
            // A push that arrives before the response lowers the latch
            datastore.Apply(Snapshot());
            Assert.IsFalse(fetcher.IsFetchInFlight(TrainTestData.Unit1));
            Assert.AreEqual(1, releasedCount);

            // 下りた後の削除通知や再通知は流さない
            // Later removals or repeats do not notify again
            datastore.Apply(Snapshot());
            datastore.Remove(TrainTestData.Unit1);
            Assert.AreEqual(1, releasedCount);

            // 同じ列車の重複要求は送り直さない
            // A duplicate request for the same train is not sent again
            fetcher.RequestForOpenedTab(TrainTestData.Unit1);
            Assert.AreEqual(1, sender.SentTrainUnitInstanceIds.Count);
            fetcher.Close();
        }

        private static TrainTimetableSnapshot Snapshot()
        {
            return new TrainTimetableSnapshot(TrainTestData.Unit1, false, 0, new TrainTimetableStop[0]);
        }
    }
}
