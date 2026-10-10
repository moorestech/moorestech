using Client.Game.InGame.UltraLight;
using Game.MapGeneration.Surface;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain
{
    // 地形完成後に海の表示契約を適用する
    // Applies the ocean presentation contract once the terrain is complete
    public sealed class OceanPresentationApplier : ITerrainSurfacePresentationVisitor
    {
        private readonly Transform _environmentRoot;

        public OceanPresentationApplier(Transform environmentRoot)
        {
            _environmentRoot = environmentRoot;
        }

        public void VisitLegacy()
        {
            // 旧版ワールドはprefab既定の海をそのまま使う
            // Legacy worlds keep the prefab's default ocean as is
            HideOceanForUltraLight();
        }

        public void VisitGrounded(SurfaceEnvelope envelope)
        {
            var ocean = _environmentRoot.GetComponentInChildren<GeneratedOceanSurface>(true);
            if (ocean == null)
                throw SurfaceContractFailure.Create("[TerrainRuntimeBuilder] GeneratedOceanSurface is not wired on the environment prefab.");
            ocean.Initialize(envelope);
            HideOceanForUltraLight();
        }

        private void HideOceanForUltraLight()
        {
            // 超軽量設定: 屈折付きの海を描かない
            // Ultra-light preset: skip the refractive ocean entirely
            foreach (var ocean in _environmentRoot.GetComponentsInChildren<GeneratedOceanSurface>(true)) UltraLightPreset.HideRenderers(ocean.gameObject);
        }
    }
}
