using System.Linq;
using Client.Common;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.Block.IconCapture
{
    /// <summary>
    ///     被写体1件を複製し、撮影専用レイヤで撮って資源を同じ寿命で破棄する
    ///     Clones one subject, shoots it on the capture-only layer, and releases resources together
    /// </summary>
    public class BlockIconCaptureShot
    {
        private readonly Transform _captureParent;
        private readonly Camera _cameraPrefab;
        private readonly int _iconSize;

        public BlockIconCaptureShot(Transform captureParent, Camera cameraPrefab, int iconSize)
        {
            _captureParent = captureParent;
            _cameraPrefab = cameraPrefab;
            _iconSize = iconSize;
        }

        // Renderer が無い被写体は null を返す。例外にするかログにするかは呼び出し側が決める
        // Returns null for a subject without renderers; the caller decides between throwing and logging
        public async UniTask<Texture2D> TryCapture(GameObject capturePrefab, string captureDebugName, string captureProgress)
        {
            // 撮影1件の起点。ここで最後のログが止まっていれば複製〜Camera生成側の固着
            // The start of one capture; a log stopping here means the freeze is on the clone-to-Camera setup side
            var iconStartedAt = Time.realtimeSinceStartup;
            Debug.Log($"{BlockIconImagePhotographer.CaptureLogPrefix} {captureProgress} stage:setup");

            var captureTarget = Object.Instantiate(capturePrefab, _captureParent);
            captureTarget.SetActive(true);
            captureTarget.transform.localPosition = Vector3.zero;
            captureTarget.transform.rotation = Quaternion.identity;
            captureTarget.transform.localScale = Vector3.one;
            MoveBlockLayerToIconCaptureLayer(captureTarget);

            var bounds = captureTarget.GetComponentsInChildren<Renderer>().Select(b => b.bounds).ToList();
            if (bounds.Count == 0)
            {
                DestroyByPlayState(captureTarget);
                return null;
            }

            // 生成直後にARGB32の出力先を固定し、メイン画面へ描画される隙を作らない
            // Bind an ARGB32 target right after creation so the Camera never draws to the main screen
            var blockImageCamera = Object.Instantiate(_cameraPrefab);
            var renderTexture = new RenderTexture(_iconSize, _iconSize, 24, RenderTextureFormat.ARGB32)
            {
                name = $"BlockIconCapture:{captureDebugName}",
                useMipMap = false,
                autoGenerateMips = false
            };
            blockImageCamera.targetTexture = renderTexture;
            blockImageCamera.aspect = (float)renderTexture.width / renderTexture.height;
            AimCamera(blockImageCamera, bounds.Select(b => b.min).Aggregate(Vector3.Min), bounds.Select(b => b.max).Aggregate(Vector3.Max));

            if (Application.isPlaying)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            // GPUへ渡す直前。ここで最後のログが止まっていればRender側の固着
            // Right before handing work to the GPU; a log stopping here means the freeze is on the Render side
            Debug.Log($"{BlockIconImagePhotographer.CaptureLogPrefix} {captureProgress} stage:render");

            // PlayModeは通常の描画フレームへ委ね、同期Render内でのメインスレッド固着を避ける。
            // Let the normal PlayMode frame render the Camera to avoid a main-thread freeze inside synchronous Render.
            if (Application.isPlaying)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            else
            {
                blockImageCamera.Render();
            }
            blockImageCamera.enabled = false;
            blockImageCamera.targetTexture = null;

            // 同期読み戻しの直前。ここで止まっていればReadPixelsかApplyの固着
            // Right before the synchronous readback; a log stopping here means the freeze is in ReadPixels or Apply
            Debug.Log($"{BlockIconImagePhotographer.CaptureLogPrefix} {captureProgress} stage:readback");
            var texture = new Texture2D(_iconSize, _iconSize, TextureFormat.RGBA32, false);
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, _iconSize, _iconSize), 0, 0);
            texture.Apply();
            RenderTexture.active = null;

            // 画素取得の完了直後。ここで止まっていれば破棄側の固着で、読み戻し窓とは切り分けられる
            // Right after the pixels are in hand; a log stopping here isolates the freeze to the release side
            Debug.Log($"{BlockIconImagePhotographer.CaptureLogPrefix} {captureProgress} stage:captured");

            // 撮影対象・一時描画資源・撮影Cameraを同じ寿命で破棄する
            // Destroy the subject, temporary render resource, and capture Camera within the same lifetime
            DestroyByPlayState(captureTarget);
            DestroyByPlayState(renderTexture);
            DestroyByPlayState(blockImageCamera.gameObject);

            Debug.Log($"{BlockIconImagePhotographer.CaptureLogPrefix} {captureProgress} stage:done elapsed:{(Time.realtimeSinceStartup - iconStartedAt) * 1000f:F0}ms");
            return texture;
        }

        private static void MoveBlockLayerToIconCaptureLayer(GameObject captureTarget)
        {
            // 撮影カメラ・ライトは撮影レイヤだけを見るため、従来写していたBlock層の部位だけを移す
            // Capture camera and light see only the capture layer, so move just the Block-layer parts they used to shoot
            foreach (var child in captureTarget.GetComponentsInChildren<Transform>(true))
            {
                if (child.gameObject.layer == LayerConst.BlockLayer) child.gameObject.layer = LayerConst.IconCaptureLayer;
            }
        }

        private static void AimCamera(Camera blockImageCamera, Vector3 minPos, Vector3 maxPos)
        {
            // 部品数の偏りに依存せず、外形全体の中心へ上30度・Y45度から向ける
            // Aim from 30 degrees down and 45 degrees around Y at the union center, independent of part counts
            blockImageCamera.transform.rotation = Quaternion.Euler(30f, 45f, 0f);
            var center = (minPos + maxPos) * 0.5f;
            var radius = Vector3.Distance(minPos, maxPos) * 0.5f;

            // 狭い側の視野へ外接球を余白付きで収め、近クリップ面も避ける
            // Fit the bounding sphere inside the narrower field of view with padding and near-plane clearance
            var verticalHalfFov = blockImageCamera.fieldOfView * Mathf.Deg2Rad * 0.5f;
            var horizontalHalfFov = Mathf.Atan(Mathf.Tan(verticalHalfFov) * blockImageCamera.aspect);
            var distance = Mathf.Max(radius / Mathf.Sin(Mathf.Min(verticalHalfFov, horizontalHalfFov)) * 1.05f, radius + blockImageCamera.nearClipPlane);
            blockImageCamera.transform.position = center - blockImageCamera.transform.forward * distance;
            blockImageCamera.transform.LookAt(center);

            // 白背景の単色で塗りつぶす
            // Clear to a solid white background
            blockImageCamera.clearFlags = CameraClearFlags.SolidColor;
            blockImageCamera.backgroundColor = Color.white;
        }

        private static void DestroyByPlayState(Object target)
        {
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }
    }
}
