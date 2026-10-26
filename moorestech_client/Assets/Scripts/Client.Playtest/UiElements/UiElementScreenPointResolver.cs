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

            // 未表示・被覆中・変動中の要素は一時状態として期限内で再試行する
            // Retry hidden, covered, or moving elements as transient until the deadline
            while (Time.realtimeSinceStartup <= deadline)
            {
                var first = await locator.Find(testid);
                ThrowIfAmbiguous(first);
                lastStatus = first.Status;
                if (IsClickable(first))
                {
                    // 2フレーム後も同じ矩形ならレイアウトが落ち着いたとみなす
                    // Treat the layout as settled when the rect is unchanged two frames later
                    await UniTask.DelayFrame(2);
                    var second = await locator.Find(testid);
                    lastStatus = second.Status;
                    ThrowIfAmbiguous(second);
                    if (IsClickable(second) && second.ScreenRect == first.ScreenRect) return second;
                }
                await UniTask.DelayFrame(2);
            }
            throw new TimeoutException($"UI element could not be resolved (last: {lastStatus}): {testid}");
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
                throw new InvalidOperationException($"UI testid matched multiple elements ({snapshot.Renderer}): {snapshot.Testid}");
        }
    }
}
