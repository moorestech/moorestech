using Core.Block.BlockInventory;
using Core.Block.Config;
using Core.Block.Config.LoadConfig;
using Core.Block.Config.LoadConfig.Param;
using Core.Block.Machine;
using Core.Block.Machine.Inventory;
using Core.Block.Machine.InventoryController;
using Core.Block.Machine.SaveLoad;
using Core.Block.RecipeConfig;
using Core.Item;

namespace Core.Block.BlockFactory.BlockTemplate
{
    public class NormalMachineTemplate : IBlockTemplate
    {
        private readonly IMachineRecipeConfig _machineRecipeConfig;
        private readonly ItemStackFactory _itemStackFactory;

        public NormalMachineTemplate(IMachineRecipeConfig machineRecipeConfig, ItemStackFactory itemStackFactory)
        {
            _machineRecipeConfig = machineRecipeConfig;
            _itemStackFactory = itemStackFactory;
        }

        public IBlock New(BlockConfigData param, int intId)
        {
            var machineParam = param.Param as MachineBlockConfigParam;
            
            var input = new NormalMachineInputInventory(param.BlockId, machineParam.InputSlot, _machineRecipeConfig,
                _itemStackFactory);
            
            var output = new NormalMachineOutputInventory(new NullIBlockInventory(), machineParam.OutputSlot,
                _itemStackFactory);

            var runProcess = new NormalMachineRunProcess(input,output,_machineRecipeConfig.GetNullRecipeData(),machineParam.RequiredPower);
            
            return new NormalMachine(param.BlockId,intId , 
                new NormalMachineBlockInventory(input,output),
                new NormalMachineInventory(input,output),
                new NormalMachineSave(input,output,runProcess),
                runProcess
                );

        }

        public IBlock Load(BlockConfigData param, int intId, string state)
        {
            var machineParam = param.Param as MachineBlockConfigParam;
            
            var input = new NormalMachineInputInventory(param.BlockId, machineParam.InputSlot, _machineRecipeConfig,
                _itemStackFactory);
            
            var output = new NormalMachineOutputInventory(new NullIBlockInventory(), machineParam.OutputSlot,
                _itemStackFactory);

            var runProcess =  new NormalMachineLoad(input,output,_itemStackFactory,_machineRecipeConfig,machineParam.RequiredPower).Load(state);
           
            
            return new NormalMachine(param.BlockId,intId , 
                new NormalMachineBlockInventory(input,output),
                new NormalMachineInventory(input,output),
                new NormalMachineSave(input,output,runProcess),
                runProcess
            );
        }
    }
}