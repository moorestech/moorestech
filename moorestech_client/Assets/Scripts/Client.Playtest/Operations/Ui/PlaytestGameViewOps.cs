using System;
using System.Threading;
using Client.Ui.Editor.Capture;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.Operations.Ui
{
    /// <summary>
    /// Game View の解像度をシナリオから切り替える（1280×720・2560×1080 の並べ比べ用）
    /// Switches the Game View resolution from a scenario (for 1280x720 and 2560x1080 comparisons)
    /// </summary>
    public static class PlaytestGameViewOps
    {
        private const float ResolutionApplyTimeoutSeconds = 5f;

        public static async UniTask SetResolution(int width, int height, CancellationToken token)
        {
            var selected = GameViewResolution.Set(width, height);
            Debug.Log($"[PlaytestGameViewOps] Game View {width}x{height}: {selected}");

            // 画面の寸法は次の描画から変わるので、Screen が追いつくまでフレームで待つ
            // The screen size changes from the next render, so wait per frame until Screen catches up
            var startTime = Time.realtimeSinceStartup;
            while (Screen.width != width || Screen.height != height)
            {
                if (ResolutionApplyTimeoutSeconds < Time.realtimeSinceStartup - startTime)
                {
                    throw new TimeoutException($"Game View did not reach {width}x{height} within {ResolutionApplyTimeoutSeconds}s (Screen {Screen.width}x{Screen.height})");
                }
                await UniTask.Yield(token);
            }
        }
    }
}
