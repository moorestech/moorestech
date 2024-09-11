using Mooresmaster.Loader.MachineRecipesModule;
using Mooresmaster.Model.MachineRecipesModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class MachineRecipesMaster
    {
        public readonly MachineRecipes MachineRecipes; // TODO 個々の使用箇所をメソッドか
        
        public MachineRecipesMaster(JToken jToken)
        {
            MachineRecipes = MachineRecipesLoader.Load(jToken);
        }
        
    }
}