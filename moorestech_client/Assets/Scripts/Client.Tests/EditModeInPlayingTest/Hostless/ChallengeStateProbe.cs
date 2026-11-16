using System;
using Client.Ui.State.Challenge;
using Client.Network.API;
using Client.Tests.EditModeInPlayingTest.Playtest;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.Hostless
{
    // 完了でDI状態置換と通知
    // Completion replaces DI state and notifies
    internal static class ChallengeStateProbe
    {
        public static async UniTask AssertAvailableAsync(IObjectResolver resolver)
        {
            var state = resolver.Resolve<ChallengeProgressState>();
            var handshake = resolver.Resolve<InitialHandshakeResponse>();
            Assert.IsNotNull(state.Categories, "チャレンジの状態がDIに無い");

            Guid? notifiedGuid = null;
            using var subscription = UniRx.ObservableExtensions.Subscribe(state.OnChallengeCompleted, guid => notifiedGuid = guid);

            // 購読経由でのみ全量置換+通知
            // Only the subscription replaces state and notifies
            var completedGuid = PlaytestProgressDriveUtil.CompleteCurrentChallenge();
            for (var i = 0; i < 600 && !notifiedGuid.HasValue; i++) await UniTask.Yield();
            Assert.AreEqual(completedGuid, notifiedGuid, "チャレンジ完了イベントが状態に届かない");
            Assert.IsFalse(ReferenceEquals(state.Categories, handshake.Challenges), "完了後も状態がhandshakeの初期値のまま");
        }
    }
}
