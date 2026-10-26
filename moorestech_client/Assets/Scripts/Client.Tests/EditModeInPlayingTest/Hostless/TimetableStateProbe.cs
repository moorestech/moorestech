using Client.Game.InGame.Train.Timetable;
using Client.Game.InGame.UI.UIState;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using NUnit.Framework;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.Hostless
{
    // 不在列車を取得不可と判定
    // Mark a nonexistent train unavailable
    internal static class TimetableStateProbe
    {
        public static async UniTask AssertAvailableAsync(IObjectResolver resolver)
        {
            Assert.IsNotNull(resolver.Resolve<OpenTrainTimetableTracker>());
            var fetcher = resolver.Resolve<TrainTimetableFetcher>();
            var unknownTrain = TrainUnitInstanceId.Create();

            fetcher.RequestForOpenedTab(unknownTrain);
            Assert.IsTrue(fetcher.IsFetchInFlight(unknownTrain), "時刻表の取得要求が出ない");

            // 不在列車は取得不可になる
            // A missing train becomes unavailable
            for (var i = 0; i < 600 && !fetcher.IsUnavailable(unknownTrain); i++) await UniTask.Yield();
            Assert.IsTrue(fetcher.IsUnavailable(unknownTrain), "存在しない列車が取得不可にならない");

            // 画面遷移でtrackerがInitializeで張った購読から取得状態を忘れる。手動のCloseでは確かめられない
            // A screen transition makes the tracker forget the fetch state via its Initialize subscription, which a manual Close cannot verify
            var uiStateControl = resolver.Resolve<UIStateControl>();
            uiStateControl.RequestTransition(UIStateEnum.ChallengeList);
            for (var i = 0; i < 120 && fetcher.IsUnavailable(unknownTrain); i++) await UniTask.Yield();
            Assert.IsFalse(fetcher.IsUnavailable(unknownTrain), "画面遷移で取得状態が忘れられない");

            // 後続のプローブのためにゲーム画面へ戻す
            // Return to the game screen for the following probes
            uiStateControl.RequestTransition(UIStateEnum.GameScreen);
            for (var i = 0; i < 120 && uiStateControl.CurrentState != UIStateEnum.GameScreen; i++) await UniTask.Yield();
            Assert.AreEqual(UIStateEnum.GameScreen, uiStateControl.CurrentState);
        }
    }
}
