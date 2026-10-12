using System;
using Game.Blueprint;
using MessagePack;

namespace Server.Protocol.PacketResponse
{
    [MessagePackObject]
    public class BlueprintLineMessagePack
    {
        [Key(0)] public int BlockIndexA { get; set; }
        [Key(1)] public int BlockIndexB { get; set; }
        [Key(2)] public string ConnectToolGuidStr { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlueprintLineMessagePack() { }

        public BlueprintLineMessagePack(BlueprintLineJsonObject jsonObject)
        {
            BlockIndexA = jsonObject.BlockIndexA;
            BlockIndexB = jsonObject.BlockIndexB;
            ConnectToolGuidStr = jsonObject.ConnectToolGuidStr;
        }

        internal BlueprintLineJsonObject ToJsonObject()
        {
            return new BlueprintLineJsonObject(BlockIndexA, BlockIndexB, Guid.Parse(ConnectToolGuidStr));
        }
    }
}
