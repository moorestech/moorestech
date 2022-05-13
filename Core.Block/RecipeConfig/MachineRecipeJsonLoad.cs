using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Core.Block.RecipeConfig.Data;
using Core.Item;
using Newtonsoft.Json;

namespace Core.Block.RecipeConfig
{
    internal class MachineRecipeJsonLoad
    {
        internal List<IMachineRecipeData> LoadConfig(ItemStackFactory itemStackFactory,List<string> configJsons)
        {
            var recipes = new List<IMachineRecipeData>();
            foreach (var json in configJsons)
            {
                recipes.AddRange(Load(itemStackFactory,json));
            }

            return recipes;
        }

        private List<IMachineRecipeData> Load(ItemStackFactory itemStackFactory,string json)
        {
            //JSONデータの読み込み
            var data = JsonConvert.DeserializeObject<PurseJsonMachineRecipes>(json);

            //レシピデータを実際に使用する形式に変換
            var r = data.Recipes.ToList().Select((r, index) =>
            {
                var inputItem =
                    r.ItemInputs.ToList().Select(item => itemStackFactory.Create(item.ItemId, item.Count)).ToList();


                inputItem = inputItem.OrderBy(i => i.Id).ToList();

                var outputs =
                    r.ItemOutputs.Select(r => new ItemOutput(itemStackFactory.Create(r.ItemId, r.Count), r.Percent));

                return (IMachineRecipeData) new MachineRecipeData(r.BlockId, r.Time, inputItem, outputs.ToList(),
                    index);
            });

            return r.ToList();
        }
    }

    //JSONからのデータを格納するためのクラス
    [DataContract]
    class PurseJsonMachineRecipes
    {
        [DataMember(Name = "recipes")] private MachineRecipe[] _recipes;

        public MachineRecipe[] Recipes => _recipes;
    }

    [DataContract]
    class MachineRecipe
    {
        [DataMember(Name = "BlockID")] private int _blockId;
        [DataMember(Name = "time")] private int _time;
        [DataMember(Name = "input")] private MachineRecipeInput[] _itemInputs;
        [DataMember(Name = "output")] private MachineRecipeOutput[] _itemOutputs;

        public MachineRecipeOutput[] ItemOutputs => _itemOutputs;

        public MachineRecipeInput[] ItemInputs => _itemInputs;

        public int Time => _time;

        public int BlockId => _blockId;
    }

    [DataContract]
    class MachineRecipeInput
    {
        [DataMember(Name = "id")] private int _itemId;
        [DataMember(Name = "count")] private int _count;

        public int ItemId => _itemId;

        public int Count => _count;
    }

    [DataContract]
    class MachineRecipeOutput
    {
        [DataMember(Name = "id")] private int _itemId;
        [DataMember(Name = "count")] private int _count;
        [DataMember(Name = "percent")] private double _percent;

        public double Percent => _percent;

        public int ItemId => _itemId;

        public int Count => _count;
    }
}