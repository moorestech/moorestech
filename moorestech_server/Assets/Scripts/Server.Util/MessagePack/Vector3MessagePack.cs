using System;
using MessagePack;
using UnityEngine;

namespace Server.Util.MessagePack
{
    [MessagePackObject]
    public class Vector3MessagePack
    {
        [Obsolete("This constructor is for deserialization. Do not use directly.")]
        public Vector3MessagePack()
        {
        }
        
        public Vector3MessagePack(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
        
        public Vector3MessagePack(Vector3 vector3)
        {
            X = vector3.x;
            Y = vector3.y;
            Z = vector3.z;
        }
        
        [Key(0)] public float X { get; set; }
        [Key(1)] public float Y { get; set; }
        [Key(2)] public float Z { get; set; }
        
        [IgnoreMember] public Vector3 Vector3 => new(X, Y, Z);
        
        public static implicit operator Vector3(Vector3MessagePack pack)
        {
            return pack.Vector3;
        }
        
        public override string ToString()
        {
            return Vector3.ToString();
        }
    }
}