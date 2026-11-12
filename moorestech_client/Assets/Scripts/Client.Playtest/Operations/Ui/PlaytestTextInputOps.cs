using System;
using Client.Playtest.UiElements;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.Operations
{
    internal static class PlaytestTextInputOps
    {
        internal static async UniTask SetText(string testid, string text, float timeoutSeconds)
        {
            // 欄の出現・有効化を待ち、値保持で完了
            // Wait for the field to appear and enable; done when the value holds
            var generation = PlaytestUiLocatorSession.CaptureGeneration();
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            var lastStatus = UiSetTextStatus.NotFound;
            while (Time.realtimeSinceStartup <= deadline)
            {
                PlaytestUiLocatorSession.RequireGeneration(generation);
                var result = await PlaytestUiLocatorSession.Locator.SetText(testid, text, UiElementScreenPointResolver.RemainingSeconds(deadline, testid));
                PlaytestUiLocatorSession.RequireGeneration(generation);
                lastStatus = result.Status;
                if (deadline <= Time.realtimeSinceStartup) break;
                if (result.Status == UiSetTextStatus.Applied)
                {
                    Debug.Log($"[PlaytestUi] {testid} text set");
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

    }
}
