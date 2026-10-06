using Game.MapGeneration.Surface;
using UnityEngine;

namespace Client.Game.InGame.Map.Outcrop
{
    // ロード済み露頭prefabと、接地に使うmesh範囲・接地契約の検査結果を1組で持つ
    // A loaded outcrop prefab together with the mesh extent used for grounding and its grounding-contract verdict
    public sealed class OutcropPrefab
    {
        public readonly GameObject Prefab;
        public readonly bool HasMeshBounds;

        // pivot基準・回転なしで置いたときのmesh範囲
        // Mesh extent relative to the pivot when instantiated without rotation
        public readonly Bounds MeshBounds;

        // 接地契約の違反理由。満たしていればnull
        // Why the grounding contract is violated, or null when it holds
        public readonly string ContractViolation;

        private OutcropPrefab(GameObject prefab, bool hasMeshBounds, Bounds meshBounds, string contractViolation)
        {
            Prefab = prefab;
            HasMeshBounds = hasMeshBounds;
            MeshBounds = meshBounds;
            ContractViolation = contractViolation;
        }

        public static OutcropPrefab Create(GameObject prefab, TerrainSurfacePresentation presentation)
        {
            // 実体の有効状態でなくprefab由来の有効MeshRendererで測る。粒子等の描画範囲は寸法に混ぜない
            // Measure prefab-authored enabled MeshRenderers, not the instance's active state; particles and the like never enter the size
            var hasMeshBounds = TryMeasureMeshBounds(out var meshBounds);
            var contract = new OutcropGroundingContract(prefab.name, hasMeshBounds, meshBounds);
            presentation.Accept(contract);
            if (contract.Violation != null) Debug.LogError($"[OutcropPrefab] {contract.Violation}");
            return new OutcropPrefab(prefab, hasMeshBounds, meshBounds, contract.Violation);

            #region Internal

            bool TryMeasureMeshBounds(out Bounds bounds)
            {
                bounds = default;
                var found = false;
                var root = prefab.transform;
                foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (!renderer.enabled || filter == null || filter.sharedMesh == null || !IsActiveUnderRoot(renderer.transform)) continue;
                    var local = filter.sharedMesh.bounds;
                    for (var corner = 0; corner < 8; corner++)
                    {
                        var point = local.center + Vector3.Scale(local.extents,
                            new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                        var offset = Vector3.Scale(root.localScale, root.InverseTransformPoint(renderer.transform.TransformPoint(point)));
                        if (found) bounds.Encapsulate(offset);
                        else bounds = new Bounds(offset, Vector3.zero);
                        found = true;
                    }
                }
                return found;
            }

            bool IsActiveUnderRoot(Transform node)
            {
                for (var current = node; current != null; current = current.parent)
                {
                    if (!current.gameObject.activeSelf) return false;
                    if (current == prefab.transform) return true;
                }
                return true;
            }

            #endregion
        }
    }
}
