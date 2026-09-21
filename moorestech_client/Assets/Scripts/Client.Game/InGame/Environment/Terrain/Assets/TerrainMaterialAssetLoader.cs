using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Assets
{
    public static class TerrainMaterialAssetLoader
    {
        // URPの既定材質はビルドでnullになるため、共通のプロジェクト資産を使う
        // URP's default material is null in builds, so both callers use the project asset
        private const string TerrainMaterialAddress = "Vanilla/Environment/Terrain/TerrainLitMaterial";

        public static async UniTask<Material> LoadAsync(ITerrainAssetLoader assets, CancellationToken cancellationToken)
        {
            var material = await assets.LoadAsync<Material>(TerrainMaterialAddress, cancellationToken);
            if (material == null)
                throw new InvalidOperationException($"[TerrainMaterialAssetLoader] Terrain material '{TerrainMaterialAddress}' could not be loaded.");

            return material;
        }
    }
}
