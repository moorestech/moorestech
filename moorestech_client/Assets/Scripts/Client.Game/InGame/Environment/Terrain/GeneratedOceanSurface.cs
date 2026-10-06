using System;
using Game.MapGeneration.Facade.Surface;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain
{
    public sealed class GeneratedOceanSurface : MonoBehaviour
    {
        [SerializeField] private Renderer[] waterRenderers;
        private const string WaveHeightProperty = "_WavesHeight";

        public void Initialize(SurfaceEnvelope envelope)
        {
            if (float.IsNaN(envelope.SeaY) || float.IsInfinity(envelope.SeaY) ||
                float.IsNaN(envelope.MaximumWaveRise) || float.IsInfinity(envelope.MaximumWaveRise) || envelope.MaximumWaveRise < 0f)
                throw Failure("[GeneratedOceanSurface] Sea envelope must contain finite heights and a nonnegative wave rise.");
            if (waterRenderers == null || waterRenderers.Length != 2)
                throw Failure("[GeneratedOceanSurface] Exactly two water renderers must be wired.");

            if (waterRenderers[0] == waterRenderers[1])
                throw Failure("[GeneratedOceanSurface] Water renderer references must be distinct.");

            // 全入力を検証してから描画実体を書き換える
            // Validate every input before modifying presentation instances
            foreach (var renderer in waterRenderers)
            {
                if (renderer == null)
                    throw Failure("[GeneratedOceanSurface] A water renderer reference is missing.");
                ValidatePlane(renderer);
                if (renderer.sharedMaterials.Length == 0)
                    throw Failure($"[GeneratedOceanSurface] No water material on {renderer.name}.");
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader.name != "BK/Water" || !material.HasProperty(WaveHeightProperty))
                        throw Failure($"[GeneratedOceanSurface] Unsupported water material on {renderer.name}.");
                }
            }

            // 共有assetを保護しXZ配置と寸法を維持する
            // Protect shared assets and preserve XZ placement and dimensions
            foreach (var renderer in waterRenderers)
            {
                var position = renderer.transform.position;
                position.y = envelope.SeaY;
                renderer.transform.position = position;
                foreach (var material in renderer.materials)
                {
                    material.SetFloat(WaveHeightProperty, envelope.MaximumWaveRise);
                    if (material.GetFloat(WaveHeightProperty) > envelope.MaximumWaveRise)
                        throw Failure($"[GeneratedOceanSurface] Wave envelope exceeded on {renderer.name}.");
                }
            }
        }

        private static void ValidatePlane(Renderer renderer)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                throw Failure($"[GeneratedOceanSurface] Missing plane mesh on {renderer.name}.");

            // shaderはobject法線を世界変位に使うため元法線と平面を確認する
            // The shader uses object normals as world displacement, so validate original normals and the plane
            var mesh = filter.sharedMesh;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            if (vertices.Length == 0 || normals.Length != vertices.Length)
                throw Failure($"[GeneratedOceanSurface] Invalid plane geometry on {renderer.name}.");
            for (var index = 0; index < vertices.Length; index++)
            {
                var normal = renderer.transform.TransformDirection(normals[index]).normalized;
                var offset = renderer.transform.TransformVector(vertices[index]);
                if ((normals[index] - Vector3.up).sqrMagnitude > 0.000001f ||
                    (normal - Vector3.up).sqrMagnitude > 0.000001f || Mathf.Abs(offset.y) > 0.0001f)
                    throw Failure($"[GeneratedOceanSurface] Plane must have a zero base and upward normals: {renderer.name}, vertex {index}.");
            }
        }
        private static InvalidOperationException Failure(string reason)
        {
            // 拒否理由を起動失敗の前に記録する
            // Record the rejection before failing startup
            Debug.LogWarning(reason);
            return new InvalidOperationException(reason);
        }
    }
}
