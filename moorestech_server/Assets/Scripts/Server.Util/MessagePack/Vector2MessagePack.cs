using System;
using Core.Util;
using MessagePack;

namespace Server.Util.MessagePack
{
    [MessagePackObject]
    public class Vector2MessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public Vector2MessagePack()
        {
        }

        public Vector2MessagePack(float x, float y)
        {
            X = x;
            Y = y;
        }

        public Vector2MessagePack(CoreVector2Int coreVector2Int)
        {
            X = coreVector2Int.X;
            Y = coreVector2Int.Y;
        }


        [Key(0)] public float X { get; set; }

        [Key(1)] public float Y { get; set; }
    }
}