using System;
using Game.MapGeneration.Pipeline.Visual.Placement;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Visual.Surround;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Config;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Surround
{
    /// <summary>
    ///     岩周辺の裸地テストが共有する小さな地形。40m四方・alphamap8画素なので1画素は40/7mで、
    ///     岩は画素4の中心にちょうど乗る。遷移帯15mが数画素に届く寸法に揃えてある
    ///     The small terrain the bare-ground tests share: 40m square over an 8-pixel alphamap, so a pixel spans 40/7m
    ///     and the rock lands exactly on pixel 4's center, sized so a 15m transition band reaches several pixels
    /// </summary>
    public static class SurroundTestFixtures
    {
        public const int Resolution = 9;
        public const int AlphaResolution = 8;
        public const float TerrainSize = 40f;
        public const int LayerCount = 3;

        // 岩の裸地が書き込まれる列。マスタが必須キーで指すアドレスと索引の対応をここで固定する
        // The column the rocks' bare ground writes into, pinning the address the required master key names to its index
        public const string MudLayerAddress = "addr/Mud01";
        public const int MudLayerIndex = 1;

        // painterがSplitを内側で回すので、テストのguidもforUnitTestマスタに実在する種別付きのものを使う
        // The painters run Split internally, so the tests' guids must be ones the forUnitTest master really classifies
        public const string StoneGuid = "00000000-0000-2222-0000-000000000001";
        public const string TreeGuid = "00000000-0000-1111-0000-000000000001";
        // 裸地を塗らない岩(rockNoBareGround)。距離場には岩として乗り、裸地painterは触らない
        // A rock that paints no bare ground (rockNoBareGround): it feeds the rock distance field and the bare-ground painter skips it
        public const string NoBareGroundStoneGuid = "00000000-0000-3333-0000-000000000001";

        public const int RockPixel = 4;

        // painterがSplitを内側で回すのでMasterHolderが要る。呼ばないとguidの種別が引けず例外で落ちる
        // The painters run Split internally and need MasterHolder; without this the guid's kind cannot be resolved and it throws
        public static void LoadMasterData()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
        public const float RockLocalPosition = TerrainSize * RockPixel / (AlphaResolution - 1);

        public static TerrainGenerationConfig CreateConfig()
        {
            return new TerrainGenerationConfig
            {
                overrideResolution = Resolution,
                detailResolution = 1024,
                terrainWidth = TerrainSize,
                terrainLength = TerrainSize,
                terrainHeight = 600f,
            };
        }

        public static SplatLayerTable CreateLayerTable()
        {
            return SplatLayerTable.Build(
                "addr/beach", MudLayerAddress, new[] { "addr/grass" },
                new[] { new BiomeTextureConfig { entries = new TextureEntry[0] } },
                new[] { new SurroundTextureConfig { surroundLayerAddressablePath = MudLayerAddress } },
                CreateTreeSurroundSpecies(), Array.Empty<string>());
        }

        // 樹種テーブルは本番と同じくConfigから組む。列やマップを手書きで注げる口を作ると、その導出をテストが迂回してしまう
        // The species table is assembled from a config as production does; a hand-written map or column list would let tests bypass that derivation
        public static TreeSurroundSpeciesTable CreateTreeSurroundSpecies(params TreePrototypeEntry[] prototypes)
        {
            var config = CreateConfig();
            config.grassland.treePlacement = new TreePlacementConfig { prototypes = prototypes };

            return TreeSurroundSpeciesTable.Build(new BiomePlacementHelper(config), new[] { BiomeType.Grassland });
        }

        public static TreePrototypeEntry CreateTreePrototype(
            string[] mapObjectGuids, string layerAddress, float weight, float width)
        {
            return new TreePrototypeEntry
            {
                mapObjectGuids = mapObjectGuids,
                surroundLayerAddressablePath = layerAddress,
                surroundLayerWeight = weight,
                surroundLayerWidth = width,
            };
        }

        public static SurroundTextureConfig CreateSurroundConfig()
        {
            return new SurroundTextureConfig
            {
                enabled = true,
                surroundLayerAddressablePath = MudLayerAddress,
                coreRadius = 5f,
                coreBlendMin = 0.8f,
                coreBlendMax = 0.95f,
                transitionRadius = 15f,
                transitionBlendMin = 0.15f,
                transitionBlendMax = 0.5f,
                noiseLowFrequency = 0.03f,
                noiseHighFrequency = 0.15f,
                noiseLowWeight = 0.6f,
                rockMeshBaseSize = 5f,
                singleRockRadius = 8f,
                singleRockBlend = 0.6f,
            };
        }

        // Scaleは2倍。フットプリント半径は (2+2)/2 * rockMeshBaseSize になる
        // The scale is doubled, making the footprint radius (2+2)/2 * rockMeshBaseSize
        public static LedgerPlacement CreateRock(int clusterId)
        {
            return CreateRock(clusterId, StoneGuid);
        }

        public static LedgerPlacement CreateRock(int clusterId, string mapObjectGuid)
        {
            // 種別はguidではなくSurroundEffectで直接指定する。台帳はマスタ参照済みで既に種別を運んでいるため
            // The kind is given directly as a SurroundEffect rather than by guid; the ledger already carries it, having resolved the master itself
            var effect = mapObjectGuid == NoBareGroundStoneGuid
                ? TerrainSurroundEffectType.rockNoBareGround
                : TerrainSurroundEffectType.rockBareGround;

            PlacementCluster? cluster = clusterId < 0
                ? null
                : new PlacementCluster(clusterId, new Vector2(RockLocalPosition, RockLocalPosition));

            return new LedgerPlacement(mapObjectGuid,
                new Vector3(RockLocalPosition, 0f, RockLocalPosition), new Vector3(2f, 2f, 2f),
                effect, cluster);
        }

        // 指定バイオームだけが重み1を持つ配置用重み。列オフセット+2はOcean/Beach列ぶん
        // Placement weights giving weight 1 to one biome only; the +2 column offset covers Ocean and Beach
        public static float[,] CreateBiomeWeights(int winningBiome)
        {
            var weights = new float[Resolution * Resolution, 2 + 2];
            for (var pixel = 0; pixel < Resolution * Resolution; pixel++)
                weights[pixel, 2 + winningBiome] = 1f;

            return weights;
        }

        public static float[,] CreateHeights(float slopeAlongX)
        {
            var heights = new float[Resolution, Resolution];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
                heights[z, x] = x / (float)(Resolution - 1) * slopeAlongX;

            return heights;
        }

        // 書き込み先のMud列が0で始まる初期状態。合計が保たれるのはこの m=0 の場合だけ
        // A start state whose Mud column begins at zero, the m = 0 case in which alone the weight sum is preserved
        public static float[,,] CreateUniformAlphamap()
        {
            var alphamap = new float[AlphaResolution, AlphaResolution, LayerCount];
            for (var z = 0; z < AlphaResolution; z++)
            for (var x = 0; x < AlphaResolution; x++)
                alphamap[z, x, 2] = 1f;

            return alphamap;
        }

        // Mud列に元から重みがある初期状態。desertはMudDryをtextureConfigに持つので実データではこちらが普通
        // A start state already carrying weight on the Mud column, the ordinary case in real data since desert lists MudDry in its textureConfig
        public const float PresetSurroundWeight = 0.4f;
        public const float PresetMainWeight = 1f - PresetSurroundWeight;

        public static float[,,] CreateAlphamapWithSurroundWeight()
        {
            var alphamap = new float[AlphaResolution, AlphaResolution, LayerCount];
            for (var z = 0; z < AlphaResolution; z++)
            for (var x = 0; x < AlphaResolution; x++)
            {
                alphamap[z, x, MudLayerIndex] = PresetSurroundWeight;
                alphamap[z, x, 2] = PresetMainWeight;
            }

            return alphamap;
        }
    }
}
