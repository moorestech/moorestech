using System;
using Client.Playtest.Input;
using Client.Playtest.WebUi;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     DOMのtestidを安定した画面座標へ解決し、Web UIをセマンティック入力で操作する
    ///     Resolves DOM testids to stable screen coordinates and operates Web UI through semantic input
    /// </summary>
    public static class PlaytestWebUiOps
    {
        private const float ActionTimeoutSeconds = 15f;
        private const float QueryTimeoutSeconds = 1f;
        private const float MouseGlideSeconds = 0.3f;

        public static async UniTask ClickWebUi(string testid)
        {
            await ClickWebUi(testid, ActionTimeoutSeconds);
        }

        public static async UniTask ClickWebUi(string testid, float timeoutSeconds)
        {
            // React描画が安定した要素中心へ移動し、実プレイヤーと同じマウス経路でクリックする
            // Move to the center of a React-stable element and click through the same mouse path as a real player
            var resolution = await ResolveScreenPointUntil(testid, timeoutSeconds);
            await SemanticInput.MouseGlideTo(resolution.ScreenPosition, MouseGlideSeconds);
            await UniTask.DelayFrame(1);
            await SemanticInput.Click();
        }

        public static async UniTask HoverWebUi(string testid)
        {
            // クリックと同じ安定性判定を通し、対象中心へのポインタ移動だけを行う
            // Apply the same stability check as clicks, then only move the pointer to the target center
            var resolution = await ResolveScreenPointUntil(testid, ActionTimeoutSeconds);
            await SemanticInput.MouseGlideTo(resolution.ScreenPosition, MouseGlideSeconds);
            await UniTask.DelayFrame(1);
        }

        public static async UniTask WaitWebUiElement(string testid, float timeoutSeconds)
        {
            // DOM上で可視かつ中心点が実際にヒットするまで応答を繰り返し確認する
            // Repeatedly check responses until the element is visible and its center genuinely hits
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup <= deadline)
            {
                var result = await QueryWithinDeadline(testid, deadline);
                if (IsUsable(result)) return;
                await UniTask.DelayFrame(2);
            }
            throw new TimeoutException($"Web UI element did not become available: {testid}");
        }

        private static async UniTask<ScreenPointResolution> ResolveScreenPointUntil(string testid, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;

            // 解決失敗を指定期限内で再試行し、未表示・被覆中・変動中の要素を一時状態として扱う
            // Retry resolution within the provided timeout, treating hidden, covered, or moving elements as transient
            while (Time.realtimeSinceStartup <= deadline)
            {
                var resolution = await TryResolveScreenPoint();
                if (resolution.Resolved) return resolution;
                await UniTask.DelayFrame(2);
            }
            throw new TimeoutException($"Web UI element could not be resolved: {testid}");

            #region Internal

            async UniTask<ScreenPointResolution> TryResolveScreenPoint()
            {
                // 連続する2応答の矩形一致でReactレイアウトの安定を確認してから座標変換する
                // Require matching rectangles in two consecutive responses before mapping the settled React layout
                var first = await QueryWithinDeadline(testid, deadline);
                if (!IsUsable(first)) return new ScreenPointResolution(false, default);
                await UniTask.DelayFrame(2);

                var second = await QueryWithinDeadline(testid, deadline);
                if (!IsUsable(second) || !HasSameRect(first, second) ||
                    !PlaytestDomQuery.TryGetScreenCenter(second, out var screenPosition))
                {
                    return new ScreenPointResolution(false, default);
                }
                return new ScreenPointResolution(true, screenPosition);
            }

            bool HasSameRect(DomQueryResult left, DomQueryResult right)
            {
                return left.X == right.X && left.Y == right.Y && left.Width == right.Width && left.Height == right.Height;
            }

            #endregion
        }

        private static async UniTask<DomQueryResult> QueryWithinDeadline(string testid, float deadline)
        {
            var remainingSeconds = deadline - Time.realtimeSinceStartup;
            if (remainingSeconds <= 0f) return new DomQueryResult();
            return await PlaytestDomQuery.Query(testid, Mathf.Min(QueryTimeoutSeconds, remainingSeconds));
        }

        private static bool IsUsable(DomQueryResult result)
        {
            return result != null && result.Found && result.HitTestPassed;
        }

        private readonly struct ScreenPointResolution
        {
            public readonly bool Resolved;
            public readonly Vector2 ScreenPosition;

            public ScreenPointResolution(bool resolved, Vector2 screenPosition)
            {
                Resolved = resolved;
                ScreenPosition = screenPosition;
            }
        }
    }
}
