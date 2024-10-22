using System;
using MessagePack;

namespace Game.Gear.Common
{
    [MessagePackObject]
    public class GearStateDetail
    {
        [Key(0)] public float CurrentRpm { get; set; }
        
        [Key(1)] public bool IsClockwise { get; set; }
        
        public const string BlockStateDetailKey = "GearStateData";
        public GearStateDetail(float currentRpm, bool isClockwise)
        {
            CurrentRpm = currentRpm;
            IsClockwise = isClockwise;
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public GearStateDetail()
        {
        }
    }
}