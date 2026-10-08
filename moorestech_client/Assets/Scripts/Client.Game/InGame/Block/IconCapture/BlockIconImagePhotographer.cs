using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.Block.IconCapture
{
    public class BlockIconImagePhotographer : MonoBehaviour
    {
        // 固着時に箇所を名指しするための目印。ログ検索とテストが同じ1箇所を参照する（ADR 0063）
        // The marker that names a freeze site; log searches and tests share this single source (ADR 0063)
        internal const string CaptureLogPrefix = "[BlockIconCapture]";

        [SerializeField] private int iconSize = 512;
        [SerializeField] Camera cameraPrefab;

        public void PrepareForMainScene()
        {
            // 撮影ライトは撮影レイヤだけを照らすが、URPは最も明るい平行光を主光源に選ぶため主シーンでは消しておく
            // The capture light only hits the capture layer, but URP may pick it as the main light, so keep it off in the main scene
            foreach (var light in GetComponentsInChildren<Light>(true)) light.enabled = false;
        }

        public static Renderer[] GetRenderableComponents(GameObject target)
        {
            return target.GetComponentsInChildren<Renderer>();
        }

        public async UniTask<List<Texture2D>> TakeBlockIconImages(List<BlockPrefabInfo> blockObjectInfos)
        {
            var targets = blockObjectInfos.Select(info => (info.BlockObjectPrefab, info.BlockMasterElement.Name)).ToList();
            return await TakeIconImages(targets);
        }

        public async UniTask<List<Texture2D>> TakeIconImages(List<(GameObject prefab, string debugName)> targets)
        {
            var previousLightStates = EnableCaptureLights();

            // 撮影を終えたら主シーンの照明状態へ戻す
            // Restore scene lighting after the capture completes
            var result = await CaptureTargets(targets);
            RestoreCaptureLights(previousLightStates);
            return result;
        }

        private async UniTask<List<Texture2D>> CaptureTargets(List<(GameObject prefab, string debugName)> targets)
        {
            var result = new List<Texture2D>();
            var shot = new BlockIconCaptureShot(transform, cameraPrefab, iconSize);

            // 固着はメインスレッドごと止まるため、各段階へ入る直前に出したログだけが手掛かりになる
            // A freeze stops the main thread itself, so only logs emitted before each stage remain as evidence
            Debug.Log($"{CaptureLogPrefix} start count:{targets.Count}");
            var captureStartedAt = Time.realtimeSinceStartup;

            // 撮影資源を一件ずつ破棄し、対象数に依存する瞬間メモリ増加を防ぐ
            // Release capture resources one subject at a time to bound peak memory regardless of subject count
            for (var index = 0; index < targets.Count; index++)
            {
                var target = targets[index];
                var icon = await shot.TryCapture(target.prefab, target.debugName, $"{index + 1}/{targets.Count} {target.debugName}");

                // マスタ不備は起動時に大声で落とすため、Rendererの無い被写体は例外にする
                // Master defects must fail loudly at startup, so a subject without renderers throws
                if (icon == null) throw new System.Exception("撮影対象にメッシュレンダラーがありませんでした:" + target.prefab.name + " " + target.debugName);
                result.Add(icon);
                if (Application.isPlaying)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }

            Debug.Log($"{CaptureLogPrefix} completed count:{targets.Count} elapsed:{Time.realtimeSinceStartup - captureStartedAt:F1}s");
            return result;
        }

        private (Light light, bool wasEnabled)[] EnableCaptureLights()
        {
            var lights = GetComponentsInChildren<Light>(true);
            var previousStates = lights.Select(light => (light, light.enabled)).ToArray();
            foreach (var light in lights) light.enabled = true;
            return previousStates;
        }

        private static void RestoreCaptureLights((Light light, bool wasEnabled)[] previousStates)
        {
            foreach (var (light, wasEnabled) in previousStates) light.enabled = wasEnabled;
        }
    }
}
