using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    public class BlockIconImagePhotographer : MonoBehaviour
    {
        // 固着時に箇所を名指しするための目印。ログ検索とテストが同じ1箇所を参照する（ADR 0063）
        // The marker that names a freeze site; log searches and tests share this single source (ADR 0063)
        internal const string CaptureLogPrefix = "[BlockIconCapture]";

        [SerializeField] private int iconSize = 512;
        [SerializeField] Camera cameraPrefab;

        public async UniTask<List<Texture2D>> TakeBlockIconImages(List<BlockPrefabInfo> blockObjectInfos)
        {
            var targets = blockObjectInfos.Select(info => (info.BlockObjectPrefab, info.BlockMasterElement.Name)).ToList();
            return await TakeIconImages(targets);
        }

        public async UniTask<List<Texture2D>> TakeIconImages(List<(GameObject prefab, string debugName)> targets)
        {
            var result = new List<Texture2D>();

            // 固着はメインスレッドごと止まるため、各段階へ入る直前に出したログだけが手掛かりになる
            // A freeze stops the main thread itself, so only logs emitted before each stage remain as evidence
            Debug.Log($"{CaptureLogPrefix} start count:{targets.Count}");
            var captureStartedAt = Time.realtimeSinceStartup;

            // 撮影資源を一件ずつ破棄し、対象数に依存する瞬間メモリ増加を防ぐ
            // Release capture resources one subject at a time to bound peak memory regardless of subject count
            for (var index = 0; index < targets.Count; index++)
            {
                var target = targets[index];
                result.Add(await GetIcon(target.prefab, target.debugName, index, targets.Count));
                if (Application.isPlaying)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }

            Debug.Log($"{CaptureLogPrefix} completed count:{targets.Count} elapsed:{Time.realtimeSinceStartup - captureStartedAt:F1}s");
            return result;

            #region Internal

            async UniTask<Texture2D> GetIcon(GameObject capturePrefab, string captureDebugName, int captureIndex, int captureCount)
            {
                var captureProgress = $"{captureIndex + 1}/{captureCount} {captureDebugName}";

                // 撮影1件の起点。ここで最後のログが止まっていれば複製〜Camera生成側の固着
                // The start of one capture; a log stopping here means the freeze is on the clone-to-Camera setup side
                var iconStartedAt = Time.realtimeSinceStartup;
                Debug.Log($"{CaptureLogPrefix} {captureProgress} stage:setup");

                var captureTarget = Instantiate(capturePrefab, transform);
                captureTarget.transform.position = Vector3.zero;
                captureTarget.transform.rotation = Quaternion.identity;
                captureTarget.transform.localScale = Vector3.one;

                var bounds = captureTarget.GetComponentsInChildren<Renderer>().Select(b => b.bounds).ToList();
                if (bounds.Count == 0)
                {
                    throw new System.Exception("撮影対象にメッシュレンダラーがありませんでした:" + captureTarget.name + " " + captureDebugName);
                }
                var center = bounds.Select(b => b.center).Aggregate((b1, b2) => b1 + b2) / bounds.Count;

                // カメラを上30度・Y45度に設定
                // Aim the Camera 30 degrees down and 45 degrees around the Y axis
                var blockImageCamera = Instantiate(cameraPrefab);
                blockImageCamera.transform.rotation = Quaternion.Euler(30f, 45f, 0f);

                var minPos = bounds.Select(b => b.min).Aggregate(Vector3.Min);
                var maxPos = bounds.Select(b => b.max).Aggregate(Vector3.Max);
                var maxSize = Vector3.Distance(minPos, maxPos);

                // 視野角と最大寸法から距離算出
                // Derive the distance from the field of view and maximum extent
                float fovRad = blockImageCamera.fieldOfView * Mathf.Deg2Rad;
                float distance = (maxSize * 0.5f) / Mathf.Tan(fovRad * 0.5f);

                blockImageCamera.transform.position = center - blockImageCamera.transform.forward * (distance * 0.8f);
                blockImageCamera.transform.LookAt(center);

                // カメラ背景をアルファ付き透明に設定
                // Configure a transparent Camera background
                blockImageCamera.clearFlags = CameraClearFlags.SolidColor;
                blockImageCamera.backgroundColor = Color.white;

                if (Application.isPlaying)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }

                // GPUへ渡す直前。ここで最後のログが止まっていればRender側の固着
                // Right before handing work to the GPU; a log stopping here means the freeze is on the Render side
                Debug.Log($"{CaptureLogPrefix} {captureProgress} stage:render");

                // ARGB32で透明度を保持
                // Preserve alpha with an ARGB32 RenderTexture
                var renderTexture = new RenderTexture(iconSize, iconSize, 24, RenderTextureFormat.ARGB32)
                {
                    name = $"BlockIconCapture:{captureDebugName}",
                    useMipMap = false,
                    autoGenerateMips = false
                };

                blockImageCamera.targetTexture = renderTexture;
                blockImageCamera.Render();
                blockImageCamera.targetTexture = null;

                // 同期読み戻しの直前。ここで止まっていればReadPixelsかApplyの固着
                // Right before the synchronous readback; a log stopping here means the freeze is in ReadPixels or Apply
                Debug.Log($"{CaptureLogPrefix} {captureProgress} stage:readback");

                // RGBA32へ画素を読み込む
                // Read pixels into an RGBA32 Texture2D
                var texture = new Texture2D(iconSize, iconSize, TextureFormat.RGBA32, false);
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, iconSize, iconSize), 0, 0);
                texture.Apply();
                RenderTexture.active = null;

                // 画素取得の完了直後。ここで止まっていれば破棄側の固着で、読み戻し窓とは切り分けられる
                // Right after the pixels are in hand; a log stopping here isolates the freeze to the release side
                Debug.Log($"{CaptureLogPrefix} {captureProgress} stage:captured");

                // 撮影対象・一時描画資源・撮影Cameraを同じ寿命で破棄する
                // Destroy the subject, temporary render resource, and capture Camera within the same lifetime
                if (Application.isPlaying)
                {
                    Destroy(captureTarget);
                    Destroy(renderTexture);
                    Destroy(blockImageCamera.gameObject);
                }
                else
                {
                    DestroyImmediate(captureTarget);
                    DestroyImmediate(renderTexture);
                    DestroyImmediate(blockImageCamera.gameObject);
                }

                Debug.Log($"{CaptureLogPrefix} {captureProgress} stage:done elapsed:{(Time.realtimeSinceStartup - iconStartedAt) * 1000f:F0}ms");
                return texture;
            }

            #endregion
        }

    }
}
