using System;
using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround;

namespace Client.Game.InGame.Environment.Terrain.Visual.Splat
{
    /// <summary>
    ///     splatmapのレイヤー並びとインデックスを確定する。MapMaking JobDataConverter.BuildTerrainLayers の移植で、
    ///     TerrainLayer参照をアドレス文字列に置き換えたもの。実アセットの解決は呼び出し側がこの並び順どおりに行う
    ///     Fixes the splatmap's layer order and indices; ported from MapMaking's JobDataConverter.BuildTerrainLayers
    ///     with TerrainLayer references replaced by address strings. The caller resolves assets following this order
    /// </summary>
    public class SplatLayerTable
    {
        // この並びがsplatmapの列順そのもの。呼び出し側はこの順にAddressablesを解決してTerrainLayer[]を組む
        // This order is the splatmap's column order; callers resolve Addressables in it to build the TerrainLayer array
        public readonly IReadOnlyList<string> OrderedLayerAddresses;
        public readonly IReadOnlyDictionary<string, int> LayerIndexByAddress;

        private SplatLayerTable(IReadOnlyList<string> orderedLayerAddresses, IReadOnlyDictionary<string, int> layerIndexByAddress)
        {
            OrderedLayerAddresses = orderedLayerAddresses;
            LayerIndexByAddress = layerIndexByAddress;
        }

        // biomeMainLayerAddresses と biomeTextureConfigs は有効バイオームの並びで対応する
        // biomeMainLayerAddresses and biomeTextureConfigs are parallel arrays over the enabled biomes
        public static SplatLayerTable Build(
            string beachLayerAddress, string rockLayerAddress,
            string[] biomeMainLayerAddresses, BiomeTextureConfig[] biomeTextureConfigs,
            SurroundTextureConfig[] biomeSurroundTextureConfigs)
        {
            var orderedLayerAddresses = new List<string>();
            var layerIndexByAddress = new Dictionary<string, int>();

            // インデックス0はビーチ固定。SplatmapJobが海ピクセルで splatWeights[idx*totalLayers] を砂として書くため動かせない
            // Index 0 is pinned to the beach layer: SplatmapJob writes sand into splatWeights[idx*totalLayers] for sea pixels
            Register(beachLayerAddress, "shoreConfig.beachLayerAddressablePath");
            Register(rockLayerAddress, "rockLayerAddressablePath");

            for (var biome = 0; biome < biomeMainLayerAddresses.Length; biome++)
            {
                Register(biomeMainLayerAddresses[biome], $"biome[{biome}].terrainLayerAddressablePath");

                foreach (var entry in biomeTextureConfigs[biome].entries)
                    Register(entry.layerAddressablePath, $"biome[{biome}].textureConfig.entries[].layerAddressablePath");
            }

            // 岩周辺の裸地レイヤーは未設定が既定でMudフォールバックへ倒れる。空を欠落として弾くと全バイオームで落ちる
            // The bare-ground layer around rocks is unset by default and falls back to Mud, so rejecting empties would fail every biome
            foreach (var surroundTextureConfig in biomeSurroundTextureConfigs)
                RegisterOptional(surroundTextureConfig.surroundLayerAddressablePath);

            return new SplatLayerTable(orderedLayerAddresses, layerIndexByAddress);

            #region Internal

            void Register(string layerAddress, string masterFieldName)
            {
                // 空アドレスは「意図的に未設定」ではなくアドレス整備漏れ。0番へフォールバックさせると全面がビーチ色になる
                // An empty address is a data gap, not a deliberate blank; falling back to index 0 would paint everything with sand
                if (string.IsNullOrEmpty(layerAddress))
                    throw new InvalidOperationException(
                        $"[SplatLayerTable] '{masterFieldName}' is empty: every splatmap layer needs an Addressables address.");

                if (layerIndexByAddress.ContainsKey(layerAddress)) return;

                layerIndexByAddress[layerAddress] = orderedLayerAddresses.Count;
                orderedLayerAddresses.Add(layerAddress);
            }

            // 空を欠落と見なさない唯一の経路。空でなければ通常の登録と同じ扱いに戻す
            // The one path that does not read an empty address as a gap; anything non-empty rejoins the normal registration
            void RegisterOptional(string layerAddress)
            {
                if (string.IsNullOrEmpty(layerAddress)) return;
                Register(layerAddress, "biome[].objectConfig.surroundTextureConfig.surroundLayerAddressablePath");
            }

            #endregion
        }
    }
}
