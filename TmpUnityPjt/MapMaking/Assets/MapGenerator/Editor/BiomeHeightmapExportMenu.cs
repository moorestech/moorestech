using MapGenerator.Pipeline.Biomes;
using MapGenerator.Pipeline.Diagnostics;
using UnityEditor;
using UnityEngine;

namespace MapGenerator.Editor
{
    /// <summary>
    /// バイオーム別ハイトマップPNG出力のメニューアイテム。
    /// シーン内のMapGeneratorFacadeからConfigを取得し、BiomeHeightmapExporterに委譲する。
    /// </summary>
    public static class BiomeHeightmapExportMenu
    {
        const int Resolution = 512;

        [MenuItem("Tools/MapGenerator/Export Biome Heightmap/Grassland")]
        static void ExportGrassland() => Export(BiomeType.Grassland);

        [MenuItem("Tools/MapGenerator/Export Biome Heightmap/Forest")]
        static void ExportForest() => Export(BiomeType.Forest);

        [MenuItem("Tools/MapGenerator/Export Biome Heightmap/Savanna")]
        static void ExportSavanna() => Export(BiomeType.Savanna);

        [MenuItem("Tools/MapGenerator/Export Biome Heightmap/Desert")]
        static void ExportDesert() => Export(BiomeType.Desert);

        [MenuItem("Tools/MapGenerator/Export Biome Heightmap/Mesa")]
        static void ExportMesa() => Export(BiomeType.Mesa);

        [MenuItem("Tools/MapGenerator/Export Biome Heightmap/Alpine")]
        static void ExportAlpine() => Export(BiomeType.Alpine);

        [MenuItem("Tools/MapGenerator/Export Biome Heightmap/Jungle")]
        static void ExportJungle() => Export(BiomeType.Jungle);

        [MenuItem("Tools/MapGenerator/Export Biome Heightmap/Woods")]
        static void ExportWoods() => Export(BiomeType.Woods);

        static void Export(BiomeType biomeType)
        {
            var facade = GameObject.Find("MapGenerator")?.GetComponent<MapGeneratorFacade>();
            if (facade == null)
            {
                Debug.LogError("[BiomeHeightmapExport] シーンに 'MapGenerator' オブジェクトが見つかりません");
                return;
            }

            var path = BiomeHeightmapExporter.Export(biomeType, facade.config, Resolution);
            if (path != null)
                AssetDatabase.Refresh();
        }
    }
}
