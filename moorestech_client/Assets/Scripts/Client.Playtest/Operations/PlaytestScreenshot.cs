using System;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     GameViewのスクリーンショットを撮影して実行ディレクトリへ保存する
    ///     Captures a GameView screenshot into the run directory
    /// </summary>
    public static class PlaytestScreenshot
    {
        public static async UniTask<string> Capture(string runDirectory, string name)
        {
            var path = Path.Combine(runDirectory, $"{name}.png");
            if (File.Exists(path)) File.Delete(path);

            // ScreenCaptureはUIオーバーレイ込みのGameViewを撮る（フォーカス不要）
            // ScreenCapture takes the GameView including UI overlays (no focus needed)
            ScreenCapture.CaptureScreenshot(path);

            // 書き出しは非同期なのでファイル出現をフレームポーリングで待つ
            // The write is asynchronous, so poll per frame until the file appears
            var startTime = Time.realtimeSinceStartup;
            while (!File.Exists(path))
            {
                if (10f < Time.realtimeSinceStartup - startTime)
                {
                    throw new TimeoutException($"screenshot '{name}' was not written within 10s");
                }
                await UniTask.Yield();
            }
            return path;
        }
    }
}
