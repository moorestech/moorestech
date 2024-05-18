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

        public List<ConnectSettings> GearConnectSettings;

        public static IBlockConfigParam Generate(dynamic blockParam, IItemConfig itemConfig)
        {
            int inputSlot = blockParam.inputSlot;
            int outputSlot = blockParam.outputSlot;
            int requiredPower = blockParam.requiredPower;

            var gearConnectSettings = BlockConfigJsonLoad.GetConnectSettings(blockParam, "gearConnects");

            return new GearMachineConfigParam(inputSlot, outputSlot, requiredPower, gearConnectSettings);
        }

        private GearMachineConfigParam(int inputSlot, int outputSlot, int requiredPower, List<ConnectSettings> gearConnectSettings)
        {
            InputSlot = inputSlot;
            OutputSlot = outputSlot;
            RequiredPower = requiredPower;
            GearConnectSettings = gearConnectSettings;
        }
    }
}