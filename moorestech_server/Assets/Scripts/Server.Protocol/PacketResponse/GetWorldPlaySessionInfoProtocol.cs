using System;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

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
            var createdAt = _worldSettingsDatastore.WorldCreationDateTimeUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
            return new ResponseWorldPlaySessionInfoMessagePack(createdAt, _worldSettingsDatastore.GetCurrentPlayTime().TotalSeconds);
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

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseWorldPlaySessionInfoMessagePack() { }

            public ResponseWorldPlaySessionInfoMessagePack(string worldCreatedAt, double totalPlaySeconds)
            {
                Tag = ProtocolTag;
                WorldCreatedAt = worldCreatedAt;
                TotalPlaySeconds = totalPlaySeconds;
            }
        }
    }
}
