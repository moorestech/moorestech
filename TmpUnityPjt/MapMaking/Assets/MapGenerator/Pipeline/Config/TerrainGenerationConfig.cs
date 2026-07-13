using MapGenerator.Pipeline.Biomes;
using MapGenerator.Pipeline.Config;
using MapGenerator.Pipeline.Spawn;
using UnityEngine;

namespace MapGenerator.Pipeline
{
    /// <summary>
    /// 全パイプラインパラメータを保持するScriptableObject。
    /// seedひとつで全ノイズが決定論的に生成される。
    /// バイオームごとのSOアセットを参照として保持する。
    /// </summary>
    [CreateAssetMenu(fileName = "MapGeneratorConfig", menuName = "MapGenerator/Config")]
    public class TerrainGenerationConfig : ScriptableObject
    {
        // スポーン候補探索プリパス。ONなら生成前に草原-森林隣接の良地を探索しオフセットを自動設定する
        [Header("スポーン候補探索")]
        [Label("スポーン候補探索を使う")]
        public bool useSpawnOffsetSearch = false;
        [Label("探索パラメータ")]
        public SpawnSearchConfig spawnSearch = new SpawnSearchConfig();

        // プリセットからハイトマップ(2^n+1)とスプラットマップ(2^n)を一括決定
        [Header("出力設定")]
        [Label("解像度プリセット")]
        public TerrainResolutionPreset resolutionPreset = TerrainResolutionPreset._256;

        // パイプライン用の読み取りプロパティ（overrideResolution > 0 ならプリセットを無視）
        public int Resolution => overrideResolution > 0 ? overrideResolution : (int)resolutionPreset + 1;
        public int AlphamapResolution => overrideResolution > 0 ? overrideResolution - 1 : (int)resolutionPreset;

        // terrainHeightはハイトマップ値(0-1)をワールド高さに変換するスケール
        [Header("地形サイズ")]
        [Label("地形の高さ")]
        public float terrainHeight = 600f;
        [Label("地形の幅")]
        public float terrainWidth = 1000f;
        [Label("地形の奥行き")]
        public float terrainLength = 1000f;

        // エディタ用のチャンクグリッドサイズ。Generate AllでX×Z枚のテレインを生成する
        [Header("チャンク管理")]
        [Label("グリッド X")]
        [Range(1, 20)]
        public int gridSizeX = 5;
        [Label("グリッド Z")]
        [Range(1, 20)]
        public int gridSizeZ = 5;

        // 全ノイズオフセット・樹木配置RNGの起点。同じseed = 同じ地形
        [Header("シード")]
        [Label("シード値")]
        public int seed = 160;

        // チャンクのワールド座標オフセット。無限生成で各チャンクが異なるノイズ領域をサンプルする
        [Header("ワールドオフセット")]
        [Label("X オフセット")]
        public float worldOffsetX = 0f;
        [Label("Z オフセット")]
        public float worldOffsetZ = 0f;

        // スポーン地点。鉱石の距離バンド判定の中心。ワールド座標(m)。
        // 単一テレイン(worldOffset=0)ではマップローカルと一致し、(500,500)は1000x1000マップの中心。
        // InfiniteTerrainManager で複数チャンク生成時も、この1点を全チャンク共通のスポーン基準に使う。
        [Header("スポーン地点")]
        [Label("スポーン地点(ワールド座標 X,Z m)")]
        public Vector2 spawnWorldPosition = new Vector2(500f, 500f);

        // チャンク境界のシームを防ぐためのパディング（ピクセル数）。
        // biomeBlendRadius以上の値を設定すると境界ブレンドが完全になる
        [Label("チャンクパディング")]
        [Range(0, 100)]
        public int chunkPadding = 50;

        // 内部使用: パディング生成時にプリセットを無視して直接解像度を指定する
        [HideInInspector]
        public int overrideResolution = 0;

        // Continentalnessノイズで大陸/海洋の大構造を決定する
        [Header("大陸性ノイズ (Continentalness)")]
        [Label("大陸ノイズ周波数")]
        public float continentalnessFrequency = 0.00043f;
        [Label("大陸ノイズオクターブ")]
        [Range(1, 6)]
        public int continentalnessOctaves = 5;
        [Label("大陸ノイズ持続性")]
        [Range(0.1f, 0.9f)]
        public float continentalnessPersistence = 0.496f;
        [Label("陸地閾値")]
        [Range(0.0f, 1.0f)]
        public float landThreshold = 0.35f;

        // Erosionノイズで海岸線の複雑さ（入り江・半島）を制御する
        [Header("浸食ノイズ (Erosion)")]
        [Label("浸食ノイズ周波数")]
        public float erosionFrequency = 0.00014f;
        [Label("浸食ノイズオクターブ")]
        [Range(1, 4)]
        public int erosionOctaves = 3;
        [Label("浸食強度")]
        [Range(0f, 0.3f)]
        public float erosionStrength = 0.156f;

