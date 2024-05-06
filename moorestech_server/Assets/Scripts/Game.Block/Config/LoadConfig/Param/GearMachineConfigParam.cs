using System.Collections.Generic;
using Core.Item.Interface.Config;
using Game.Block.Interface.BlockConfig;

namespace Game.Block.Config.LoadConfig.Param
{
    public class GearMachineConfigParam : IBlockConfigParam
    {
        public readonly int InputSlot;
        public readonly int OutputSlot;
        public readonly int RequiredPower;

        public List<ConnectSettings> InputConnectSettings;
        public List<ConnectSettings> OutputConnectSettings;

        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            int inputSlot = blockParam.inputSlot;
            int outputSlot = blockParam.outputSlot;
            int requiredPower = blockParam.requiredPower;

            var gearConnectors = blockParam.gearConnectors;
            var inputConnectSettings = BlockConfigJsonLoad.GetConnectSettings(gearConnectors, true);
            var outputConnectSettings = BlockConfigJsonLoad.GetConnectSettings(gearConnectors, false);

            return new GearMachineConfigParam(inputSlot, outputSlot, requiredPower, inputConnectSettings, outputConnectSettings);
        }

        private GearMachineConfigParam(int inputSlot, int outputSlot, int requiredPower, List<ConnectSettings> inputConnectSettings, List<ConnectSettings> outputConnectSettings)
        {
            InputSlot = inputSlot;
            OutputSlot = outputSlot;
            RequiredPower = requiredPower;
            InputConnectSettings = inputConnectSettings;
            OutputConnectSettings = outputConnectSettings;
        }
    }
}