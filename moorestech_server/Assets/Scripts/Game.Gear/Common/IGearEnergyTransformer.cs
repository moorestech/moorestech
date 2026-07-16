using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.GearConnectOptionModule;
using Mooresmaster.Model.GearModule;

namespace Game.Gear.Common
{
    public interface IGearEnergyTransformer : IBlockComponent
    {
        public BlockInstanceId BlockInstanceId { get; }

        public RPM CurrentRpm { get; }
        public Torque CurrentTorque { get; }
        public bool IsCurrentClockwise { get; }

        public Torque GetRequiredTorque(RPM rpm, bool isClockwise);

        // tick計算後、再計算されたnetworkのgearに対しGearTickUpdaterが呼ぶ。クライアントへ状態変化を通知する
        // Called by GearTickUpdater after tick calculation for gears in recalculated networks; notifies clients of the state change
        public void NotifyStateChanged();

        public List<GearConnect> GetGearConnects();
    }
    
    public readonly struct GearConnect
    {
        public readonly IGearEnergyTransformer Transformer;
        public readonly GearConnectOption Self;
        public readonly GearConnectOption Target;
        
        public GearConnect(IGearEnergyTransformer transformer, GearConnectOption self, GearConnectOption target)
        {
            Transformer = transformer;
            Self = self;
            Target = target;
        }

        public static GearConnect FromConnectedInfo(IGearEnergyTransformer transformer, ConnectedInfo info)
        {
            // 生成型からOptionを取り出す
            // Read options from the generated type
            var selfConnector = info.SelfConnector as GearConnectsElement;
            var targetConnector = info.TargetConnector as GearConnectsElement;
            if (selfConnector == null || targetConnector == null) throw new ArgumentException("Gear connector option is not set");

            return new GearConnect(transformer, selfConnector.Option, targetConnector.Option);
        }
    }
    
    public readonly struct GearNetworkInfo
    {
        public readonly float TotalRequiredGearPower;
        public readonly float TotalGenerateGearPower;
        public readonly float OperatingRate;
        public readonly GearNetworkStopReason StopReason;

        public GearNetworkInfo(float totalRequiredGearPower, float totalGenerateGearPower, float operatingRate, GearNetworkStopReason stopReason)
        {
            TotalRequiredGearPower = totalRequiredGearPower;
            TotalGenerateGearPower = totalGenerateGearPower;
            OperatingRate = operatingRate;
            StopReason = stopReason;
        }

        public static GearNetworkInfo CreateEmpty()
        {
            return new GearNetworkInfo(0, 0, 0, GearNetworkStopReason.None);
        }
    }
}