using System;
using MessagePack;
using UnityEngine;

namespace Game.Common.MessagePack
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
        
        [Key(0)] public float X { get; set; }
        [Key(1)] public float Y { get; set; }
        
        [IgnoreMember] public Vector2 Vector2 => new(X, Y);
        
        public static implicit operator Vector2(Vector2MessagePack pack)
        {
            return pack.Vector2;
        }
        
        public override string ToString()
        {
            return Vector2.ToString();
        }
    }
}
