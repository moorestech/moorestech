using Game.MapGeneration.Surface;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain
{
    public sealed class GeneratedOceanSurface : MonoBehaviour
    {
        [SerializeField] private Renderer[] waterRenderers;
        private readonly System.Collections.Generic.Dictionary<Renderer, Material[]> _ownedMaterials = new();
        private const string WaveHeightProperty = "_WavesHeight";

        public void Initialize(SurfaceEnvelope envelope)
        {
            if (float.IsNaN(envelope.SeaY) || float.IsInfinity(envelope.SeaY) ||
                float.IsNaN(envelope.MaximumWaveRise) || float.IsInfinity(envelope.MaximumWaveRise) || envelope.MaximumWaveRise < 0f)
                throw SurfaceContractFailure.Create("[GeneratedOceanSurface] Sea envelope must contain finite heights and a nonnegative wave rise.");
            if (waterRenderers == null || waterRenderers.Length == 0)
                throw SurfaceContractFailure.Create("[GeneratedOceanSurface] At least one water renderer must be wired.");

            // 全入力を検証してから描画実体を書き換える
            // Validate every input before modifying presentation instances
            var distinctRenderers = new System.Collections.Generic.HashSet<Renderer>();
            foreach (var renderer in waterRenderers)
            {
                if (renderer == null)
                    throw SurfaceContractFailure.Create("[GeneratedOceanSurface] A water renderer reference is missing.");
                if (!distinctRenderers.Add(renderer))
                    throw SurfaceContractFailure.Create("[GeneratedOceanSurface] Water renderer references must be distinct.");
                ValidatePlane(renderer);
                if (renderer.sharedMaterials.Length == 0)
                    throw SurfaceContractFailure.Create($"[GeneratedOceanSurface] No water material on {renderer.name}.");
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader.name != "BK/Water" || !material.HasProperty(WaveHeightProperty))
                        throw SurfaceContractFailure.Create($"[GeneratedOceanSurface] Unsupported water material on {renderer.name}.");
                }
            }

            // 共有assetを守りXZと寸法を維持
            // Protect shared assets and keep XZ placement and size
            foreach (var renderer in waterRenderers)
            {
                var position = renderer.transform.position;
                position.y = envelope.SeaY;
                renderer.transform.position = position;
                // 再初期化では所有材質を再利用
                // Reuse owned materials on re-initialization
                if (!_ownedMaterials.TryGetValue(renderer, out var materials))
                {
                    materials = renderer.sharedMaterials;
                    for (var index = 0; index < materials.Length; index++)
                        materials[index] = new Material(materials[index]);
                    _ownedMaterials.Add(renderer, materials);
                }
                foreach (var material in materials)
                    material.SetFloat(WaveHeightProperty, envelope.MaximumWaveRise);
                renderer.sharedMaterials = materials;
            }

            #region Internal

            void ValidatePlane(Renderer renderer)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    throw SurfaceContractFailure.Create($"[GeneratedOceanSurface] Missing plane mesh on {renderer.name}.");

                // shaderはobject法線を世界変位に使うため元法線と平面を確認する
                // The shader uses object normals as world displacement, so validate original normals and the plane
                var mesh = filter.sharedMesh;
                var vertices = mesh.vertices;
                var normals = mesh.normals;
                if (vertices.Length == 0 || normals.Length != vertices.Length)
                    throw SurfaceContractFailure.Create($"[GeneratedOceanSurface] Invalid plane geometry on {renderer.name}.");
                for (var index = 0; index < vertices.Length; index++)
                {
                    var normal = renderer.transform.TransformDirection(normals[index]).normalized;
                    var offset = renderer.transform.TransformVector(vertices[index]);
                    if (0.000001f < (normals[index] - Vector3.up).sqrMagnitude ||
                        0.000001f < (normal - Vector3.up).sqrMagnitude || 0.0001f < Mathf.Abs(offset.y))
                        throw SurfaceContractFailure.Create($"[GeneratedOceanSurface] Plane must have a zero base and upward normals: {renderer.name}, vertex {index}.");
                }
            }

            #endregion
        }
    }
}
