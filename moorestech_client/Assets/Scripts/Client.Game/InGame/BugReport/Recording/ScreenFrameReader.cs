using System;
using UnityEngine;

namespace Client.Game.InGame.BugReport.Recording
{
    // UI合成後の画面を固定解像度のRenderTextureへ写す。カメラを描き直すとWeb UI(CEF)もUIも映らない（前例 PlaytestScreenshot）
    // Copies the screen after UI composition into a fixed-size RenderTexture; re-rendering the camera shows neither the Web UI (CEF) nor the UI (cf. PlaytestScreenshot)
    public sealed class ScreenFrameReader : IDisposable
    {
        private readonly RenderTexture _scaledTexture;

        // 画面と同じ大きさの受け皿。解像度が変わると使えなくなるので、変化を見て作り直す
        // The screen-sized target; a resolution change invalidates it, so it is rebuilt when the size moves
        private RenderTexture _screenTexture;

        public ScreenFrameReader(int width, int height)
        {
            _scaledTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        }

        public RenderTexture CaptureScaledScreen()
        {
            EnsureScreenTexture();
            ScreenCapture.CaptureScreenshotIntoRenderTexture(_screenTexture);
            Graphics.Blit(_screenTexture, _scaledTexture);
            return _scaledTexture;
        }

        public void Dispose()
        {
            if (_screenTexture != null) _screenTexture.Release();
            _scaledTexture.Release();
        }

        private void EnsureScreenTexture()
        {
            if (_screenTexture != null && _screenTexture.width == Screen.width && _screenTexture.height == Screen.height) return;
            if (_screenTexture != null) _screenTexture.Release();
            _screenTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
        }
    }
}
