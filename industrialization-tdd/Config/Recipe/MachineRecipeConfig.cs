using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using industrialization.Config;
using industrialization.Item;

namespace industrialization_tdd.Config.Recipe
{
    public static class MachineRecipeConfig
    {
        
        private const string ConfigPath = "C:\\Users\\satou_katsumi\\RiderProjects\\industrialization-tdd\\industrialization-tdd\\Config\\Json\\macineRecipe.json";
        private static MachineRecipeData[] _recipeDatas;

        public static MachineRecipeData GetRecipeData(int id)
        {
            if (_recipeDatas == null)
            {
                _recipeDatas = LoadConfig();
            }
            return _recipeDatas[id];
        }

        private static Dictionary<string, MachineRecipeData> _recipeDataCash;
        //TODO ここのロジックの実装、テストの作成
        public static IMachineRecipeData GetRecipeData(int installationId, List<IItemStack> iunputItem)
        {
            if (_recipeDatas == null)
            {
                _recipeDatas = LoadConfig();
            }

            if (_recipeDataCash == null)
            {
                _recipeDataCash = SetupCash(_recipeDatas);
            }

            var key = GetKey(installationId, iunputItem);
            if (_recipeDataCash.ContainsKey(key))
            {
                return _recipeDataCash[key];
            }
            else
            {
                return new NullMachineRecipeData();
            }
        }

        static MachineRecipeData[] LoadConfig()
        {
            //JSONデータの読み込み
            var json = File.ReadAllText(ConfigPath);
            var ms = new MemoryStream(Encoding.UTF8.GetBytes((json)));
            ms.Seek(0, SeekOrigin.Begin);
            var serializer = new DataContractJsonSerializer(typeof(MachineRecipes));
            var data = serializer.ReadObject(ms) as MachineRecipes;

            IEnumerable<MachineRecipeData> r = data.Recipes.ToList().Select(r =>
            {
                IEnumerable<ItemStack> inputItem = 
                    r.
                        ItemInputs.
                        ToList().
                        Select(item => item.ItemStack);
                inputItem = inputItem.ToList().OrderBy(i => i.ID);

                IEnumerable<ItemOutput> outputs =
                    r.ItemOutputs.Select(r => new ItemOutput(r.ItemStack, r.Percent));
                
                return new MachineRecipeData(r.InstallationId,r.Time,inputItem.ToArray(),outputs.ToArray());
            });
            
            return r.ToArray();
        }
        static Dictionary<string, MachineRecipeData> SetupCash(MachineRecipeData[] recipeDatas)
        {
            var cash = new Dictionary<string, MachineRecipeData>();
            recipeDatas.ToList().ForEach(recipe =>
            {
                cash.Add(
                    GetKey(recipe.InstallationId,recipe.ItemInputs.ToList()),
                    recipe);
            });
            return cash;
        }

        private static string GetKey(int installationId, List<IItemStack> itemId)
        {
            var itemlist = "";
            itemId = itemId.OrderBy(i => i.ID).ToList();
            itemId.ForEach(i =>
            {
                itemlist = itemlist + "_" + i.ID;
            });
            return installationId + itemlist;
        }
    }
}