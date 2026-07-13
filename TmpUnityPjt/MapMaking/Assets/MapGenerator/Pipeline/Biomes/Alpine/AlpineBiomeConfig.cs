using MapGenerator.Pipeline.Config;
using UnityEngine;

namespace MapGenerator.Pipeline.Biomes
{
    [CreateAssetMenu(fileName = "AlpineBiome", menuName = "MapGenerator/Biome/Alpine")]
    public class AlpineBiomeConfig : ScriptableObject
    {
        // 標高ノイズがこの値以上で高山判定。continentalness値による内陸バイアスで大陸内部に高山が集中する
        [Header("分類")]
        [Label("標高閾値")]
        [Range(0f, 1f)] public float elevationThreshold = 0.58f;

        // =====================================================================
        // Stage 1: ドメインワープ — 座標を歪めて機械的な繰り返しパターンを崩す
        // =====================================================================
        [Header("Stage 1: ドメインワープ")]
        [Label("ワープ強度")]
        public float warpStrength = 4f;
        [Label("ワープ反復")]
        [Range(0, 3)] public int warpIterations = 3;

        // =====================================================================
        // Stage 2: マスFBm — 低周波FBmで山塊のボリュームを形成
        // =====================================================================
        [Header("Stage 2: マスFBm（山塊ボリューム）")]
        [Label("周波数")]
        public float frequency = 0.0010f;
        // fBmの重ね合わせ回数。多いほど山のディテールが細かくなる
        [Label("オクターブ数")]
        public int octaves = 3;

        // =====================================================================
        // Stage 3: ブロードリッジ — リッジノイズで山の尾根構造を形成
        // =====================================================================
        [Header("Stage 3: ブロードリッジ")]
        [Label("リッジ混合率")]
        [Range(0f, 1f)] public float ridgeBlend = 0.64f;
        [Label("リッジオクターブ")]
        [Range(1, 8)] public int ridgeOctaves = 5;

        // =====================================================================
        // Stage 4: べき乗コントラスト — 低い値を押し下げ谷を広く山頂を鋭くする
        // =====================================================================
        [Header("Stage 4: べき乗コントラスト")]
        [Label("べき乗指数")]
        [Range(0.5f, 3f)] public float exponent = 1.72f;

        // =====================================================================
        // Stage 5: 山頂平坦化 — 天井 — ピークをソフトクランプして平坦エリアを形成
        // =====================================================================
        [Header("Stage 5: 山頂平坦化 — 天井（ピーク引き下げ）")]
        [Label("天井高度")]
        [Range(0.3f, 0.9f)] public float ceilHeight = 0.682f;
        [Label("天井強度")]
        [Range(0f, 1f)] public float ceilStrength = 0.858f;

        // =====================================================================
        // Stage 6: 山頂平坦化 — 床 — 谷底を引き上げて台地内の最低高度を制御
        // =====================================================================
        [Header("Stage 6: 山頂平坦化 — 床（谷底引き上げ）")]
        [Label("床高度")]
        [Range(0.1f, 0.9f)] public float floorHeight = 0.58f;
        [Label("床強度")]
        [Range(0f, 1f)] public float floorStrength = 0.40f;

        // =====================================================================
        // 出力スケール — 最終出力: baseHeight + terrain * amplitude
        // =====================================================================
        [Header("出力スケール")]
        [Label("基底高度")]
        public float baseHeight = 0.06f;
        [Label("振幅")]
        public float amplitude = 0.60f;

        // =====================================================================
        // Post 1: 台地化検出 — 8方向prominenceでドーム状ピークを検出
        // =====================================================================
        [Header("Post 1: 台地化検出（方向別prominence）")]
        [Label("台地化を有効にする")]
        public bool enablePlateau = true;
        [Label("prominence 閾値")]
        [Range(0.01f, 0.3f)] public float prominenceThreshold = 0.06f;
        // 8方向中、何方向以上で突出していれば候補とするか
        [Label("最小突出方向数")]
        [Range(3, 8)] public int minProminentDirections = 6;
        // AlpinePlateauJobの検索半径とブレンド（BiomeBoundaryConfigから移動）
        [Label("プラトー検索基本半径")]
        [Range(2, 32)] public int plateauSearchBaseRadius = 8;
        [Label("プラトー境界ブレンド")]
        [Range(0f, 1f)] public float plateauBoundaryBlend = 0.6f;

