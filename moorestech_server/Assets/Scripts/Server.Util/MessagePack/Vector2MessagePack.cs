using System;
using MessagePack;
using UnityEngine;

namespace Server.Util.MessagePack
{
    [MessagePackObject]
    public class Vector2MessagePack
    {
        [Key(0)] public float X { get; set; }
        [Key(1)] public float Y { get; set; }
        
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public Vector2MessagePack()
        {
        }

        public Vector2MessagePack(float x, float y)
        {
            X = x;
            Y = y;
        }

        public Vector2MessagePack(Vector3Int vector2Int)
        {
            X = vector2Int.x;
            Y = vector2Int.y;
        }

        public Vector2MessagePack(Vector2 vector2)
        {
            X = vector2.x;
            Y = vector2.y;
        }
        
        public static implicit operator Vector2(Vector2MessagePack pack)
        {
            return new Vector2(pack.X, pack.Y);
        }
    }
}