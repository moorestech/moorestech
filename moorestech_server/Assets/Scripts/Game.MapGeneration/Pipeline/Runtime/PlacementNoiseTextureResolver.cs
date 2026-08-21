using System;
using System.IO;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Runtime
{
    // PlacementNoise の texturePngPath を実画素へ展開する。マスタは PNG のパスしか持たないので、
    // 生成器が回り始める前にここで一度だけファイルを読み、以後の配置処理はメモリ上の画素だけを見る。
    // Expands each PlacementNoise texturePngPath into real pixels. Master only carries the PNG path,
    // so the file is read once here before generation and placement then reads memory only.
    public static class PlacementNoiseTextureResolver
    {
        public static void Resolve(TerrainGenerationConfig config, string serverDataDirectory)
        {
            // 樹木プロトタイプが PlacementNoise を持つ唯一の場所。バイオーム横断で全ノイズ4種を舐める。
            // Tree prototypes are the only holders of PlacementNoise; sweep all four per prototype across biomes.
            var helper = new BiomePlacementHelper(config);
            foreach (BiomeType biome in Enum.GetValues(typeof(BiomeType)))
            {
                var treePlacement = helper.GetTreePlacementConfig(biome);
                if (treePlacement?.prototypes == null) continue;

                foreach (var entry in treePlacement.prototypes)
                {
                    LoadInto(ref entry.clusterNoise, serverDataDirectory);
                    LoadInto(ref entry.clusterNoise2, serverDataDirectory);
                    LoadInto(ref entry.slopeFilter.noise, serverDataDirectory);
                    LoadInto(ref entry.curvatureFilter.noise, serverDataDirectory);
                }
            }

            #region Internal

            static void LoadInto(ref PlacementNoise noise, string serverDataDirectory)
            {
                // 空文字はテクスチャ源なしの表明。読み込まないので texturePixels は null のまま。
                // An empty string declares "no texture source", so nothing is read and texturePixels stays null.
                if (string.IsNullOrEmpty(noise.texturePngPath)) return;

                var path = Path.Combine(serverDataDirectory, noise.texturePngPath);
                if (!File.Exists(path))
                    throw new InvalidOperationException(
                        $"[PlacementNoiseTextureResolver] texturePngPath points at a missing file: '{path}'.");

                // 外部入力(PNG)の境界。壊れた画像は無言で無視せずマスタ不備として即例外にする。
                // Boundary against external input (PNG); a corrupt image fails loud as a master-data gap.
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                var loaded = ImageConversion.LoadImage(texture, File.ReadAllBytes(path), false);
                if (!loaded)
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                    throw new InvalidOperationException(
                        $"[PlacementNoiseTextureResolver] texturePngPath is not a decodable PNG: '{path}'.");
                }

                noise.texturePixels = texture.GetPixels32();
                noise.textureWidth = texture.width;
                noise.textureHeight = texture.height;

                // 破棄は DestroyImmediate 固定。Destroy は EditMode で例外になりネイティブ実体が解放されない。
                // Disposal is pinned to DestroyImmediate; Destroy throws in EditMode and leaks the native texture.
                UnityEngine.Object.DestroyImmediate(texture);
            }

            #endregion
        }
    }
}
