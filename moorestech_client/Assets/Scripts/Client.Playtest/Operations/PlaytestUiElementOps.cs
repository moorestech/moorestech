using System;
using Client.Playtest.Input;
using Client.Playtest.UiElements;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     ・描画系非依存の要素解決
    ///     ・セマンティック入力操作
    ///     ・Renderer-independent element resolution
    ///     ・Semantic input operations
    /// </summary>
    public static class PlaytestUiElementOps
    {
        private const float ActionTimeoutSeconds = 15f;
        private const float MouseGlideSeconds = 0.3f;

        public static UniTask<UiElementSnapshot> FindUi(string testid)
        {
            return PlaytestUiLocatorSession.Locator.Find(testid, ActionTimeoutSeconds);
        }

        public static async UniTask ClickUi(string testid)
        {
            await ClickUi(testid, ActionTimeoutSeconds);
        }

        public static async UniTask ClickUi(string testid, float timeoutSeconds)
        {
            // 安定中心へ実マウス経路でクリック
            // Click the settled center through the real mouse path
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            await GlideToSettledCenter(testid, deadline);
            UiElementScreenPointResolver.RemainingSeconds(deadline, testid);
            await SemanticInput.Click();
        }

        public static async UniTask HoverUi(string testid)
        {
            // クリック同様の安定中心へ移動のみ
            // Only move to the center using the click stability check
            var deadline = Time.realtimeSinceStartup + ActionTimeoutSeconds;
            await GlideToSettledCenter(testid, deadline);
        }

        public static async UniTask WaitUiElement(string testid, float timeoutSeconds)
        {
            // 可視かつ中心ヒットまで再照会
            // Repeat lookups until visible with a center hit
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            var lastStatus = UiElementLookupStatus.NotFound;
            while (Time.realtimeSinceStartup <= deadline)
            {
                var snapshot = await PlaytestUiLocatorSession.Locator.Find(testid, UiElementScreenPointResolver.RemainingSeconds(deadline, testid));
                lastStatus = snapshot.Status;
                if (deadline <= Time.realtimeSinceStartup) break;
                UiElementScreenPointResolver.ThrowIfAmbiguous(snapshot);
                if (UiElementScreenPointResolver.IsClickable(snapshot))
                {
                    LogResolved(snapshot);
                    return;
                }
                lastStatus = snapshot.Status;
                await UniTask.DelayFrame(2);
            }
            throw new TimeoutException($"UI element did not become available (last: {lastStatus}): {testid}");
        }

        public static async UniTask ScrollDownUntilUiElementIsClickable(string testid, string scrollAreaTestid, float timeoutSeconds)
        {
            // 領域上の実ホイールで枠外要素を表示
            // Use real wheel input over the scroll area to reveal clipped elements
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            var scrollArea = await UiElementScreenPointResolver.ResolveClickableUntil(PlaytestUiLocatorSession.Locator, scrollAreaTestid, UiElementScreenPointResolver.RemainingSeconds(deadline, testid));
            await SemanticInput.MouseGlideTo(scrollArea.ScreenRect.center, Mathf.Min(MouseGlideSeconds, UiElementScreenPointResolver.RemainingSeconds(deadline, testid)));
            var lastStatus = UiElementLookupStatus.NotFound;
            while (Time.realtimeSinceStartup <= deadline)
            {
                var target = await PlaytestUiLocatorSession.Locator.Find(testid, UiElementScreenPointResolver.RemainingSeconds(deadline, testid));
                lastStatus = target.Status;
                if (deadline <= Time.realtimeSinceStartup) break;
                UiElementScreenPointResolver.ThrowIfAmbiguous(target);
                if (UiElementScreenPointResolver.IsClickable(target)) return;
                lastStatus = target.Status;
                UiElementScreenPointResolver.RemainingSeconds(deadline, testid);
                SemanticInput.MouseWheel(-1f);
                await UniTask.DelayFrame(5);
            }
            throw new TimeoutException($"UI element stayed outside the scroll area (last: {lastStatus}): {testid}");
        }

        public static async UniTask WaitUiTextContains(string testid, string expected, float timeoutSeconds)
        {
            // 再描画後の実表示文字列を検証
            // Verify rendered text after re-rendering
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            UiElementSnapshot last = null;
            while (Time.realtimeSinceStartup <= deadline)
            {
                var snapshot = await PlaytestUiLocatorSession.Locator.Find(testid, UiElementScreenPointResolver.RemainingSeconds(deadline, testid));
                last = snapshot;
                if (deadline <= Time.realtimeSinceStartup) break;
                switch (snapshot.Status)
                {
                    case UiElementLookupStatus.Found:
                        if (snapshot.Visible && snapshot.Text.Contains(expected)) return;
                        break;
                    case UiElementLookupStatus.NotFound:
                    case UiElementLookupStatus.RendererUnavailable:
                        break; // silent-ok: 次の照会を待つ / 期限切れ例外に最終状態を記録する
                    case UiElementLookupStatus.Ambiguous:
                        UiElementScreenPointResolver.ThrowIfAmbiguous(snapshot);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown UI lookup status: {snapshot.Status}");
                }
                await UniTask.DelayFrame(2);
            }
            var lastDescription = last == null ? "no lookup completed" : $"{last.Status}, visible: {last.Visible}, text: '{last.Text}'";
            throw new TimeoutException($"UI text did not contain '{expected}' (last: {lastDescription}): {testid}");
        }

        public static async UniTask SetText(string testid, string text)
        {
            await SetText(testid, text, ActionTimeoutSeconds);
        }

        public static UniTask SetText(string testid, string text, float timeoutSeconds)
        {
            return PlaytestTextInputOps.SetText(testid, text, timeoutSeconds);
        }

        private static async UniTask GlideToSettledCenter(string testid, float deadline)
        {
            // 動かしている間に矩形が動いたら（中身の文の長さでパネルの幅が変わる等）、落ち着いた矩形の中心へ向け直す。期限切れは RemainingSeconds が投げる
            // If the rect moves during the glide (a panel resized by its text length, etc.), re-aim at the settled rect's center; RemainingSeconds throws on expiry
            var target = await UiElementScreenPointResolver.ResolveClickableUntil(PlaytestUiLocatorSession.Locator, testid, UiElementScreenPointResolver.RemainingSeconds(deadline, testid));
            while (true)
            {
                LogResolved(target);
                await SemanticInput.MouseGlideTo(target.ScreenRect.center, Mathf.Min(MouseGlideSeconds, UiElementScreenPointResolver.RemainingSeconds(deadline, testid)));
                await UniTask.DelayFrame(1);
                var settled = await UiElementScreenPointResolver.ResolveClickableUntil(PlaytestUiLocatorSession.Locator, testid, UiElementScreenPointResolver.RemainingSeconds(deadline, testid));
                if (settled.ScreenRect == target.ScreenRect) return;
                Debug.Log($"[PlaytestUi] {testid} moved during the glide {target.ScreenRect} -> {settled.ScreenRect}; re-aiming");
                target = settled;
            }
        }

        private static void LogResolved(UiElementSnapshot snapshot)
        {
            // 解決した要素をログへ残す
            // Log the resolved element
            Debug.Log($"[PlaytestUi] {snapshot.Testid} resolved");
        }
    }
}
