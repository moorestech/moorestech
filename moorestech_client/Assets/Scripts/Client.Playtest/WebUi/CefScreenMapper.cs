using System.Reflection;
using Client.Game.InGame.UI.UIState;
using CefUnity.Interop;
using CefUnity.Runtime;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Client.Playtest.WebUi
{
    /// <summary>
    ///     Unityスクリーン座標とCEFブラウザ座標を相互変換する
    ///     Converts between Unity screen coordinates and CEF browser coordinates
    /// </summary>
    public static class CefScreenMapper
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo BrowserField = typeof(CefUnityBrowserSample).GetField("_browser", PrivateInstance);
        private static readonly FieldInfo CurrentWidthField = typeof(CefUnityBrowserSample).GetField("_currentWidth", PrivateInstance);
        private static readonly FieldInfo CurrentHeightField = typeof(CefUnityBrowserSample).GetField("_currentHeight", PrivateInstance);

        private static CefUnityBrowserSample _sample;
        private static RawImage _rawImage;

        public static bool IsWebUiAvailable()
        {
            return WebUiScreenGate.IsWebUiMode && TryGetBrowser(out _);
        }

        public static bool TryGetBrowser(out Browser browser)
        {
            var sample = GetSample();
            browser = sample == null ? null : (Browser)BrowserField.GetValue(sample);
            return browser != null;
        }

        public static bool TryScreenToBrowser(Vector2 screenPosition, out Vector2Int browserPosition)
        {
            browserPosition = default;
            if (!TryGetMappingContext(out var rawImage, out var browserWidth, out var browserHeight)) return false;

            // Canvasの描画方式に合わせてスクリーン座標をRawImageローカル座標へ変換する
            // Convert the screen point to RawImage local space using the canvas render mode
            var rectTransform = rawImage.rectTransform;
            var canvas = rawImage.canvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPosition, camera, out var localPosition)) return false;

            // 矩形内を正規化し、表示テクスチャの上下反転をCEFの上原点へ戻す
            // Normalize within the rect and undo the display texture flip for CEF's top origin
            var rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return false;
            var normalizedX = (localPosition.x - rect.x) / rect.width;
            var normalizedY = (localPosition.y - rect.y) / rect.height;
            if (normalizedX < 0f || 1f < normalizedX || normalizedY < 0f || 1f < normalizedY) return false;

            normalizedY = 1f - normalizedY;
            var browserX = Mathf.Clamp((int)(normalizedX * browserWidth), 0, browserWidth - 1);
            var browserY = Mathf.Clamp((int)(normalizedY * browserHeight), 0, browserHeight - 1);
            browserPosition = new Vector2Int(browserX, browserY);
            return true;
        }

        public static bool TryBrowserToScreen(Vector2 browserPosition, out Vector2 screenPosition)
        {
            screenPosition = default;
            if (!TryGetMappingContext(out var rawImage, out var browserWidth, out var browserHeight)) return false;
            if (browserPosition.x < 0f || browserWidth < browserPosition.x || browserPosition.y < 0f || browserHeight < browserPosition.y) return false;

            // CEFの上原点座標をRawImageの下原点ローカル座標へ逆変換する
            // Invert CEF's top-origin coordinates into RawImage's bottom-origin local space
            var normalizedX = browserPosition.x / browserWidth;
            var normalizedY = 1f - browserPosition.y / browserHeight;
            var rectTransform = rawImage.rectTransform;
            var rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return false;
            var localPosition = new Vector2(rect.x + normalizedX * rect.width, rect.y + normalizedY * rect.height);

            // Canvasのカメラを通してワールド座標からUnityスクリーン座標へ戻す
            // Return from world space to Unity screen space through the canvas camera
            var canvas = rawImage.canvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var worldPosition = rectTransform.TransformPoint(localPosition);
            screenPosition = RectTransformUtility.WorldToScreenPoint(camera, worldPosition);
            return true;
        }

        private static CefUnityBrowserSample GetSample()
        {
            if (_sample != null)
            {
                // 子RawImageだけ再生成された場合はUnityのfake-nullを検知して取り直す
                // Reacquire the child RawImage when Unity fake-null indicates it was recreated
                if (_rawImage == null) _rawImage = _sample.GetComponentInChildren<RawImage>(true);
                return _sample;
            }

            // シーン配置済みCEFサンプルを初回だけ探索し、子階層のRawImageも保持する
            // Find the scene-provided CEF sample once and retain its child RawImage too
            _sample = Object.FindFirstObjectByType<CefUnityBrowserSample>();
            _rawImage = _sample == null ? null : _sample.GetComponentInChildren<RawImage>(true);
            return _sample;
        }

        private static bool TryGetMappingContext(out RawImage rawImage, out int browserWidth, out int browserHeight)
        {
            var sample = GetSample();
            rawImage = _rawImage;
            browserWidth = sample == null ? 0 : (int)CurrentWidthField.GetValue(sample);
            browserHeight = sample == null ? 0 : (int)CurrentHeightField.GetValue(sample);
            return rawImage != null && 0 < browserWidth && 0 < browserHeight;
        }
    }
}
