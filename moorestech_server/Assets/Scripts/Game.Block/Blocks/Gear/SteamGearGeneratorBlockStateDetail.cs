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
        public new const string BlockStateDetailKey = "SteamGearGenerator";
        
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
        
        public SteamGearGeneratorBlockStateDetail(
            string state, 
            RPM rpm, 
            Torque torque, 
            bool isClockwise, 
            float steamConsumptionRate, 
            FluidContainer steamTank, 
            GearNetworkInfo gearNetworkInfo)
            : base(isClockwise, rpm.AsPrimitive(), torque.AsPrimitive(), gearNetworkInfo)
        {
            State = state;
            SteamConsumptionRate = steamConsumptionRate;
            SteamAmount = steamTank.Amount;
            SteamFluidId = steamTank.FluidId.AsPrimitive();
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public SteamGearGeneratorBlockStateDetail()
        {
        }
    }
}