using System;
using MessagePack;

namespace Game.Gear.Common
{
    [MessagePackObject]
    public class GearStateDetail
    {
        public const string BlockStateDetailKey = "GearStateData";
        
        [Key(0)] public bool IsClockwise { get; set; }
        [Key(1)] public float CurrentRpm { get; set; }
        [Key(1)] public float CurrentTorque { get; set; }
        
        public GearStateDetail(bool isClockwise, float currentRpm, float currentTorque)
        {
            CurrentRpm = currentRpm;
            IsClockwise = isClockwise;
            CurrentTorque = currentTorque;
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public GearStateDetail()
        {
        }
    }
}