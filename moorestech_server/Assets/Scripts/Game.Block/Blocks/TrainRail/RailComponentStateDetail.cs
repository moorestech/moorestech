using System;
using MessagePack;
using Server.Util.MessagePack;
using UnityEngine;

namespace Game.Block.Blocks.TrainRail
{
    [Serializable,MessagePackObject]
    public class RailComponentStateDetail
    {
        public const string StateDetailKey = "RailComponent";
        
        [Key(0)] public Vector3MessagePack RailBlockDirection;
        
        public RailComponentStateDetail(Vector3Int direction)
        {
            RailBlockDirection = new Vector3MessagePack(direction);
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RailComponentStateDetail() { }
    }
}