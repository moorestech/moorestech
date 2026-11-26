using System.Collections.Generic;
using Client.Common.PrefabPostProcess;
using Client.Common.PrefabPostProcess.MeshMerge;
using Client.Game.InGame.BlockSystem.StateProcessor;
using UnityEngine;

namespace Client.Game.InGame.Block.GearRotation.PrefabPostProcess
{
    /// <summary>
    /// 回す部品をシェーダー回転へ移せるかを判定し、まとめるRendererを集める
    /// Decides whether a rotating part can move to shader rotation and collects the renderers to merge
    /// </summary>
    public static class GearRotationMergeEligibility
    {
        public static bool TryCollect(GameObject template, TransformRotationInfo info, PrefabPostProcessContext context, Shader sourceShader, out List<MeshRenderer> renderers, out string reason)
        {
            renderers = new List<MeshRenderer>();
            var rotationTransform = info.RotationTransform;
            reason = rotationTransform == null ? "rotationTransformが未設定"
                : rotationTransform == template.transform ? "rotationTransformがprefabのルート"
                : FindUnsupportedComponent(rotationTransform);
            if (reason != null) return false;

            foreach (var renderer in rotationTransform.GetComponentsInChildren<MeshRenderer>(true))
            {
                // 元から表示していないRendererはまとめずそのまま残す。今も描かれていないので見た目は変わらない
                // Renderers hidden from the start are left in place unmerged; they are not drawn today either
                if (!renderer.enabled || !IsActiveWithin(renderer.transform, rotationTransform)) continue;
                reason = CheckRenderer(renderer, context, sourceShader);
                if (reason != null) return false;
                renderers.Add(renderer);
            }
            if (renderers.Count == 0) reason = "まとめるMeshRendererが無い";
            return reason == null;
        }

        // 下にTransform・MeshFilter・MeshRenderer・Collider以外があると、まとめると動きや参照が壊れうる
        // Anything other than Transform, MeshFilter, MeshRenderer or Collider underneath could break behavior or references when merged
        private static string FindUnsupportedComponent(Transform rotationTransform)
        {
            foreach (var component in rotationTransform.GetComponentsInChildren<Component>(true))
            {
                if (component is Transform or MeshFilter or MeshRenderer or Collider) continue;
                return $"下に{component.GetType().Name}がある({component.name})";
            }
            return null;
        }

        private static string CheckRenderer(MeshRenderer renderer, PrefabPostProcessContext context, Shader sourceShader)
        {
            if (context.IsClaimed(renderer)) return $"{renderer.name}は別の後処理が使用済み";
            if (!MeshMerger.CanMerge(renderer, out var mergeReason)) return $"{renderer.name}: {mergeReason}";
            foreach (var material in renderer.sharedMaterials)
            {
                if (material == null || material.shader != sourceShader) return $"{renderer.name}のマテリアル{(material == null ? "null" : material.name)}が{sourceShader.name}ではない";
            }
            return null;
        }

        private static bool IsActiveWithin(Transform target, Transform root)
        {
            for (var current = target; current != null; current = current.parent)
            {
                if (!current.gameObject.activeSelf) return false;
                if (current == root) return true;
            }
            return true;
        }
    }
}
