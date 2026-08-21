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

        // 台地デバッグ用の列。PlateauDebugOverlayJobは領域IDをこの連番へ割り当てるので、末尾の連続域であることが前提
        // The plateau debug columns; PlateauDebugOverlayJob maps region ids onto this run, so it must stay contiguous at the tail
        public readonly int DebugLayerStart;
        public readonly int DebugLayerCount;

        private SplatLayerTable(
            IReadOnlyList<string> orderedLayerAddresses, IReadOnlyDictionary<string, int> layerIndexByAddress,
            int debugLayerStart, int debugLayerCount)
        {
            OrderedLayerAddresses = orderedLayerAddresses;
            LayerIndexByAddress = layerIndexByAddress;
            DebugLayerStart = debugLayerStart;
            DebugLayerCount = debugLayerCount;
        }

        // biomeMainLayerAddresses と biomeTextureConfigs は有効バイオームの並びで対応する
        // biomeMainLayerAddresses and biomeTextureConfigs are parallel arrays over the enabled biomes
        public static SplatLayerTable Build(
            string beachLayerAddress, string rockLayerAddress,
            string[] biomeMainLayerAddresses, BiomeTextureConfig[] biomeTextureConfigs,
            SurroundTextureConfig[] biomeSurroundTextureConfigs, TreeSurroundSpeciesTable treeSurroundSpecies,
            string[] debugLayerAddresses)
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

            // 岩周辺の裸地レイヤーはマスタの必須キー。空はフォールバック指示ではなくアドレス整備漏れとして弾く
            // The bare-ground layer around rocks is a required master key, so an empty address is a data gap rather than a request to fall back
            foreach (var surroundTextureConfig in biomeSurroundTextureConfigs)
                Register(
                    surroundTextureConfig.surroundLayerAddressablePath,
                    "biome[].objectConfig.surroundTextureConfig.surroundLayerAddressablePath");

            // 木の根元のレイヤーは樹種ごと。塗る樹種のぶんだけを樹種テーブル自身が数えているので、ここへ来る時点で空は無い
            // A tree's root layer is per species, counted by the species table itself for the painting ones alone, so nothing empty arrives here
            foreach (var treeSurroundLayerAddress in treeSurroundSpecies.LayerAddresses)
                Register(treeSurroundLayerAddress, "treePlacement.prototypes.surroundLayerAddressablePath");

            // 台地デバッグの列は全レイヤーの後ろ。前へ入れると既存の列がずれ、過去タイルの見た目キャッシュと意味が食い違う
            // The plateau debug columns go behind every layer; inserting them earlier shifts the existing ones away from what the cached tiles mean
            // オーバーレイを切ってある構成では呼び出し側が空を渡す。塗らない列を確保するとTerrainLayerを無駄に読み込む
            // A configuration with the overlay off hands in an empty array: reserving unpainted columns would load TerrainLayers for nothing
            var debugLayerStart = orderedLayerAddresses.Count;
            foreach (var debugLayerAddress in debugLayerAddresses)
                RegisterDebug(debugLayerAddress);

            // 列数は実際に積んだ本数。設定の本数で数えると未割当スロットのぶんだけ領域IDが空の列へ飛ぶ
            // The column count is what was really appended; counting the configured entries would send region ids onto columns that do not exist
            var debugLayerCount = orderedLayerAddresses.Count - debugLayerStart;

            return new SplatLayerTable(
                orderedLayerAddresses, layerIndexByAddress, debugLayerStart, debugLayerCount);

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

            // 重複を畳まない唯一の経路。積んだ本数がそのまま列数になり、領域IDの剰余がその中を巡る
            // The one path that never folds duplicates: the appended count is the column count the region ids cycle through
            void RegisterDebug(string layerAddress)
            {
                // 空は未割当のデバッグスロット。移植元がTerrainLayer未設定を読み飛ばすのと同義で、列を作らない
                // An empty entry is an unassigned debug slot, the source's null TerrainLayer, and adds no column
                if (string.IsNullOrEmpty(layerAddress)) return;

                // 索引辞書へは載せない。同じアドレスの通常レイヤーがあると索引を奪い、そのバイオームが台地色に染まる
                // They stay out of the index dictionary: an ordinary layer sharing the address would lose its index and take the plateau colour
                orderedLayerAddresses.Add(layerAddress);
            }

            #endregion
        }
    }
}
