using System;
using Core.Update;
using Game.Block.Blocks.Gear;
using MessagePack;

namespace Game.Block.Blocks.PowerGenerator
{
    [Serializable,MessagePackObject]
    public class PowerGeneratorStateDetail
    {
        public const string StateDetailKey = "PowerGenerator";

        // クライアント表示用に秒単位で送信（内部はtick管理）
        // Send in seconds for client display (internally managed as ticks)
        [Key(0)] public double RemainingFuelTime;
        [Key(1)] public double CurrentFuelTime;

        [Key(2)] public float OperatingRate;

        public PowerGeneratorStateDetail(FuelGearGeneratorFuelService fuelService, float operatingRate)
        {
            OperatingRate = operatingRate;
            RemainingFuelTime = GameUpdater.TicksToSeconds(fuelService.RemainingFuelTicks);
            CurrentFuelTime = GameUpdater.TicksToSeconds(fuelService.CurrentFuelTicks);
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public PowerGeneratorStateDetail() { }
    }
}