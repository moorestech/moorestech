using UnityEngine;
using Game.MapGeneration.Pipeline.Biomes;
using Game.MapGeneration.Pipeline.Spawn;

namespace Game.MapGeneration.Pipeline.Config
{
    // 全パイプラインパラメータを保持する実行時 POCO。マスタ(GenerationModule)から
    // GenerationRuntimeConfigFactory で materialize される一時ビューで、永続データではない。
    // Runtime POCO holding all pipeline parameters; a transient view materialized from the
    // GenerationModule master by GenerationRuntimeConfigFactory, not persisted authoring data.
    public class TerrainGenerationConfig
    {
        // スポーン候補探索
        // Spawn candidate search
        public bool useSpawnOffsetSearch = false;
        public SpawnSearchConfig spawnSearch = new SpawnSearchConfig();

        // 出力・地形サイズ
        // Output / terrain size
        public TerrainResolutionPreset resolutionPreset = TerrainResolutionPreset._256;
        public int overrideResolution = 0;

        // overrideResolution>0 ならプリセットを無視して直接解像度を使う。
        // When overrideResolution>0 the preset is ignored and the value is used directly.
        public int Resolution => overrideResolution > 0 ? overrideResolution : (int)resolutionPreset + 1;
        public int AlphamapResolution => overrideResolution > 0 ? overrideResolution - 1 : (int)resolutionPreset;

        public float terrainHeight = 600f;
        public float terrainWidth = 1000f;
        public float terrainLength = 1000f;
        public int gridSizeX = 5;
        public int gridSizeZ = 5;
        public int seed = 160;
        public float worldOffsetX = 0f;
        public float worldOffsetZ = 0f;
        public Vector2 spawnWorldPosition = new Vector2(500f, 500f);
        public int chunkPadding = 50;

        // 大陸性・浸食・海面
        // Continentalness / erosion / sea level
        public float continentalnessFrequency = 0.00043f;
        public int continentalnessOctaves = 5;
        public float continentalnessPersistence = 0.496f;
        public float landThreshold = 0.35f;
        public float erosionFrequency = 0.00014f;
        public int erosionOctaves = 3;
        public float erosionStrength = 0.156f;
        public float seaLevel = 0.008333f;

        // 共通海岸線・境界・鉱脈
        // Common shore / boundary / veins
        public BiomeShoreConfig shoreConfig = new BiomeShoreConfig();
        public BiomeBoundaryConfig boundaryConfig = new BiomeBoundaryConfig();
        public WorldOreConfig oreConfig = new WorldOreConfig();

        // バイオーム分布ノイズ・ボロノイ・境界ワープ
        // Biome distribution noise / voronoi / boundary warp
        public float biomeScale = 0.001f;
        public int biomeBlendRadius = 200;
        public float voronoiCellSize = 1000f;
        public float voronoiJitter = 1.58f;
        public int boundaryWarpOctaves = 3;
        public float boundaryWarpStrength = 100f;
        public float boundaryWarpFrequency = 0.0024f;
        public float boundaryNoiseAmount = 0.207f;
        public float boundaryNoiseFrequency = 1f;

        // 共通テクスチャレイヤー（rockLayer→addressablePath）
        // Common texture layer (rockLayer to addressablePath)
        public string rockLayerAddressablePath = "";

        // 生成レイヤーのオン/オフ
        // Generation layer toggles
        public bool generateHeightmap = true;
        public bool generateTexture = true;
        public bool generateDetail = true;
        public bool generateObject = true;
        public bool generateOre = true;

        // バイオーム有効/無効
        // Biome enable flags
        public bool grasslandEnabled = true;
        public bool forestEnabled = true;
        public bool savannaEnabled = true;
        public bool desertEnabled = true;
        public bool mesaEnabled = true;
        public bool alpineEnabled = true;
        public bool jungleEnabled = true;
        public bool woodsEnabled = true;

        // バイオーム定義
        // Biome definitions
        public GrasslandBiomeConfig grassland = new GrasslandBiomeConfig();
        public ForestBiomeConfig forest = new ForestBiomeConfig();
        public SavannaBiomeConfig savanna = new SavannaBiomeConfig();
        public DesertBiomeConfig desert = new DesertBiomeConfig();
        public MesaBiomeConfig mesa = new MesaBiomeConfig();
        public AlpineBiomeConfig alpine = new AlpineBiomeConfig();
        public JungleBiomeConfig jungle = new JungleBiomeConfig();
        public WoodsBiomeConfig woods = new WoodsBiomeConfig();
    }
}
