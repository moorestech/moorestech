using System.Text.RegularExpressions;
using Client.Game.InGame.Train.Timetable;
using Client.Ui.State.Train;
using Client.Tests.Ui.State.Train.Fakes;
using Game.Train.Unit;
using NUnit.Framework;
using UniRx;
using UnityEngine.TestTools;

namespace Client.Tests.Ui.State.Train.Timetable
{
    public class TrainTimetableFetchLatchReleaseTest
    {
        [Test]
        public void 取得中に時刻表が届くとラッチを下ろして購読側へ知らせる()
        {
            var datastore = new ClientTrainTimetableDatastore();
            var fetcher = new TrainTimetableFetcher(datastore, datastore);
            var releasedCount = 0;
            fetcher.OnFetchLatchReleased.Subscribe(_ => releasedCount++);

            // 送信境界（ClientContext.VanillaApi）はテストに無く、取得の非同期側は落ちる。ここで見るのは同期で立つラッチと push の経路だけ
            // The transport (ClientContext.VanillaApi) is absent in tests so the async fetch throws; only the synchronous latch and the push path are checked here
            LogAssert.Expect(UnityEngine.LogType.Exception, new Regex("NullReferenceException"));
            fetcher.RequestForOpenedTab(TrainTestData.Unit1);
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
            fetcher.Close();
        }

        private static TrainTimetableSnapshot Snapshot()
        {
            return new TrainTimetableSnapshot(TrainTestData.Unit1, false, 0, new TrainTimetableStop[0]);
        }
    }
}
