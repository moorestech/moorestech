using UnityEngine;

namespace Client.UitkPoc.Preview3D
{
    // ブロックのプレハブから描画用のメッシュだけを複製する。スクリプトを一切起動させないため
    // Clones only render meshes from a block prefab so no scripts ever run
    public static class RenderOnlyModelBuilder
    {
        public static GameObject Build(GameObject prefab, int layer)
        {
            var root = new GameObject($"Preview_{prefab.name}") { layer = layer };
            var prefabRoot = prefab.transform;
            foreach (var source in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                // プレハブ根からの相対姿勢をそのまま写す
                // Copy the pose relative to the prefab root
                var part = new GameObject(source.name) { layer = layer };
                part.transform.SetParent(root.transform, false);
                var matrix = prefabRoot.worldToLocalMatrix * source.transform.localToWorldMatrix;
                part.transform.localPosition = matrix.GetPosition();
                part.transform.localRotation = matrix.rotation;
                part.transform.localScale = matrix.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
            }
            return root;
        }

        public static Bounds CalculateBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
