using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Playtest.Recording
{
    // UI 合成後の Game View を縦横比を保った固定寸法の動画フレームに収める
    // Fit the composited Game View within the fixed movie frame without distorting its aspect ratio
    internal sealed class ResizingGameViewCapture : IDisposable
    {
        public RenderTexture Output { get; }
        private RenderTexture _native;
        private RenderTexture _fitted;

        private bool _recording;

        public ResizingGameViewCapture(int width, int height)
        {
            Output = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            ClearOutput();
            _recording = true;
            CaptureFrames().Forget();
        }

        private async UniTask CaptureFrames()
        {
            while (true)
            {
                await Awaitable.EndOfFrameAsync();
                if (!_recording) break;
                if (_native == null || _native.width != Screen.width || _native.height != Screen.height)
                    Resize();

                // UI と HUD まで描画された実寸のフレームを縮小し、黒帯の中央へ置く
                // Scale the fully composited frame including UI and HUD, then center it inside the matte
                var previous = RenderTexture.active;
                ScreenCapture.CaptureScreenshotIntoRenderTexture(_native);
                Graphics.Blit(_native, _fitted);
                Graphics.CopyTexture(_fitted, 0, 0, 0, 0, _fitted.width, _fitted.height, Output, 0, 0,
                    (Output.width - _fitted.width) / 2, (Output.height - _fitted.height) / 2);
                RenderTexture.active = previous;
            }
        }

        private void Resize()
        {
            ReleaseInputs();
            var scale = Mathf.Min((float)Output.width / Screen.width, (float)Output.height / Screen.height);
            _native = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            _fitted = RenderTexture.GetTemporary(Mathf.Max(1, Mathf.RoundToInt(Screen.width * scale)),
                Mathf.Max(1, Mathf.RoundToInt(Screen.height * scale)), 0, RenderTextureFormat.ARGB32);
            ClearOutput();
        }

        private void ClearOutput()
        {
            // 比率が変わった直後も以前のフレームの端を残さない
            // Clear the previous frame's edges when the aspect ratio changes
            var previous = RenderTexture.active;
            RenderTexture.active = Output;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = previous;
        }

        private void ReleaseInputs()
        {
            if (_native != null) RenderTexture.ReleaseTemporary(_native);
            if (_fitted != null) RenderTexture.ReleaseTemporary(_fitted);
        }

        public void Dispose()
        {
            _recording = false;
            ReleaseInputs();
            RenderTexture.ReleaseTemporary(Output);
        }
    }
}
