using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.GraphicsQuality.Appliers
{
    /// <summary>
    /// 地形の細かさと海の表示を画質設定に合わせる
    /// Matches terrain detail and ocean visibility to graphics quality
    /// </summary>
    public sealed class EnvironmentQualityApplier
    {
        // 精細はTerrain既定の5、粗いはv7と同じ10
        // Fine is the Terrain default 5; coarse matches v7's 10
        private const float FinePixelError = 5f;
        private const float CoarsePixelError = 10f;

        private readonly IReadOnlyList<UnityEngine.Terrain> _terrains;
        private readonly Renderer[] _oceanRenderers;

        public EnvironmentQualityApplier(IReadOnlyList<UnityEngine.Terrain> terrains, Transform environmentRoot)
        {
            _terrains = terrains;
            var oceanRenderers = new List<Renderer>();
            foreach (var ocean in environmentRoot.GetComponentsInChildren<GeneratedOceanSurface>(true))
                oceanRenderers.AddRange(ocean.GetComponentsInChildren<Renderer>(true));
            _oceanRenderers = oceanRenderers.ToArray();
        }

        public void Initialize()
        {
            Apply(GraphicsQualityPreference.Current);
            GraphicsQualityPreference.OnSettingsChanged.Subscribe(Apply);

            #region Internal

            void Apply(GraphicsQualitySettings settings)
            {
                var isFine = settings.TerrainDetail == GraphicsTerrainDetail.Fine;
                foreach (var terrain in _terrains)
                {
                    terrain.heightmapPixelError = isFine ? FinePixelError : CoarsePixelError;
                    terrain.drawTreesAndFoliage = isFine;
                }
                foreach (var oceanRenderer in _oceanRenderers) oceanRenderer.enabled = settings.OceanVisible;
            }

            #endregion
        }

    }
}
