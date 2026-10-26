using System;
using Client.Playtest.Input;
using Client.Playtest.UiElements;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     testidのUI要素をロケータで解決し、セマンティック入力で操作する（描画系に依存しない）
    ///     Resolves testid UI elements through the locator and operates them with semantic input (renderer-independent)
    /// </summary>
    public static class PlaytestUiElementOps
    {
        private const float ActionTimeoutSeconds = 15f;
        private const float MouseGlideSeconds = 0.3f;

        public static UniTask<UiElementSnapshot> FindUi(string testid)
        {
            return PlaytestUiLocatorSession.Locator.Find(testid);
        }

        public static async UniTask ClickUi(string testid)
        {
            await ClickUi(testid, ActionTimeoutSeconds);
        }

        public static async UniTask ClickUi(string testid, float timeoutSeconds)
        {
            // 安定した要素中心へ移動し、実プレイヤーと同じマウス経路でクリックする
            // Move to the settled element center and click through the same mouse path as a real player
            var target = await UiElementScreenPointResolver.ResolveClickableUntil(PlaytestUiLocatorSession.Locator, testid, timeoutSeconds);
            LogResolved(target);
            await SemanticInput.MouseGlideTo(target.ScreenRect.center, MouseGlideSeconds);
            await UniTask.DelayFrame(1);
            await SemanticInput.Click();
        }

        public static async UniTask HoverUi(string testid)
        {
            // クリックと同じ安定性判定を通し、対象中心へのポインタ移動だけを行う
            // Apply the same stability check as clicks, then only move the pointer to the target center
            var target = await UiElementScreenPointResolver.ResolveClickableUntil(PlaytestUiLocatorSession.Locator, testid, ActionTimeoutSeconds);
            LogResolved(target);
            await SemanticInput.MouseGlideTo(target.ScreenRect.center, MouseGlideSeconds);
            await UniTask.DelayFrame(1);
        }

        public static async UniTask WaitUiElement(string testid, float timeoutSeconds)
        {
            // 可視かつ中心が実際にヒットするまで照会を繰り返す
            // Repeat lookups until the element is visible and its center genuinely hits
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            var lastStatus = UiElementLookupStatus.NotFound;
            while (Time.realtimeSinceStartup <= deadline)
            {
                var snapshot = await FindUi(testid);
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
            // スクロール領域へポインタを置き、枠外のスロットを実際のホイール入力で表示領域へ送る
            // Put the pointer on the scroll area and wheel clipped slots into view with real wheel input
            var scrollArea = await UiElementScreenPointResolver.ResolveClickableUntil(PlaytestUiLocatorSession.Locator, scrollAreaTestid, timeoutSeconds);
            await SemanticInput.MouseGlideTo(scrollArea.ScreenRect.center, MouseGlideSeconds);
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            var lastStatus = UiElementLookupStatus.NotFound;
            while (Time.realtimeSinceStartup <= deadline)
            {
                var target = await FindUi(testid);
                UiElementScreenPointResolver.ThrowIfAmbiguous(target);
                if (UiElementScreenPointResolver.IsClickable(target)) return;
                lastStatus = target.Status;
                SemanticInput.MouseWheel(-120f);
                await UniTask.DelayFrame(5);
            }
            throw new TimeoutException($"UI element stayed outside the scroll area (last: {lastStatus}): {testid}");
        }

        public static async UniTask WaitUiTextContains(string testid, string expected, float timeoutSeconds)
        {
            // 再描画を待ち、対象要素の実表示文字列を検証する
            // Wait for re-rendering and verify the element's rendered text
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            UiElementSnapshot last = null;
            while (Time.realtimeSinceStartup <= deadline)
            {
                var snapshot = await FindUi(testid);
                last = snapshot;
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
            var lastDescription = last == null ? "no lookup completed" : $"{last.Status}, renderer: {last.Renderer}, visible: {last.Visible}, text: '{last.Text}'";
            throw new TimeoutException($"UI text did not contain '{expected}' (last: {lastDescription}): {testid}");
        }

        public static async UniTask SetText(string testid, string text)
        {
            await SetText(testid, text, ActionTimeoutSeconds);
        }

        public static async UniTask SetText(string testid, string text, float timeoutSeconds)
        {
            // 欄の出現・有効化を期限内で待ち、置き換えた値が保たれたら完了とする
            // Wait for the field to appear and enable, completing once the replaced value holds
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            var lastStatus = UiSetTextStatus.NotFound;
            while (Time.realtimeSinceStartup <= deadline)
            {
                var result = await PlaytestUiLocatorSession.Locator.SetText(testid, text);
                if (result.Status == UiSetTextStatus.Applied)
                {
                    Debug.Log($"[PlaytestUi] {testid} text set by {result.Renderer}");
                    return;
                }
                if (IsPermanentFailure(result.Status))
                    throw new InvalidOperationException($"UI text input rejected ({result.Status}, actual '{result.ActualValue}'): {testid}");
                lastStatus = result.Status;
                await UniTask.DelayFrame(2);
            }
            throw new TimeoutException($"UI text input did not apply (last: {lastStatus}): {testid}");

            #region Internal

            bool IsPermanentFailure(UiSetTextStatus status)
            {
                return status == UiSetTextStatus.NotTextField || status == UiSetTextStatus.ValueMismatch || status == UiSetTextStatus.Ambiguous;
            }

            #endregion
        }

        private static void LogResolved(UiElementSnapshot snapshot)
        {
            // どの描画系で解決したかを残す（段階1以降の切替確認に使う）
            // Record which renderer resolved the element (used to confirm cutovers from stage 1)
            Debug.Log($"[PlaytestUi] {snapshot.Testid} resolved by {snapshot.Renderer}");
        }
    }
}
