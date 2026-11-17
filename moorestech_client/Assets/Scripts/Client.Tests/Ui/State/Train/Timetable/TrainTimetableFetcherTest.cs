using Client.Game.InGame.Train.Timetable;
using Client.Tests.Ui.State.Train.Fakes;
using Client.Ui.State.Train;
using Game.Train.Unit;
using NUnit.Framework;

namespace Client.Tests.Ui.State.Train.Timetable
{
    // 送信なしのラッチだけを固定する
    // Pins only the send-free latches
    public class TrainTimetableFetcherTest
    {
        [Test]
        public void NothingIsUnavailableOrInFlightBeforeTheTabOpens()
        {
            var datastore = new ClientTrainTimetableDatastore();
            var fetcher = new TrainTimetableFetcher(datastore, datastore, new FakeTrainTimetableRequestSender());
            var id = TrainUnitInstanceId.Create();

            Assert.That(fetcher.IsUnavailable(id), Is.False);
            Assert.That(fetcher.IsFetchInFlight(id), Is.False);
        }

        [Test]
        public void TrainChangeBeforeTheTabOpensDoesNotFetch()
        {
            var datastore = new ClientTrainTimetableDatastore();
            var fetcher = new TrainTimetableFetcher(datastore, datastore, new FakeTrainTimetableRequestSender());
            var id = TrainUnitInstanceId.Create();

            // ガードが外れると取得中が立つ
            // If the tab-closed guard broke, the fetch would be in flight
            fetcher.FollowOpenTrainChange(id);

            Assert.That(fetcher.IsFetchInFlight(id), Is.False);
        }

        [Test]
        public void CloseBeforeAnyRequestIsSafe()
        {
            var datastore = new ClientTrainTimetableDatastore();
            var fetcher = new TrainTimetableFetcher(datastore, datastore, new FakeTrainTimetableRequestSender());
            var id = TrainUnitInstanceId.Create();

            fetcher.Close();

            Assert.That(fetcher.IsUnavailable(id), Is.False);
            Assert.That(fetcher.IsFetchInFlight(id), Is.False);
        }
    }
}
