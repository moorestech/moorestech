using System;
using Game.MapGeneration.Transfer;
using MessagePack;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse.MapData
{
    /// <summary>
    ///     Layout応答の地形メタ。転送メタとハッシュをワイヤ1キーへ畳む
    ///     Terrain meta of the Layout response, folding the transfer meta and its hash into one wire key
    /// </summary>
    [MessagePackObject]
    public class TerrainTransferMetaMessagePack
    {
        [Key(0)] public string MapMode { get; set; }
        [Key(1)] public string WorldId { get; set; }
        [Key(2)] public int TerrainResolution { get; set; }
        [Key(3)] public int TerrainTileCount { get; set; }
        [Key(4)] public int TerrainChunkTotal { get; set; }

        // 論理ストリーム全体のSHA256。地形なしワールドは空文字
        // SHA256 of the whole logical stream; empty for terrain-less worlds
        [Key(5)] public string TerrainHash { get; set; }

        // world.jsonのseed。クライアントは転送地形と整合する分類段をこのseedで再現する（WorldIdはハッシュなので復元できない）
        // The world.json seed; clients reproduce the classification stage consistent with the transferred terrain from it (WorldId is a hash and cannot be inverted)
        [Key(6)] public int WorldSeed { get; set; }

        // 生成時のノイズ窓原点。スポーン探索の中央化オフセットを含むためマスタからは組み直せない。名前付きの対で運びX/Zの取り違えを封じる
        // The generation-time noise window origin; it embeds the spawn-search centering offset and cannot be rebuilt from the master, and travels as a named pair to prevent X/Z mix-ups
        [Key(7)] public Vector2MessagePack NoiseOrigin { get; set; }

        // 生成タイルのシーン原点。地形をこの位置に置くとMapObjects/MapVeinsの座標と揃う
        // Scene origin of the generated tile; placing the terrain there aligns it with the MapObjects/MapVeins coordinates
        [Key(8)] public Vector2MessagePack SceneOrigin { get; set; }

        // 生成マスタの指紋。templateは空文字
        // Generation master fingerprint; empty for template
        [Key(9)] public string GenerationMasterFingerprint { get; set; }

        // ワールドを作った生成器の版。別ビルドのサーバーに繋いだクライアントが転送ファイル構成の違いを検出するのに要る
        // The generator version that created the world; a client connected to a server on another build needs it to detect a differing transfer layout
        [Key(10)] public string GeneratorVersion { get; set; }

        // ワールド作成時の配置台帳の指紋。クライアントはこれを見た目キャッシュの鍵に使い、鍵のためのpass-1を回さない
        // The placement ledger digest from world creation; a client keys its visual cache on it instead of running a pass-1 for the key
        [Key(11)] public string PlacementLedgerDigest { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public TerrainTransferMetaMessagePack() { }

        public TerrainTransferMetaMessagePack(TerrainTransferMeta terrainMeta, string terrainHash)
        {
            MapMode = terrainMeta.MapMode;
            WorldId = terrainMeta.WorldId;
            TerrainResolution = terrainMeta.TerrainResolution;
            TerrainTileCount = terrainMeta.TerrainTileCount;
            TerrainChunkTotal = terrainMeta.TerrainChunkTotal;
            TerrainHash = terrainHash;
            WorldSeed = terrainMeta.WorldSeed;
            NoiseOrigin = new Vector2MessagePack(terrainMeta.Origins.NoiseOrigin);
            SceneOrigin = new Vector2MessagePack(terrainMeta.Origins.SceneOrigin);
            GenerationMasterFingerprint = terrainMeta.GenerationMasterFingerprint;
            GeneratorVersion = terrainMeta.GeneratorVersion;
            PlacementLedgerDigest = terrainMeta.PlacementLedgerDigest;
        }

        // ワイヤ値を渡すだけ。モード解釈も未知モードの拒否もドメイン側(TerrainTransferMeta.FromWire)が持つ
        // Values are merely handed over; interpreting the mode and rejecting unknown ones belongs to the domain side (TerrainTransferMeta.FromWire)
        public TerrainTransferMeta ToTerrainTransferMeta()
        {
            return TerrainTransferMeta.FromWire(
                MapMode, WorldId, TerrainResolution, TerrainTileCount, TerrainChunkTotal, WorldSeed,
                new TerrainOrigins(noiseOrigin: NoiseOrigin, sceneOrigin: SceneOrigin), GenerationMasterFingerprint, GeneratorVersion,
                PlacementLedgerDigest);
        }
    }
}