        // =====================================================================
        // Post 2: 台地フラット化 — 連結領域の高さを目標値に収束させる
        // =====================================================================
        [Header("Post 2: 台地フラット化")]
        // 連結領域のピクセル数がこれ未満なら台地化をスキップ
        [Label("最小領域サイズ(px)")]
        [Range(1, 2000)] public int minRegionSize = 390;
        // 台地化後、目標高度付近のピクセルがこの割合未満ならロールバック
        [Label("最小カバー率")]
        [Range(0f, 1f)] public float minPlateauCoverage = 0.6f;
        [Label("カバー判定許容差")]
        [Range(0.005f, 0.1f)] public float coverageTolerance = 0.01f;
        // 境界高さと目標高さの差が大きい箇所ほど長い遷移距離を取る
        [Label("遷移ベース幅(px)")]
        [Range(1f, 30f)] public float plateauBaseTransition = 3f;
        [Label("遷移スケール")]
        [Range(10f, 1000f)] public float plateauTransitionScale = 300f;

        // =====================================================================
        // Post 3: 台地内部スパイク除去 — 同一領域ピクセルのみのボックスブラー
        // =====================================================================
        [Header("Post 3: 台地内部スパイク除去")]
        [Label("スムーズ半径(px)")]
        [Range(0, 8)] public int smoothRadius = 4;
        [Label("スムーズ反復回数")]
        [Range(0, 8)] public int smoothIterations = 4;

        // =====================================================================
        // Post 4: 台地境界リファイン — ガウシアン＋ノイズで自然な遷移を形成
        // =====================================================================
        [Header("Post 4: 台地境界リファイン")]
        [Label("内側帯幅(px)")]
        [Range(1, 12)] public int boundaryInnerBand = 3;
        [Label("外側帯幅(px)")]
        [Range(1, 16)] public int boundaryOuterBand = 4;
        [Label("ガウシアンσ")]
        [Range(0.5f, 6f)] public float boundaryGaussSigma = 1.8f;
        [Label("ノイズ周波数")]
        [Range(0.001f, 0.1f)] public float boundaryNoiseFrequency = 0.08f;
        [Label("ノイズ振幅上限")]
        [Range(0f, 0.01f)] public float boundaryNoiseAmplitude = 0.004f;
        [Label("ノイズオクターブ")]
        [Range(1, 3)] public int boundaryNoiseOctaves = 2;
        [Label("リファイン反復回数")]
        [Range(1, 4)] public int boundaryRefineIterations = 2;

        // =====================================================================
        // デバッグ — 台地化候補の可視化
        // =====================================================================
        [Header("デバッグ")]
        [Label("デバッグオーバーレイ")]
        public bool debugPlateauOverlay = true;
        // 領域IDごとに塗り分けるためのレイヤー（textureConfigとは独立）
        [Label("デバッグ用レイヤー")]
        public TerrainLayer[] debugTerrainLayers;

        // =====================================================================
        // Visual 1: テクスチャ — SplatmapJobでバイオーム重みと傾斜/高度フィルタから合成
        // =====================================================================
        [Header("Visual 1: テクスチャ")]
        [Label("テレインレイヤー")]
        public TerrainLayer terrainLayer;
        [Label("テクスチャ設定")]
        public BiomeTextureConfig textureConfig = new BiomeTextureConfig();

        // =====================================================================
        // Visual 2: 樹木配置 — 高山には樹木がないが拡張時のためにフィールドを用意
        // =====================================================================
        [Header("Visual 2: 樹木配置")]
        [Label("樹木配置")]
        public TreePlacementConfig treePlacement = new TreePlacementConfig();

        // =====================================================================
        // Visual 3: オブジェクト配置 — 樹木SpatialGrid参照後にPoissonで配置
        // =====================================================================
        [Header("Visual 3: オブジェクト配置")]
        [Label("オブジェクト設定")]
        public BiomeObjectConfig objectConfig = new BiomeObjectConfig();

        // =====================================================================
        // Visual 4: 草花ディテール — Tree/ObjectのSpatialGrid参照後に配置
        // =====================================================================
        [Header("Visual 4: 草花ディテール")]
        [Label("ディテール設定")]
        public BiomeDetailConfig detailConfig = new BiomeDetailConfig();

        [Header("海岸設定")]
        [Label("海岸設定")]
        public BiomeShoreConfig shoreConfig = new BiomeShoreConfig();

        [Header("境界設定")]
        [Label("境界設定")]
        public BiomeBoundaryConfig boundaryConfig = new BiomeBoundaryConfig();
    }
}
