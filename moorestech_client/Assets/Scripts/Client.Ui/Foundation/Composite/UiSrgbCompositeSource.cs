using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Client.Ui.Foundation.Composite
{
    /// <summary>
    /// URP の差し込み口が読む、いま画面へ重ねる UI のパネル RT と、重ねるカメラの選び方
    /// The UI panel RT the URP hook composites now, and which camera composites it
    /// </summary>
    internal static class UiSrgbCompositeSource
    {
        private static RenderTexture _texture;
        private static int _chosenFrame = -1;
        private static Camera _chosenCamera;
        private static bool _reportedNotComposited;
        private static bool _reportedCameraOverflow;
        private static int _lastCompositedFrame;
        private static readonly Camera[] CameraBuffer = new Camera[32];

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

        // パネル RT があるのに数フレーム合成パスが走っていない状態へ入った瞬間に 1 回だけ知らせる（カメラ 0 台・全 RT 出力などで UI が画面から消える）
        // Report once on entering the state where a panel RT exists but the composite pass has not run for several frames (no screen camera etc.; the UI disappears)
        public static void ReportWhenNotComposited()
        {
            if (_texture == null || _reportedNotComposited) return;
            if (Time.frameCount - _lastCompositedFrame <= 3) return;
            _reportedNotComposited = true;
            Debug.LogError("[UiSrgbCompositeSource] the UI composite pass has not run for several frames (no screen camera to composite onto?); the UI is not shown");
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
                var count = Camera.GetAllCameras(CameraBuffer);
                ReportWhenCamerasOverflow();
                Camera last = null;
                for (var i = 0; i < count; i++)
                {
                    var candidate = CameraBuffer[i];
                    if (candidate.targetTexture != null || candidate.cameraType != CameraType.Game) continue;
                    if (candidate.TryGetComponent<UniversalAdditionalCameraData>(out var data) && data.renderType != CameraRenderType.Base) continue;
                    if (last == null || last.depth <= candidate.depth) last = candidate;
                }
                return last;
            }

            // 有効カメラがバッファより多いときに 1 回だけ知らせる（超えた分は選定から漏れる）
            // Report once when enabled cameras exceed the buffer (the excess is left out of the choice)
            void ReportWhenCamerasOverflow()
            {
                var overflow = Camera.allCamerasCount > CameraBuffer.Length;
                if (!overflow) { _reportedCameraOverflow = false; return; }
                if (_reportedCameraOverflow) return;
                _reportedCameraOverflow = true;
                Debug.LogError($"[UiSrgbCompositeSource] more than {CameraBuffer.Length} cameras are enabled; cameras beyond that are ignored when choosing the UI composite camera");
            }

            // 重ねるカメラのスタックの最後の overlay カメラが代わりに重ねる
            // The last overlay camera of the compositing camera's stack composites instead
            bool IsOverlayOf(Camera overlay)
            {
                if (_chosenCamera == null || !_chosenCamera.TryGetComponent<UniversalAdditionalCameraData>(out var data)) return false;
                return data.cameraStack.Contains(overlay);
            }

            #endregion
        }
    }
}
