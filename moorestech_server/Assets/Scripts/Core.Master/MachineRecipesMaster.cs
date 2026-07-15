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
            // レシピ選択はGUID指定のため、GUID→レシピの辞書のみ構築する
            // Recipe selection is GUID-based, so build only the GUID-to-recipe dictionary
            _machineRecipesByGuid = new Dictionary<Guid, MachineRecipeMasterElement>();
            foreach (var recipe in MachineRecipes.Data)
            {
                _machineRecipesByGuid.Add(recipe.MachineRecipeGuid, recipe);
            }
        }

        public MachineRecipeMasterElement GetRecipeElement(Guid machineRecipeGuid)
        {
            return _machineRecipesByGuid.GetValueOrDefault(machineRecipeGuid);
        }
    }
}
