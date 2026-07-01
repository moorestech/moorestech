using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.Gear.Common
{
    public static class GearNetworkRuntimeStateWriter
    {
        public static void WriteSupplied(GearNetworkWriteContext context)
        {
            context.RuntimeStore.SetNetworkState(new GearNetworkRuntimeState(context.NetworkId, context.DemandPower, context.AvailablePower, context.LoadRate, false, GearNetworkStopReason.None));
            foreach (var transformer in context.Transformers) WriteTransformerSupply(context, transformer);
            foreach (var generator in context.Generators) WriteGeneratorSupply(context, generator, false, GearNetworkStopReason.None);
        }

        public static void WriteStopped(GearNetworkWriteContext context, GearNetworkStopReason stopReason)
        {
            context.RuntimeStore.SetNetworkState(new GearNetworkRuntimeState(context.NetworkId, context.DemandPower, context.AvailablePower, 0f, true, stopReason));
            foreach (var transformer in context.Transformers) WriteStoppedGear(context, transformer, stopReason);
            foreach (var generator in context.Generators) WriteGeneratorSupply(context, generator, true, stopReason);
        }

        public static void WriteNoGenerator(GearNetworkWriteContext context)
        {
            context.RuntimeStore.SetNetworkState(new GearNetworkRuntimeState(context.NetworkId, 0f, 0f, 0f, false, GearNetworkStopReason.None));
            foreach (var transformer in context.Transformers)
            {
                var state = new GearRuntimeState(transformer.BlockInstanceId, context.NetworkId, new RPM(0), new Torque(0), true, false, GearNetworkStopReason.None);
                context.RuntimeStore.SetGearState(state);
                transformer.SupplyPower(new RPM(0), new Torque(0), true);
            }
        }

        private static void WriteTransformerSupply(GearNetworkWriteContext context, IGearEnergyTransformer transformer)
        {
            var info = context.RotationInfos[transformer.BlockInstanceId];
            var state = new GearRuntimeState(transformer.BlockInstanceId, context.NetworkId, info.Rpm, info.RequiredTorque, info.IsClockwise, false, GearNetworkStopReason.None);
            context.RuntimeStore.SetGearState(state);
            transformer.SupplyPower(info.Rpm, info.RequiredTorque, info.IsClockwise);
        }

        private static void WriteGeneratorSupply(GearNetworkWriteContext context, IGearGenerator generator, bool stopped, GearNetworkStopReason stopReason)
        {
            var hasInfo = context.RotationInfos.TryGetValue(generator.BlockInstanceId, out var info);
            var rpm = stopped ? new RPM(0) : hasInfo ? info.Rpm : generator.GenerateRpm;
            var clockwise = hasInfo ? info.IsClockwise : generator.GenerateIsClockwise;
            var torque = stopped ? new Torque(0) : generator.GenerateTorque;
            context.RuntimeStore.SetGearState(new GearRuntimeState(generator.BlockInstanceId, context.NetworkId, rpm, torque, clockwise, stopped, stopReason));
            if (stopped) generator.StopNetwork();
            else generator.SupplyPower(rpm, torque, clockwise);
        }

        private static void WriteStoppedGear(GearNetworkWriteContext context, IGearEnergyTransformer transformer, GearNetworkStopReason stopReason)
        {
            var state = new GearRuntimeState(transformer.BlockInstanceId, context.NetworkId, new RPM(0), new Torque(0), true, true, stopReason);
            context.RuntimeStore.SetGearState(state);
            transformer.StopNetwork();
        }
    }

    public readonly struct GearNetworkWriteContext
    {
        public readonly GearNetworkId NetworkId;
        public readonly IReadOnlyList<IGearEnergyTransformer> Transformers;
        public readonly IReadOnlyList<IGearGenerator> Generators;
        public readonly Dictionary<BlockInstanceId, GearRotationInfo> RotationInfos;
        public readonly GearRuntimeStateStore RuntimeStore;
        public readonly float DemandPower;
        public readonly float AvailablePower;
        public readonly float LoadRate;

        public GearNetworkWriteContext(GearNetworkId networkId, IReadOnlyList<IGearEnergyTransformer> transformers, IReadOnlyList<IGearGenerator> generators, Dictionary<BlockInstanceId, GearRotationInfo> rotationInfos, GearRuntimeStateStore runtimeStore, float demandPower, float availablePower, float loadRate)
        {
            NetworkId = networkId;
            Transformers = transformers;
            Generators = generators;
            RotationInfos = rotationInfos;
            RuntimeStore = runtimeStore;
            DemandPower = demandPower;
            AvailablePower = availablePower;
            LoadRate = loadRate;
        }
    }
}
