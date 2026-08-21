using System;
using Game.MapGeneration.Provisioning;
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
        }

        // ワイヤ値から転送メタを組み直す唯一の入口。モード解釈を各所へ散らさない
        // The single entry rebuilding the transfer meta from wire values, keeping mode interpretation in one place
        public TerrainTransferMeta ToTerrainTransferMeta()
        {
            if (MapMode == WorldProvisioner.TemplateMapMode) return TerrainTransferMeta.CreateTemplate(WorldId, WorldSeed);
            if (MapMode == WorldProvisioner.GeneratedMapMode)
                return TerrainTransferMeta.CreateGenerated(
                    WorldId, TerrainResolution, TerrainTileCount, TerrainChunkTotal, WorldSeed,
                    new TerrainOrigins(noiseOrigin: NoiseOrigin, sceneOrigin: SceneOrigin), GenerationMasterFingerprint);
            throw new InvalidOperationException($"[TerrainTransferMetaMessagePack] Unknown map mode '{MapMode}'.");
        }
    }
}
