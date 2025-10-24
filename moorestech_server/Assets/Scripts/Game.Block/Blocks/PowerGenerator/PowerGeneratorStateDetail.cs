using System;
using Game.Block.Blocks.Gear;
using MessagePack;

namespace Game.Block.Blocks.PowerGenerator
{
    [Serializable,MessagePackObject]
    public class PowerGeneratorStateDetail
    {
        public const string StateDetailKey = "PowerGenerator";
        
        [Key(0)] public double RemainingFuelTime;
        [Key(1)] public double CurrentFuelTime;
        
        [Key(2)] public float OperatingRate;
        
        public PowerGeneratorStateDetail(SteamGearGeneratorFuelService fuelService, float operatingRate)
        {
            OperatingRate = operatingRate;
            RemainingFuelTime = fuelService.RemainingFuelTime;
            CurrentFuelTime = fuelService.CurrentFuelTime;
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public PowerGeneratorStateDetail() { }
    }
}