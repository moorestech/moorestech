using System;
using MessagePack;
using UnityEngine;

namespace Game.Common.MessagePack
{
    [MessagePackObject]
    public class Vector2IntMessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public Vector2IntMessagePack()
        {
        }
        
        public Vector2IntMessagePack(int x, int y)
        {
            X = x;
            Y = y;
        }
        
        public Vector2IntMessagePack(Vector2Int coreVector2Int)
        {
            X = coreVector2Int.x;
            Y = coreVector2Int.y;
        }
        
        [Key(0)] public int X { get; set; }
        
        [Key(1)] public int Y { get; set; }
        
        [IgnoreMember] public Vector2Int Vector2Int => new(X, Y);
        
        public static implicit operator Vector2Int(Vector2IntMessagePack pack)
        {
            return pack.Vector2Int;
        }
        
        public override string ToString()
        {
            return Vector2Int.ToString();
        }
    }
}
