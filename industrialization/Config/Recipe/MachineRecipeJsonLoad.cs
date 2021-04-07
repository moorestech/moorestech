using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace industrialization.Config.Recipe
{
    public static class MachineRecipeJsonLoad
    {
        private const string ConfigPath = "C:\\Users\\satou_katsumi\\RiderProjects\\industrialization\\industrialization\\Config\\Json\\macineRecipe.json";
        public static IMachineRecipeData[] LoadConfig()
        {
            //JSONデータの読み込み
            var json = File.ReadAllText(ConfigPath);
            var ms = new MemoryStream(Encoding.UTF8.GetBytes((json)));
            ms.Seek(0, SeekOrigin.Begin);
            var serializer = new DataContractJsonSerializer(typeof(JsonMachineRecipes));
            var data = serializer.ReadObject(ms) as JsonMachineRecipes;

            var r = data.Recipes.ToList().Select(r =>
            {
                var inputItem = 
                    r.
                        ItemInputs.
                        ToList().
                        Select(item => item.ItemStack);
                inputItem = inputItem.ToList().OrderBy(i => i.Id);

                var outputs =
                    r.ItemOutputs.Select(r => new ItemOutput(r.ItemStack, r.Percent));
                
                return new MachineRecipeData(r.InstallationId,r.Time,inputItem.ToArray(),outputs.ToArray());
            });
            
            return r.ToArray();
        }
    }
}