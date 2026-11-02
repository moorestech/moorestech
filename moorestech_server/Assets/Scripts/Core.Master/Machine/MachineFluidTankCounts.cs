using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Machine
{
    // 機械の液体タンク構成。タンクを持たない機械（歯車機械等）はここで0本・容量0に決める
    // Fluid tank layout of a machine; tankless machines (gear machines etc.) are fixed to zero tanks and capacity here
    public readonly struct MachineFluidTankCounts
    {
        public readonly int InputTankCount;
        public readonly int OutputTankCount;
        public readonly float InnerTankCapacity;

        private MachineFluidTankCounts(int inputTankCount, int outputTankCount, float innerTankCapacity)
        {
            InputTankCount = inputTankCount;
            OutputTankCount = outputTankCount;
            InnerTankCapacity = innerTankCapacity;
        }

        public static MachineFluidTankCounts From(IMachineParam machineParam)
        {
            // タンク項目は液体タンクを持つ機械だけが実装する
            // Only machines with fluid tanks implement the tank params
            if (machineParam is not IMachineFluidTankParam tankParam) return new MachineFluidTankCounts(0, 0, 0);
            return new MachineFluidTankCounts(tankParam.InputTankCount, tankParam.OutputTankCount, tankParam.InnerTankCapacity);
        }
    }
}
