using System;
using Game.Base;
using Game.Entity.Interface;
using Game.World.Interface.DataStore;
using MessagePack;

namespace Server.Util.MessagePack
{
    [MessagePackObject(false)]
    public class Vector3MessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public Vector3MessagePack() { }
        public Vector3MessagePack(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public Vector3MessagePack(ServerVector3 vector3)
        {
            X = vector3.X;
            Y = vector3.Y;
            Z = vector3.Z;
        }
        

        [Key(0)]
        public float X { get; set; }
        [Key(1)]
        public float Y { get; set; }
        [Key(2)]
        public float Z { get; set; }
        
    }
}