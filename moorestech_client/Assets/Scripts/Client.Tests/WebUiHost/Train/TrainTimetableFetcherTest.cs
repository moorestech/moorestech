using Client.Game.InGame.Train.Timetable;
using Client.WebUiHost.Game.Topics;
using Game.Train.Unit;
using NUnit.Framework;

namespace Client.Tests.WebUiHost.Train
{
    // 送信境界を直接呼ぶ形に戻したため、送信なしのラッチだけを固定する
    // Sending now follows the precedent of calling ClientContext.VanillaApi directly, so only the send-free latches are pinned
    public class TrainTimetableFetcherTest
    {
        [Test]
        public void NothingIsUnavailableOrInFlightBeforeTheTabOpens()
        {
            var datastore = new ClientTrainTimetableDatastore();
            var fetcher = new TrainTimetableFetcher(datastore, datastore);
            var id = TrainUnitInstanceId.Create();

            Assert.That(fetcher.IsUnavailable(id), Is.False);
            Assert.That(fetcher.IsFetchInFlight(id), Is.False);
        }

        [Test]
        public void TrainChangeBeforeTheTabOpensDoesNotFetch()
        {
            var datastore = new ClientTrainTimetableDatastore();
            var fetcher = new TrainTimetableFetcher(datastore, datastore);
            var id = TrainUnitInstanceId.Create();

            // ガードが外れると通信境界に触れて落ちる
            // If the tab-closed guard broke, this would reach the transport boundary and throw
            fetcher.FollowOpenTrainChange(id);

            Assert.That(fetcher.IsFetchInFlight(id), Is.False);
        }

        [Test]
        public void CloseBeforeAnyRequestIsSafe()
        {
            var datastore = new ClientTrainTimetableDatastore();
            var fetcher = new TrainTimetableFetcher(datastore, datastore);
            var id = TrainUnitInstanceId.Create();

            fetcher.Close();

            Assert.That(fetcher.IsUnavailable(id), Is.False);
            Assert.That(fetcher.IsFetchInFlight(id), Is.False);
        }
    }
}
