using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using industrialization.Config.Recipe.Data;
using industrialization.Item;

namespace industrialization.Config.Recipe
{
    public static class MachineRecipeJsonLoad
    {
        public static IMachineRecipeData[] LoadConfig()
        {
            //JSONデータの読み込み
            var json = File.ReadAllText(ConfigPath.RecipeConfigPath);
            var ms = new MemoryStream(Encoding.UTF8.GetBytes((json)));
            ms.Seek(0, SeekOrigin.Begin);
            var serializer = new DataContractJsonSerializer(typeof(PurseJsonMachineRecipes));
            var data = serializer.ReadObject(ms) as PurseJsonMachineRecipes;

            //レシピデータを実際に使用する形式に変換
            var r = data.Recipes.ToList().Select(r =>
            {
                var inputItem = 
                        r.
                        ItemInputs.
                        ToList().
                        Select(item => (IItemStack)item.ItemStack).ToList();
                
                
                inputItem = inputItem.OrderBy(i => i.Id).ToList();

                var outputs =
                        r.
                        ItemOutputs.
                        Select(r => new ItemOutput(r.ItemStack, r.Percent));
                
                return new MachineRecipeData(r.InstallationId,r.Time,inputItem,outputs.ToList());
            });
            
            return r.ToArray();
        }
    }
    
    //JSONからのデータを格納するためのクラス
    [DataContract] 
    class PurseJsonMachineRecipes
    {
        
        [DataMember(Name = "recipes")]
        private MachineRecipe[] _recipes;

        public MachineRecipe[] Recipes => _recipes;
    }

    [DataContract] 
    class MachineRecipe
    {
        [DataMember(Name = "installationID")]
        private int _installationId;
        [DataMember(Name = "time")]
        private int _time;
        [DataMember(Name = "input")]
        private MachineRecipeInput[] _itemInputs;
        [DataMember(Name = "output")]
        private MachineRecipeOutput[] _itemOutputs;

        public MachineRecipeOutput[] ItemOutputs => _itemOutputs;

        public MachineRecipeInput[] ItemInputs => _itemInputs;

        public int Time => _time;

        public int InstallationId => _installationId;
    }

    [DataContract] 
    class MachineRecipeInput
    {
        [DataMember(Name = "id")]
        private int _itemId;
        [DataMember(Name = "amount")]
        private int _amount;


        public ItemStack ItemStack => new ItemStack(_itemId, _amount);
    }

    [DataContract] 
    class MachineRecipeOutput
    {
        [DataMember(Name = "id")]
        private int _itemId;
        [DataMember(Name = "amount")]
        private int _amount;
        [DataMember(Name = "percent")]
        private double _percent;

        public double Percent => _percent;

        public ItemStack ItemStack => new ItemStack(_itemId, _amount);
    }
}