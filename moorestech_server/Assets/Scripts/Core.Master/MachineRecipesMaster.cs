using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master.Validator;
using Mooresmaster.Loader.MachineRecipesModule;
using Mooresmaster.Model.MachineRecipesModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class MachineRecipesMaster : IMasterValidator
    {
        public readonly MachineRecipes MachineRecipes;
        private readonly Dictionary<string, MachineRecipeMasterElement> _machineRecipesByRecipeKey;

        public MachineRecipesMaster(JToken jToken)
        {
            MachineRecipes = MachineRecipesLoader.Load(jToken);
        }

        public bool Validate(out string errorLogs)
        {
            return MachineRecipesMasterUtil.Validate(MachineRecipes, out errorLogs);
        }

        public void Initialize()
        {
            MachineRecipesMasterUtil.Initialize(MachineRecipes, out _machineRecipesByRecipeKey);
        }

        public bool TryGetRecipeElement(BlockId blockId, List<ItemId> inputItemIds, List<FluidId> inputFluids, out MachineRecipeMasterElement recipe)
        {
            var key = MachineRecipesMasterUtil.GetRecipeElementKey(blockId, inputItemIds, inputFluids);
            return _machineRecipesByRecipeKey.TryGetValue(key, out recipe);
        }

        public MachineRecipeMasterElement GetRecipeElement(Guid machineRecipeGuid)
        {
            return MachineRecipes.Data.ToList().Find(x => x.MachineRecipeGuid == machineRecipeGuid);
        }
    }
    
    public static class MachineRecipeMasterExtension
    {
        public static ItemId GetBlockItemId(this MachineRecipeMasterElement recipe)
        {
            var blockId = MasterHolder.BlockMaster.GetBlockId(recipe.BlockGuid);
            return MasterHolder.BlockMaster.GetItemId(blockId);
        }
    }
}