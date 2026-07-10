using System;
using System.Collections.Generic;
using Core.Master.Validator;
using Mooresmaster.Loader.MachineRecipesModule;
using Mooresmaster.Model.MachineRecipesModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class MachineRecipesMaster : IMasterValidator
    {
        public readonly MachineRecipes MachineRecipes;
        private Dictionary<Guid, MachineRecipeMasterElement> _machineRecipesByGuid;

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
            MachineRecipesMasterUtil.Initialize(MachineRecipes, out _machineRecipesByGuid);
        }

        public MachineRecipeMasterElement GetRecipeElement(Guid machineRecipeGuid)
        {
            _machineRecipesByGuid.TryGetValue(machineRecipeGuid, out var recipe);
            return recipe;
        }
    }
}
