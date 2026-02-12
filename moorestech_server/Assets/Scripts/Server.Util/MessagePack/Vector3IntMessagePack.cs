using System;
using MessagePack;
using UnityEngine;

namespace Server.Util.MessagePack
{
    [MessagePackObject]
    public class Vector3IntMessagePack
    {
        [Obsolete("This constructor is for deserialization. Do not use directly.")]
        public Vector3IntMessagePack()
        {
        }
        
        public Vector3IntMessagePack(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        
        public Vector3IntMessagePack(Vector3Int vector3Int)
        {
            X = vector3Int.x;
            Y = vector3Int.y;
            Z = vector3Int.z;
        }
        
        [Key(0)] public int X { get; set; }
        [Key(1)] public int Y { get; set; }
        [Key(2)] public int Z { get; set; }
        
        [IgnoreMember] public Vector3Int Vector3Int => new(X, Y, Z); 
        
        public static implicit operator Vector3Int(Vector3IntMessagePack pack)
        {
            return pack.Vector3Int;
        }
        
        public override string ToString()
        {
            return Vector3Int.ToString();
        }
    }
}