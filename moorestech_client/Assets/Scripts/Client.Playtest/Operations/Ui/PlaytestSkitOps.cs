using System;
using Client.Game.InGame.UI.UIState;
using Client.Skit.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.Operations.Ui
{
    /// <summary>
    ///     開幕スキットを飛ばしてゲーム画面へ抜けるための前提操作
    ///     Precondition operations for skipping the opening skit and landing on the game screen
    /// </summary>
    public static class PlaytestSkitOps
    {
        // 再生されないワールドでも待ちすぎないための、任意スキップの上限
        // Upper bound for the optional skip so worlds without a skit do not stall
        public const float OptionalSkipTimeoutSeconds = 10f;
        private const float TimeScaleResumeTimeoutSeconds = 10f;

        // 開幕スキット表示中はホットバー入力もビルドメニューもポーズメニューも通らないため、全シナリオ共通の前提として飛ばす
        // The opening skit blocks hotbar input, the build menu, and the pause menu alike, so every scenario skips it as a shared precondition
        public static async UniTask SkipOpeningSkit(float skipTimeoutSeconds, float uiStateTimeoutSeconds)
        {
            var skitStore = SkitPresentationStateStore.Instance;
            var startTime = Time.realtimeSinceStartup;
            while (!TrySkipCurrentSkit())
            {
                if (skipTimeoutSeconds < Time.realtimeSinceStartup - startTime) throw new TimeoutException("Opening skit's skip intent was never accepted");
                await UniTask.Yield();
            }

            await PlaytestUiOps.WaitUiState(UIStateEnum.GameScreen, uiStateTimeoutSeconds);

            #region Internal

            bool TrySkipCurrentSkit()
            {
                var current = skitStore.GetCurrent();
                return current != null && skitStore.TrySkip(current.SessionId, current.SceneRevision).Ok;
            }

            #endregion
        }

        // スキットが再生されないワールドもあるため、最大timeoutSecondsまで再生開始を待ち、始まっていれば飛ばして終わりを待つ
        // Some worlds never play the opening skit, so wait up to timeoutSeconds for it to start, then skip it and wait for it to end
        public static async UniTask SkipOpeningSkitIfPlaying(float timeoutSeconds)
        {
            var skitStore = SkitPresentationStateStore.Instance;
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            var skitSkipped = false;
            while (Time.realtimeSinceStartup < deadline)
            {
                var current = skitStore.GetCurrent();
                var canSkip = current != null && SkitPresentationData.IsIntentAllowed(current.AllowedIntents, SkitPresentationData.SkipIntent);

                // 飛ばし終えたあとにskipが引けなくなったら完了。まだ一度も出ていないなら再生開始を待ち続ける
                // Once skipped, losing the skip intent means it ended; before that, keep waiting for it to start
                if (!canSkip && skitSkipped) break;
                if (canSkip) skitSkipped |= skitStore.TrySkip(current.SessionId, current.SceneRevision).Ok;
                await UniTask.Delay(TimeSpan.FromSeconds(0.25f), ignoreTimeScale: true);
            }

            Debug.Log(skitSkipped ? "[Playtest] 開幕スキットをSkipインテントで飛ばした" : "[Playtest] 開幕スキットは再生されなかった");
        }

        // Game View を表へ出し、Story の間だけ開幕スキットを飛ばし直す。Story を離れたら回した回数を返す
        // Bring the Game View front and re-skip the opening skit only while in Story; return the rounds once it leaves Story
        public static async UniTask<int> SkipOpeningSkitUntilLeftStory(int maxRounds, float pollSeconds)
        {
            // 裏タブの Game View は描画されず timeScale=0 のまま Skip 送りが止まるので、表へ出して時間が進むのを待つ
            // A Game View behind a tab never renders and skip stalls at timeScale=0, so bring it front and wait for time to run
            PlaytestGameViewOps.BringToFront();
            var timeScaleDeadline = Time.realtimeSinceStartup + TimeScaleResumeTimeoutSeconds;
            while (Time.timeScale <= 0f)
            {
                if (timeScaleDeadline < Time.realtimeSinceStartup) throw new TimeoutException($"Game View を表へ出しても {TimeScaleResumeTimeoutSeconds}s 以内に timeScale が戻らない");
                await UniTask.Yield();
            }

            // 飛ばした後も Story のままなら、pollSeconds 待って飛ばし直す
            // If still in Story after a skip, wait up to pollSeconds and skip again
            for (var round = 1; round <= maxRounds; round++)
            {
                await SkipOpeningSkitIfPlaying(OptionalSkipTimeoutSeconds);
                if (await WaitLeftStory()) return round;
                Debug.LogWarning($"[Playtest] 開幕スキットを飛ばして {pollSeconds}s 待っても Story のまま（{round}/{maxRounds} 回目）");
            }
            throw new TimeoutException($"開幕スキットを {maxRounds} 回飛ばしても Story を抜けない（1 回 {pollSeconds}s 待ち）");

            #region Internal

            async UniTask<bool> WaitLeftStory()
            {
                var deadline = Time.realtimeSinceStartup + pollSeconds;
                while (Time.realtimeSinceStartup < deadline)
                {
                    if (PlaytestUiOps.CurrentUiState() != UIStateEnum.Story) return true;
                    await UniTask.DelayFrame(5);
                }
                return PlaytestUiOps.CurrentUiState() != UIStateEnum.Story;
            }

            #endregion
        }
    }
}
