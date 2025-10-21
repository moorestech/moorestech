using System;
using Game.Fluid;
using Game.Gear.Common;
using MessagePack;

namespace Game.Block.Blocks.Gear
{
    /// <summary>
    /// 蒸気ギアジェネレーターのステートの詳細なデータ
    /// </summary>
    [Serializable,MessagePackObject]
    public class SteamGearGeneratorBlockStateDetail : GearStateDetail
    {
        public const string SteamGearGeneratorBlockStateDetailKey = "SteamGearGenerator";
        
        /// <summary>
        /// 現在の状態（Idle, Accelerating, Running, Decelerating）
        /// </summary>
        [Key(6)] public string State;
        
        /// <summary>
        /// 蒸気消費率（0〜1）
        /// </summary>
        [Key(7)] public float SteamConsumptionRate;
        
        /// <summary>
        /// 蒸気タンクの現在の量
        /// </summary>
        [Key(8)] public double SteamAmount;
        
        /// <summary>
        /// 蒸気タンクの流体ID
        /// </summary>
        [Key(9)] public int SteamFluidId;
        
        // ギア生成サービスと流体コンポーネントから詳細情報を抽出する
        // Populate detail fields by querying services and the fluid component
        public SteamGearGeneratorBlockStateDetail(
            SteamGearGeneratorStateService stateService,
            SteamGearGeneratorFluidComponent fluidComponent,
            GearNetworkInfo gearNetworkInfo,
            bool isClockwise)
            : base(isClockwise, stateService.CurrentGeneratedRpm.AsPrimitive(), stateService.CurrentGeneratedTorque.AsPrimitive(), gearNetworkInfo)
        {
            var steamTank = fluidComponent.SteamTank;
            State = stateService.CurrentStateName;
            SteamConsumptionRate = stateService.SteamConsumptionRate;
            SteamAmount = steamTank.Amount;
            SteamFluidId = steamTank.FluidId.AsPrimitive();
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public SteamGearGeneratorBlockStateDetail()
        {
        }
    }
}
