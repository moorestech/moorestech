using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace industrialization.Config
{
    public class MachineRecipeConfig
    {
        
        private const string configPath = "C:\\Users\\satou\\RiderProjects\\industrialization-tdd\\industrialization-tdd\\Config\\Json\\macineRecipe.json";
        private static MachineRecipeData[] _recipeDatas;

        public static MachineRecipeData GetRecipeData(int id)
        {
            if (_recipeDatas == null)
            {
                _recipeDatas = loadConfig();
            }
            return _recipeDatas[id];
        }

        private static Dictionary<string, MachineRecipeData> _recipeDataCash;
        //TODO ここのロジックの実装、テストの作成
        public static MachineRecipeData GetRecipeData(int installationID, List<int> iunputItem)
        {
            if (_recipeDatas == null)
            {
                _recipeDatas = loadConfig();
            }

            if (_recipeDataCash == null)
            {
                _recipeDataCash = setupCash(_recipeDatas);
            }

            var key = getKey(installationID, iunputItem);
            return _recipeDataCash[key];
        }

        static MachineRecipeData[] loadConfig()
        {
            //JSONデータの読み込み
            var json = File.ReadAllText(configPath);
            var ms = new MemoryStream(Encoding.UTF8.GetBytes((json)));
            ms.Seek(0, SeekOrigin.Begin);
            var serializer = new DataContractJsonSerializer(typeof(MachineRecipe));
            var data = serializer.ReadObject(ms) as MachineRecipe;
            return data.Recipes;
        }
        static Dictionary<string, MachineRecipeData> setupCash(MachineRecipeData[] recipeDatas)
        {
            var cash = new Dictionary<string, MachineRecipeData>();
            recipeDatas.ToList().ForEach(recipe =>
            {
                var items = new List<int>();
                recipe.ItemInputs.ToList().ForEach(item => items.Add(item.ItemId));
                cash.Add(getKey(recipe.InstallationId,items),recipe);
            });
            return cash;
        }

        static string getKey(int installationID, List<int> itemID)
        {
            string itemlist = "";
            itemID.Sort();
            itemID.ForEach(i =>
            {
                itemlist = itemlist + "_" + i;
            });
            return installationID + itemlist;
        }
    }
}