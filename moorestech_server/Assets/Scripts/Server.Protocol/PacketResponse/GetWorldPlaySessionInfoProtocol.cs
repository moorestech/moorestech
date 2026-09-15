using System;
using System.Globalization;
using Game.Paths;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    // ワールドの作成日時と累計プレイ時間を1回で返す。進行記録がセッション開始時に取得する
    // Returns the world creation time and the total play time in one call; the progress record fetches it at session start
    public class GetWorldPlaySessionInfoProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getWorldPlaySessionInfo";
        private readonly IWorldSettingsDatastore _worldSettingsDatastore;

        public GetWorldPlaySessionInfoProtocol(ServiceProvider serviceProvider)
        {
            _worldSettingsDatastore = serviceProvider.GetService<IWorldSettingsDatastore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var totalPlaySeconds = _worldSettingsDatastore.GetCurrentPlayTime().TotalSeconds;

            // 作成日時の欠損は累計プレイ時間と独立。片方が欠けてももう片方は実値のまま返す
            // A missing creation time is independent of the total play time; one gap never discards the other value
            if (!_worldSettingsDatastore.TryGetWorldCreationDateTimeUtc(out var worldCreationDateTimeUtc))
            {
                Debug.LogWarning("セーブに世界作成日時が無いため、進行記録へは欠損として返します");
                return new ResponseWorldPlaySessionInfoMessagePack(null, "セーブに世界作成日時が無い", totalPlaySeconds, null);
            }

            var createdAt = worldCreationDateTimeUtc.ToUniversalTime().ToString(BugReportBundleLayout.Utc8601Format, CultureInfo.InvariantCulture);
            return new ResponseWorldPlaySessionInfoMessagePack(createdAt, null, totalPlaySeconds, null);
        }

        [MessagePackObject]
        public class RequestWorldPlaySessionInfoMessagePack : ProtocolMessagePackBase
        {
            public RequestWorldPlaySessionInfoMessagePack()
            {
                Tag = ProtocolTag;
            }
        }

        [MessagePackObject]
        public class ResponseWorldPlaySessionInfoMessagePack : ProtocolMessagePackBase
        {
            // 値と欠損理由を項目ごとに組で持つ。理由が null なら値が実データで、空文字や0で埋めた偽の値とは区別される
            // Each item pairs its value with its own missing reason; a null reason means real data, never an empty string or zero standing in
            [Key(2)] public string WorldCreatedAt { get; set; }
            [Key(3)] public string WorldCreatedAtMissingReason { get; set; }
            [Key(4)] public double TotalPlaySeconds { get; set; }
            [Key(5)] public string TotalPlaySecondsMissingReason { get; set; }

            // 復号はこの引数なしコンストラクタ＋セッターで行う。指定しないと4引数コンストラクタが位置で束ねられ、tag が worldCreatedAt に入る
            // Decoding goes through this parameterless constructor and the setters; without it the 4-argument one is bound positionally and the tag lands in worldCreatedAt
            [SerializationConstructor]
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseWorldPlaySessionInfoMessagePack() { }

            public ResponseWorldPlaySessionInfoMessagePack(string worldCreatedAt, string worldCreatedAtMissingReason, double totalPlaySeconds, string totalPlaySecondsMissingReason)
            {
                Tag = ProtocolTag;
                WorldCreatedAt = worldCreatedAt;
                WorldCreatedAtMissingReason = worldCreatedAtMissingReason;
                TotalPlaySeconds = totalPlaySeconds;
                TotalPlaySecondsMissingReason = totalPlaySecondsMissingReason;
            }
        }
    }
}