        // seaLevelは海面の高さ。これより低い地点は水没扱いになる
        [Label("海面レベル")]
        [Range(0f, 0.1f)]
        public float seaLevel = 0.008333f;

        // 海岸線の遷移幅や水際配置除外は全バイオームで同じ基準を使う。
        // ここを共通化しておくと、海岸線の調整結果がバイオーム境界で割れない。
        [Header("海岸線")]
        [Label("海岸線設定")]
        public BiomeShoreConfig shoreConfig = new BiomeShoreConfig();

        // 全バイオーム共通の境界ブレンド設定
        [Header("境界設定")]
        [Label("境界設定")]
        public BiomeBoundaryConfig boundaryConfig = new BiomeBoundaryConfig();

        // 鉱脈はワールド全体で一元管理する。各エントリが出現バイオーム(biomes)を持ち、
        // バイオームごとの個別設定ではなくこのグローバルリストで配置する。
        [Header("鉱脈")]
        [Label("グローバル鉱脈設定")]
        public WorldOreConfig oreConfig = new WorldOreConfig();

        // biomeScaleはバイオーム分布ノイズの周波数。小さいほど広いバイオーム領域になる
        [Header("バイオーム")]
        [Label("バイオームスケール")]
        public float biomeScale = 0.001f;
        // Minecraft式バイオーム補間の半径（ピクセル単位）。大きいほど広い範囲をサンプリングし滑らかに接続する
        [Label("バイオーム補間半径")]
        [Range(1, 500)]
        public int biomeBlendRadius = 200;

        // ボロノイセルのワールド空間サイズ。大きいほどバイオーム領域が広くなる
        [Label("ボロノイセルサイズ")]
        public float voronoiCellSize = 1000f;
        // セル中心からのジッター量。0=格子、≤1=セル内、>1=セル境界を越えて不規則な形状に
        [Label("ボロノイジッター")]
        [Range(0f, 3f)]
        public float voronoiJitter = 1.58f;

        // 境界ドメインワープ: fBmで座標を歪めてボロノイ境界を有機的にする
        [Label("境界ワープオクターブ")]
        [Range(0, 6)]
        public int boundaryWarpOctaves = 3;
        // ワープ強度（ワールド単位）
        [Label("境界ワープ強度")]
        public float boundaryWarpStrength = 100f;
        [Label("境界ワープ周波数")]
        public float boundaryWarpFrequency = 0.0024f;

        // 遷移帯にノイズを加えて直線的な境界を崩す
        [Label("境界ノイズ量")]
        [Range(0f, 0.3f)]
        public float boundaryNoiseAmount = 0.207f;
        [Label("境界ノイズ周波数")]
        public float boundaryNoiseFrequency = 1f;

        // 岩は全バイオーム共通。砂浜はshoreConfig側でまとめて管理する。
        [Header("共通テクスチャレイヤー")]
        [Label("岩レイヤー")]
        public TerrainLayer rockLayer;

        // 生成するレイヤーの選択。無効レイヤーは計算・適用をスキップし既存データを保持する
        [Header("生成レイヤー")]
        [Label("高さマップ")] public bool generateHeightmap = true;
        [Label("テクスチャ")] public bool generateTexture = true;
        [Label("Detail")] public bool generateDetail = true;
        [Label("オブジェクト")] public bool generateObject = true;
        [Label("鉱脈")] public bool generateOre = true;

        // 生成に含めるバイオームのオン/オフ。無効バイオームの領域はフォールバック（Grassland）になる
        [Header("バイオーム有効/無効")]
        [Label("草原")] public bool grasslandEnabled = true;
        [Label("森林")] public bool forestEnabled = true;
        [Label("サバンナ")] public bool savannaEnabled = true;
        [Label("砂漠")] public bool desertEnabled = true;
        [Label("メサ")] public bool mesaEnabled = true;
        [Label("高山")] public bool alpineEnabled = true;
        [Label("ジャングル")] public bool jungleEnabled = true;
        [Label("林")] public bool woodsEnabled = true;

        // 各バイオームのScriptableObjectアセット参照
        [Header("バイオーム定義")]
        [Label("草原バイオーム")]
        public GrasslandBiomeConfig grassland;
        [Label("森林バイオーム")]
        public ForestBiomeConfig forest;
        [Label("サバンナバイオーム")]
        public SavannaBiomeConfig savanna;
        [Label("砂漠バイオーム")]
        public DesertBiomeConfig desert;
        [Label("メサバイオーム")]
        public MesaBiomeConfig mesa;
        [Label("高山バイオーム")]
        public AlpineBiomeConfig alpine;
        [Label("ジャングルバイオーム")]
        public JungleBiomeConfig jungle;
        [Label("林バイオーム")]
        public WoodsBiomeConfig woods;
    }
}
