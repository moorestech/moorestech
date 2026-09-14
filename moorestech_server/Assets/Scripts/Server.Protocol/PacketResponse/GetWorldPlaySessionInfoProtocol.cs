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
            // 登録漏れの NullReferenceException は PacketResponseCreator の catch が空リストへ握り潰す。取得できない事実を理由付きで返す
            // A registration gap's NullReferenceException is swallowed into an empty list by PacketResponseCreator, so the failure is returned with its reason instead
            if (_worldSettingsDatastore == null)
            {
                Debug.LogError("IWorldSettingsDatastore が登録されていないため、ワールドの作成日時と累計プレイ時間を返せません");
                return new ResponseWorldPlaySessionInfoMessagePack(null, 0, "IWorldSettingsDatastore が登録されていない");
            }

            var totalPlaySeconds = _worldSettingsDatastore.GetCurrentPlayTime().TotalSeconds;

            // 作成日時が欠けたセーブは既定値のままロードされる。DateTime.MinValue をUTC化してもっともらしい実日時として名乗らない
            // A save without a creation time loads with the default; converting DateTime.MinValue to UTC would claim it as a plausible real timestamp
            if (_worldSettingsDatastore.WorldCreationDateTimeUtc == default)
            {
                Debug.LogWarning("セーブに世界作成日時が無いため、進行記録へは欠損として返します");
                return new ResponseWorldPlaySessionInfoMessagePack(null, totalPlaySeconds, "セーブに世界作成日時が無い");
            }

            var createdAt = _worldSettingsDatastore.WorldCreationDateTimeUtc.ToUniversalTime().ToString(BugReportBundleLayout.Utc8601Format, CultureInfo.InvariantCulture);
            return new ResponseWorldPlaySessionInfoMessagePack(createdAt, totalPlaySeconds, null);
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
            [Key(2)] public string WorldCreatedAt;
            [Key(3)] public double TotalPlaySeconds;

            // 取得できなかった理由。null なら値が揃っている。空文字や0で埋めると欠損と実データを読み手が区別できない
            // Why the values could not be obtained; null means they are complete. Empty strings and zeros would be indistinguishable from real data
            [Key(4)] public string MissingReason;

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseWorldPlaySessionInfoMessagePack() { }

            public ResponseWorldPlaySessionInfoMessagePack(string worldCreatedAt, double totalPlaySeconds, string missingReason)
            {
                Tag = ProtocolTag;
                WorldCreatedAt = worldCreatedAt;
                TotalPlaySeconds = totalPlaySeconds;
                MissingReason = missingReason;
            }
        }
    }
}
