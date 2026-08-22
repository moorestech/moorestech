using UnityEngine;

namespace Game.MapGeneration.Facade
{
    // template専用の見た目定数。生成タイルはCreateTileMapsの実値を使う
    // Visual constants for template only; generated tiles use CreateTileMaps' real values
    public static class TerrainRenderingDefaults
    {
        public const string TemplateTerrainDataAddress = "Vanilla/Environment/TemplateTerrainData";

        // Environment.prefabのTerrainが持っていたオーサリング配置の移設先。sizeは2048角なのに位置は-1000で、
        // 中心合わせでは24mずれてベイク済みmapObject座標が全部崩れる
        // Migrated from the authored placement on Environment.prefab's Terrain; its size is 2048 square yet the
        // position is -1000, so centering it would shift 24m and break every baked mapObject coordinate
        public static readonly Vector3 TemplateTerrainOrigin = new(-1000f, 0f, -1000f);

        public const float TemplateDetailObjectDistance = 80f;
        public const float TemplateDetailObjectDensity = 1f;

        public const float BakedDetailObjectDistance = 200f;
        public const float BakedDetailObjectDensity = 0.3f;
    }
}
