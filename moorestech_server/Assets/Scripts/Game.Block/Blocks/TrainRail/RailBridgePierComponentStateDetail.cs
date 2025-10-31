using System;
using MessagePack;
using Server.Util.MessagePack;
using UnityEngine;

namespace Game.Block.Blocks.TrainRail
{
    [Serializable,MessagePackObject]
    public class RailBridgePierComponentStateDetail
    {
        public const string StateDetailKey = "RailBridgePier";
        
        [Key(0)] public Vector3MessagePack RailBlockDirection;
        
        public RailBridgePierComponentStateDetail(Vector3 direction)
        {
            RailBlockDirection = new Vector3MessagePack(direction);
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RailBridgePierComponentStateDetail() { }
    }
}