using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Client.Ui.Foundation.Composite
{
    /// <summary>
    /// 合成する UI の RT とカメラの選定
    /// The UI RT to composite and the camera selection
    /// </summary>
    internal static class UiSrgbCompositeSource
    {
        private static RenderTexture _texture;
        private static int _chosenFrame = -1;
        private static Camera _chosenCamera;
        private static bool _reportedNotComposited;
        private static int _lastCompositedFrame;
        private static Camera[] _cameraBuffer = new Camera[32];

        public static void SetTexture(RenderTexture texture)
        {
            _texture = texture;
            _lastCompositedFrame = Time.frameCount;
            _reportedNotComposited = false;
        }

        public static void MarkComposited()
        {
            _lastCompositedFrame = Time.frameCount;
            _reportedNotComposited = false;
        }

        // RT があるのに合成が数フレーム走らない状態へ入ったら 1 回知らせる。Game ビューが隠れた PlayMode や batchmode でも起きるので警告にする
        // Warn once when the RT exists but compositing has not run for frames; a hidden Game view or batchmode also causes it, so it is a warning
        public static void ReportWhenNotComposited()
        {
            if (_texture == null || _reportedNotComposited) return;
            if (Time.frameCount - _lastCompositedFrame <= 3) return;
            _reportedNotComposited = true;
            Debug.LogWarning("[UiSrgbCompositeSource] the UI composite pass has not run for several frames (no screen camera to composite onto?); the UI is not shown");
        }

        public static void ClearTexture(RenderTexture texture)
        {
            if (_texture == texture) _texture = null;
        }

        public static bool TryGetTexture(out RenderTexture texture)
        {
            texture = _texture;
            return texture != null;
        }

        // 画面へ出す base カメラのうち最後に描くもの（depth 最大）だけが重ねる。二度重ねると半透明が濃くなる
        // Only the last base camera drawing to the screen (largest depth) composites; compositing twice would darken translucency
        public static bool IsCompositingCamera(Camera camera)
        {
            if (_chosenFrame != Time.frameCount)
            {
                _chosenFrame = Time.frameCount;
                _chosenCamera = ChooseLastScreenCamera();
            }
            return camera == _chosenCamera || IsOverlayOf(camera);

            #region Internal

            Camera ChooseLastScreenCamera()
            {
                // 配列が足りないと GetAllCameras が例外を投げるので先に広げる
                // GetAllCameras throws when the array is too small, so grow it first
                if (_cameraBuffer.Length < Camera.allCamerasCount) _cameraBuffer = new Camera[Camera.allCamerasCount];
                var count = Camera.GetAllCameras(_cameraBuffer);
                Camera last = null;
                for (var i = 0; i < count; i++)
                {
                    var candidate = _cameraBuffer[i];
                    if (candidate.targetTexture != null || candidate.cameraType != CameraType.Game) continue;
                    if (candidate.TryGetComponent<UniversalAdditionalCameraData>(out var data) && data.renderType != CameraRenderType.Base) continue;
                    if (last == null || last.depth <= candidate.depth) last = candidate;
                }
                return last;
            }

            // 最後の overlay カメラが代わりに重ねる
            // The last overlay camera composites instead
            bool IsOverlayOf(Camera overlay)
            {
                if (_chosenCamera == null || !_chosenCamera.TryGetComponent<UniversalAdditionalCameraData>(out var data)) return false;
                return data.cameraStack.Contains(overlay);
            }

            #endregion
        }
    }
}
