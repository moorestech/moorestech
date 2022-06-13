using System;
using Game.World.Interface.DataStore;
using MessagePack;

namespace Server.Util.MessagePack
{
    [MessagePackObject(false)]
    public class Vector2MessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public Vector2MessagePack() { }
        public Vector2MessagePack(float x, float y)
        {
            X = x;
            Y = y;
        }

        public Vector2MessagePack(Coordinate coordinate)
        {
            X = coordinate.X;
            Y = coordinate.Y;
        }
        

        [Key(0)]
        public float X { get; set; }
        [Key(1)]
        public float Y { get; set; }
        
    }
}