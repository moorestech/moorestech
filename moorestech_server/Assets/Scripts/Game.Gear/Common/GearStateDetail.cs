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
        [Key(2)] public float CurrentTorque { get; set; }
        [Key(3)] public GearNetworkStopReason StopReason { get; set; }
        
        [Key(4)] public float GearNetworkTotalRequiredPower { get; set; }
        [Key(5)] public float GearNetworkTotalGeneratePower { get; set; }
        [Key(6)] public float GearNetworkOperatingRate { get; set; }
        
        public GearStateDetail(bool isClockwise, float currentRpm, float currentTorque, GearNetworkStopReason stopReason, GearNetworkInfo gearNetworkInfo)
        {
            CurrentRpm = currentRpm;
            IsClockwise = isClockwise;
            CurrentTorque = currentTorque;
            StopReason = stopReason;
            
            GearNetworkTotalRequiredPower = gearNetworkInfo.TotalRequiredGearPower;
            GearNetworkTotalGeneratePower = gearNetworkInfo.TotalGenerateGearPower;
            GearNetworkOperatingRate = gearNetworkInfo.OperatingRate;
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public GearStateDetail()
        {
        }
    }
}