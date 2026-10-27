using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.UiElements
{
    /// <summary>
    ///     testidをクリック可能かつ矩形が落ち着いた要素へ解決する
    ///     Resolves a testid to an element that is clickable with a settled rect
    /// </summary>
    internal static class UiElementScreenPointResolver
    {
        public static async UniTask<UiElementSnapshot> ResolveClickableUntil(IUiElementLocator locator, string testid, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            var lastStatus = UiElementLookupStatus.NotFound;

            // 未表示・被覆・変動中は期限内再試行
            // Retry hidden, covered or moving elements within the deadline
            while (Time.realtimeSinceStartup <= deadline)
            {
                var first = await locator.Find(testid, RemainingSeconds(deadline, testid));
                lastStatus = first.Status;
                if (deadline <= Time.realtimeSinceStartup) break;
                ThrowIfAmbiguous(first);
                if (IsClickable(first))
                {
                    // 2フレーム同矩形なら安定とみなす
                    // Same rect after two frames means the layout settled
                    await UniTask.DelayFrame(2);
                    if (deadline <= Time.realtimeSinceStartup) break;
                    var second = await locator.Find(testid, RemainingSeconds(deadline, testid));
                    lastStatus = second.Status;
                    if (deadline <= Time.realtimeSinceStartup) break;
                    ThrowIfAmbiguous(second);
                    if (IsClickable(second) && second.ScreenRect == first.ScreenRect) return second;
                }
                await UniTask.DelayFrame(2);
            }
            throw new TimeoutException($"UI element could not be resolved (last: {lastStatus}): {testid}");
        }

        internal static float RemainingSeconds(float deadline, string testid)
        {
            var remaining = deadline - Time.realtimeSinceStartup;
            if (!(0f < remaining)) throw new TimeoutException($"UI operation deadline expired: {testid}");
            return remaining;
        }

        public static bool IsClickable(UiElementSnapshot snapshot)
        {
            return snapshot.IsFound && snapshot.Clickable;
        }

        public static void ThrowIfAmbiguous(UiElementSnapshot snapshot)
        {
            // 重複testidはどれを押すか決められないため待たずに失敗させる
            // Duplicate testids cannot pick a target, so fail without waiting
            if (snapshot.Status == UiElementLookupStatus.Ambiguous)
                throw new InvalidOperationException($"UI testid matched multiple elements: {snapshot.Testid}");
        }
    }
}
