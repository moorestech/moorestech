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
        
        [Key(3)] public float GearNetworkTotalRequiredTorque { get; set; }
        [Key(4)] public float GearNetworkTotalSupplyTorque { get; set; }
        [Key(5)] public float GearNetworkOperatingRate { get; set; }
        
        public GearStateDetail(bool isClockwise, float currentRpm, float currentTorque, GearNetworkInfo gearNetworkInfo)
        {
            CurrentRpm = currentRpm;
            IsClockwise = isClockwise;
            CurrentTorque = currentTorque;
            
            GearNetworkTotalRequiredTorque = gearNetworkInfo.TotalRequiredGearPower;
            GearNetworkTotalSupplyTorque = gearNetworkInfo.TotalGenerateGearPower;
            GearNetworkOperatingRate = gearNetworkInfo.OperatingRate;
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public GearStateDetail()
        {
        }
    }
}