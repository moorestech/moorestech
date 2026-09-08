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
                var canSkip = current != null && 0 <= Array.IndexOf(current.AllowedIntents, "skip");

                // 飛ばし終えたあとにskipが引けなくなったら完了。まだ一度も出ていないなら再生開始を待ち続ける
                // Once skipped, losing the skip intent means it ended; before that, keep waiting for it to start
                if (!canSkip && skitSkipped) break;
                if (canSkip) skitSkipped |= skitStore.TrySkip(current.SessionId, current.SceneRevision).Ok;
                await UniTask.Delay(TimeSpan.FromSeconds(0.25f), ignoreTimeScale: true);
            }

            Debug.Log(skitSkipped ? "[Playtest] 開幕スキットをSkipインテントで飛ばした" : "[Playtest] 開幕スキットは再生されなかった");
        }
    }
}
