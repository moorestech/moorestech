using Client.Game.InGame.Train.Timetable;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using NUnit.Framework;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.Hostless
{
    // Webホスト無しで要求を送り、存在しない列車を取得不可と判定する
    // Send a request and mark a nonexistent train unavailable without the web host
    internal static class TimetableStateProbe
    {
        public static async UniTask AssertAvailableAsync(IObjectResolver resolver)
        {
            Assert.IsNotNull(resolver.Resolve<OpenTrainTimetableTracker>());
            var fetcher = resolver.Resolve<TrainTimetableFetcher>();
            var unknownTrain = TrainUnitInstanceId.Create();

            fetcher.RequestForOpenedTab(unknownTrain);
            Assert.IsTrue(fetcher.IsFetchInFlight(unknownTrain), "時刻表の取得要求が出ない");

            // サーバーに無い列車は列車なし応答か一度の再試行後に取得不可となる
            // A train missing on the server becomes unavailable via a no-train response or one retry
            for (var i = 0; i < 600 && !fetcher.IsUnavailable(unknownTrain); i++) await UniTask.Yield();
            Assert.IsTrue(fetcher.IsUnavailable(unknownTrain), "存在しない列車が取得不可にならない");
            fetcher.Close();
        }
    }
}
